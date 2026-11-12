const express = require('express');
const { AppError } = require('../middleware/errorHandler');
const { authenticate } = require('../middleware/auth');

function createScenarioRouter(pool) {
  const router = express.Router();

  router.get('/', authenticate, async (req, res, next) => {
    try {
      const [rows] = await pool.execute(
        `SELECT id, code, title, description, base_smoke_growth_rate,
                time_limit_seconds, max_casualties_lost, is_active
         FROM scenarios
         ORDER BY is_active DESC, id ASC`
      );
      return res.json({ scenarios: rows });
    } catch (err) {
      return next(err);
    }
  });

  router.get('/:idOrCode/steps', authenticate, async (req, res, next) => {
    try {
      const param = req.params.idOrCode;
      const isNumeric = /^\d+$/.test(param);

      const [scenarios] = await pool.execute(
        isNumeric
          ? `SELECT id, code, title, description, base_smoke_growth_rate,
                    time_limit_seconds, max_casualties_lost, is_active
             FROM scenarios WHERE id = ? LIMIT 1`
          : `SELECT id, code, title, description, base_smoke_growth_rate,
                    time_limit_seconds, max_casualties_lost, is_active
             FROM scenarios WHERE code = ? LIMIT 1`,
        [isNumeric ? Number(param) : param]
      );

      if (scenarios.length === 0) {
        return next(new AppError(404, 'SCENARIO_NOT_FOUND', 'Requested scenario does not exist.'));
      }

      const scenario = scenarios[0];
      const [steps] = await pool.execute(
        `SELECT id, scenario_id, step_number, step_key, title, instruction,
                default_hint, mistake_hint, expected_time_seconds, weight_percent
         FROM scenario_steps
         WHERE scenario_id = ?
         ORDER BY step_number ASC`,
        [scenario.id]
      );

      return res.json({ scenario, steps });
    } catch (err) {
      return next(err);
    }
  });

  return router;
}

module.exports = { createScenarioRouter };
