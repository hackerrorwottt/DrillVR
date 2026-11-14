require('dotenv').config();
const request = require('supertest');
const { createPool } = require('../src/config/db');
const { createApp } = require('../src/app');

describe('E2E Offline Session Queue Replay & Sync Tests (E2E-02)', () => {
  let pool;
  let app;
  let traineeToken;
  const offlineTraineeEmail = `offline_trainee_${Date.now()}@drillvr.edu`;

  beforeAll(async () => {
    pool = createPool();
    app = createApp(pool);

    const reg = await request(app)
      .post('/api/auth/register')
      .send({
        name: 'Offline Queue Tester',
        email: offlineTraineeEmail,
        password: 'Password@123',
      });
    traineeToken = reg.body.token;
  });

  afterAll(async () => {
    await pool.execute('DELETE FROM users WHERE email = ?', [offlineTraineeEmail]);
    await pool.end();
  });

  test('E2E-02: Replays queued offline sessions sequentially upon network reconnection', async () => {
    // Simulate 2 sessions queued in phone local storage during Airplane Mode
    const offlineSessionQueue = [
      {
        scenarioCode: 'building_emergency_v1',
        difficultyMultiplier: 1.0,
        totalScore: 78.5,
        stepsScore: 40,
        evacuationScore: 26.7,
        timeBonusScore: 11.8,
        mistakePenalty: 0,
        totalTimeSeconds: 125,
        totalMistakes: 0,
        casualtiesEvacuated: 2,
        casualtiesTotal: 3,
        passed: true,
        weakestStepKey: 'search_casualties',
        stepLogs: [
          { stepKey: 'briefing', timeSpentSeconds: 15, mistakesInStep: 0, completed: true },
          { stepKey: 'assess_situation', timeSpentSeconds: 20, mistakesInStep: 0, completed: true },
          { stepKey: 'equipment_selection', timeSpentSeconds: 25, mistakesInStep: 0, completed: true },
          { stepKey: 'search_casualties', timeSpentSeconds: 35, mistakesInStep: 0, completed: true },
          { stepKey: 'evacuate_safe_route', timeSpentSeconds: 20, mistakesInStep: 0, completed: true },
          { stepKey: 'report_command_post', timeSpentSeconds: 10, mistakesInStep: 0, completed: true },
          { stepKey: 'debrief', timeSpentSeconds: 0, mistakesInStep: 0, completed: true },
        ],
      },
      {
        scenarioCode: 'building_emergency_v1',
        difficultyMultiplier: 1.0,
        totalScore: 92.0,
        stepsScore: 40,
        evacuationScore: 40,
        timeBonusScore: 17.0,
        mistakePenalty: 5,
        totalTimeSeconds: 95,
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
          { stepKey: 'report_command_post', timeSpentSeconds: 10, mistakesInStep: 0, completed: true },
          { stepKey: 'debrief', timeSpentSeconds: 0, mistakesInStep: 0, completed: true },
        ],
      },
    ];

    // Simulate network reconnection: ApiClient drains and uploads queued items
    const uploadedSessionIds = [];
    for (const sessionPayload of offlineSessionQueue) {
      const res = await request(app)
        .post('/api/sessions')
        .set('Authorization', `Bearer ${traineeToken}`)
        .send(sessionPayload);

      expect(res.statusCode).toBe(201);
      expect(res.body.sessionId).toBeTruthy();
      uploadedSessionIds.push(res.body.sessionId);
    }

    expect(uploadedSessionIds.length).toBe(2);
    expect(uploadedSessionIds[0]).not.toBe(uploadedSessionIds[1]);

    // Verify both runs appear in trainee's synchronized history
    const historyRes = await request(app)
      .get('/api/users/me/history')
      .set('Authorization', `Bearer ${traineeToken}`);

    expect(historyRes.statusCode).toBe(200);
    expect(historyRes.body.sessions.length).toBe(2);
    expect(historyRes.body.sessions[0].id).toBe(uploadedSessionIds[1]);
    expect(historyRes.body.sessions[1].id).toBe(uploadedSessionIds[0]);
  });
});
