require('dotenv').config();
const request = require('supertest');
const { createPool } = require('../src/config/db');
const { createApp } = require('../src/app');

describe('Session Payload Validation Boundary Tests', () => {
  let pool;
  let app;
  let traineeToken;
  const testTraineeEmail = `boundary_trainee_${Date.now()}@drillvr.edu`;

  beforeAll(async () => {
    pool = createPool();
    app = createApp(pool);

    const regRes = await request(app)
      .post('/api/auth/register')
      .send({
        name: 'Boundary Tester',
        email: testTraineeEmail,
        password: 'Password@123',
      });
    traineeToken = regRes.body.token;
  });

  afterAll(async () => {
    await pool.execute('DELETE FROM users WHERE email = ?', [testTraineeEmail]);
    await pool.end();
  });

  const baseValidPayload = () => ({
    scenarioCode: 'building_emergency_v1',
    difficultyMultiplier: 1.0,
    totalScore: 90,
    stepsScore: 40,
    evacuationScore: 40,
    timeBonusScore: 10,
    mistakePenalty: 0,
    totalTimeSeconds: 120,
    totalMistakes: 0,
    casualtiesEvacuated: 3,
    casualtiesTotal: 3,
    passed: true,
    stepLogs: [{ stepKey: 'briefing', timeSpentSeconds: 10, mistakesInStep: 0, completed: true }],
  });

  test('Rejects score > 100', async () => {
    const payload = baseValidPayload();
    payload.totalScore = 105;

    const res = await request(app)
      .post('/api/sessions')
      .set('Authorization', `Bearer ${traineeToken}`)
      .send(payload);

    expect(res.statusCode).toBe(400);
    expect(res.body.error.code).toBe('VALIDATION_ERROR');
  });

  test('Rejects negative score', async () => {
    const payload = baseValidPayload();
    payload.totalScore = -5;

    const res = await request(app)
      .post('/api/sessions')
      .set('Authorization', `Bearer ${traineeToken}`)
      .send(payload);

    expect(res.statusCode).toBe(400);
    expect(res.body.error.code).toBe('VALIDATION_ERROR');
  });

  test('Rejects casualtiesEvacuated > casualtiesTotal', async () => {
    const payload = baseValidPayload();
    payload.casualtiesEvacuated = 5;
    payload.casualtiesTotal = 3;

    const res = await request(app)
      .post('/api/sessions')
      .set('Authorization', `Bearer ${traineeToken}`)
      .send(payload);

    expect(res.statusCode).toBe(400);
    expect(res.body.error.code).toBe('VALIDATION_ERROR');
  });

  test('Rejects non-positive difficultyMultiplier', async () => {
    const payload = baseValidPayload();
    payload.difficultyMultiplier = 0;

    const res = await request(app)
      .post('/api/sessions')
      .set('Authorization', `Bearer ${traineeToken}`)
      .send(payload);

    expect(res.statusCode).toBe(400);
    expect(res.body.error.code).toBe('VALIDATION_ERROR');
  });

  test('Rejects empty stepLogs array', async () => {
    const payload = baseValidPayload();
    payload.stepLogs = [];

    const res = await request(app)
      .post('/api/sessions')
      .set('Authorization', `Bearer ${traineeToken}`)
      .send(payload);

    expect(res.statusCode).toBe(400);
    expect(res.body.error.code).toBe('VALIDATION_ERROR');
  });

  test('Rejects duplicate step keys in stepLogs', async () => {
    const payload = baseValidPayload();
    payload.stepLogs = [
      { stepKey: 'briefing', timeSpentSeconds: 10, mistakesInStep: 0, completed: true },
      { stepKey: 'briefing', timeSpentSeconds: 15, mistakesInStep: 1, completed: true },
    ];

    const res = await request(app)
      .post('/api/sessions')
      .set('Authorization', `Bearer ${traineeToken}`)
      .send(payload);

    expect(res.statusCode).toBe(400);
    expect(res.body.error.code).toBe('INVALID_STEP_LOG');
  });
});
