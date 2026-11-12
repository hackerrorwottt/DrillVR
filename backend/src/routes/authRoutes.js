const express = require('express');
const bcrypt = require('bcrypt');
const jwt = require('jsonwebtoken');
const { AppError } = require('../middleware/errorHandler');
const { getJwtSecret } = require('../middleware/auth');

function signUserToken(user) {
  return jwt.sign(
    {
      sub: String(user.id),
      email: user.email,
      role: user.role,
      name: user.name,
    },
    getJwtSecret(),
    { expiresIn: process.env.JWT_EXPIRES_IN || '7d' }
  );
}

function createAuthRouter(pool) {
  const router = express.Router();

  router.post('/register', async (req, res, next) => {
    try {
      const name = typeof req.body.name === 'string' ? req.body.name.trim() : '';
      const email = typeof req.body.email === 'string' ? req.body.email.trim().toLowerCase() : '';
      const password = typeof req.body.password === 'string' ? req.body.password : '';

      if (!name || !email || !email.includes('@') || password.length < 6) {
        return next(
          new AppError(400, 'VALIDATION_ERROR', 'Provide valid name, email, and password (min 6 chars).')
        );
      }

      const [existing] = await pool.execute('SELECT id FROM users WHERE email = ? LIMIT 1', [email]);
      if (existing.length > 0) {
        return next(new AppError(409, 'EMAIL_EXISTS', 'An account with this email already exists.'));
      }

      const passwordHash = await bcrypt.hash(password, 10);
      // Public registration always creates a trainee; instructor/admin roles are provisioned server-side.
      const [result] = await pool.execute(
        'INSERT INTO users (name, email, password_hash, role) VALUES (?, ?, ?, ?)',
        [name, email, passwordHash, 'trainee']
      );

      const user = {
        id: result.insertId,
        name,
        email,
        role: 'trainee',
      };

      const token = signUserToken(user);
      return res.status(201).json({ token, user });
    } catch (err) {
      return next(err);
    }
  });

  router.post('/login', async (req, res, next) => {
    try {
      const email = typeof req.body.email === 'string' ? req.body.email.trim().toLowerCase() : '';
      const password = typeof req.body.password === 'string' ? req.body.password : '';

      if (!email || !password) {
        return next(new AppError(400, 'VALIDATION_ERROR', 'Email and password are required.'));
      }

      const [rows] = await pool.execute(
        'SELECT id, name, email, password_hash, role FROM users WHERE email = ? LIMIT 1',
        [email]
      );

      if (rows.length === 0) {
        return next(new AppError(401, 'INVALID_CREDENTIALS', 'Invalid email or password.'));
      }

      const row = rows[0];
      const matches = await bcrypt.compare(password, row.password_hash);
      if (!matches) {
        return next(new AppError(401, 'INVALID_CREDENTIALS', 'Invalid email or password.'));
      }

      const user = {
        id: row.id,
        name: row.name,
        email: row.email,
        role: row.role,
      };

      const token = signUserToken(user);
      return res.json({ token, user });
    } catch (err) {
      return next(err);
    }
  });

  return router;
}

module.exports = { createAuthRouter };
