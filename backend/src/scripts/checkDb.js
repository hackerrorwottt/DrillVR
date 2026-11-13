require('dotenv').config();
const { createPool } = require('../config/db');

async function checkDatabaseIntegrity() {
  const pool = createPool();
  console.log('--- DrillVR Database Diagnostics ---');

  try {
    const [connCheck] = await pool.query('SELECT VERSION() as version, DATABASE() as currentDb');
    console.log(`[✓] MySQL Connection OK: ${connCheck[0].currentDb} (v${connCheck[0].version})`);

    const tables = ['users', 'scenarios', 'scenario_steps', 'sessions', 'session_step_logs'];
    for (const tbl of tables) {
      const [countResult] = await pool.query(`SELECT COUNT(*) as cnt FROM ${tbl}`);
      console.log(`[✓] Table '${tbl}': ${countResult[0].cnt} row(s)`);
    }

    const [roleCounts] = await pool.query('SELECT role, COUNT(*) as count FROM users GROUP BY role');
    console.log('User roles distribution:');
    roleCounts.forEach((r) => console.log(`    - ${r.role}: ${r.count}`));

    const [activeScenarios] = await pool.query(
      'SELECT id, code, title, is_active FROM scenarios WHERE is_active = TRUE'
    );
    console.log(`Active Scenarios (${activeScenarios.length}):`);
    activeScenarios.forEach((s) => console.log(`    - [${s.code}] ${s.title}`));

    console.log('[✓] All database integrity checks PASSED successfully.');
  } catch (err) {
    console.error('[✗] Database diagnostic check failed:', err.message);
    process.exitCode = 1;
  } finally {
    await pool.end();
  }
}

if (require.main === module) {
  checkDatabaseIntegrity();
}

module.exports = { checkDatabaseIntegrity };
