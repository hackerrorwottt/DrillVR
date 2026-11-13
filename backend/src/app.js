const path = require('path');
const express = require('express');
const cors = require('cors');
const { notFoundHandler, errorHandler } = require('./middleware/errorHandler');
const { createAuthRouter } = require('./routes/authRoutes');
const { createScenarioRouter } = require('./routes/scenarioRoutes');
const { createSessionRouter } = require('./routes/sessionRoutes');
const { createUserRouter } = require('./routes/userRoutes');
const { createInstructorRouter } = require('./routes/instructorRoutes');

function createApp(pool) {
  const app = express();

  app.use(cors());
  app.use(express.json({ limit: '1mb' }));

  // Real-time latency tracking header for VR sync performance auditing
  app.use((req, res, next) => {
    const start = process.hrtime.bigint();
    const originalSend = res.send;
    res.send = function (...args) {
      const durationMs = Number(process.hrtime.bigint() - start) / 1e6;
      if (!res.headersSent) {
        res.setHeader('X-Response-Time', `${durationMs.toFixed(2)}ms`);
      }
      return originalSend.apply(this, args);
    };
    next();
  });

  app.get('/api/health', async (req, res, next) => {
    try {
      await pool.query('SELECT 1 AS ok');
      return res.json({ status: 'ok', service: 'drillvr-backend' });
    } catch (err) {
      return next(err);
    }
  });

  app.use('/api/auth', createAuthRouter(pool));
  app.use('/api/scenarios', createScenarioRouter(pool));
  app.use('/api', createSessionRouter(pool));
  app.use('/api/users', createUserRouter(pool));
  app.use('/api/instructor', createInstructorRouter(pool));
  app.use('/dashboard', express.static(path.join(__dirname, '../../dashboard')));

  app.use(notFoundHandler);
  app.use(errorHandler);

  return app;
}

module.exports = { createApp };
