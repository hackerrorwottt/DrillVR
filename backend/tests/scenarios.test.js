require('dotenv').config();
const request = require('supertest');
const { createPool } = require('../src/config/db');
const { createApp } = require('../src/app');

describe('Scenario & Curriculum Step Progression API Tests', () => {
  let pool;
  let app;
  let authToken;
  const testEmail = `scenario_tester_${Date.now()}@drillvr.edu`;

  beforeAll(async () => {
    pool = createPool();
    app = createApp(pool);

    const reg = await request(app)
      .post('/api/auth/register')
      .send({
        name: 'Scenario Curriculum Tester',
        email: testEmail,
        password: 'Password@123',
      });
    authToken = reg.body.token;
  });

  afterAll(async () => {
    await pool.execute('DELETE FROM users WHERE email = ?', [testEmail]);
    await pool.end();
  });

  test('GET /api/scenarios requires valid authentication token', async () => {
    const res = await request(app).get('/api/scenarios');
    expect(res.statusCode).toBe(401);
  });

  test('GET /api/scenarios returns available scenarios ordered by active status', async () => {
    const res = await request(app)
      .get('/api/scenarios')
      .set('Authorization', `Bearer ${authToken}`);

    expect(res.statusCode).toBe(200);
    expect(Array.isArray(res.body.scenarios)).toBe(true);
    expect(res.body.scenarios.length).toBeGreaterThanOrEqual(1);

    const activeScenario = res.body.scenarios.find(s => s.code === 'building_emergency_v1');
    expect(activeScenario).toBeDefined();
    expect(activeScenario.is_active).toBe(1);
    expect(activeScenario.time_limit_seconds).toBeGreaterThan(0);
  });

  test('GET /api/scenarios/:idOrCode/steps returns ordered 7-step drill sequence by scenario code', async () => {
    const res = await request(app)
      .get('/api/scenarios/building_emergency_v1/steps')
      .set('Authorization', `Bearer ${authToken}`);

    expect(res.statusCode).toBe(200);
    expect(res.body.scenario).toBeDefined();
    expect(res.body.scenario.code).toBe('building_emergency_v1');
    expect(Array.isArray(res.body.steps)).toBe(true);
    expect(res.body.steps.length).toBe(7);

    // Verify step sequence order (1..7)
    const stepNumbers = res.body.steps.map(s => s.step_number);
    expect(stepNumbers).toEqual([1, 2, 3, 4, 5, 6, 7]);

    const expectedKeys = [
      'briefing',
      'assess_situation',
      'equipment_selection',
      'search_casualties',
      'evacuate_safe_route',
      'report_command_post',
      'debrief',
    ];
    const actualKeys = res.body.steps.map(s => s.step_key);
    expect(actualKeys).toEqual(expectedKeys);

    // Verify weights sum to 100%
    const totalWeight = res.body.steps.reduce((sum, s) => sum + Number(s.weight_percent), 0);
    expect(totalWeight).toBeCloseTo(100.0, 1);
  });

  test('GET /api/scenarios/:idOrCode/steps resolves correctly using numeric scenario ID', async () => {
    const listRes = await request(app)
      .get('/api/scenarios')
      .set('Authorization', `Bearer ${authToken}`);
    const activeScenario = listRes.body.scenarios.find(s => s.code === 'building_emergency_v1');

    const res = await request(app)
      .get(`/api/scenarios/${activeScenario.id}/steps`)
      .set('Authorization', `Bearer ${authToken}`);

    expect(res.statusCode).toBe(200);
    expect(res.body.scenario.id).toBe(activeScenario.id);
    expect(res.body.steps.length).toBe(7);
  });

  test('GET /api/scenarios/:idOrCode/steps returns 404 SCENARIO_NOT_FOUND for non-existent scenario', async () => {
    const res = await request(app)
      .get('/api/scenarios/invalid_scenario_xyz/steps')
      .set('Authorization', `Bearer ${authToken}`);

    expect(res.statusCode).toBe(404);
    expect(res.body.error.code).toBe('SCENARIO_NOT_FOUND');
  });
});
