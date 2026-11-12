class AppError extends Error {
  constructor(statusCode, code, message) {
    super(message);
    this.statusCode = statusCode;
    this.code = code;
  }
}

function notFoundHandler(req, res) {
  res.status(404).json({
    error: {
      code: 'NOT_FOUND',
      message: `Route ${req.method} ${req.originalUrl} does not exist.`,
    },
  });
}

// eslint-disable-next-line no-unused-vars
function errorHandler(err, req, res, next) {
  const status = err.statusCode || 500;
  const code = err.code || 'INTERNAL_SERVER_ERROR';
  const message =
    status >= 500 && process.env.NODE_ENV === 'production'
      ? 'An unexpected server error occurred.'
      : err.message || 'An unexpected error occurred.';

  res.status(status).json({
    error: { code, message },
  });
}

module.exports = { AppError, notFoundHandler, errorHandler };
