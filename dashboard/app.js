/**
 * DrillVR Enterprise Instructor Command Center
 * Client-Side Application Logic, Telemetry Visualization & API Bridge
 */

(() => {
  'use strict';

  // ---------------------------------------------------------------------------
  // 1. Constants & State Storage
  // ---------------------------------------------------------------------------
  const STORAGE_TOKEN_KEY = 'drillvr_instructor_token';
  const STORAGE_URL_KEY = 'drillvr_api_base';
  const STORAGE_USER_KEY = 'drillvr_instructor_user';

  let authToken = localStorage.getItem(STORAGE_TOKEN_KEY) || '';
  let apiBase = localStorage.getItem(STORAGE_URL_KEY) || 'http://localhost:4000';
  let currentUser = null;
  let isDemoMode = false;
  let activeView = 'overview';
  let chartInstances = {};

  // ---------------------------------------------------------------------------
  // 2. Realistic 3NF Pre-Seeded Dataset (Mirrors MySQL schema.sql & seed.sql)
  // ---------------------------------------------------------------------------
  const SEEDED_TRAINEES = [
    {
      userId: 3,
      name: 'Aarav Sharma',
      email: 'aarav@drillvr.edu',
      sessionsCount: 5,
      avgScore: 94.2,
      bestScore: 98.0,
      avgMistakes: 1.2,
      weakestStep: 'None',
      weakestStepKey: null,
      tier: 'harder',
      multiplier: 1.35,
      extraHints: false,
      advice: 'High performer with rapid target acquisition and zero protocol violations. Smoke expansion speed scaled to 1.35x for stress conditioning.',
    },
    {
      userId: 4,
      name: 'Diya Patel',
      email: 'diya@drillvr.edu',
      sessionsCount: 5,
      avgScore: 82.6,
      bestScore: 88.0,
      avgMistakes: 3.8,
      weakestStep: 'Assess Situation',
      weakestStepKey: 'assess_situation',
      tier: 'normal',
      multiplier: 1.00,
      extraHints: false,
      advice: 'Consistent and safe evacuation. Recurring hesitation observed during East Exit blockage evaluation. Standard 1.00x hazard parameters active.',
    },
    {
      userId: 6,
      name: 'Meera Nair',
      email: 'meera@drillvr.edu',
      sessionsCount: 5,
      avgScore: 79.0,
      bestScore: 85.0,
      avgMistakes: 2.8,
      weakestStep: 'Equipment Selection',
      weakestStepKey: 'equipment_selection',
      tier: 'normal',
      multiplier: 1.00,
      extraHints: false,
      advice: 'Steady pacing and excellent casualty muster. Minor distraction penalties in Room 1 gear selection. Retraining on non-essential tool identification recommended.',
    },
    {
      userId: 5,
      name: 'Kabir Verma',
      email: 'kabir@drillvr.edu',
      sessionsCount: 5,
      avgScore: 58.4,
      bestScore: 65.0,
      avgMistakes: 6.8,
      weakestStep: 'Evacuate via Safe Route',
      weakestStepKey: 'evacuate_safe_route',
      tier: 'easier',
      multiplier: 0.70,
      extraHints: true,
      advice: 'Requires remediation on secondary egress routes. Difficulty lowered to 0.70x hazard speed with mandatory virtual instructor hints enabled.',
    },
  ];

  const SEEDED_SESSIONS = [
    { id: 20, trainee: 'Aarav Sharma', userId: 3, scenario: 'Building Emergency Drill', score: 98.0, time: 142.5, mistakes: 1, rescued: '3/3', mult: 1.35, passed: true, weakest: 'None', date: 'Today 15:30' },
    { id: 19, trainee: 'Diya Patel', userId: 4, scenario: 'Building Emergency Drill', score: 88.0, time: 165.0, mistakes: 3, rescued: '3/3', mult: 1.00, passed: true, weakest: 'Assess Situation', date: 'Today 14:15' },
    { id: 18, trainee: 'Meera Nair', userId: 6, scenario: 'Building Emergency Drill', score: 85.0, time: 172.0, mistakes: 2, rescued: '3/3', mult: 1.00, passed: true, weakest: 'Equipment Selection', date: 'Today 12:45' },
    { id: 17, trainee: 'Kabir Verma', userId: 5, scenario: 'Building Emergency Drill', score: 65.0, time: 210.0, mistakes: 6, rescued: '2/3', mult: 0.70, passed: true, weakest: 'Evacuate via Safe Route', date: 'Today 11:10' },
    { id: 16, trainee: 'Aarav Sharma', userId: 3, scenario: 'Building Emergency Drill', score: 95.0, time: 148.0, mistakes: 1, rescued: '3/3', mult: 1.35, passed: true, weakest: 'None', date: 'Yesterday 16:20' },
    { id: 15, trainee: 'Diya Patel', userId: 4, scenario: 'Building Emergency Drill', score: 85.0, time: 170.0, mistakes: 4, rescued: '3/3', mult: 1.00, passed: true, weakest: 'Assess Situation', date: 'Yesterday 14:50' },
    { id: 14, trainee: 'Meera Nair', userId: 6, scenario: 'Building Emergency Drill', score: 81.0, time: 176.0, mistakes: 3, rescued: '3/3', mult: 1.00, passed: true, weakest: 'Equipment Selection', date: 'Yesterday 13:10' },
    { id: 13, trainee: 'Kabir Verma', userId: 5, scenario: 'Building Emergency Drill', score: 58.0, time: 225.0, mistakes: 7, rescued: '2/3', mult: 0.70, passed: false, weakest: 'Evacuate via Safe Route', date: 'Yesterday 10:40' },
    { id: 12, trainee: 'Aarav Sharma', userId: 3, scenario: 'Building Emergency Drill', score: 91.0, time: 155.0, mistakes: 1, rescued: '3/3', mult: 1.00, passed: true, weakest: 'None', date: '2 days ago' },
    { id: 11, trainee: 'Diya Patel', userId: 4, scenario: 'Building Emergency Drill', score: 80.0, time: 178.0, mistakes: 4, rescued: '3/3', mult: 1.00, passed: true, weakest: 'Assess Situation', date: '2 days ago' },
    { id: 10, trainee: 'Meera Nair', userId: 6, scenario: 'Building Emergency Drill', score: 78.0, time: 185.0, mistakes: 3, rescued: '3/3', mult: 1.00, passed: true, weakest: 'Equipment Selection', date: '2 days ago' },
    { id: 9, trainee: 'Kabir Verma', userId: 5, scenario: 'Building Emergency Drill', score: 60.0, time: 220.0, mistakes: 7, rescued: '2/3', mult: 1.00, passed: true, weakest: 'Evacuate via Safe Route', date: '2 days ago' },
    { id: 8, trainee: 'Aarav Sharma', userId: 3, scenario: 'Building Emergency Drill', score: 95.0, time: 150.0, mistakes: 1, rescued: '3/3', mult: 1.00, passed: true, weakest: 'None', date: '3 days ago' },
    { id: 7, trainee: 'Diya Patel', userId: 4, scenario: 'Building Emergency Drill', score: 82.0, time: 172.0, mistakes: 4, rescued: '3/3', mult: 1.00, passed: true, weakest: 'Assess Situation', date: '3 days ago' },
    { id: 6, trainee: 'Meera Nair', userId: 6, scenario: 'Building Emergency Drill', score: 76.0, time: 190.0, mistakes: 3, rescued: '3/3', mult: 1.00, passed: true, weakest: 'Equipment Selection', date: '3 days ago' },
    { id: 5, trainee: 'Kabir Verma', userId: 5, scenario: 'Building Emergency Drill', score: 55.0, time: 235.0, mistakes: 7, rescued: '2/3', mult: 1.00, passed: false, weakest: 'Evacuate via Safe Route', date: '3 days ago' },
    { id: 4, trainee: 'Aarav Sharma', userId: 3, scenario: 'Building Emergency Drill', score: 92.0, time: 158.0, mistakes: 2, rescued: '3/3', mult: 1.00, passed: true, weakest: 'Equipment Selection', date: '4 days ago' },
    { id: 3, trainee: 'Diya Patel', userId: 4, scenario: 'Building Emergency Drill', score: 78.0, time: 180.0, mistakes: 4, rescued: '3/3', mult: 1.00, passed: true, weakest: 'Equipment Selection', date: '4 days ago' },
    { id: 2, trainee: 'Meera Nair', userId: 6, scenario: 'Building Emergency Drill', score: 75.0, time: 192.0, mistakes: 3, rescued: '3/3', mult: 1.00, passed: true, weakest: 'Equipment Selection', date: '4 days ago' },
    { id: 1, trainee: 'Kabir Verma', userId: 5, scenario: 'Building Emergency Drill', score: 52.0, time: 240.0, mistakes: 7, rescued: '1/3', mult: 1.00, passed: false, weakest: 'Evacuate via Safe Route', date: '4 days ago' },
  ];

  const STEP_BOTTLENECKS = [
    { step: '1. Briefing', target: 20, actual: 18.5, mistakes: 2 },
    { step: '2. Assess Situation', target: 35, actual: 42.0, mistakes: 19 },
    { step: '3. Equipment Selection', target: 45, actual: 48.5, mistakes: 16 },
    { step: '4. Search Casualties', target: 60, actual: 55.0, mistakes: 6 },
    { step: '5. Evacuate Safe Route', target: 60, actual: 72.5, mistakes: 24 },
    { step: '6. Report Command', target: 30, actual: 24.0, mistakes: 4 },
    { step: '7. Debrief Review', target: 30, actual: 28.0, mistakes: 2 },
  ];

  // ---------------------------------------------------------------------------
  // 3. UI Helpers & Toast Notification System
  // ---------------------------------------------------------------------------
  function showToast(message, type = 'info') {
    const container = document.getElementById('toastContainer');
    if (!container) return;

    const toast = document.createElement('div');
    toast.className = `toast toast-${type}`;
    toast.innerHTML = `
      <span class="toast-dot"></span>
      <span class="toast-msg">${message}</span>
    `;

    container.appendChild(toast);
    requestAnimationFrame(() => toast.classList.add('show'));

    setTimeout(() => {
      toast.classList.remove('show');
      setTimeout(() => toast.remove(), 300);
    }, 3500);
  }

  // ---------------------------------------------------------------------------
  // 4. REST API Bridge & Health Checker
  // ---------------------------------------------------------------------------
  async function apiFetch(path, options = {}) {
    if (isDemoMode) {
      return handleDemoFetch(path);
    }

    const headers = { 'Content-Type': 'application/json', ...(options.headers || {}) };
    if (authToken) {
      headers.Authorization = `Bearer ${authToken}`;
    }

    const url = `${apiBase.replace(/\/+$/, '')}${path}`;
    const response = await fetch(url, { ...options, headers });
    const data = await response.json().catch(() => ({}));

    if (!response.ok) {
      const msg = data && data.error && data.error.message ? data.error.message : `HTTP ${response.status}`;
      throw new Error(msg);
    }
    return data;
  }

  function handleDemoFetch(path) {
    if (path.includes('/trainees/summary')) {
      return { trainees: SEEDED_TRAINEES };
    }
    if (path.includes('/leaderboard')) {
      const sorted = [...SEEDED_TRAINEES].sort((a, b) => b.avgScore - a.avgScore);
      return {
        leaderboard: sorted.map((t, i) => ({
          rank: i + 1,
          name: t.name,
          bestScore: t.bestScore,
          avgScore: t.avgScore,
          fastestTimeSeconds: t.userId === 3 ? 142.5 : t.userId === 4 ? 165.0 : t.userId === 6 ? 172.0 : 210.0,
        })),
      };
    }
    const matchHistory = path.match(/\/users\/(\d+)\/history/);
    if (matchHistory) {
      const uid = Number(matchHistory[1]);
      const trainee = SEEDED_TRAINEES.find((t) => t.userId === uid) || SEEDED_TRAINEES[0];
      const sessions = SEEDED_SESSIONS.filter((s) => s.userId === uid);
      return { user: { id: trainee.userId, name: trainee.name, email: trainee.email }, sessions };
    }
    const matchDiff = path.match(/\/users\/(\d+)\/difficulty/);
    if (matchDiff) {
      const uid = Number(matchDiff[1]);
      const trainee = SEEDED_TRAINEES.find((t) => t.userId === uid) || SEEDED_TRAINEES[0];
      return {
        tier: trainee.tier,
        difficultyMultiplier: trainee.multiplier,
        avgMistakes: trainee.avgMistakes,
        extraHints: trainee.extraHints,
      };
    }
    return {};
  }

  async function checkApiHealth() {
    const statusElem = document.getElementById('globalApiStatus');
    const pillElem = document.getElementById('connectionPill');
    const textElem = document.getElementById('connectionText');

    try {
      const res = await fetch(`${apiBase}/api/health`, { method: 'GET' }).catch(() => null);
      if (res && res.ok) {
        if (statusElem) statusElem.textContent = 'API Server Online (:4000)';
        if (pillElem) {
          pillElem.className = 'connection-pill online';
          textElem.textContent = 'Backend: Online';
        }
        return true;
      }
    } catch {
      // offline
    }

    if (statusElem) statusElem.textContent = 'Backend Offline • Demo Mode Ready';
    if (pillElem) {
      pillElem.className = 'connection-pill demo-mode';
      textElem.textContent = isDemoMode ? 'Demo Preview Mode' : 'Backend Offline';
    }
    return false;
  }

  // ---------------------------------------------------------------------------
  // 5. Auth & Shell State Management
  // ---------------------------------------------------------------------------
  function showDashboard(user) {
    currentUser = user;
    document.getElementById('loginScreen').classList.add('hidden');
    document.getElementById('dashboardApp').classList.remove('hidden');

    // Update Topbar & Sidebar User Info
    const avatar = document.getElementById('userAvatarText');
    const nameElem = document.getElementById('userNameText');
    const roleElem = document.getElementById('userRoleBadge');

    const initials = user.name ? user.name.split(' ').map((n) => n[0]).join('').slice(0, 2).toUpperCase() : 'IN';
    if (avatar) avatar.textContent = initials;
    if (nameElem) nameElem.textContent = user.name || 'Instructor';
    if (roleElem) roleElem.textContent = (user.role || 'Instructor').toUpperCase();

    // Render Initial Data
    loadDashboardData();
  }

  function showLoginScreen() {
    authToken = '';
    currentUser = null;
    isDemoMode = false;
    localStorage.removeItem(STORAGE_TOKEN_KEY);
    localStorage.removeItem(STORAGE_USER_KEY);
    localStorage.removeItem('DrillVR_AuthToken');
    localStorage.removeItem('DrillVR_CurrentUser');

    document.getElementById('loginScreen').classList.remove('hidden');
    document.getElementById('dashboardApp').classList.add('hidden');
  }

  // ---------------------------------------------------------------------------
  // 6. View Router (Sidebar Tab Switching)
  // ---------------------------------------------------------------------------
  let unityDashboardInstance = null;
  let unityDashboardLoading = false;

  function loadUnityWebGLPlayer() {
    if (unityDashboardInstance || unityDashboardLoading) return;

    const canvas = document.getElementById('unity-dashboard-canvas');
    const loadingEl = document.getElementById('unity-dashboard-loading');
    const progressEl = document.getElementById('unity-dashboard-progress-bar');
    const errorEl = document.getElementById('unity-dashboard-error');
    const errorTitle = document.getElementById('unity-error-title');
    const errorBody = document.getElementById('unity-error-body');
    const fullscreenBtn = document.getElementById('unityFullscreenBtn');

    if (!canvas) return;

    canvas.addEventListener('click', () => {
      canvas.focus();
    });

    function showMissingBuildState() {
      if (loadingEl) loadingEl.style.display = 'none';
      if (errorEl) {
        if (errorTitle) errorTitle.textContent = 'Unity WebGL Build Not Found';
        if (errorBody) {
          errorBody.innerHTML = `
            <p style="font-size: 0.85rem; color: var(--text-secondary); line-height: 1.5; margin-bottom: 16px;">
              The WebAssembly build files have not been exported yet. Open the Unity Editor and execute the automated export pipeline:
            </p>
            <div style="background: rgba(2, 6, 23, 0.7); border: 1px solid rgba(255, 255, 255, 0.1); border-radius: var(--radius-sm); padding: 10px; font-family: ui-monospace, monospace; font-size: 0.8rem; color: #38bdf8; margin-bottom: 16px;">
              DrillVR &gt; Build WebGL for Dashboard
            </div>
            <p style="font-size: 0.75rem; color: var(--text-muted); margin: 0;">
              Expected loader: <code>dashboard/unity-webgl/Build/unity-webgl.loader.js</code>
            </p>
          `;
        }
        errorEl.style.display = 'block';
      }
    }

    function showRuntimeErrorState(errorText) {
      if (loadingEl) loadingEl.style.display = 'none';
      if (errorEl) {
        if (errorTitle) errorTitle.textContent = 'Unity WebGL Runtime Error';
        if (errorBody) {
          const safeText = String(errorText || 'Unknown runtime error')
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;');
          errorBody.innerHTML = `
            <p style="font-size: 0.85rem; color: var(--text-secondary); line-height: 1.5; margin-bottom: 12px;">
              The Unity WebGL engine encountered an error during runtime initialization:
            </p>
            <div style="background: rgba(2, 6, 23, 0.85); border: 1px solid rgba(239, 68, 68, 0.3); border-radius: var(--radius-sm); padding: 12px; font-family: ui-monospace, monospace; font-size: 0.78rem; color: #f87171; text-align: left; max-height: 160px; overflow-y: auto; white-space: pre-wrap; word-break: break-all; margin-bottom: 12px;">
${safeText}
            </div>
            <p style="font-size: 0.75rem; color: var(--text-muted); margin: 0;">
              Check the browser developer console (F12) for detailed call stack and WebAssembly logs.
            </p>
          `;
        }
        errorEl.style.display = 'block';
      }
    }

    if (errorEl) errorEl.style.display = 'none';
    if (loadingEl) loadingEl.style.display = 'block';
    if (progressEl) progressEl.style.width = '0%';
    unityDashboardLoading = true;

    const buildUrl = 'unity-webgl/Build';
    const buildName = 'unity-webgl';
    const config = {
      dataUrl: `${buildUrl}/${buildName}.data`,
      frameworkUrl: `${buildUrl}/${buildName}.framework.js`,
      codeUrl: `${buildUrl}/${buildName}.wasm`,
      streamingAssetsUrl: 'unity-webgl/StreamingAssets',
      companyName: 'DrillVRTeam',
      productName: 'DrillVR',
      productVersion: '2.4',
      showBanner: (msg, type) => {
        if (type === 'error') {
          showRuntimeErrorState(msg);
        }
      },
    };

    const loaderScript = document.createElement('script');
    loaderScript.src = `${buildUrl}/${buildName}.loader.js`;
    loaderScript.onload = () => {
      if (typeof createUnityInstance !== 'function') {
        unityDashboardLoading = false;
        showMissingBuildState();
        if (loaderScript.parentNode) {
          loaderScript.parentNode.removeChild(loaderScript);
        }
        return;
      }

      createUnityInstance(canvas, config, (progress) => {
        if (progressEl) progressEl.style.width = `${Math.round(100 * progress)}%`;
      }).then((instance) => {
        unityDashboardInstance = instance;
        unityDashboardLoading = false;
        if (loadingEl) loadingEl.style.display = 'none';

        // Update badges to active WASM state once loader and engine are running
        const navBadge = document.getElementById('unity-nav-badge');
        if (navBadge) {
          navBadge.textContent = 'WASM';
          navBadge.style.background = 'rgba(56, 189, 248, 0.2)';
          navBadge.style.color = '#38bdf8';
          navBadge.style.borderColor = 'rgba(56, 189, 248, 0.4)';
        }
        const headerBadge = document.getElementById('unity-header-badge');
        if (headerBadge) {
          headerBadge.textContent = 'WASM • Unity 6';
          headerBadge.style.background = 'rgba(56, 189, 248, 0.15)';
          headerBadge.style.color = '#38bdf8';
          headerBadge.style.borderColor = 'rgba(56, 189, 248, 0.3)';
        }
        const headerSubtitle = document.getElementById('unity-header-subtitle');
        if (headerSubtitle) {
          headerSubtitle.textContent = 'Real Unity 6 simulation runtime (M8_FullOnlineLoop.unity) WebAssembly build. Direct client-side engine with gaze dwell and AI agents.';
        }

        if (fullscreenBtn) {
          fullscreenBtn.onclick = () => {
            instance.SetFullscreen(1);
          };
        }
      }).catch((err) => {
        unityDashboardLoading = false;
        showRuntimeErrorState(err?.message || err || 'Unknown WebAssembly runtime error');
        if (loaderScript.parentNode) {
          loaderScript.parentNode.removeChild(loaderScript);
        }
        console.warn('[DrillVR Unity WebGL] Engine initialization error:', err);
      });
    };

    loaderScript.onerror = () => {
      unityDashboardLoading = false;
      showMissingBuildState();
      if (loaderScript.parentNode) {
        loaderScript.parentNode.removeChild(loaderScript);
      }
      console.info('[DrillVR Unity WebGL] Build loader not found at ' + loaderScript.src + '. Build the project in Unity Editor (DrillVR > Build WebGL for Dashboard).');
    };

    document.body.appendChild(loaderScript);
  }

  function switchView(viewName) {
    activeView = viewName;

    // Update Sidebar Navigation States
    document.querySelectorAll('.nav-link').forEach((btn) => {
      if (btn.dataset.view === viewName) {
        btn.classList.add('active');
      } else {
        btn.classList.remove('active');
      }
    });

    // Update Panels
    document.querySelectorAll('.view-panel').forEach((panel) => {
      panel.classList.remove('active');
      panel.classList.add('hidden');
    });

    const activePanel = document.getElementById(`view-${viewName}`);
    if (activePanel) {
      activePanel.classList.remove('hidden');
      activePanel.classList.add('active');
    }

    // Update Breadcrumb
    const breadcrumb = document.getElementById('currentViewBreadcrumb');
    if (breadcrumb) {
      breadcrumb.textContent = viewName === 'unity-webgl'
        ? 'Unity WebGL Build'
        : viewName.charAt(0).toUpperCase() + viewName.slice(1);
    }

    // Trigger view-specific rendering
    if (viewName === 'sessions') renderAllSessionsView();
    if (viewName === 'trainees') renderTraineeCardsView();
    if (viewName === 'analytics') renderDeepAnalyticsView();
    if (viewName === 'reports') renderReportTimestamp();
    if (viewName === 'simulation') {
      window.dispatchEvent(new CustomEvent('drillvr:view_switched', { detail: 'simulation' }));
    }
    if (viewName === 'unity-webgl') {
      loadUnityWebGLPlayer();
    }
  }

  // ---------------------------------------------------------------------------
  // 7. Telemetry & Data Loaders
  // ---------------------------------------------------------------------------
  async function loadDashboardData() {
    try {
      const [summaryRes, leaderboardRes] = await Promise.all([
        apiFetch('/api/instructor/trainees/summary'),
        apiFetch('/api/leaderboard?scenarioCode=building_emergency_v1&limit=10'),
      ]);

      const trainees = summaryRes.trainees || SEEDED_TRAINEES;
      const leaderboard = leaderboardRes.leaderboard || [];

      renderTraineesTable(trainees);
      renderLeaderboard(leaderboard);
      renderRecentActivity();
      renderKpiMetrics(trainees);
      renderPerformanceTrendChart();
      renderStepDurationChart();

      showToast('Live cohort telemetry synchronized.', 'success');
    } catch (err) {
      console.warn('Backend query failed, falling back to local dataset:', err);
      isDemoMode = true;
      renderTraineesTable(SEEDED_TRAINEES);
      renderLeaderboard(SEEDED_TRAINEES.map((t, i) => ({
        rank: i + 1,
        name: t.name,
        bestScore: t.bestScore,
        avgScore: t.avgScore,
        fastestTimeSeconds: t.userId === 3 ? 142.5 : t.userId === 4 ? 165.0 : t.userId === 6 ? 172.0 : 210.0,
      })));
      renderRecentActivity();
      renderKpiMetrics(SEEDED_TRAINEES);
      renderPerformanceTrendChart();
      renderStepDurationChart();
      showToast('Loaded pre-seeded simulation telemetry.', 'info');
    }
  }

  function renderKpiMetrics(trainees) {
    const kpiTrainees = document.getElementById('kpiTraineesCount');
    const kpiSessions = document.getElementById('kpiSessionsCount');
    const kpiAvgScore = document.getElementById('kpiAvgScore');
    const kpiAvgDur = document.getElementById('kpiAvgDuration');

    if (kpiTrainees) kpiTrainees.textContent = trainees.length;
    if (kpiSessions) kpiSessions.textContent = '20';

    const avg = trainees.reduce((acc, t) => acc + Number(t.avgScore), 0) / (trainees.length || 1);
    if (kpiAvgScore) kpiAvgScore.textContent = avg.toFixed(1);
    if (kpiAvgDur) kpiAvgDur.textContent = '172.5';
  }

  function renderTraineesTable(trainees) {
    const tbody = document.getElementById('traineesTableBody');
    if (!tbody) return;
    tbody.innerHTML = '';

    trainees.forEach((t) => {
      const tier = t.tier || (t.avgMistakes <= 2 ? 'harder' : t.avgMistakes <= 5 ? 'normal' : 'easier');
      const tr = document.createElement('tr');
      tr.innerHTML = `
        <td>
          <div class="trainee-cell-title">${t.name}</div>
          <div class="trainee-cell-sub">${t.email}</div>
        </td>
        <td class="text-center font-mono">${t.sessionsCount || 5}</td>
        <td class="text-right font-semibold ${t.avgScore >= 80 ? 'text-emerald-400' : 'text-cyan-400'}">${Number(t.avgScore).toFixed(1)}</td>
        <td class="text-right font-bold">${Number(t.bestScore).toFixed(1)}</td>
        <td class="text-center text-amber-400 font-mono">${Number(t.avgMistakes).toFixed(1)}</td>
        <td class="font-mono text-slate-300 text-xs">${t.weakestStep || 'None'}</td>
        <td>
          <span class="tier-pill tier-${tier}">
            ${tier.toUpperCase()} (${tier === 'harder' ? '1.35x' : tier === 'normal' ? '1.00x' : '0.70x'})
          </span>
        </td>
        <td class="text-right">
          <button class="btn btn-secondary btn-sm" onclick="window.DrillVRApp.inspectTrainee(${t.userId || t.id})">
            Inspect
          </button>
        </td>
      `;
      tbody.appendChild(tr);
    });
  }

  function renderLeaderboard(rows) {
    const list = document.getElementById('leaderboardList');
    if (!list) return;
    list.innerHTML = '';

    rows.slice(0, 4).forEach((r, idx) => {
      const rankClass = idx === 0 ? 'rank-1' : idx === 1 ? 'rank-2' : idx === 2 ? 'rank-3' : 'rank-other';
      const item = document.createElement('div');
      item.className = 'leader-item';
      item.innerHTML = `
        <div class="leader-rank ${rankClass}">${idx + 1}</div>
        <div class="leader-info">
          <div class="leader-name">${r.name}</div>
          <div class="leader-sub">Fastest: ${Number(r.fastestTimeSeconds).toFixed(1)}s</div>
        </div>
        <div class="leader-score">
          <div class="score-num">${Number(r.bestScore).toFixed(1)}</div>
          <div class="score-avg">Avg ${Number(r.avgScore).toFixed(1)}</div>
        </div>
      `;
      list.appendChild(item);
    });
  }

  function renderRecentActivity() {
    const list = document.getElementById('recentActivityList');
    if (!list) return;
    list.innerHTML = '';

    const activities = [
      { text: '<strong>Aarav Sharma</strong> finished Run #20 with Grade S (98.0 pts)', time: '12m ago' },
      { text: '<strong>Diya Patel</strong> finished Run #19 with Grade A (88.0 pts)', time: '48m ago' },
      { text: '<strong>Meera Nair</strong> cleared equipment cache with 2 mistakes', time: '2h ago' },
      { text: '<strong>Kabir Verma</strong> evacuated 2/3 casualties (Safe route delay)', time: '3h ago' },
    ];

    activities.forEach((act) => {
      const li = document.createElement('li');
      li.className = 'activity-item';
      li.innerHTML = `
        <span class="activity-icon-dot"></span>
        <div class="activity-text">
          <p>${act.text}</p>
          <span class="activity-time">${act.time}</span>
        </div>
      `;
      list.appendChild(li);
    });
  }

  // ---------------------------------------------------------------------------
  // 8. Trainee Detailed Inspection Drawer
  // ---------------------------------------------------------------------------
  async function inspectTrainee(userId) {
    const drawer = document.getElementById('traineeDetailDrawer');
    if (!drawer) return;

    try {
      const [historyRes, diffRes] = await Promise.all([
        apiFetch(`/api/users/${userId}/history`),
        apiFetch(`/api/users/${userId}/difficulty`),
      ]);

      const trainee = historyRes.user || SEEDED_TRAINEES.find((t) => t.userId === userId) || SEEDED_TRAINEES[0];
      const sessions = historyRes.sessions && historyRes.sessions.length > 0 ? historyRes.sessions : SEEDED_SESSIONS.filter((s) => s.userId === userId);
      const advice = (SEEDED_TRAINEES.find((t) => t.userId === userId) || {}).advice || 'Standard evaluation recorded.';

      document.getElementById('drawerName').textContent = trainee.name;
      document.getElementById('drawerAvatar').textContent = trainee.name.split(' ').map((n) => n[0]).join('').slice(0, 2);
      document.getElementById('drawerMeta').textContent = `${trainee.email} • ${sessions.length} recorded session(s) • Avg Mistakes: ${Number(diffRes.avgMistakes || 2.5).toFixed(1)}`;

      const badge = document.getElementById('drawerTierBadge');
      const tier = diffRes.tier || 'normal';
      badge.className = `tier-pill tier-${tier}`;
      badge.textContent = `${tier.toUpperCase()} (${Number(diffRes.difficultyMultiplier || 1.0).toFixed(2)}x)${diffRes.extraHints ? ' • EXTRA HINTS ON' : ''}`;

      document.getElementById('drawerAiAdvice').innerHTML = `<strong>Adaptive AI Engine Advice:</strong> ${advice}`;

      // Populate history rows
      const tbody = document.getElementById('drawerSessionsBody');
      tbody.innerHTML = '';
      sessions.forEach((s) => {
        const tr = document.createElement('tr');
        tr.innerHTML = `
          <td class="font-mono text-cyan-400 font-semibold">#${s.id}</td>
          <td class="text-muted text-xs">${s.date || new Date(s.completedAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}</td>
          <td class="font-mono text-amber-300 text-xs">${Number(s.mult || s.difficultyMultiplier || 1.0).toFixed(2)}x</td>
          <td class="text-right font-bold ${Number(s.score || s.totalScore) >= 80 ? 'text-emerald-400' : 'text-amber-400'}">${Number(s.score || s.totalScore).toFixed(1)}</td>
          <td class="text-right font-mono text-xs">${Number(s.time || s.totalTimeSeconds).toFixed(1)}s</td>
          <td class="text-center font-semibold text-amber-400">${s.mistakes !== undefined ? s.mistakes : s.totalMistakes}</td>
          <td class="text-center text-emerald-400 font-semibold">${s.rescued || `${s.casualtiesEvacuated || 3}/${s.casualtiesTotal || 3}`}</td>
          <td class="text-center">
            <span class="status-pill ${s.passed ? 'pass' : 'fail'}">${s.passed ? 'PASS' : 'FAIL'}</span>
          </td>
          <td class="text-xs font-mono text-slate-300">${s.weakest || s.weakestStepKey || 'None'}</td>
        `;
        tbody.appendChild(tr);
      });

      drawer.classList.remove('hidden');
      drawer.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
      showToast(`Loaded telemetry for ${trainee.name}`, 'info');
    } catch (err) {
      showToast(`Inspect error: ${err.message}`, 'error');
    }
  }

  // ---------------------------------------------------------------------------
  // 9. Secondary Views (Sessions, Trainees, Analytics)
  // ---------------------------------------------------------------------------
  function renderAllSessionsView() {
    const tbody = document.getElementById('allSessionsTableBody');
    const statusSelect = document.getElementById('sessionStatusFilter');
    const searchInput = document.getElementById('sessionSearchInput');
    const resetBtn = document.getElementById('sessionResetFiltersBtn');
    const countBadge = document.getElementById('sessionFilterCount');
    if (!tbody) return;
    tbody.innerHTML = '';

    const statusFilter = statusSelect ? statusSelect.value : 'all';
    const searchQuery = searchInput ? searchInput.value.trim().toLowerCase() : '';

    let sessions = [...SEEDED_SESSIONS];
    const totalCount = sessions.length;

    if (statusFilter === 'passed') sessions = sessions.filter((s) => s.passed);
    if (statusFilter === 'failed') sessions = sessions.filter((s) => !s.passed);

    if (searchQuery) {
      sessions = sessions.filter((s) =>
        (s.trainee && s.trainee.toLowerCase().includes(searchQuery)) ||
        (s.scenario && s.scenario.toLowerCase().includes(searchQuery)) ||
        (s.weakest && s.weakest.toLowerCase().includes(searchQuery))
      );
    }

    if (resetBtn) {
      const isFiltered = statusFilter !== 'all' || searchQuery.length > 0;
      resetBtn.style.display = isFiltered ? 'inline-flex' : 'none';
    }

    if (countBadge) {
      countBadge.textContent = `${sessions.length} of ${totalCount} sessions`;
    }

    if (sessions.length === 0) {
      const emptyTr = document.createElement('tr');
      emptyTr.innerHTML = `
        <td colspan="11" class="text-center text-muted" style="padding: 2rem 1rem;">
          No training sessions match the selected filters.
        </td>
      `;
      tbody.appendChild(emptyTr);
      return;
    }

    sessions.forEach((s) => {
      const tr = document.createElement('tr');
      tr.innerHTML = `
        <td class="font-mono text-cyan-400 font-semibold">#${s.id}</td>
        <td class="font-semibold">${s.trainee}</td>
        <td class="text-muted text-xs">${s.scenario}</td>
        <td class="text-right font-bold ${s.score >= 80 ? 'text-emerald-400' : 'text-amber-400'}">${s.score.toFixed(1)}</td>
        <td class="text-right font-mono text-xs">${s.time.toFixed(1)}s</td>
        <td class="text-center text-amber-400 font-semibold">${s.mistakes}</td>
        <td class="text-center text-emerald-400">${s.rescued}</td>
        <td class="text-center font-mono text-xs">${s.mult.toFixed(2)}x</td>
        <td class="text-center"><span class="status-pill ${s.passed ? 'pass' : 'fail'}">${s.passed ? 'PASS' : 'FAIL'}</span></td>
        <td class="font-mono text-xs text-slate-300">${s.weakest}</td>
        <td class="text-muted text-xs">${s.date}</td>
      `;
      tbody.appendChild(tr);
    });
  }

  function renderTraineeCardsView() {
    const container = document.getElementById('traineeCardsContainer');
    if (!container) return;
    container.innerHTML = '';

    SEEDED_TRAINEES.forEach((t) => {
      const card = document.createElement('div');
      card.className = 'trainee-card';
      card.innerHTML = `
        <div>
          <div class="trainee-card-top">
            <div class="user-avatar">${t.name.split(' ').map((n) => n[0]).join('')}</div>
            <div>
              <h4 class="font-bold text-sm">${t.name}</h4>
              <p class="text-xs text-muted">${t.email}</p>
            </div>
          </div>
          <span class="tier-pill tier-${t.tier} text-xs">
            ${t.tier.toUpperCase()} (${t.multiplier.toFixed(2)}x)
          </span>
          <div class="trainee-card-stats">
            <div><span class="text-muted">Avg Score:</span> <strong class="${t.avgScore >= 80 ? 'text-emerald-400' : 'text-cyan-400'}">${t.avgScore.toFixed(1)}</strong></div>
            <div><span class="text-muted">Best Score:</span> <strong>${t.bestScore.toFixed(1)}</strong></div>
            <div><span class="text-muted">Drills:</span> <strong>5 Runs</strong></div>
            <div><span class="text-muted">Weakness:</span> <strong>${t.weakestStep}</strong></div>
          </div>
          <p class="text-xs text-secondary mt-2 line-clamp-2">${t.advice}</p>
        </div>
        <button class="btn btn-secondary btn-block btn-sm mt-4" onclick="window.DrillVRApp.inspectTrainee(${t.userId})">
          View Detailed Telemetry
        </button>
      `;
      container.appendChild(card);
    });
  }

  function renderDeepAnalyticsView() {
    renderMistakeDistributionChart();
  }

  function renderReportTimestamp() {
    const elem = document.getElementById('reportTimestamp');
    if (elem) elem.textContent = new Date().toLocaleDateString(undefined, { dateStyle: 'full' });
  }

  // ---------------------------------------------------------------------------
  // 10. Chart.js Initializers with Fallbacks
  // ---------------------------------------------------------------------------
  function renderPerformanceTrendChart() {
    const canvas = document.getElementById('mainScoreChart');
    if (!canvas) return;

    if (typeof Chart === 'undefined') {
      const fallback = document.getElementById('chartFallback');
      if (fallback) {
        fallback.classList.remove('hidden');
        fallback.innerHTML = `
          <svg viewBox="0 0 500 150" class="w-full h-full">
            <polyline fill="none" stroke="#38bdf8" stroke-width="3" points="20,120 120,40 220,70 320,30 420,20" />
            <polyline fill="none" stroke="#f59e0b" stroke-width="2" stroke-dasharray="4,4" points="20,80 120,110 220,100 320,130 420,140" />
            <text x="420" y="35" fill="#38bdf8" font-size="11">Score: 98</text>
            <text x="420" y="130" fill="#f59e0b" font-size="11">Mistakes: 1</text>
          </svg>
        `;
      }
      return;
    }

    if (chartInstances.score) chartInstances.score.destroy();

    const ctx = canvas.getContext('2d');
    chartInstances.score = new Chart(ctx, {
      type: 'line',
      data: {
        labels: ['Run #1', 'Run #2', 'Run #3', 'Run #4', 'Run #5', 'Run #6', 'Run #7', 'Run #8'],
        datasets: [
          {
            label: 'Score (0-100)',
            data: [72, 78, 81, 85, 88, 91, 95, 98],
            borderColor: '#38BDF8',
            backgroundColor: 'rgba(56, 189, 248, 0.1)',
            fill: true,
            tension: 0.35,
            yAxisID: 'y',
            pointRadius: 4,
            pointBackgroundColor: '#38BDF8',
          },
          {
            label: 'Mistakes',
            data: [5, 4, 3, 3, 2, 1, 1, 1],
            borderColor: '#F59E0B',
            backgroundColor: 'transparent',
            borderDash: [4, 4],
            tension: 0.2,
            yAxisID: 'y1',
            pointRadius: 3,
            pointBackgroundColor: '#F59E0B',
          },
        ],
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        plugins: {
          legend: { display: false },
          tooltip: {
            backgroundColor: '#0F172A',
            titleColor: '#F8FAFC',
            bodyColor: '#94A3B8',
            borderColor: '#334155',
            borderWidth: 1,
          },
        },
        scales: {
          x: { ticks: { color: '#64748B' }, grid: { color: '#1E293B' } },
          y: {
            min: 50,
            max: 100,
            ticks: { color: '#38BDF8' },
            grid: { color: '#1E293B' },
          },
          y1: {
            position: 'right',
            min: 0,
            max: 8,
            ticks: { color: '#F59E0B', stepSize: 2 },
            grid: { drawOnChartArea: false },
          },
        },
      },
    });
  }

  function renderStepDurationChart() {
    const canvas = document.getElementById('stepDurationChart');
    if (!canvas || typeof Chart === 'undefined') return;
    if (chartInstances.step) chartInstances.step.destroy();

    const ctx = canvas.getContext('2d');
    chartInstances.step = new Chart(ctx, {
      type: 'bar',
      data: {
        labels: ['Briefing', 'Assess', 'Equip', 'Search', 'Evacuate', 'Report', 'Debrief'],
        datasets: [
          {
            label: 'Actual Avg Time (s)',
            data: STEP_BOTTLENECKS.map((b) => b.actual),
            backgroundColor: 'rgba(56, 189, 248, 0.85)',
            borderRadius: 4,
          },
          {
            label: 'Par Target (s)',
            data: STEP_BOTTLENECKS.map((b) => b.target),
            backgroundColor: 'rgba(100, 116, 139, 0.4)',
            borderRadius: 4,
          },
        ],
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        plugins: {
          legend: { labels: { color: '#94A3B8', font: { size: 10 } } },
        },
        scales: {
          x: { ticks: { color: '#64748B', font: { size: 9 } }, grid: { display: false } },
          y: { ticks: { color: '#64748B' }, grid: { color: '#1E293B' } },
        },
      },
    });
  }

  function renderMistakeDistributionChart() {
    const canvas = document.getElementById('mistakeDistributionChart');
    if (!canvas || typeof Chart === 'undefined') return;
    if (chartInstances.mistakes) chartInstances.mistakes.destroy();

    const ctx = canvas.getContext('2d');
    chartInstances.mistakes = new Chart(ctx, {
      type: 'bar',
      data: {
        labels: STEP_BOTTLENECKS.map((s) => s.step),
        datasets: [
          {
            label: 'Total Mistakes Accumulated Across Cohort',
            data: STEP_BOTTLENECKS.map((s) => s.mistakes),
            backgroundColor: [
              '#38BDF8',
              '#F43F5E', // Step 2 high
              '#F59E0B', // Step 3
              '#10B981',
              '#EF4444', // Step 5 high
              '#6366F1',
              '#14B8A6',
            ],
            borderRadius: 4,
          },
        ],
      },
      options: {
        indexAxis: 'y',
        responsive: true,
        maintainAspectRatio: false,
        plugins: {
          legend: { display: false },
        },
        scales: {
          x: { ticks: { color: '#64748B' }, grid: { color: '#1E293B' } },
          y: { ticks: { color: '#F8FAFC' }, grid: { display: false } },
        },
      },
    });
  }

  // ---------------------------------------------------------------------------
  // 11. CSV Export Handler
  // ---------------------------------------------------------------------------
  function exportSessionsCsv() {
    const headers = [
      'Session ID',
      'Trainee',
      'Scenario',
      'Total Score',
      'Steps Score (0-40)',
      'Evacuation Score (0-40)',
      'Time Bonus (0-20)',
      'Mistake Penalty',
      'Duration Seconds',
      'Mistakes',
      'Casualties Rescued',
      'Hazard Multiplier',
      'Status',
      'Weakest Step',
      'Completed Date'
    ];

    const sourceData = (window._drillvrLoadedSessions && window._drillvrLoadedSessions.length > 0)
      ? window._drillvrLoadedSessions
      : SEEDED_SESSIONS;

    const rows = sourceData.map((s) => [
      s.id,
      `"${s.trainee || s.userName || 'Trainee'}"`,
      `"${s.scenario || s.scenarioTitle || 'Building Emergency'}"`,
      s.totalScore ?? s.score ?? 0,
      s.stepsScore ?? 40,
      s.evacuationScore ?? 40,
      s.timeBonusScore ?? 10,
      s.mistakePenalty ?? (s.mistakes ? s.mistakes * 5 : 0),
      s.totalTimeSeconds ?? s.time ?? 0,
      s.totalMistakes ?? s.mistakes ?? 0,
      `"${s.casualtiesEvacuated ?? 3}/${s.casualtiesTotal ?? 3}"`,
      s.difficultyMultiplier ?? s.mult ?? 1.0,
      (s.passed !== false && s.passed !== 0) ? 'PASS' : 'FAIL',
      `"${s.weakestStepKey || s.weakest || 'None'}"`,
      `"${s.completedAt || s.date || new Date().toISOString()}"`,
    ]);

    const csvContent = 'data:text/csv;charset=utf-8,' + [headers.join(','), ...rows.map((e) => e.join(','))].join('\n');
    const encodedUri = encodeURI(csvContent);
    const link = document.createElement('a');
    link.setAttribute('href', encodedUri);
    link.setAttribute('download', `DrillVR_Cohort_Telemetry_${Date.now()}.csv`);
    document.body.appendChild(link);
    link.click();
    link.remove();

    showToast(`Exported ${rows.length} session telemetry rows to CSV.`, 'success');
  }

  // ---------------------------------------------------------------------------
  // 12. Event Listeners & Wire-up
  // ---------------------------------------------------------------------------
  function initializeEventListeners() {
    // Password Visibility Toggle
    const togglePw = document.getElementById('togglePasswordBtn');
    const pwInput = document.getElementById('passwordInput');
    const pwText = document.getElementById('pwToggleText');
    if (togglePw && pwInput) {
      togglePw.addEventListener('click', () => {
        const isPw = pwInput.type === 'password';
        pwInput.type = isPw ? 'text' : 'password';
        if (pwText) pwText.textContent = isPw ? 'Hide' : 'Show';
      });
    }

    // API Config Accordion
    const toggleApi = document.getElementById('toggleApiConfigBtn');
    const apiBody = document.getElementById('apiConfigBody');
    const apiArrow = document.getElementById('accordionArrow');
    if (toggleApi && apiBody) {
      toggleApi.addEventListener('click', () => {
        const isHidden = apiBody.classList.contains('hidden');
        apiBody.classList.toggle('hidden', !isHidden);
        if (apiArrow) apiArrow.textContent = isHidden ? '▴' : '▾';
      });
    }

    // Save API Base
    const saveApiBtn = document.getElementById('saveApiBaseBtn');
    const apiInput = document.getElementById('apiBaseInput');
    const apiDisplay = document.getElementById('apiDisplayUrl');
    if (saveApiBtn && apiInput) {
      saveApiBtn.addEventListener('click', () => {
        apiBase = apiInput.value.trim().replace(/\/+$/, '');
        localStorage.setItem(STORAGE_URL_KEY, apiBase);
        if (apiDisplay) apiDisplay.textContent = apiBase;
        checkApiHealth();
        showToast(`API Base URL saved: ${apiBase}`, 'success');
      });
    }

    // Fill Demo Credentials Button
    const fillDemoBtn = document.getElementById('fillDemoBtn');
    const emailInput = document.getElementById('emailInput');
    if (fillDemoBtn && emailInput && pwInput) {
      fillDemoBtn.addEventListener('click', () => {
        emailInput.value = 'instructor@drillvr.edu';
        pwInput.value = 'Instructor@123';
        showToast('Demo instructor credentials inserted.', 'info');
      });
    }

    // Instant Demo Preview Button
    const previewBtn = document.getElementById('previewDemoBtn');
    if (previewBtn) {
      previewBtn.addEventListener('click', () => {
        isDemoMode = true;
        showDashboard({ name: 'Dr. Vikram Rao', role: 'Lead Instructor' });
        showToast('Entered Instant Demo Mode (No backend required).', 'success');
      });
    }

    // Login Form Submit Handler
    const loginForm = document.getElementById('loginForm');
    const signInBtn = document.getElementById('signInBtn');
    const loginError = document.getElementById('loginError');

    if (loginForm) {
      loginForm.addEventListener('submit', async (e) => {
        e.preventDefault();
        loginError.classList.add('hidden');
        loginError.textContent = '';

        const email = emailInput.value.trim();
        const password = pwInput.value;
        const spinner = signInBtn.querySelector('.btn-spinner');
        const btnText = signInBtn.querySelector('.btn-text');

        if (spinner) spinner.classList.remove('hidden');
        if (btnText) btnText.textContent = 'Authenticating...';

        try {
          const data = await apiFetch('/api/auth/login', {
            method: 'POST',
            body: JSON.stringify({ email, password }),
          });

          if (!data.user || (data.user.role !== 'instructor' && data.user.role !== 'admin')) {
            throw new Error('Instructor or Admin privileges required for command center access.');
          }

          authToken = data.token;
          localStorage.setItem(STORAGE_TOKEN_KEY, authToken);
          localStorage.setItem('DrillVR_AuthToken', authToken);
          localStorage.setItem(STORAGE_USER_KEY, JSON.stringify(data.user));
          localStorage.setItem('DrillVR_CurrentUser', JSON.stringify(data.user));
          showDashboard(data.user);
          showToast(`Welcome back, ${data.user.name}`, 'success');
        } catch (err) {
          console.warn('Real login rejected or connection dropped:', err.message);
          // If offline, seamlessly fall back to demo preview
          isDemoMode = true;
          const fallbackUser = {
            name: email ? email.split('@')[0].toUpperCase() : 'Dr. Vikram Rao',
            role: 'Lead Instructor (Demo)',
          };
          showDashboard(fallbackUser);
          showToast(`Backend :4000 unavailable. Loaded in Demo Preview.`, 'info');
        } finally {
          if (spinner) spinner.classList.add('hidden');
          if (btnText) btnText.textContent = 'Sign In to Dashboard';
        }
      });
    }

    // Navigation Links (Sidebar)
    document.querySelectorAll('.nav-link').forEach((link) => {
      link.addEventListener('click', (e) => {
        const view = e.currentTarget.dataset.view;
        if (view) switchView(view);
      });
    });

    // Keyboard navigation shortcuts (1-8 to switch views)
    const viewShortcuts = {
      '1': 'overview',
      '2': 'simulation',
      '3': 'sessions',
      '4': 'trainees',
      '5': 'analytics',
      '6': 'scenarios',
      '7': 'reports',
      '8': 'settings',
      '9': 'unity-webgl',
    };

    window.addEventListener('keydown', (e) => {
      if (['INPUT', 'TEXTAREA', 'SELECT'].includes(document.activeElement?.tagName)) {
        return;
      }
      if (document.activeElement?.id === 'unity-dashboard-canvas' || e.target?.id === 'unity-dashboard-canvas') {
        return;
      }
      if (e.key === 'Escape') {
        return;
      }
      if (e.metaKey || e.ctrlKey || e.altKey) {
        return;
      }
      const targetView = viewShortcuts[e.key];
      if (targetView) {
        e.preventDefault();
        switchView(targetView);
      }
    });

    // Sidebar Collapse Toggle
    const collapseBtn = document.getElementById('sidebarCollapseBtn');
    const sidebar = document.getElementById('sidebar');
    if (collapseBtn && sidebar) {
      collapseBtn.addEventListener('click', () => {
        sidebar.classList.toggle('collapsed');
      });
    }

    // Mobile Menu Toggle
    const mobileBtn = document.getElementById('mobileMenuBtn');
    if (mobileBtn && sidebar) {
      mobileBtn.addEventListener('click', () => {
        sidebar.classList.toggle('open');
      });
    }

    // Notifications Dropdown Toggle
    const notifBtn = document.getElementById('notificationBtn');
    const notifMenu = document.getElementById('notificationDropdown');
    if (notifBtn && notifMenu) {
      notifBtn.addEventListener('click', (e) => {
        e.stopPropagation();
        notifMenu.classList.toggle('hidden');
      });
      document.addEventListener('click', () => notifMenu.classList.add('hidden'));
    }

    // Refresh Data Button
    const refreshBtn = document.getElementById('refreshDataBtn');
    if (refreshBtn) {
      refreshBtn.addEventListener('click', () => {
        loadDashboardData();
        showToast('Refreshing live telemetry...', 'info');
      });
    }

    // Close Detail Drawer
    const closeDrawerBtn = document.getElementById('closeDrawerBtn');
    const drawer = document.getElementById('traineeDetailDrawer');
    if (closeDrawerBtn && drawer) {
      closeDrawerBtn.addEventListener('click', () => drawer.classList.add('hidden'));
    }

    // Export CSV
    const exportBtn = document.getElementById('exportCsvBtn');
    if (exportBtn) exportBtn.addEventListener('click', exportSessionsCsv);

    // Print PDF
    const printBtn = document.getElementById('downloadPdfBtn');
    if (printBtn) printBtn.addEventListener('click', () => window.print());

    // Sessions Status & Search Filters
    const statusFilter = document.getElementById('sessionStatusFilter');
    if (statusFilter) statusFilter.addEventListener('change', renderAllSessionsView);

    const sessionSearch = document.getElementById('sessionSearchInput');
    if (sessionSearch) sessionSearch.addEventListener('input', renderAllSessionsView);

    const sessionReset = document.getElementById('sessionResetFiltersBtn');
    if (sessionReset) {
      sessionReset.addEventListener('click', () => {
        if (statusFilter) statusFilter.value = 'all';
        if (sessionSearch) sessionSearch.value = '';
        renderAllSessionsView();
      });
    }

    // Trainee Search Filter (Overview Table)
    const filterInput = document.getElementById('traineeFilterInput');
    if (filterInput) {
      filterInput.addEventListener('input', (e) => {
        const query = e.target.value.toLowerCase();
        const filtered = SEEDED_TRAINEES.filter((t) => t.name.toLowerCase().includes(query) || t.email.toLowerCase().includes(query));
        renderTraineesTable(filtered);
      });
    }

    // Global Search Bar Keyboard Shortcut (⌘K / Ctrl+K)
    const globalSearch = document.getElementById('globalSearchInput');
    document.addEventListener('keydown', (e) => {
      if (document.activeElement?.id === 'unity-dashboard-canvas' || e.target?.id === 'unity-dashboard-canvas') {
        return;
      }
      if ((e.metaKey || e.ctrlKey) && e.key === 'k') {
        e.preventDefault();
        if (globalSearch) globalSearch.focus();
      }
    });

    // Launch Simulation (Switch to 3D Viewport)
    const launchBtn = document.getElementById('launchSimBtn');
    const simModal = document.getElementById('simModal');
    const closeSimBtn = document.getElementById('closeSimModalBtn');
    const dismissSimBtn = document.getElementById('dismissSimModalBtn');

    if (launchBtn) {
      launchBtn.addEventListener('click', () => {
        switchView('simulation');
        if (window.DrillVRBridge && typeof window.DrillVRBridge.start === 'function') {
          window.DrillVRBridge.start();
        }
        showToast('Switched to Live 3D Simulation.', 'info');
      });
      if (closeSimBtn && simModal) closeSimBtn.addEventListener('click', () => simModal.classList.add('hidden'));
      if (dismissSimBtn && simModal) dismissSimBtn.addEventListener('click', () => simModal.classList.add('hidden'));
    }

    // Settings Save Button
    const settingsSaveBtn = document.getElementById('saveSettingsBtn');
    const settingsApiInput = document.getElementById('settingsApiUrl');
    if (settingsSaveBtn && settingsApiInput) {
      settingsSaveBtn.addEventListener('click', () => {
        apiBase = settingsApiInput.value.trim().replace(/\/+$/, '');
        localStorage.setItem(STORAGE_URL_KEY, apiBase);
        localStorage.setItem('DrillVR_ApiBaseUrl', apiBase);
        checkApiHealth();
        showToast('Settings saved successfully.', 'success');
      });
    }

    // Logout Button
    const logoutBtn = document.getElementById('logoutBtn');
    if (logoutBtn) {
      logoutBtn.addEventListener('click', () => {
        showLoginScreen();
        showToast('Signed out of command center.', 'info');
      });
    }

    // Native Unity WebGL Bridge Event Listener (Outbound notification from DrillVRBridge.jslib)
    // Only Unity's DrillBackendSync may POST sessions. The dashboard never POSTs sessions for the Unity build;
    // it simply listens for drill_ended and refreshes cohort telemetry.
    window.addEventListener('drillvr:drill_ended', (event) => {
      const detail = event.detail || {};
      console.log('[DrillVR Bridge] drill_ended event received:', detail);
      const score = Math.round(detail.totalScore || 0);
      if (detail.saved) {
        showToast(`Simulation drill finished (Score: ${score}). Telemetry saved to database.`, 'success');
      } else {
        showToast(`Simulation drill finished (Score: ${score}). Buffered in local queue.`, 'info');
      }
      loadDashboardData();
    });
  }

  // ---------------------------------------------------------------------------
  // 13. Application Bootstrap
  // ---------------------------------------------------------------------------
  window.addEventListener('DOMContentLoaded', () => {
    initializeEventListeners();
    checkApiHealth();

    // Export inspect and bridge methods to global namespace
    window.DrillVRApp = {
      inspectTrainee,
      switchView,
      refreshTelemetry: loadDashboardData,
      onLiveStepTelemetry: (detail) => {
        const timeStr = new Date().toLocaleTimeString();
        const activityList = document.getElementById('recentActivityList');
        if (activityList) {
          const li = document.createElement('li');
          li.className = 'activity-item info';
          li.innerHTML = `
            <span class="activity-time">${timeStr}</span>
            <span class="activity-text">Live 3D Step ${detail.stepNumber} complete (Score: ${detail.score}, Mistakes: ${detail.mistakes}).</span>
          `;
          activityList.prepend(li);
        }
      }
    };

    // Auto-login if previously authenticated
    if (authToken) {
      try {
        const cached = JSON.parse(localStorage.getItem(STORAGE_USER_KEY) || 'null');
        if (cached) {
          showDashboard(cached);
          return;
        }
      } catch {
        // ignore
      }
    }
  });
})();
