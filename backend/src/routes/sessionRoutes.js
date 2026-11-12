const express = require('express');
const { AppError } = require('../middleware/errorHandler');
const { authenticate } = require('../middleware/auth');

function isFiniteNonNegative(val) {
  return typeof val === 'number' && Number.isFinite(val) && val >= 0;
}

function createSessionRouter(pool) {
  const router = express.Router();

  // Save a completed drill session + all step logs inside ONE transaction
  router.post('/sessions', authenticate, async (req, res, next) => {
    const body = req.body || {};
    const scenarioCode = typeof body.scenarioCode === 'string' ? body.scenarioCode.trim() : '';
    const scenarioIdInput = body.scenarioId;
    const totalScore = Number(body.totalScore);
    const stepsScore = Number(body.stepsScore ?? 0);
    const evacuationScore = Number(body.evacuationScore ?? 0);
    const timeBonusScore = Number(body.timeBonusScore ?? 0);
    const mistakePenalty = Number(body.mistakePenalty ?? 0);
    const totalTimeSeconds = Number(body.totalTimeSeconds);
    const totalMistakes = Number(body.totalMistakes);
    const casualtiesEvacuated = Number(body.casualtiesEvacuated ?? 0);
    const casualtiesTotal = Number(body.casualtiesTotal ?? 0);
    const difficultyMultiplier = Number(body.difficultyMultiplier ?? 1.0);
    const passed = Boolean(body.passed);
    const weakestStepKey =
      typeof body.weakestStepKey === 'string' && body.weakestStepKey.trim() !== ''
        ? body.weakestStepKey.trim()
        : null;
    const stepLogs = Array.isArray(body.stepLogs) ? body.stepLogs : null;

    if (
      (!scenarioCode && (!Number.isInteger(Number(scenarioIdInput)) || Number(scenarioIdInput) <= 0)) ||
      !Number.isFinite(totalScore) ||
      totalScore < 0 ||
      totalScore > 100 ||
      !isFiniteNonNegative(stepsScore) ||
      !isFiniteNonNegative(evacuationScore) ||
      !isFiniteNonNegative(timeBonusScore) ||
      !isFiniteNonNegative(mistakePenalty) ||
      mistakePenalty > 999.99 ||
      !isFiniteNonNegative(totalTimeSeconds) ||
      !Number.isInteger(totalMistakes) ||
      totalMistakes < 0 ||
      !Number.isInteger(casualtiesEvacuated) ||
      casualtiesEvacuated < 0 ||
      !Number.isInteger(casualtiesTotal) ||
      casualtiesTotal < 0 ||
      casualtiesEvacuated > casualtiesTotal ||
      !Number.isFinite(difficultyMultiplier) ||
      difficultyMultiplier <= 0 ||
      !stepLogs ||
      stepLogs.length === 0
    ) {
      return next(
        new AppError(
          400,
          'VALIDATION_ERROR',
          'Invalid session payload: valid scenario, score (0-100), non-negative time/mistakes, and non-empty stepLogs are required.'
        )
      );
    }

    let conn;
    try {
      conn = await pool.getConnection();
      await conn.beginTransaction();

      const [scenarioRows] = await conn.execute(
        scenarioIdInput
          ? 'SELECT id FROM scenarios WHERE id = ? LIMIT 1'
          : 'SELECT id FROM scenarios WHERE code = ? LIMIT 1',
        [scenarioIdInput ? Number(scenarioIdInput) : scenarioCode]
      );

      if (scenarioRows.length === 0) {
        await conn.rollback();
        return next(new AppError(404, 'SCENARIO_NOT_FOUND', 'Scenario not found.'));
      }

      const scenarioId = scenarioRows[0].id;
      const [validSteps] = await conn.execute(
        'SELECT id, step_key FROM scenario_steps WHERE scenario_id = ?',
        [scenarioId]
      );
      const stepByKey = new Map(validSteps.map((s) => [s.step_key, s.id]));
      const stepById = new Set(validSteps.map((s) => s.id));

      const [sessionResult] = await conn.execute(
        `INSERT INTO sessions (
          user_id, scenario_id, difficulty_multiplier, total_score,
          steps_score, evacuation_score, time_bonus_score, mistake_penalty,
          total_time_seconds, total_mistakes, casualties_evacuated, casualties_total,
          passed, weakest_step_key
        ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)`,
        [
          req.user.id,
          scenarioId,
          difficultyMultiplier,
          totalScore,
          stepsScore,
          evacuationScore,
          timeBonusScore,
          mistakePenalty,
          totalTimeSeconds,
          totalMistakes,
          casualtiesEvacuated,
          casualtiesTotal,
          passed ? 1 : 0,
          weakestStepKey,
        ]
      );

      const sessionId = sessionResult.insertId;

      const seenStepIds = new Set();
      for (const entry of stepLogs) {
        const resolvedStepId = entry.stepId
          ? Number(entry.stepId)
          : stepByKey.get(String(entry.stepKey || ''));
        const timeSpent = Number(entry.timeSpentSeconds);
        const mistakesInStep = Number(entry.mistakesInStep ?? 0);
        const completed = Boolean(entry.completed);

        if (
          !resolvedStepId ||
          !stepById.has(resolvedStepId) ||
          seenStepIds.has(resolvedStepId) ||
          !isFiniteNonNegative(timeSpent) ||
          !Number.isInteger(mistakesInStep) ||
          mistakesInStep < 0
        ) {
          await conn.rollback();
          return next(
            new AppError(
              400,
              'INVALID_STEP_LOG',
              `Invalid or duplicate step log entry (${entry.stepKey || entry.stepId || 'unknown'}); transaction rolled back.`
            )
          );
        }
        seenStepIds.add(resolvedStepId);

        await conn.execute(
          `INSERT INTO session_step_logs (
            session_id, step_id, time_spent_seconds, mistakes_in_step, completed
          ) VALUES (?, ?, ?, ?, ?)`,
          [sessionId, resolvedStepId, timeSpent, mistakesInStep, completed ? 1 : 0]
        );
      }

      await conn.commit();
      return res.status(201).json({
        sessionId,
        userId: req.user.id,
        scenarioId,
        totalScore,
        passed,
      });
    } catch (err) {
      if (conn) {
        try {
          await conn.rollback();
        } catch (_) {
          // Ignore rollback secondary error
        }
      }
      return next(err);
    } finally {
      if (conn) conn.release();
    }
  });

  // Leaderboard (never exposes password_hash)
  router.get('/leaderboard', authenticate, async (req, res, next) => {
    try {
      const scenarioCode =
        typeof req.query.scenarioCode === 'string' && req.query.scenarioCode.trim()
          ? req.query.scenarioCode.trim()
          : 'building_emergency_v1';
      const parsedLimit = Number.parseInt(req.query.limit, 10);
      const limit = Number.isFinite(parsedLimit) ? Math.min(50, Math.max(1, parsedLimit)) : 10;

      const [rows] = await pool.query(
        `SELECT u.id AS userId, u.name, sc.code AS scenarioCode,
                MAX(s.total_score) AS bestScore,
                ROUND(AVG(s.total_score), 2) AS avgScore,
                COUNT(s.id) AS sessionsCount,
                MIN(s.total_time_seconds) AS fastestTimeSeconds
         FROM sessions s
         INNER JOIN users u ON u.id = s.user_id
         INNER JOIN scenarios sc ON sc.id = s.scenario_id
         WHERE sc.code = ? AND u.role = 'trainee'
         GROUP BY u.id, u.name, sc.code
         ORDER BY bestScore DESC, fastestTimeSeconds ASC
         LIMIT ?`,
        [scenarioCode, limit]
      );

      return res.json({ scenarioCode, leaderboard: rows });
    } catch (err) {
      return next(err);
    }
  });

  return router;
}

module.exports = { createSessionRouter };
