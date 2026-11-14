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

  const allowedOrigins = ['http://localhost:8080', 'http://127.0.0.1:8080'];
  const corsOptions = {
    origin: (origin, callback) => {
      // Allow requests with no origin (like mobile apps, curl, Unity Editor, or server-to-server)
      if (!origin || allowedOrigins.includes(origin)) {
        return callback(null, true);
      }
      return callback(new Error(`Origin ${origin} not allowed by CORS`));
    },
    methods: ['GET', 'POST', 'PUT', 'DELETE', 'OPTIONS'],
    allowedHeaders: ['Content-Type', 'Authorization', 'X-Requested-With'],
    credentials: true,
    optionsSuccessStatus: 200,
  };

  app.use(cors(corsOptions));
  app.options('*', cors(corsOptions));
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
