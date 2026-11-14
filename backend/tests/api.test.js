require('dotenv').config();
const request = require('supertest');
const { createPool } = require('../src/config/db');
const { createApp } = require('../src/app');

describe('DrillVR REST API Integration Tests', () => {
  let pool;
  let app;
  let traineeToken;
  let instructorToken;
  const testEmail = `test_trainee_${Date.now()}@drillvr.edu`;

  beforeAll(() => {
    pool = createPool();
    app = createApp(pool);
  });

  afterAll(async () => {
    await pool.execute('DELETE FROM users WHERE email = ?', [testEmail]);
    await pool.end();
  });

  test('GET /api/health returns ok', async () => {
    const res = await request(app).get('/api/health');
    expect(res.statusCode).toBe(200);
    expect(res.body.status).toBe('ok');
  });

  describe('CORS Configuration Tests', () => {
    test('Allows requests with no origin (curl, mobile Cardboard APK, Unity Editor)', async () => {
      const res = await request(app).get('/api/health');
      expect(res.statusCode).toBe(200);
      expect(res.headers['access-control-allow-origin']).toBeUndefined();
    });

    test('Allows requests from whitelisted dashboard origin http://localhost:8080 and handles preflight', async () => {
      const preflight = await request(app)
        .options('/api/sessions')
        .set('Origin', 'http://localhost:8080')
        .set('Access-Control-Request-Method', 'POST')
        .set('Access-Control-Request-Headers', 'Content-Type, Authorization');

      expect(preflight.statusCode).toBe(200);
      expect(preflight.headers['access-control-allow-origin']).toBe('http://localhost:8080');
      expect(preflight.headers['access-control-allow-methods']).toContain('POST');
      expect(preflight.headers['access-control-allow-headers']).toContain('Authorization');

      const actual = await request(app)
        .get('/api/health')
        .set('Origin', 'http://localhost:8080');

      expect(actual.statusCode).toBe(200);
      expect(actual.headers['access-control-allow-origin']).toBe('http://localhost:8080');
    });

    test('Rejects unknown origins by omitting Access-Control-Allow-Origin header without throwing error', async () => {
      const res = await request(app)
        .get('/api/health')
        .set('Origin', 'http://malicious-website.com');

      expect(res.statusCode).toBe(200);
      expect(res.headers['access-control-allow-origin']).toBeUndefined();

      const preflight = await request(app)
        .options('/api/sessions')
        .set('Origin', 'http://malicious-website.com')
        .set('Access-Control-Request-Method', 'POST');

      expect(preflight.headers['access-control-allow-origin']).toBeUndefined();
    });
  });

  test('POST /api/auth/register creates trainee and never returns password_hash', async () => {
    const res = await request(app)
      .post('/api/auth/register')
      .send({
        name: 'Test Trainee',
        email: testEmail,
        password: 'Password@123',
        role: 'admin', // Must be ignored/forced to trainee
      });

    expect(res.statusCode).toBe(201);
    expect(res.body.token).toBeTruthy();
    expect(res.body.user.role).toBe('trainee');
    expect(JSON.stringify(res.body)).not.toContain('password_hash');
    traineeToken = res.body.token;
  });

  test('POST /api/auth/login authenticates seeded instructor without exposing password_hash', async () => {
    const res = await request(app)
      .post('/api/auth/login')
      .send({ email: 'instructor@drillvr.edu', password: 'Instructor@123' });

    expect(res.statusCode).toBe(200);
    expect(res.body.user.role).toBe('instructor');
    expect(JSON.stringify(res.body)).not.toContain('password_hash');
    instructorToken = res.body.token;
  });

  test('Role enforcement blocks trainee from instructor summary (403) and allows instructor (200)', async () => {
    const forbidden = await request(app)
      .get('/api/instructor/trainees/summary')
      .set('Authorization', `Bearer ${traineeToken}`);

    expect(forbidden.statusCode).toBe(403);
    expect(forbidden.body.error.code).toBe('FORBIDDEN');

    const allowed = await request(app)
      .get('/api/instructor/trainees/summary')
      .set('Authorization', `Bearer ${instructorToken}`);

    expect(allowed.statusCode).toBe(200);
    expect(Array.isArray(allowed.body.trainees)).toBe(true);
    expect(JSON.stringify(allowed.body)).not.toContain('password_hash');
  });

  test('GET /api/scenarios and steps return building_emergency_v1 with 7 ordered steps', async () => {
    const scenariosRes = await request(app)
      .get('/api/scenarios')
      .set('Authorization', `Bearer ${traineeToken}`);
    expect(scenariosRes.statusCode).toBe(200);
    expect(scenariosRes.body.scenarios.length).toBeGreaterThanOrEqual(1);

    const stepsRes = await request(app)
      .get('/api/scenarios/building_emergency_v1/steps')
      .set('Authorization', `Bearer ${traineeToken}`);
    expect(stepsRes.statusCode).toBe(200);
    expect(stepsRes.body.steps.length).toBe(7);
  });

  test('POST /api/sessions rolls back entire transaction on invalid step log', async () => {
    const [beforeRows] = await pool.execute(
      'SELECT COUNT(*) AS cnt FROM sessions s INNER JOIN users u ON u.id = s.user_id WHERE u.email = ?',
      [testEmail]
    );

    const badRes = await request(app)
      .post('/api/sessions')
      .set('Authorization', `Bearer ${traineeToken}`)
      .send({
        scenarioCode: 'building_emergency_v1',
        totalScore: 88,
        totalTimeSeconds: 110,
        totalMistakes: 1,
        passed: true,
        stepLogs: [
          { stepKey: 'briefing', timeSpentSeconds: 10, mistakesInStep: 0, completed: true },
          { stepKey: 'non_existent_step_key', timeSpentSeconds: 12, mistakesInStep: 0, completed: true },
        ],
      });

    expect(badRes.statusCode).toBe(400);
    expect(badRes.body.error.code).toBe('INVALID_STEP_LOG');

    const [afterRows] = await pool.execute(
      'SELECT COUNT(*) AS cnt FROM sessions s INNER JOIN users u ON u.id = s.user_id WHERE u.email = ?',
      [testEmail]
    );
    expect(afterRows[0].cnt).toBe(beforeRows[0].cnt);
  });

  test('POST /api/sessions saves valid session + step logs atomically and updates history', async () => {
    const saveRes = await request(app)
      .post('/api/sessions')
      .set('Authorization', `Bearer ${traineeToken}`)
      .send({
        scenarioCode: 'building_emergency_v1',
        difficultyMultiplier: 1.0,
        totalScore: 91.5,
        stepsScore: 40,
        evacuationScore: 40,
        timeBonusScore: 16.5,
        mistakePenalty: 5,
        totalTimeSeconds: 95.5,
        totalMistakes: 1,
        casualtiesEvacuated: 3,
        casualtiesTotal: 3,
        passed: true,
        weakestStepKey: 'equipment_selection',
        stepLogs: [
          { stepKey: 'briefing', timeSpentSeconds: 10, mistakesInStep: 0, completed: true },
          { stepKey: 'assess_situation', timeSpentSeconds: 15, mistakesInStep: 0, completed: true },
          { stepKey: 'equipment_selection', timeSpentSeconds: 20, mistakesInStep: 1, completed: true },
          { stepKey: 'search_casualties', timeSpentSeconds: 22, mistakesInStep: 0, completed: true },
          { stepKey: 'evacuate_safe_route', timeSpentSeconds: 18, mistakesInStep: 0, completed: true },
          { stepKey: 'report_command_post', timeSpentSeconds: 10.5, mistakesInStep: 0, completed: true },
          { stepKey: 'debrief', timeSpentSeconds: 0, mistakesInStep: 0, completed: true },
        ],
      });

    expect(saveRes.statusCode).toBe(201);
    expect(saveRes.body.sessionId).toBeTruthy();

    const histRes = await request(app)
      .get('/api/users/me/history')
      .set('Authorization', `Bearer ${traineeToken}`);
    expect(histRes.statusCode).toBe(200);
    expect(histRes.body.sessions.length).toBe(1);
    expect(histRes.body.sessions[0].stepLogs.length).toBe(7);
  });

  test('POST /api/sessions allows instructor to record evaluation on behalf of trainee', async () => {
    // 1. Instructor records drill for test trainee
    const [targetUserRows] = await pool.execute('SELECT id FROM users WHERE email = ? LIMIT 1', [testEmail]);
    const expectedTraineeId = targetUserRows[0].id;

    const evalRes = await request(app)
      .post('/api/sessions')
      .set('Authorization', `Bearer ${instructorToken}`)
      .send({
        scenarioCode: 'building_emergency_v1',
        targetUserEmail: testEmail,
        difficultyMultiplier: 1.0,
        totalScore: 85,
        stepsScore: 40,
        evacuationScore: 40,
        timeBonusScore: 10,
        mistakePenalty: 5,
        totalTimeSeconds: 120,
        totalMistakes: 1,
        casualtiesEvacuated: 3,
        casualtiesTotal: 3,
        passed: true,
        weakestStepKey: 'search_casualties',
        stepLogs: [
          { stepKey: 'briefing', timeSpentSeconds: 15, mistakesInStep: 0, completed: true },
          { stepKey: 'assess_situation', timeSpentSeconds: 20, mistakesInStep: 0, completed: true },
          { stepKey: 'equipment_selection', timeSpentSeconds: 25, mistakesInStep: 0, completed: true },
          { stepKey: 'search_casualties', timeSpentSeconds: 30, mistakesInStep: 1, completed: true },
          { stepKey: 'evacuate_safe_route', timeSpentSeconds: 20, mistakesInStep: 0, completed: true },
          { stepKey: 'report_command_post', timeSpentSeconds: 10, mistakesInStep: 0, completed: true },
          { stepKey: 'debrief', timeSpentSeconds: 0, mistakesInStep: 0, completed: true },
        ],
      });

    expect(evalRes.statusCode).toBe(201);
    expect(evalRes.body.userId).toBe(expectedTraineeId); // Attributed to the target trainee

    // 2. Instructor cannot record for non-existent user
    const notFoundRes = await request(app)
      .post('/api/sessions')
      .set('Authorization', `Bearer ${instructorToken}`)
      .send({
        scenarioCode: 'building_emergency_v1',
        targetUserEmail: 'nobody@drillvr.edu',
        totalScore: 80,
        totalTimeSeconds: 100,
        totalMistakes: 0,
        stepLogs: [{ stepKey: 'briefing', timeSpentSeconds: 10, completed: true }],
      });
    expect(notFoundRes.statusCode).toBe(404);
    expect(notFoundRes.body.error.code).toBe('TARGET_USER_NOT_FOUND');

    // 3. Instructor cannot record session for an admin account
    const invalidTargetRes = await request(app)
      .post('/api/sessions')
      .set('Authorization', `Bearer ${instructorToken}`)
      .send({
        scenarioCode: 'building_emergency_v1',
        targetUserEmail: 'admin@drillvr.edu',
        totalScore: 80,
        totalTimeSeconds: 100,
        totalMistakes: 0,
        stepLogs: [{ stepKey: 'briefing', timeSpentSeconds: 10, completed: true }],
      });
    expect(invalidTargetRes.statusCode).toBe(400);
    expect(invalidTargetRes.body.error.code).toBe('INVALID_TARGET_USER');
  });

  test('Adaptive difficulty endpoint returns harder, normal, and easier for seeded trainees', async () => {
    const aarav = await request(app)
      .get('/api/users/3/difficulty')
      .set('Authorization', `Bearer ${instructorToken}`);
    expect(aarav.statusCode).toBe(200);
    expect(aarav.body.tier).toBe('harder');

    const diya = await request(app)
      .get('/api/users/4/difficulty')
      .set('Authorization', `Bearer ${instructorToken}`);
    expect(diya.statusCode).toBe(200);
    expect(diya.body.tier).toBe('normal');

    const kabir = await request(app)
      .get('/api/users/5/difficulty')
      .set('Authorization', `Bearer ${instructorToken}`);
    expect(kabir.statusCode).toBe(200);
    expect(kabir.body.tier).toBe('easier');
    expect(kabir.body.extraHints).toBe(true);
  });

  test('GET /api/leaderboard returns ranked trainees without password_hash', async () => {
    const res = await request(app)
      .get('/api/leaderboard?scenarioCode=building_emergency_v1&limit=5')
      .set('Authorization', `Bearer ${traineeToken}`);

    expect(res.statusCode).toBe(200);
    expect(Array.isArray(res.body.leaderboard)).toBe(true);
    expect(JSON.stringify(res.body)).not.toContain('password_hash');
  });
});
