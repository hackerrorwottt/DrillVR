require('dotenv').config();
const jwt = require('jsonwebtoken');
const { authenticate, authorizeRoles, getJwtSecret } = require('../src/middleware/auth');
const { AppError } = require('../src/middleware/errorHandler');

describe('Auth Middleware Unit Tests', () => {
  const secret = getJwtSecret();

  test('authenticate rejects requests without Authorization header', () => {
    const req = { headers: {} };
    const res = {};
    const next = jest.fn();

    authenticate(req, res, next);

    expect(next).toHaveBeenCalledTimes(1);
    const err = next.mock.calls[0][0];
    expect(err).toBeInstanceOf(AppError);
    expect(err.statusCode).toBe(401);
    expect(err.code).toBe('UNAUTHORIZED');
  });

  test('authenticate rejects malformed Bearer header scheme', () => {
    const req = { headers: { authorization: 'Basic 123456' } };
    const res = {};
    const next = jest.fn();

    authenticate(req, res, next);

    expect(next).toHaveBeenCalledTimes(1);
    const err = next.mock.calls[0][0];
    expect(err).toBeInstanceOf(AppError);
    expect(err.statusCode).toBe(401);
    expect(err.code).toBe('UNAUTHORIZED');
  });

  test('authenticate rejects invalid token signature', () => {
    const badToken = jwt.sign({ sub: '1', email: 'test@drillvr.edu' }, 'wrong_secret');
    const req = { headers: { authorization: `Bearer ${badToken}` } };
    const res = {};
    const next = jest.fn();

    authenticate(req, res, next);

    expect(next).toHaveBeenCalledTimes(1);
    const err = next.mock.calls[0][0];
    expect(err).toBeInstanceOf(AppError);
    expect(err.statusCode).toBe(401);
    expect(err.code).toBe('INVALID_TOKEN');
  });

  test('authenticate attaches decoded user on valid token', () => {
    const validToken = jwt.sign(
      { sub: '4', email: 'diya@drillvr.edu', role: 'trainee', name: 'Diya Patel' },
      secret,
      { expiresIn: '1h' }
    );
    const req = { headers: { authorization: `Bearer ${validToken}` } };
    const res = {};
    const next = jest.fn();

    authenticate(req, res, next);

    expect(next).toHaveBeenCalledTimes(1);
    expect(next).toHaveBeenCalledWith(); // called with no error
    expect(req.user).toEqual({
      id: 4,
      email: 'diya@drillvr.edu',
      role: 'trainee',
      name: 'Diya Patel',
    });
  });

  test('authorizeRoles allows authorized role', () => {
    const middleware = authorizeRoles('instructor', 'admin');
    const req = { user: { role: 'instructor' } };
    const res = {};
    const next = jest.fn();

    middleware(req, res, next);

    expect(next).toHaveBeenCalledTimes(1);
    expect(next).toHaveBeenCalledWith();
  });

  test('authorizeRoles blocks unauthorized role with 403 FORBIDDEN', () => {
    const middleware = authorizeRoles('instructor', 'admin');
    const req = { user: { role: 'trainee' } };
    const res = {};
    const next = jest.fn();

    middleware(req, res, next);

    expect(next).toHaveBeenCalledTimes(1);
    const err = next.mock.calls[0][0];
    expect(err).toBeInstanceOf(AppError);
    expect(err.statusCode).toBe(403);
    expect(err.code).toBe('FORBIDDEN');
  });
});
