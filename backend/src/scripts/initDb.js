require('dotenv').config();
const fs = require('fs');
const path = require('path');
const mysql = require('mysql2/promise');
const bcrypt = require('bcrypt');

async function main() {
  const conn = await mysql.createConnection({
    host: process.env.DB_HOST || '127.0.0.1',
    port: Number(process.env.DB_PORT || 3306),
    user: process.env.DB_USER || 'root',
    password: process.env.DB_PASSWORD || '',
    multipleStatements: true,
  });

  try {
    const schemaSql = fs.readFileSync(path.join(__dirname, '../../db/schema.sql'), 'utf8');
    const seedSql = fs.readFileSync(path.join(__dirname, '../../db/seed.sql'), 'utf8');

    await conn.query(schemaSql);
    await conn.query(seedSql);

    const traineeHash = await bcrypt.hash('Trainee@123', 10);
    const instructorHash = await bcrypt.hash('Instructor@123', 10);
    const adminHash = await bcrypt.hash('Admin@123', 10);

    await conn.execute('UPDATE drillvr.users SET password_hash = ? WHERE role = ?', [
      traineeHash,
      'trainee',
    ]);
    await conn.execute('UPDATE drillvr.users SET password_hash = ? WHERE role = ?', [
      instructorHash,
      'instructor',
    ]);
    await conn.execute('UPDATE drillvr.users SET password_hash = ? WHERE role = ?', [
      adminHash,
      'admin',
    ]);

    console.log('DrillVR schema and seed data initialized successfully.');
    console.log('Test logins: aarav@drillvr.edu / Trainee@123 | instructor@drillvr.edu / Instructor@123 | admin@drillvr.edu / Admin@123');
  } finally {
    await conn.end();
  }
}

main().catch((err) => {
  console.error('Database seed failed:', err.message);
  process.exit(1);
});
