const jwt = require('jsonwebtoken');
const { AppError } = require('../middleware/errorHandler');

function getJwtSecret() {
  return process.env.JWT_SECRET || 'dev_only_drillvr_secret_change_me';
}

function authenticate(req, res, next) {
  const header = req.headers.authorization || '';
  const [scheme, token] = header.split(' ');

  if (scheme !== 'Bearer' || !token) {
    return next(new AppError(401, 'UNAUTHORIZED', 'Missing or malformed Authorization header.'));
  }

  try {
    const payload = jwt.verify(token, getJwtSecret());
    req.user = {
      id: Number(payload.sub),
      email: payload.email,
      role: payload.role,
      name: payload.name,
    };
    return next();
  } catch (err) {
    return next(new AppError(401, 'INVALID_TOKEN', 'Token is invalid or expired.'));
  }
}

function authorizeRoles(...allowedRoles) {
  return (req, res, next) => {
    if (!req.user || !allowedRoles.includes(req.user.role)) {
      return next(
        new AppError(403, 'FORBIDDEN', 'You do not have permission to access this resource.')
      );
    }
    return next();
  };
}

module.exports = { authenticate, authorizeRoles, getJwtSecret };
