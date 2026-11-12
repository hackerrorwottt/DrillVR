const express = require('express');
const { AppError } = require('../middleware/errorHandler');
const { authenticate } = require('../middleware/auth');
const { getUserAdaptiveDifficulty } = require('../services/difficultyService');

function resolveTargetUserId(req) {
  if (!req.params.id || req.params.id === 'me') {
    return req.user.id;
  }
  const parsed = Number(req.params.id);
  if (!Number.isInteger(parsed) || parsed <= 0) {
    throw new AppError(400, 'INVALID_USER_ID', 'User ID must be a positive integer.');
  }
  if (parsed !== req.user.id && req.user.role !== 'instructor' && req.user.role !== 'admin') {
    throw new AppError(403, 'FORBIDDEN', 'Trainees can only view their own records.');
  }
  return parsed;
}

function createUserRouter(pool) {
  const router = express.Router();

  async function handleHistory(req, res, next) {
    try {
      const targetUserId = resolveTargetUserId(req);

      const [userRows] = await pool.execute(
        'SELECT id, name, email, role, created_at FROM users WHERE id = ? LIMIT 1',
        [targetUserId]
      );
      if (userRows.length === 0) {
        return next(new AppError(404, 'USER_NOT_FOUND', 'User not found.'));
      }

      const [sessions] = await pool.execute(
        `SELECT s.id, s.scenario_id AS scenarioId, sc.code AS scenarioCode, sc.title AS scenarioTitle,
                s.difficulty_multiplier AS difficultyMultiplier,
                s.total_score AS totalScore, s.steps_score AS stepsScore,
                s.evacuation_score AS evacuationScore, s.time_bonus_score AS timeBonusScore,
                s.mistake_penalty AS mistakePenalty, s.total_time_seconds AS totalTimeSeconds,
                s.total_mistakes AS totalMistakes, s.casualties_evacuated AS casualtiesEvacuated,
                s.casualties_total AS casualtiesTotal, s.passed,
                s.weakest_step_key AS weakestStepKey, s.completed_at AS completedAt
         FROM sessions s
         INNER JOIN scenarios sc ON sc.id = s.scenario_id
         WHERE s.user_id = ?
         ORDER BY s.completed_at DESC, s.id DESC`,
        [targetUserId]
      );

      if (sessions.length > 0) {
        const sessionIds = sessions.map((s) => s.id);
        const placeholders = sessionIds.map(() => '?').join(',');
        const [logs] = await pool.query(
          `SELECT l.session_id AS sessionId, st.step_number AS stepNumber, st.step_key AS stepKey,
                  st.title, l.time_spent_seconds AS timeSpentSeconds,
                  l.mistakes_in_step AS mistakesInStep, l.completed
           FROM session_step_logs l
           INNER JOIN scenario_steps st ON st.id = l.step_id
           WHERE l.session_id IN (${placeholders})
           ORDER BY st.step_number ASC`,
          sessionIds
        );

        const logsBySession = new Map();
        for (const log of logs) {
          if (!logsBySession.has(log.sessionId)) {
            logsBySession.set(log.sessionId, []);
          }
          logsBySession.get(log.sessionId).push(log);
        }

        for (const sess of sessions) {
          sess.passed = Boolean(sess.passed);
          sess.stepLogs = logsBySession.get(sess.id) || [];
        }
      }

      return res.json({ user: userRows[0], sessions });
    } catch (err) {
      return next(err);
    }
  }

  async function handleDifficulty(req, res, next) {
    try {
      const targetUserId = resolveTargetUserId(req);
      const difficulty = await getUserAdaptiveDifficulty(pool, targetUserId);
      return res.json({ userId: targetUserId, ...difficulty });
    } catch (err) {
      return next(err);
    }
  }

  router.get('/me/history', authenticate, handleHistory);
  router.get('/:id/history', authenticate, handleHistory);
  router.get('/me/difficulty', authenticate, handleDifficulty);
  router.get('/:id/difficulty', authenticate, handleDifficulty);

  return router;
}

module.exports = { createUserRouter };
