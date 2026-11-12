require('dotenv').config();
const { createPool } = require('./config/db');
const { createApp } = require('./app');

const PORT = Number(process.env.PORT || 4000);
const pool = createPool();
const app = createApp(pool);

app.listen(PORT, '0.0.0.0', () => {
  console.log(`DrillVR API listening on http://0.0.0.0:${PORT}`);
});
