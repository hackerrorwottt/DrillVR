const express = require('express');
const { authenticate, authorizeRoles } = require('../middleware/auth');

function createInstructorRouter(pool) {
  const router = express.Router();

  router.get(
    '/trainees/summary',
    authenticate,
    authorizeRoles('instructor', 'admin'),
    async (req, res, next) => {
      try {
        const [trainees] = await pool.execute(
          `SELECT u.id AS userId, u.name, u.email,
                  COUNT(s.id) AS sessionsCount,
                  ROUND(COALESCE(AVG(s.total_score), 0), 2) AS avgScore,
                  ROUND(COALESCE(MAX(s.total_score), 0), 2) AS bestScore,
                  ROUND(COALESCE(AVG(s.total_mistakes), 0), 2) AS avgMistakes,
                  MAX(s.completed_at) AS lastActiveAt
           FROM users u
           LEFT JOIN sessions s ON s.user_id = u.id
           WHERE u.role = 'trainee'
           GROUP BY u.id, u.name, u.email
           ORDER BY avgScore DESC, u.name ASC`
        );

        // Identify each trainee's weakest step (highest total mistakes across logs)
        const [weakestRows] = await pool.execute(
          `SELECT s.user_id AS userId, st.step_key AS stepKey, st.title AS stepTitle,
                  SUM(l.mistakes_in_step) AS totalStepMistakes,
                  ROUND(AVG(l.time_spent_seconds), 2) AS avgStepTime
           FROM session_step_logs l
           INNER JOIN sessions s ON s.id = l.session_id
           INNER JOIN scenario_steps st ON st.id = l.step_id
           GROUP BY s.user_id, st.id, st.step_key, st.title
           ORDER BY s.user_id ASC, totalStepMistakes DESC, avgStepTime DESC`
        );

        const weakestByUser = new Map();
        for (const row of weakestRows) {
          if (!weakestByUser.has(row.userId)) {
            weakestByUser.set(row.userId, {
              stepKey: row.stepKey,
              stepTitle: row.stepTitle,
              totalStepMistakes: Number(row.totalStepMistakes),
            });
          }
        }

        const summary = trainees.map((t) => {
          const weak = weakestByUser.get(t.userId);
          return {
            ...t,
            sessionsCount: Number(t.sessionsCount),
            avgScore: Number(t.avgScore),
            bestScore: Number(t.bestScore),
            avgMistakes: Number(t.avgMistakes),
            weakestStep: weak ? weak.stepTitle : 'N/A',
            weakestStepKey: weak ? weak.stepKey : null,
          };
        });

        return res.json({ trainees: summary });
      } catch (err) {
        return next(err);
      }
    }
  );

  return router;
}

module.exports = { createInstructorRouter };
