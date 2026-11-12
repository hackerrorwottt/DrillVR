(() => {
  const STORAGE_TOKEN_KEY = 'drillvr_instructor_token';
  const STORAGE_URL_KEY = 'drillvr_api_base';
  const STORAGE_USER_KEY = 'drillvr_instructor_user';

  const loginSection = document.getElementById('loginSection');
  const dashboardSection = document.getElementById('dashboardSection');
  const userControls = document.getElementById('userControls');
  const signedInBadge = document.getElementById('signedInBadge');
  const loginForm = document.getElementById('loginForm');
  const apiBaseInput = document.getElementById('apiBaseInput');
  const emailInput = document.getElementById('emailInput');
  const passwordInput = document.getElementById('passwordInput');
  const loginError = document.getElementById('loginError');
  const fillDemoBtn = document.getElementById('fillDemoBtn');
  const refreshBtn = document.getElementById('refreshBtn');
  const logoutBtn = document.getElementById('logoutBtn');

  const traineesBody = document.getElementById('traineesBody');
  const leaderboardBody = document.getElementById('leaderboardBody');
  const detailCard = document.getElementById('detailCard');
  const detailTitle = document.getElementById('detailTitle');
  const detailMeta = document.getElementById('detailMeta');
  const difficultyBadge = document.getElementById('difficultyBadge');
  const historyBody = document.getElementById('historyBody');

  let authToken = localStorage.getItem(STORAGE_TOKEN_KEY) || '';
  let apiBase = localStorage.getItem(STORAGE_URL_KEY) || 'http://localhost:4000';
  let scoreChartInstance = null;

  apiBaseInput.value = apiBase;

  function makeCell(text) {
    const td = document.createElement('td');
    td.textContent = text;
    return td;
  }

  async function apiFetch(path, options = {}) {
    const headers = { 'Content-Type': 'application/json', ...(options.headers || {}) };
    if (authToken) {
      headers.Authorization = `Bearer ${authToken}`;
    }

    const response = await fetch(`${apiBase.replace(/\/+$/, '')}${path}`, {
      ...options,
      headers,
    });
    const data = await response.json().catch(() => ({}));
    if (!response.ok) {
      const msg = data && data.error && data.error.message ? data.error.message : `HTTP ${response.status}`;
      throw new Error(msg);
    }
    return data;
  }

  function showSignedInState(user) {
    loginSection.classList.add('hidden');
    dashboardSection.classList.remove('hidden');
    userControls.classList.remove('hidden');
    signedInBadge.textContent = `${user.name} (${user.role})`;
  }

  function showLoginState() {
    authToken = '';
    localStorage.removeItem(STORAGE_TOKEN_KEY);
    localStorage.removeItem(STORAGE_USER_KEY);
    loginSection.classList.remove('hidden');
    dashboardSection.classList.add('hidden');
    userControls.classList.add('hidden');
    detailCard.classList.add('hidden');
  }

  async function loadDashboard() {
    const [summaryRes, leaderboardRes] = await Promise.all([
      apiFetch('/api/instructor/trainees/summary'),
      apiFetch('/api/leaderboard?scenarioCode=building_emergency_v1&limit=10'),
    ]);

    renderTraineesTable(summaryRes.trainees || []);
    renderLeaderboard(leaderboardRes.leaderboard || []);
  }

  function renderTraineesTable(trainees) {
    traineesBody.textContent = '';
    if (trainees.length === 0) {
      const tr = document.createElement('tr');
      const td = document.createElement('td');
      td.colSpan = 7;
      td.textContent = 'No trainees found.';
      tr.appendChild(td);
      traineesBody.appendChild(tr);
      return;
    }

    trainees.forEach((t) => {
      const tr = document.createElement('tr');
      tr.appendChild(makeCell(t.name));
      tr.appendChild(makeCell(String(t.sessionsCount)));
      tr.appendChild(makeCell(`${Number(t.avgScore).toFixed(1)}`));
      tr.appendChild(makeCell(`${Number(t.bestScore).toFixed(1)}`));
      tr.appendChild(makeCell(`${Number(t.avgMistakes).toFixed(1)}`));
      tr.appendChild(makeCell(t.weakestStep || 'N/A'));

      const actionTd = document.createElement('td');
      const btn = document.createElement('button');
      btn.type = 'button';
      btn.className = 'btn btn-primary';
      btn.textContent = 'Inspect';
      btn.addEventListener('click', () => loadTraineeDetail(t.userId));
      actionTd.appendChild(btn);
      tr.appendChild(actionTd);

      traineesBody.appendChild(tr);
    });
  }

  function renderLeaderboard(rows) {
    leaderboardBody.textContent = '';
    if (rows.length === 0) {
      const tr = document.createElement('tr');
      const td = document.createElement('td');
      td.colSpan = 5;
      td.textContent = 'No recorded sessions yet.';
      tr.appendChild(td);
      leaderboardBody.appendChild(tr);
      return;
    }

    rows.forEach((row, index) => {
      const tr = document.createElement('tr');
      tr.appendChild(makeCell(`#${index + 1}`));
      tr.appendChild(makeCell(row.name));
      tr.appendChild(makeCell(Number(row.bestScore).toFixed(1)));
      tr.appendChild(makeCell(Number(row.avgScore).toFixed(1)));
      tr.appendChild(makeCell(Number(row.fastestTimeSeconds).toFixed(1)));
      leaderboardBody.appendChild(tr);
    });
  }

  async function loadTraineeDetail(userId) {
    const [historyRes, diffRes] = await Promise.all([
      apiFetch(`/api/users/${userId}/history`),
      apiFetch(`/api/users/${userId}/difficulty`),
    ]);

    const user = historyRes.user;
    const sessions = historyRes.sessions || [];

    detailCard.classList.remove('hidden');
    detailTitle.textContent = `${user.name} — Performance Detail`;
    detailMeta.textContent = `${user.email} • ${sessions.length} recorded session(s)`;

    const tier = diffRes.tier || 'normal';
    difficultyBadge.className = `badge badge-${tier}`;
    difficultyBadge.textContent = `Next Difficulty: ${tier.toUpperCase()} (${Number(diffRes.difficultyMultiplier).toFixed(2)}x) | Avg Mistakes: ${diffRes.avgMistakes}${diffRes.extraHints ? ' | Extra Hints ON' : ''}`;

    renderScoreChart(sessions);
    renderHistoryRows(sessions);
    detailCard.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
  }

  function renderScoreChart(sessions) {
    const ordered = [...sessions].reverse();
    const labels = ordered.map((s, idx) => `Run ${idx + 1} (#${s.id})`);
    const scores = ordered.map((s) => Number(s.totalScore));
    const mistakes = ordered.map((s) => Number(s.totalMistakes));

    const ctx = document.getElementById('scoreChart').getContext('2d');
    if (scoreChartInstance) {
      scoreChartInstance.destroy();
    }

    scoreChartInstance = new Chart(ctx, {
      type: 'line',
      data: {
        labels,
        datasets: [
          {
            label: 'Total Score (0-100)',
            data: scores,
            borderColor: '#38bdf8',
            backgroundColor: 'rgba(56, 189, 248, 0.2)',
            tension: 0.25,
            yAxisID: 'y',
          },
          {
            label: 'Mistakes',
            data: mistakes,
            borderColor: '#f59e0b',
            backgroundColor: 'rgba(245, 158, 11, 0.2)',
            tension: 0.25,
            yAxisID: 'y1',
          },
        ],
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        plugins: {
          legend: { labels: { color: '#f1f5f9' } },
        },
        scales: {
          x: { ticks: { color: '#94a3b8' }, grid: { color: '#334155' } },
          y: {
            min: 0,
            max: 100,
            position: 'left',
            ticks: { color: '#38bdf8' },
            grid: { color: '#334155' },
          },
          y1: {
            min: 0,
            position: 'right',
            ticks: { color: '#f59e0b', stepSize: 1 },
            grid: { drawOnChartArea: false },
          },
        },
      },
    });
  }

  function renderHistoryRows(sessions) {
    historyBody.textContent = '';
    if (sessions.length === 0) {
      const tr = document.createElement('tr');
      const td = document.createElement('td');
      td.colSpan = 9;
      td.textContent = 'No sessions recorded for this trainee yet.';
      tr.appendChild(td);
      historyBody.appendChild(tr);
      return;
    }

    sessions.forEach((s) => {
      const tr = document.createElement('tr');
      tr.appendChild(makeCell(`#${s.id}`));
      tr.appendChild(makeCell(new Date(s.completedAt).toLocaleString()));
      tr.appendChild(makeCell(`${Number(s.difficultyMultiplier).toFixed(2)}x`));
      tr.appendChild(makeCell(Number(s.totalScore).toFixed(1)));
      tr.appendChild(makeCell(Number(s.totalTimeSeconds).toFixed(1)));
      tr.appendChild(makeCell(String(s.totalMistakes)));
      tr.appendChild(makeCell(`${s.casualtiesEvacuated}/${s.casualtiesTotal}`));
      tr.appendChild(makeCell(s.passed ? 'PASS' : 'FAIL'));
      tr.appendChild(makeCell(s.weakestStepKey || 'None'));
      historyBody.appendChild(tr);
    });
  }

  fillDemoBtn.addEventListener('click', () => {
    emailInput.value = 'instructor@drillvr.edu';
    passwordInput.value = 'Instructor@123';
  });

  loginForm.addEventListener('submit', async (e) => {
    e.preventDefault();
    loginError.textContent = '';
    apiBase = apiBaseInput.value.trim().replace(/\/+$/, '');
    localStorage.setItem(STORAGE_URL_KEY, apiBase);

    try {
      const data = await apiFetch('/api/auth/login', {
        method: 'POST',
        body: JSON.stringify({
          email: emailInput.value.trim(),
          password: passwordInput.value,
        }),
      });

      if (!data.user || (data.user.role !== 'instructor' && data.user.role !== 'admin')) {
        throw new Error('Instructor or admin account required for dashboard access.');
      }

      authToken = data.token;
      localStorage.setItem(STORAGE_TOKEN_KEY, authToken);
      localStorage.setItem(STORAGE_USER_KEY, JSON.stringify(data.user));
      showSignedInState(data.user);
      await loadDashboard();
    } catch (err) {
      loginError.textContent = err.message;
    }
  });

  refreshBtn.addEventListener('click', () => {
    loadDashboard().catch((err) => {
      alert(`Refresh failed: ${err.message}`);
    });
  });

  logoutBtn.addEventListener('click', showLoginState);

  if (authToken) {
    try {
      const cachedUser = JSON.parse(localStorage.getItem(STORAGE_USER_KEY) || 'null');
      if (cachedUser) {
        showSignedInState(cachedUser);
        loadDashboard().catch(() => showLoginState());
      }
    } catch {
      showLoginState();
    }
  }
})();
