/**
 * DrillVR - Interactive 3D Emergency Drill Simulation Engine
 * Replicates the Unity 3D Milestone 2-8 Environment & Gameplay in WebGL/Three.js
 * Bi-directionally bridged with the DrillVR Instructor Command Center
 */

(function () {
  'use strict';

  // ---------------------------------------------------------------------------
  // 1. Simulation Constants & State
  // ---------------------------------------------------------------------------
  const DRILL_STEPS = [
    { number: 1, key: 'briefing', title: 'Confirm Briefing', instruction: 'Review emergency protocol and confirm the Briefing Board at the West Command Post.', expectedTime: 20 },
    { number: 2, key: 'assess_situation', title: 'Assess Situation', instruction: 'Enter the building corridor. Inspect the Smoke Hazard and verify the East Exit is blocked.', expectedTime: 35 },
    { number: 3, key: 'equipment_selection', title: 'Equip Protective Gear', instruction: 'Enter Room 1 (NW) and pick Respirator Mask, Flashlight, and MedKit. Avoid non-essential clutter!', expectedTime: 45 },
    { number: 4, key: 'search_casualties', title: 'Search & Locate Casualties', instruction: 'Search Room 2 (Office) and Room 4 (Conference) to muster 3 civilian casualties.', expectedTime: 60 },
    { number: 5, key: 'evacuate_safe_route', title: 'Evacuate via Safe Route', instruction: 'Lead casualties through the North Stairwell to the Safe Assembly Zone outside.', expectedTime: 50 },
    { number: 6, key: 'report_command_post', title: 'Report to Command Post', instruction: 'Return to the West Command Post Radio and submit the mission all-clear report.', expectedTime: 30 },
    { number: 7, key: 'debrief', title: 'Debrief & Assessment', instruction: 'Drill completed! Review telemetry, mistake breakdown, and sync scores with instructor database.', expectedTime: 15 }
  ];

  const SIM_STATE = {
    running: false,
    paused: false,
    completed: false,
    currentStepIndex: 0,
    startTime: 0,
    elapsedSeconds: 0,
    score: 100,
    mistakes: 0,
    activeTrainee: 'Diya Patel',
    activeTraineeEmail: 'diya@drillvr.edu',
    difficultyTier: 'normal',
    difficultyMultiplier: 1.0, // Aligned with backend: harder=1.35, normal=1.00, easier=0.70
    smokeRadius: 2.5,
    maxSmokeRadius: 11.0,
    smokeGrowthRate: 0.25, // m/s base rate
    playerInSmoke: false,
    smokeExposureSeconds: 0,
    cameraMode: 'fps', // 'fps' or 'tactical'
    pointerLocked: false,
    dwellTarget: null,
    dwellStartTime: 0,
    dwellDuration: 1.5, // seconds
    inventory: {
      respirator: false,
      flashlight: false,
      medkit: false
    },
    stepMetrics: DRILL_STEPS.map(s => ({ stepNumber: s.number, stepKey: s.key, title: s.title, timeSpent: 0, mistakes: 0, completed: false })),
    // Distinct civilian victims inside the facility (never reused as trainee accounts)
    casualties: [
      { id: 'cas1', name: 'Meera Sen (Accountant)', room: 'Room 2 (Office)', pos: [7.5, 0.8, 6.0], state: 'waiting', health: 100 },
      { id: 'cas2', name: 'Rohan Gupta (Facilities Staff)', room: 'Room 2 (Office)', pos: [9.0, 0.8, 4.0], state: 'waiting', health: 100 },
      { id: 'cas3', name: 'Sunita Rao (Building Visitor)', room: 'Room 4 (Conference)', pos: [8.0, 0.8, -6.0], state: 'waiting', health: 100 }
    ],
    logs: [],
    lastDebriefData: null
  };

  // Helper to dynamically resolve backend API base URL
  function getApiBaseUrl() {
    const saved = localStorage.getItem('DrillVR_ApiBaseUrl') || localStorage.getItem('drillvr_api_base');
    if (saved && saved.trim()) {
      return saved.trim().replace(/\/+$/, '');
    }
    if (window.location.hostname && window.location.port === '8080') {
      return `${window.location.protocol}//${window.location.hostname}:4000`;
    }
    return 'http://127.0.0.1:4000';
  }

  // Three.js Render Globals
  let scene, camera, renderer, animationFrameId;
  let canvasContainer, canvas;
  let floorGroup, wallsGroup, propsGroup, smokeGroup, casualtyGroup;
  let smokeParticles = [];
  let playerPos = new THREE.Vector3(-15.5, 1.7, 0); // Start at Command Post table
  let playerVelocity = new THREE.Vector3();
  let moveForward = false, moveBackward = false, moveLeft = false, moveRight = false;
  let pitch = 0, yaw = Math.PI / 2; // Face East initially
  let raycaster, centerScreen;
  let interactiveObjects = [];
  let clock;

  // ---------------------------------------------------------------------------
  // 2. Three.js Scene Setup (Exact Milestone 2-8 Building Geometry)
  // ---------------------------------------------------------------------------
  function initThreeScene() {
    canvasContainer = document.getElementById('simCanvasContainer');
    if (!canvasContainer) return;

    const width = canvasContainer.clientWidth || 960;
    const height = canvasContainer.clientHeight || 540;

    clock = new THREE.Clock();
    scene = new THREE.Scene();
    scene.background = new THREE.Color(0x060913); // Deep Command Center Dark
    scene.fog = new THREE.FogExp2(0x0b1329, 0.015);

    camera = new THREE.PerspectiveCamera(65, width / height, 0.1, 100);
    camera.position.copy(playerPos);

    renderer = new THREE.WebGLRenderer({ antialias: true, alpha: false, powerPreference: 'high-performance' });
    renderer.setSize(width, height);
    renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    renderer.shadowMap.enabled = true;
    renderer.shadowMap.type = THREE.PCFSoftShadowMap;

    // Clear previous canvases if any
    const existing = canvasContainer.querySelector('canvas');
    if (existing) existing.remove();
    canvas = renderer.domElement;
    canvas.id = 'drillvrSimCanvas';
    canvas.tabIndex = 1;
    canvasContainer.appendChild(canvas);

    raycaster = new THREE.Raycaster();
    centerScreen = new THREE.Vector2(0, 0);

    // Groups
    floorGroup = new THREE.Group();
    wallsGroup = new THREE.Group();
    propsGroup = new THREE.Group();
    smokeGroup = new THREE.Group();
    casualtyGroup = new THREE.Group();

    scene.add(floorGroup);
    scene.add(wallsGroup);
    scene.add(propsGroup);
    scene.add(smokeGroup);
    scene.add(casualtyGroup);

    setupLighting();
    buildBuildingGeometry();
    buildInteractiveProps();
    buildCasualties();
    buildSmokeSystem();
    setupEventListeners();

    onWindowResize();
    window.addEventListener('resize', onWindowResize);

    logSimEvent('Simulation engine initialized. Ready for emergency drill.');
    renderUIState();
    animate();
  }

  function setupLighting() {
    const ambientLight = new THREE.AmbientLight(0x4a5568, 1.2);
    scene.add(ambientLight);

    // Sun / Exterior Light
    const sunLight = new THREE.DirectionalLight(0x90cdf4, 1.4);
    sunLight.position.set(-20, 25, -15);
    sunLight.castShadow = true;
    sunLight.shadow.mapSize.width = 1024;
    sunLight.shadow.mapSize.height = 1024;
    scene.add(sunLight);

    // Corridor Emergency Light (Amber / Cyan)
    const corridorLight1 = new THREE.PointLight(0x38bdf8, 2.0, 15);
    corridorLight1.position.set(-6, 2.8, 0);
    scene.add(corridorLight1);

    const corridorLight2 = new THREE.PointLight(0xf59e0b, 2.5, 12);
    corridorLight2.position.set(6, 2.8, 0); // East corridor near smoke
    scene.add(corridorLight2);

    // Safe Exit Strobe (Green)
    const safeExitLight = new THREE.PointLight(0x10b981, 2.5, 12);
    safeExitLight.position.set(0, 2.8, 10);
    scene.add(safeExitLight);
  }

  function buildBuildingGeometry() {
    const floorMat = new THREE.MeshStandardMaterial({ color: 0x1e293b, roughness: 0.8, metalness: 0.1 });
    const extPadMat = new THREE.MeshStandardMaterial({ color: 0x0f172a, roughness: 0.9 });
    const wallMat = new THREE.MeshStandardMaterial({ color: 0x334155, roughness: 0.7 });
    const dividerMat = new THREE.MeshStandardMaterial({ color: 0x1e293b, roughness: 0.8 });
    const safeZoneMat = new THREE.MeshStandardMaterial({ color: 0x065f46, roughness: 0.4, emissive: 0x047857, emissiveIntensity: 0.2 });

    // Floors
    // Command post pad (West [-19, -12], Z [-5, 5])
    createBox(floorGroup, extPadMat, [-15.5, -0.1, 0], [7, 0.2, 10]);
    // Main building floor ([-12, 12], Z [-10, 10])
    createBox(floorGroup, floorMat, [0, -0.1, 0], [24, 0.2, 20]);
    // East Exit landing ([12, 16], Z [-3, 3])
    createBox(floorGroup, extPadMat, [14, -0.1, 0], [4, 0.2, 6]);
    // Safe Assembly Zone ([ -4, 4 ], Z [ 10, 15 ])
    const safeZone = createBox(floorGroup, safeZoneMat, [0, -0.05, 12.5], [8, 0.2, 5]);
    safeZone.userData = { id: 'safe_assembly_zone', label: 'Safe Assembly Area' };

    // Ceiling with subtle wireframe / glow
    const ceilingMat = new THREE.MeshStandardMaterial({ color: 0x0f172a, roughness: 0.9 });
    createBox(wallsGroup, ceilingMat, [0, 3.2, 0], [24.2, 0.2, 20.2]);

    // Outer Perimeter Walls (Height = 3.0m)
    // West Wall (X = -12) with 2.4m doorway at Z = 0
    createBox(wallsGroup, wallMat, [-12, 1.5, -5.6], [0.3, 3, 8.8]);
    createBox(wallsGroup, wallMat, [-12, 1.5, 5.6], [0.3, 3, 8.8]);
    createBox(wallsGroup, wallMat, [-12, 2.7, 0], [0.3, 0.6, 2.4]); // Door Header

    // East Wall (X = +12) with 2.4m Main Exit at Z = 0
    createBox(wallsGroup, wallMat, [12, 1.5, -5.6], [0.3, 3, 8.8]);
    createBox(wallsGroup, wallMat, [12, 1.5, 5.6], [0.3, 3, 8.8]);
    createBox(wallsGroup, wallMat, [12, 2.7, 0], [0.3, 0.6, 2.4]);

    // South Perimeter Wall (Z = -10, solid)
    createBox(wallsGroup, wallMat, [0, 1.5, -10], [24, 3, 0.3]);

    // North Perimeter Wall (Z = +10) with 2.2m Safe Exit doorway at X = 0
    createBox(wallsGroup, wallMat, [-6.55, 1.5, 10], [10.9, 3, 0.3]);
    createBox(wallsGroup, wallMat, [6.55, 1.5, 10], [10.9, 3, 0.3]);
    createBox(wallsGroup, wallMat, [0, 2.7, 10], [2.2, 0.6, 0.3]);

    // North Corridor Wall (Z = +2, doors at X = -8 (Equip), X = 0 (Stairwell), X = +8 (Office))
    createBox(wallsGroup, dividerMat, [-10.55, 1.5, 2], [2.9, 3, 0.25]);
    createBox(wallsGroup, dividerMat, [-4.0, 1.5, 2], [5.8, 3, 0.25]);
    createBox(wallsGroup, dividerMat, [4.0, 1.5, 2], [5.8, 3, 0.25]);
    createBox(wallsGroup, dividerMat, [10.55, 1.5, 2], [2.9, 3, 0.25]);
    createBox(wallsGroup, dividerMat, [-8, 2.7, 2], [2.2, 0.6, 0.25]);
    createBox(wallsGroup, dividerMat, [0, 2.7, 2], [2.2, 0.6, 0.25]);
    createBox(wallsGroup, dividerMat, [8, 2.7, 2], [2.2, 0.6, 0.25]);

    // South Corridor Wall (Z = -2, doors at X = -8 (Utility), X = +8 (Conf))
    createBox(wallsGroup, dividerMat, [-10.55, 1.5, -2], [2.9, 3, 0.25]);
    createBox(wallsGroup, dividerMat, [0, 1.5, -2], [13.8, 3, 0.25]);
    createBox(wallsGroup, dividerMat, [10.55, 1.5, -2], [2.9, 3, 0.25]);
    createBox(wallsGroup, dividerMat, [-8, 2.7, -2], [2.2, 0.6, 0.25]);
    createBox(wallsGroup, dividerMat, [8, 2.7, -2], [2.2, 0.6, 0.25]);

    // Interior Dividers
    // North Dividers between Equip, Stairwell, Office (X = -4 and X = +4, Z [2, 10])
    createBox(wallsGroup, dividerMat, [-4, 1.5, 6], [0.25, 3, 8]);
    createBox(wallsGroup, dividerMat, [4, 1.5, 6], [0.25, 3, 8]);
    // South Divider (X = 0, Z [-10, -2])
    createBox(wallsGroup, dividerMat, [0, 1.5, -6], [0.25, 3, 8]);

    // North Stairwell Steps (4 steps rising to Z = 10)
    const stairMat = new THREE.MeshStandardMaterial({ color: 0x475569 });
    for (let i = 0; i < 4; i++) {
      const stepH = (i + 1) * 0.15;
      createBox(floorGroup, stairMat, [0, stepH / 2, 4.2 + i * 0.65], [7.7, stepH, 0.65]);
    }

    // Room Label Decals / Overheads
    createRoomLabel('COMMAND POST', -15.5, 2.5, -3, 0x38bdf8);
    createRoomLabel('ROOM 1: EQUIPMENT', -8, 2.8, 3, 0x22d3ee);
    createRoomLabel('NORTH STAIRWELL (SAFE ROUTE)', 0, 2.8, 3, 0x10b981);
    createRoomLabel('ROOM 2: OFFICE', 8, 2.8, 3, 0x94a3b8);
    createRoomLabel('ROOM 4: CONFERENCE', 8, 2.8, -3, 0x94a3b8);
    createRoomLabel('EAST EXIT (BLOCKED BY DEBRIS)', 11.5, 2.5, 0, 0xef4444);
  }

  function createBox(parent, material, pos, size) {
    const geo = new THREE.BoxGeometry(size[0], size[1], size[2]);
    const mesh = new THREE.Mesh(geo, material);
    mesh.position.set(pos[0], pos[1], pos[2]);
    mesh.castShadow = true;
    mesh.receiveShadow = true;
    parent.add(mesh);
    return mesh;
  }

  function createRoomLabel(text, x, y, z, colorHex) {
    const canvas = document.createElement('canvas');
    canvas.width = 512;
    canvas.height = 128;
    const ctx = canvas.getContext('2d');
    ctx.fillStyle = '#080d1a';
    ctx.fillRect(0, 0, 512, 128);
    ctx.strokeStyle = '#38bdf8';
    ctx.lineWidth = 4;
    ctx.strokeRect(4, 4, 504, 120);
    ctx.fillStyle = colorHex ? `#${colorHex.toString(16).padStart(6, '0')}` : '#f8fafc';
    ctx.font = 'bold 30px "SF Pro Display", sans-serif';
    ctx.textAlign = 'center';
    ctx.textBaseline = 'middle';
    ctx.fillText(text, 256, 64);

    const texture = new THREE.CanvasTexture(canvas);
    const spriteMat = new THREE.SpriteMaterial({ map: texture, transparent: true });
    const sprite = new THREE.Sprite(spriteMat);
    sprite.position.set(x, y, z);
    sprite.scale.set(4, 1, 1);
    propsGroup.add(sprite);
  }

  function buildInteractiveProps() {
    interactiveObjects = [];

    // 1. Briefing Board at Command Post (Step 1 Target)
    const boardMat = new THREE.MeshStandardMaterial({ color: 0x0284c7, roughness: 0.3, emissive: 0x0369a1, emissiveIntensity: 0.3 });
    const briefingBoard = createBox(propsGroup, boardMat, [-15.5, 1.5, 3.5], [2.4, 1.6, 0.15]);
    briefingBoard.userData = {
      type: 'briefing_board',
      label: 'Emergency Briefing Protocol Board',
      action: 'Confirm Briefing Protocol',
      targetStep: 1
    };
    interactiveObjects.push(briefingBoard);

    // Command Post Radio Desk (Step 6 Target)
    const deskMat = new THREE.MeshStandardMaterial({ color: 0x334155, roughness: 0.5 });
    createBox(propsGroup, deskMat, [-15.5, 0.5, 0], [2.2, 0.8, 1.2]);

    const radioMat = new THREE.MeshStandardMaterial({ color: 0x10b981, roughness: 0.2, emissive: 0x059669, emissiveIntensity: 0.4 });
    const radioMesh = createBox(propsGroup, radioMat, [-15.5, 0.95, 0], [0.5, 0.25, 0.3]);
    radioMesh.userData = {
      type: 'command_radio',
      label: 'Command Post Tactical Radio',
      action: 'Report Evacuation Complete',
      targetStep: 6
    };
    interactiveObjects.push(radioMesh);

    // East Exit Debris / Barricade (Step 2 Inspection Target)
    const hazardMat = new THREE.MeshStandardMaterial({ color: 0xd97706, roughness: 0.4, emissive: 0xb45309, emissiveIntensity: 0.5 });
    const barricade = createBox(propsGroup, hazardMat, [11.8, 0.8, 0], [0.4, 1.4, 2.2]);
    barricade.userData = {
      type: 'blocked_exit',
      label: 'Blocked East Exit Barricade',
      action: 'Inspect Debris Hazard',
      targetStep: 2
    };
    interactiveObjects.push(barricade);

    // Smoke Sensor Warning Beacon in East Corridor (Step 2 Target)
    const smokeBeacon = createBox(propsGroup, hazardMat, [6.0, 2.2, 0], [0.3, 0.3, 0.3]);
    smokeBeacon.userData = {
      type: 'smoke_beacon',
      label: 'East Corridor Smoke Detector & Flare',
      action: 'Assess Smoke Growth Rate',
      targetStep: 2
    };
    interactiveObjects.push(smokeBeacon);

    // Room 1 Equipment Table & Items (Step 3 Targets)
    createBox(propsGroup, deskMat, [-8, 0.5, 6.0], [3.2, 0.8, 1.2]);

    // Required Equipment 1: Respirator Mask (Electric Blue)
    const respMat = new THREE.MeshStandardMaterial({ color: 0x38bdf8, roughness: 0.2, emissive: 0x0284c7, emissiveIntensity: 0.4 });
    const respMesh = createBox(propsGroup, respMat, [-9.0, 0.95, 6.0], [0.35, 0.2, 0.35]);
    respMesh.userData = {
      type: 'equipment',
      equipId: 'respirator',
      label: 'Industrial Smoke Respirator Mask',
      action: 'Equip Respirator (Smoke Protection)',
      required: true
    };
    interactiveObjects.push(respMesh);

    // Required Equipment 2: Emergency Flashlight (Amber Yellow)
    const torchMat = new THREE.MeshStandardMaterial({ color: 0xfacc15, roughness: 0.2, emissive: 0xeab308, emissiveIntensity: 0.4 });
    const torchMesh = createBox(propsGroup, torchMat, [-8.0, 0.95, 6.0], [0.15, 0.15, 0.45]);
    torchMesh.userData = {
      type: 'equipment',
      equipId: 'flashlight',
      label: 'Emergency Flashlight',
      action: 'Equip Flashlight',
      required: true
    };
    interactiveObjects.push(torchMesh);

    // Required Equipment 3: First Aid Medical Kit (Emerald Green)
    const medMat = new THREE.MeshStandardMaterial({ color: 0x10b981, roughness: 0.2, emissive: 0x059669, emissiveIntensity: 0.4 });
    const medMesh = createBox(propsGroup, medMat, [-7.0, 0.95, 6.0], [0.4, 0.25, 0.3]);
    medMesh.userData = {
      type: 'equipment',
      equipId: 'medkit',
      label: 'First Aid Trauma Kit',
      action: 'Equip First Aid Kit',
      required: true
    };
    interactiveObjects.push(medMesh);

    // Wrong Item 1: Office Laptop (Non-emergency Clutter -> Mistake penalty!)
    const laptopMat = new THREE.MeshStandardMaterial({ color: 0x64748b, roughness: 0.3 });
    const laptopMesh = createBox(propsGroup, laptopMat, [-8.5, 0.95, 5.5], [0.35, 0.05, 0.25]);
    laptopMesh.userData = {
      type: 'equipment',
      equipId: 'laptop',
      label: 'Office Laptop (Non-Essential)',
      action: 'Pick Up Clutter',
      required: false,
      mistakeReason: 'Wrong item: Office Laptop is not emergency response gear.'
    };
    interactiveObjects.push(laptopMesh);

    // Wrong Item 2: Cardboard Box (Flammable Clutter -> Mistake penalty!)
    const boxMat = new THREE.MeshStandardMaterial({ color: 0x92400e, roughness: 0.9 });
    const boxMesh = createBox(propsGroup, boxMat, [-7.2, 0.3, 7.5], [0.6, 0.6, 0.6]);
    boxMesh.userData = {
      type: 'equipment',
      equipId: 'paper_box',
      label: 'Cardboard Paper Box',
      action: 'Pick Up Flammable Box',
      required: false,
      mistakeReason: 'Wrong item: Cardboard Box slows evacuation and adds fire hazard.'
    };
    interactiveObjects.push(boxMesh);
  }

  function buildCasualties() {
    casualtyGroup.clear();

    SIM_STATE.casualties.forEach((cas) => {
      const casPivot = new THREE.Group();
      casPivot.position.set(cas.pos[0], cas.pos[1], cas.pos[2]);

      // Body Capsule
      const bodyMat = new THREE.MeshStandardMaterial({
        color: cas.state === 'mustered' ? 0x10b981 : (cas.state === 'safe' ? 0x059669 : 0xf97316),
        roughness: 0.5
      });
      const bodyGeo = new THREE.CylinderGeometry(0.28, 0.28, 1.4, 16);
      const body = new THREE.Mesh(bodyGeo, bodyMat);
      body.castShadow = true;
      casPivot.add(body);

      // Head
      const headMat = new THREE.MeshStandardMaterial({ color: 0xfed7aa, roughness: 0.6 });
      const headGeo = new THREE.SphereGeometry(0.22, 16, 16);
      const head = new THREE.Mesh(headGeo, headMat);
      head.position.y = 0.9;
      casPivot.add(head);

      // Interactive Click Target
      body.userData = {
        type: 'casualty',
        casId: cas.id,
        label: cas.name,
        action: cas.state === 'mustered' ? 'Following You' : 'Muster & Guide to Safe Route'
      };
      interactiveObjects.push(body);

      cas.mesh = casPivot;
      cas.bodyMesh = body;
      casualtyGroup.add(casPivot);
    });
  }

  function buildSmokeSystem() {
    smokeGroup.clear();
    smokeParticles = [];

    const particleCount = 180;
    const smokeGeo = new THREE.SphereGeometry(0.65, 8, 8);
    const smokeMat = new THREE.MeshBasicMaterial({
      color: 0x475569,
      transparent: true,
      opacity: 0.42,
      depthWrite: false
    });

    for (let i = 0; i < particleCount; i++) {
      const p = new THREE.Mesh(smokeGeo, smokeMat);
      // Spawn near East Corridor (X = 9.0, Z = 0)
      const angle = Math.random() * Math.PI * 2;
      const dist = Math.random() * SIM_STATE.smokeRadius;
      p.position.set(
        9.0 + Math.cos(angle) * dist,
        0.5 + Math.random() * 2.2,
        Math.sin(angle) * dist
      );
      p.scale.setScalar(0.7 + Math.random() * 0.8);
      p.userData = {
        baseX: 9.0,
        baseZ: 0,
        speed: 0.2 + Math.random() * 0.4,
        rotSpeed: (Math.random() - 0.5) * 0.02,
        angle: angle,
        distRatio: Math.random()
      };
      smokeGroup.add(p);
      smokeParticles.push(p);
    }
  }

  // ---------------------------------------------------------------------------
  // 3. Simulation Loop & Real-Time Logic
  // ---------------------------------------------------------------------------
  function animate() {
    animationFrameId = requestAnimationFrame(animate);

    if (SIM_STATE.running && !SIM_STATE.paused && !SIM_STATE.completed) {
      const delta = clock.getDelta();
      SIM_STATE.elapsedSeconds += delta;

      // Update current step time
      const currStep = SIM_STATE.stepMetrics[SIM_STATE.currentStepIndex];
      if (currStep) currStep.timeSpent += delta;

      // Update Expanding Smoke Hazard
      updateSmoke(delta);

      // Check Player Smoke Exposure
      checkSmokeHazard(delta);

      // Update Casualty AI movements
      updateCasualties(delta);

      // Handle Gaze / Reticle Dwell
      updateGazeInteraction(delta);

      // Handle Keyboard Movement
      updatePlayerMovement(delta);

      // Refresh HUD telemetry
      renderHUDTelemetry();
    } else {
      clock.getDelta(); // keep clock ticking smoothly
    }

    renderer.render(scene, camera);
  }

  function updateSmoke(delta) {
    // Dynamic difficulty smoke expansion rate (0.25 m/s base * difficultyMultiplier)
    const growth = SIM_STATE.smokeGrowthRate * delta;
    if (SIM_STATE.smokeRadius < SIM_STATE.maxSmokeRadius) {
      SIM_STATE.smokeRadius = Math.min(SIM_STATE.maxSmokeRadius, SIM_STATE.smokeRadius + growth);
    }

    // Animate volumetric smoke particles
    for (let i = 0; i < smokeParticles.length; i++) {
      const p = smokeParticles[i];
      p.rotation.y += p.userData.rotSpeed;
      p.userData.angle += p.userData.speed * delta * 0.3;

      const currentDist = p.userData.distRatio * SIM_STATE.smokeRadius;
      p.position.x = p.userData.baseX + Math.cos(p.userData.angle) * currentDist;
      p.position.z = p.userData.baseZ + Math.sin(p.userData.angle) * currentDist;
    }
  }

  function checkSmokeHazard(delta) {
    const distToSmokeCenter = new THREE.Vector2(playerPos.x - 9.0, playerPos.z - 0).length();
    const insideSmoke = distToSmokeCenter < SIM_STATE.smokeRadius && playerPos.x > -12 && playerPos.x < 12;

    const alertBanner = document.getElementById('simSmokeAlert');
    if (insideSmoke) {
      SIM_STATE.playerInSmoke = true;
      SIM_STATE.smokeExposureSeconds += delta;

      if (!SIM_STATE.inventory.respirator) {
        // Severe penalty without protective mask!
        SIM_STATE.score = Math.max(10, SIM_STATE.score - (4 * delta));
        if (alertBanner) {
          alertBanner.classList.remove('hidden');
          alertBanner.textContent = 'DANGER: Smoke exposure without Respirator! Health dropping!';
        }
      } else {
        if (alertBanner) {
          alertBanner.classList.remove('hidden');
          alertBanner.textContent = 'NOTICE: Respirator active. Smoke inhalation mitigated.';
        }
      }
    } else {
      SIM_STATE.playerInSmoke = false;
      if (alertBanner) alertBanner.classList.add('hidden');
    }
  }

  function updateCasualties(delta) {
    SIM_STATE.casualties.forEach((cas) => {
      if (cas.state === 'mustered') {
        // Follow player closely toward safe exit
        const target = new THREE.Vector3(playerPos.x, 0.8, playerPos.z);
        const dist = cas.mesh.position.distanceTo(target);

        if (dist > 1.8) {
          const dir = new THREE.Vector3().subVectors(target, cas.mesh.position).normalize();
          cas.mesh.position.addScaledVector(dir, 2.8 * delta);
          cas.mesh.lookAt(target.x, cas.mesh.position.y, target.z);
        }

        // Check if casualty entered Safe Assembly Zone (Z > 10.5 in North Stairwell)
        if (cas.mesh.position.z > 10.5 && Math.abs(cas.mesh.position.x) < 4.0) {
          cas.state = 'safe';
          cas.bodyMesh.material.color.setHex(0x059669);
          logSimEvent(`${cas.name} has safely reached the Safe Assembly Zone!`);

          // Check if all casualties safe -> auto-advance to step 5 completion
          const allSafe = SIM_STATE.casualties.every(c => c.state === 'safe');
          if (allSafe && SIM_STATE.currentStepIndex === 4) {
            completeStep(5);
          }
        }
      }
    });
  }

  function updatePlayerMovement(delta) {
    if (SIM_STATE.cameraMode !== 'fps') return;

    const speed = 4.2;
    playerVelocity.set(0, 0, 0);

    const forward = new THREE.Vector3(-Math.sin(yaw), 0, -Math.cos(yaw)).normalize();
    const right = new THREE.Vector3(forward.z, 0, -forward.x).normalize();

    if (moveForward) playerVelocity.add(forward);
    if (moveBackward) playerVelocity.sub(forward);
    if (moveRight) playerVelocity.add(right);
    if (moveLeft) playerVelocity.sub(right);

    if (playerVelocity.lengthSq() > 0) {
      playerVelocity.normalize().multiplyScalar(speed * delta);
      playerPos.add(playerVelocity);

      // Boundaries ([-18, 14], [-9.5, 14.5])
      playerPos.x = Math.max(-18.5, Math.min(15.5, playerPos.x));
      playerPos.z = Math.max(-9.5, Math.min(14.5, playerPos.z));

      camera.position.copy(playerPos);
    }
  }

  // ---------------------------------------------------------------------------
  // 4. Gaze-Dwell Interaction & Raycasting (VR Emulation)
  // ---------------------------------------------------------------------------
  function updateGazeInteraction(delta) {
    raycaster.setFromCamera(centerScreen, camera);
    const intersects = raycaster.intersectObjects(interactiveObjects, true);

    const promptEl = document.getElementById('simInteractionPrompt');
    const ringEl = document.getElementById('reticleDwellProgress');

    if (intersects.length > 0 && intersects[0].distance < 4.5) {
      let hitMesh = intersects[0].object;
      while (hitMesh && !hitMesh.userData.type && hitMesh.parent) {
        hitMesh = hitMesh.parent;
      }

      if (hitMesh && hitMesh.userData.type) {
        if (SIM_STATE.dwellTarget !== hitMesh) {
          SIM_STATE.dwellTarget = hitMesh;
          SIM_STATE.dwellStartTime = SIM_STATE.elapsedSeconds;
        }

        const dwellElapsed = SIM_STATE.elapsedSeconds - SIM_STATE.dwellStartTime;
        const progress = Math.min(1.0, dwellElapsed / SIM_STATE.dwellDuration);

        // Update circular dwell SVG
        if (ringEl) {
          const dashoffset = 126 * (1.0 - progress);
          ringEl.style.strokeDashoffset = dashoffset;
        }

        if (promptEl) {
          promptEl.classList.remove('hidden');
          promptEl.innerHTML = `<strong>${hitMesh.userData.label}</strong><br><span style="color: #38bdf8;">[Click or Dwell 1.5s] ${hitMesh.userData.action}</span>`;
        }

        // Trigger action upon dwell completion
        if (progress >= 1.0) {
          triggerObjectAction(hitMesh);
          SIM_STATE.dwellTarget = null;
          SIM_STATE.dwellStartTime = SIM_STATE.elapsedSeconds + 999;
          if (ringEl) ringEl.style.strokeDashoffset = 126;
        }
        return;
      }
    }

    // Nothing targeted
    SIM_STATE.dwellTarget = null;
    if (ringEl) ringEl.style.strokeDashoffset = 126;
    if (promptEl) promptEl.classList.add('hidden');
  }

  function triggerObjectAction(mesh) {
    const data = mesh.userData;
    if (!data) return;

    // Step 1: Confirm Briefing Board
    if (data.type === 'briefing_board') {
      if (SIM_STATE.currentStepIndex === 0) {
        completeStep(1);
        logSimEvent('Emergency Briefing confirmed. Objective: Enter building & assess hazards.');
      } else {
        logSimEvent('Briefing already confirmed.');
      }
      return;
    }

    // Step 2: Inspect Blocked Exit or Smoke Beacon
    if (data.type === 'blocked_exit' || data.type === 'smoke_beacon') {
      if (SIM_STATE.currentStepIndex === 1) {
        completeStep(2);
        logSimEvent('Hazards inspected: East Exit blocked by debris, corridor smoke growing. Objective: Room 1 Gear Selection.');
      } else {
        logSimEvent(`Inspected ${data.label}.`);
      }
      return;
    }

    // Step 3: Equipment Selection
    if (data.type === 'equipment') {
      if (data.required) {
        SIM_STATE.inventory[data.equipId] = true;
        mesh.visible = false; // picked up
        logSimEvent(`Equipped: ${data.label}`);

        // Check if all 3 required pieces are picked up
        if (SIM_STATE.inventory.respirator && SIM_STATE.inventory.flashlight && SIM_STATE.inventory.medkit) {
          if (SIM_STATE.currentStepIndex === 2) {
            completeStep(3);
            logSimEvent('All required equipment equipped! Proceed to Room 2 & 4 to search for casualties.');
          }
        }
      } else {
        // Wrong item penalty!
        SIM_STATE.mistakes++;
        SIM_STATE.stepMetrics[SIM_STATE.currentStepIndex].mistakes++;
        SIM_STATE.score = Math.max(10, SIM_STATE.score - 5);
        logSimEvent(`MISTAKE: ${data.mistakeReason}`, 'error');
        showSimBanner(`Penalty (-5 pts): ${data.mistakeReason}`);
      }
      renderInventoryUI();
      return;
    }

    // Step 4: Muster Casualty
    if (data.type === 'casualty') {
      const cas = SIM_STATE.casualties.find(c => c.id === data.casId);
      if (cas && cas.state === 'waiting') {
        cas.state = 'mustered';
        cas.bodyMesh.material.color.setHex(0x10b981);
        logSimEvent(`Mustered ${cas.name}. They are now following you to safety.`);

        // Check if all casualties are mustered
        const allMustered = SIM_STATE.casualties.every(c => c.state === 'mustered' || c.state === 'safe');
        if (allMustered && SIM_STATE.currentStepIndex === 3) {
          completeStep(4);
          logSimEvent('All casualties located! Objective: Lead them to the North Stairwell Safe Assembly Zone.');
        }
      }
      return;
    }

    // Step 6: Command Post Radio
    if (data.type === 'command_radio') {
      if (SIM_STATE.currentStepIndex === 5) {
        completeStep(6);
        completeStep(7); // Final debrief
        finishDrill();
      } else {
        logSimEvent('Command Radio can only be selected after evacuating casualties via the Safe Route.', 'warning');
      }
      return;
    }
  }

  function completeStep(stepNumber) {
    const stepIdx = stepNumber - 1;
    if (SIM_STATE.stepMetrics[stepIdx]) {
      SIM_STATE.stepMetrics[stepIdx].completed = true;
    }

    if (SIM_STATE.currentStepIndex < DRILL_STEPS.length - 1) {
      SIM_STATE.currentStepIndex = Math.min(DRILL_STEPS.length - 1, stepNumber);
    }

    // Dispatch telemetry through Bi-directional Bridge
    if (window.DrillVRBridge) {
      window.DrillVRBridge.dispatchStepProgress(
        stepNumber,
        SIM_STATE.score,
        SIM_STATE.mistakes,
        SIM_STATE.stepMetrics[stepIdx] ? SIM_STATE.stepMetrics[stepIdx].timeSpent : 0
      );
    }

    renderStepTimelineUI();
    renderInstructionCard();
  }

  // ---------------------------------------------------------------------------
  // 5. Dynamic Mathematical Score Calculation
  // ---------------------------------------------------------------------------
  function calculateDetailedScoreBreakdown() {
    const totalSteps = DRILL_STEPS.length;
    const completedSteps = SIM_STATE.stepMetrics.filter(s => s.completed).length;

    // 1. Steps score (max 40 pts): proportional to validated steps completed
    const stepsScore = Math.round((completedSteps / totalSteps) * 40);

    // 2. Evacuation score (max 40 pts): proportional to living casualties safely assembled
    const totalCas = SIM_STATE.casualties.length;
    const safeCas = SIM_STATE.casualties.filter(c => c.state === 'safe').length;
    const evacuationScore = Math.round((safeCas / totalCas) * 40);

    // 3. Time bonus (max 20 pts): rewarded if mission completed in under 300 seconds
    const timeLimit = 300;
    const timeRemaining = Math.max(0, timeLimit - SIM_STATE.elapsedSeconds);
    const timeBonusScore = completedSteps >= 6 ? Math.min(20, Math.round((timeRemaining / timeLimit) * 20)) : 0;

    // 4. Mistake penalty: 5 points per mistake
    const mistakePenalty = SIM_STATE.mistakes * 5;

    // 5. Total score clamped to 0..100
    const totalScore = Math.max(0, Math.min(100, stepsScore + evacuationScore + timeBonusScore - mistakePenalty));
    const passed = totalScore >= 60;

    // 6. Weakest step: find the step that had the highest mistakes or highest elapsed vs expected time ratio
    let worstStepKey = 'briefing';
    let worstPenaltyScore = -1;

    for (let i = 0; i < totalSteps; i++) {
      const metric = SIM_STATE.stepMetrics[i];
      const stepDef = DRILL_STEPS[i];
      const timeRatio = (metric.timeSpent || 0) / stepDef.expectedTime;
      const penaltyScore = (metric.mistakes || 0) * 3 + timeRatio;

      if (penaltyScore > worstPenaltyScore && (metric.mistakes > 0 || metric.timeSpent > stepDef.expectedTime)) {
        worstPenaltyScore = penaltyScore;
        worstStepKey = metric.stepKey;
      }
    }

    if (worstPenaltyScore <= 0 && completedSteps > 0) {
      let maxTime = -1;
      SIM_STATE.stepMetrics.forEach(s => {
        if (s.timeSpent > maxTime) {
          maxTime = s.timeSpent;
          worstStepKey = s.stepKey;
        }
      });
    }

    return {
      totalScore,
      stepsScore,
      evacuationScore,
      timeBonusScore,
      mistakePenalty,
      totalTimeSeconds: Math.round(SIM_STATE.elapsedSeconds),
      totalMistakes: SIM_STATE.mistakes,
      casualtiesEvacuated: safeCas,
      casualtiesTotal: totalCas,
      passed,
      weakestStepKey: worstStepKey || 'briefing'
    };
  }

  function finishDrill() {
    SIM_STATE.completed = true;
    SIM_STATE.running = false;

    const breakdown = calculateDetailedScoreBreakdown();
    SIM_STATE.score = breakdown.totalScore;
    SIM_STATE.lastDebriefData = breakdown;

    logSimEvent(
      `DRILL FINISHED! Score: ${breakdown.totalScore}/100 (Steps: ${breakdown.stepsScore}/40, Evacuation: ${breakdown.evacuationScore}/40, Time Bonus: ${breakdown.timeBonusScore}/20, Penalty: -${breakdown.mistakePenalty}). Weakest: ${breakdown.weakestStepKey}.`
    );

    // Show on-screen Debrief Summary
    showDebriefModal(breakdown);
    renderUIState();
  }

  function showDebriefModal(breakdown) {
    const existing = document.getElementById('simDebriefModal');
    if (existing) existing.remove();

    const modal = document.createElement('div');
    modal.id = 'simDebriefModal';
    modal.className = 'modal-backdrop';
    modal.innerHTML = `
      <div class="modal-card" style="max-width: 520px;">
        <div class="modal-header">
          <h3>Simulation Debrief — ${SIM_STATE.activeTrainee}</h3>
          <button class="icon-btn" onclick="this.closest('#simDebriefModal').remove()">✕</button>
        </div>
        <div class="modal-body">
          <div class="flex justify-between items-center mb-4">
            <div>
              <span class="text-xs text-muted">FINAL DRILL SCORE</span>
              <div class="text-2xl font-bold ${breakdown.passed ? 'text-emerald' : 'text-rose'}">
                ${breakdown.totalScore} / 100
                <span class="badge-tag ml-2 ${breakdown.passed ? 'alert-success' : 'alert-danger'}">${breakdown.passed ? 'PASSED' : 'NEEDS RETRAINING'}</span>
              </div>
            </div>
            <div class="text-right">
              <span class="text-xs text-muted">TIME ELAPSED</span>
              <div class="text-lg font-bold text-white">${breakdown.totalTimeSeconds}s</div>
            </div>
          </div>

          <div class="data-card p-3 mb-3 text-xs">
            <div class="flex justify-between py-1 border-b border-subtle">
              <span class="text-secondary">Steps Execution Score (Max 40)</span>
              <strong>${breakdown.stepsScore} pts</strong>
            </div>
            <div class="flex justify-between py-1 border-b border-subtle">
              <span class="text-secondary">Casualty Evacuation (${breakdown.casualtiesEvacuated}/${breakdown.casualtiesTotal} Safe) (Max 40)</span>
              <strong>${breakdown.evacuationScore} pts</strong>
            </div>
            <div class="flex justify-between py-1 border-b border-subtle">
              <span class="text-secondary">Time Bonus Score (Max 20)</span>
              <strong>${breakdown.timeBonusScore} pts</strong>
            </div>
            <div class="flex justify-between py-1 border-b border-subtle text-amber">
              <span>Mistake Penalty (${breakdown.totalMistakes} mistakes × 5)</span>
              <strong>-${breakdown.mistakePenalty} pts</strong>
            </div>
            <div class="flex justify-between py-1 text-cyan">
              <span>Identified Weakest Step</span>
              <strong>${breakdown.weakestStepKey}</strong>
            </div>
          </div>

          <p class="text-xs text-muted leading-relaxed">
            <em>Interactive Simulation Sandbox:</em> This browser preview operates locally to prevent polluting cohort statistics.
            To record an official session into MySQL, execute the drill using the mobile VR client.
          </p>
        </div>
        <div class="modal-footer">
          <button class="btn btn-secondary mr-2" onclick="this.closest('#simDebriefModal').remove()">Close Preview</button>
          <button class="btn btn-primary" onclick="window.DrillVRBridge.submitTestSessionExplicit()">Record Test Session (Evaluation)</button>
        </div>
      </div>
    `;
    document.body.appendChild(modal);
  }

  // ---------------------------------------------------------------------------
  // 6. User Input & Controls Setup
  // ---------------------------------------------------------------------------
  function setupEventListeners() {
    window.addEventListener('keydown', (e) => {
      if (!SIM_STATE.running) return;

      switch (e.code) {
        case 'KeyW':
        case 'ArrowUp':
          moveForward = true;
          break;
        case 'KeyS':
        case 'ArrowDown':
          moveBackward = true;
          break;
        case 'KeyA':
        case 'ArrowLeft':
          moveLeft = true;
          break;
        case 'KeyD':
        case 'ArrowRight':
          moveRight = true;
          break;
        case 'KeyL':
          togglePointerLock();
          break;
        case 'KeyT':
          toggleCameraMode();
          break;
      }
    });

    window.addEventListener('keyup', (e) => {
      switch (e.code) {
        case 'KeyW':
        case 'ArrowUp':
          moveForward = false;
          break;
        case 'KeyS':
        case 'ArrowDown':
          moveBackward = false;
          break;
        case 'KeyA':
        case 'ArrowLeft':
          moveLeft = false;
          break;
        case 'KeyD':
        case 'ArrowRight':
          moveRight = false;
          break;
      }
    });

    canvasContainer.addEventListener('click', () => {
      if (!SIM_STATE.pointerLocked && SIM_STATE.cameraMode === 'fps') {
        canvasContainer.requestPointerLock();
      } else if (SIM_STATE.dwellTarget) {
        triggerObjectAction(SIM_STATE.dwellTarget);
      }
    });

    document.addEventListener('pointerlockchange', () => {
      SIM_STATE.pointerLocked = (document.pointerLockElement === canvasContainer);
      const lockBadge = document.getElementById('simLockBadge');
      if (lockBadge) {
        lockBadge.textContent = SIM_STATE.pointerLocked ? 'CURSOR LOCKED (Press L to unlock)' : 'CLICK TO AIM (Press L to lock)';
        lockBadge.className = SIM_STATE.pointerLocked ? 'badge-tag text-emerald' : 'badge-tag text-cyan';
      }
    });

    document.addEventListener('mousemove', (e) => {
      if (SIM_STATE.pointerLocked && SIM_STATE.cameraMode === 'fps') {
        const sens = 0.0022;
        yaw -= e.movementX * sens;
        pitch -= e.movementY * sens;
        pitch = Math.max(-Math.PI / 2.3, Math.min(Math.PI / 2.3, pitch));

        camera.rotation.set(0, 0, 0);
        camera.rotation.y = yaw;
        camera.rotation.x = pitch;
      }
    });
  }

  function togglePointerLock() {
    if (document.pointerLockElement === canvasContainer) {
      document.exitPointerLock();
    } else {
      canvasContainer.requestPointerLock();
    }
  }

  function toggleCameraMode() {
    if (SIM_STATE.cameraMode === 'fps') {
      SIM_STATE.cameraMode = 'tactical';
      camera.position.set(0, 32, 0);
      camera.lookAt(0, 0, 0);
      logSimEvent('Camera switched to Overhead Tactical Floorplan View.');
    } else {
      SIM_STATE.cameraMode = 'fps';
      camera.position.copy(playerPos);
      camera.rotation.set(0, 0, 0);
      camera.rotation.y = yaw;
      camera.rotation.x = pitch;
      logSimEvent('Camera switched to First-Person Reticle View.');
    }
    const modeBtn = document.getElementById('simToggleCameraBtn');
    if (modeBtn) modeBtn.textContent = SIM_STATE.cameraMode === 'fps' ? 'Overhead Tactical (T)' : 'First-Person (T)';
  }

  function onWindowResize() {
    if (!canvasContainer || !renderer || !camera) return;
    const width = canvasContainer.clientWidth || 960;
    const height = canvasContainer.clientHeight || 540;
    camera.aspect = width / height;
    camera.updateProjectionMatrix();
    renderer.setSize(width, height);
  }

  // ---------------------------------------------------------------------------
  // 7. UI Rendering & HUD Data Binding
  // ---------------------------------------------------------------------------
  function renderUIState() {
    renderInstructionCard();
    renderStepTimelineUI();
    renderInventoryUI();
    renderCasualtyUI();
    renderHUDTelemetry();
  }

  function renderInstructionCard() {
    const step = DRILL_STEPS[SIM_STATE.currentStepIndex];
    if (!step) return;

    const badge = document.getElementById('simCurrentStepBadge');
    const title = document.getElementById('simCurrentStepTitle');
    const instr = document.getElementById('simCurrentStepInstruction');

    if (badge) badge.textContent = `STEP ${step.number} OF 7`;
    if (title) title.textContent = step.title;
    if (instr) instr.textContent = step.instruction;
  }

  function renderStepTimelineUI() {
    const container = document.getElementById('simStepTimelineList');
    if (!container) return;

    container.innerHTML = DRILL_STEPS.map((s, idx) => {
      const metric = SIM_STATE.stepMetrics[idx];
      const isCurrent = idx === SIM_STATE.currentStepIndex;
      const isDone = metric && metric.completed;

      let statusClass = 'pending';
      let icon = idx + 1;
      if (isDone) {
        statusClass = 'completed';
        icon = '✓';
      } else if (isCurrent) {
        statusClass = 'active';
      }

      return `
        <div class="sim-step-item ${statusClass}">
          <div class="step-num-circle">${icon}</div>
          <div class="step-info">
            <div class="step-header-row">
              <span class="step-name">${s.title}</span>
              <span class="step-duration">${metric.timeSpent > 0 ? `${metric.timeSpent.toFixed(1)}s` : `${s.expectedTime}s target`}</span>
            </div>
            <div class="step-desc-short">${s.instruction.slice(0, 48)}...</div>
          </div>
        </div>
      `;
    }).join('');
  }

  function renderInventoryUI() {
    const resp = document.getElementById('simInvRespirator');
    const torch = document.getElementById('simInvFlashlight');
    const med = document.getElementById('simInvMedkit');

    if (resp) resp.className = `inv-slot ${SIM_STATE.inventory.respirator ? 'equipped' : 'empty'}`;
    if (torch) torch.className = `inv-slot ${SIM_STATE.inventory.flashlight ? 'equipped' : 'empty'}`;
    if (med) med.className = `inv-slot ${SIM_STATE.inventory.medkit ? 'equipped' : 'empty'}`;
  }

  function renderCasualtyUI() {
    const container = document.getElementById('simCasualtyStatusList');
    if (!container) return;

    container.innerHTML = SIM_STATE.casualties.map((c) => {
      let badgeColor = '#94a3b8';
      let statusLabel = 'Unlocated';
      if (c.state === 'mustered') {
        badgeColor = '#38bdf8';
        statusLabel = 'Mustered (Following)';
      } else if (c.state === 'safe') {
        badgeColor = '#10b981';
        statusLabel = 'Safe Assembly';
      }

      return `
        <div class="casualty-status-card">
          <div class="casualty-header">
            <strong>${c.name}</strong>
            <span class="badge-tag" style="border-color: ${badgeColor}; color: ${badgeColor};">${statusLabel}</span>
          </div>
          <div class="text-xs text-muted mt-1">Location: ${c.room}</div>
        </div>
      `;
    }).join('');
  }

  function renderHUDTelemetry() {
    const timeEl = document.getElementById('simHudTime');
    const scoreEl = document.getElementById('simHudScore');
    const mistakeEl = document.getElementById('simHudMistakes');
    const casSafeEl = document.getElementById('simHudCasualties');
    const smokeEl = document.getElementById('simHudSmokeRadius');

    if (timeEl) timeEl.textContent = `${Math.floor(SIM_STATE.elapsedSeconds / 60)}:${Math.floor(SIM_STATE.elapsedSeconds % 60).toString().padStart(2, '0')}`;
    if (scoreEl) scoreEl.textContent = `${Math.round(SIM_STATE.score)}`;
    if (mistakeEl) mistakeEl.textContent = `${SIM_STATE.mistakes}`;
    if (casSafeEl) {
      const safeCount = SIM_STATE.casualties.filter(c => c.state === 'safe').length;
      casSafeEl.textContent = `${safeCount}/3`;
    }
    if (smokeEl) smokeEl.textContent = `${SIM_STATE.smokeRadius.toFixed(1)}m`;
  }

  function logSimEvent(msg, type = 'info') {
    const timeStr = new Date().toLocaleTimeString();
    SIM_STATE.logs.unshift({ time: timeStr, msg, type });
    if (SIM_STATE.logs.length > 30) SIM_STATE.logs.pop();

    const logContainer = document.getElementById('simEventLogList');
    if (!logContainer) return;

    logContainer.innerHTML = SIM_STATE.logs.map(l => `
      <li class="activity-item ${l.type}">
        <span class="activity-time">${l.time}</span>
        <span class="activity-text">${l.msg}</span>
      </li>
    `).join('');
  }

  function showSimBanner(msg) {
    const alertBanner = document.getElementById('simSmokeAlert');
    if (!alertBanner) return;
    alertBanner.textContent = msg;
    alertBanner.classList.remove('hidden');
    setTimeout(() => {
      if (!SIM_STATE.playerInSmoke) alertBanner.classList.add('hidden');
    }, 3500);
  }

  // ---------------------------------------------------------------------------
  // 8. Public Drill Controls API (Exposed to Global Scope)
  // ---------------------------------------------------------------------------
  function startSimulation() {
    if (SIM_STATE.completed) resetSimulation();
    SIM_STATE.running = true;
    SIM_STATE.paused = false;
    logSimEvent(`Simulation started for trainee ${SIM_STATE.activeTrainee} at ${SIM_STATE.difficultyTier} difficulty (${SIM_STATE.difficultyMultiplier}x smoke).`);
    renderUIState();
  }

  function pauseSimulation() {
    SIM_STATE.paused = !SIM_STATE.paused;
    logSimEvent(SIM_STATE.paused ? 'Simulation paused.' : 'Simulation resumed.');
  }

  function resetSimulation() {
    SIM_STATE.running = false;
    SIM_STATE.paused = false;
    SIM_STATE.completed = false;
    SIM_STATE.currentStepIndex = 0;
    SIM_STATE.elapsedSeconds = 0;
    SIM_STATE.score = 100;
    SIM_STATE.mistakes = 0;
    SIM_STATE.smokeRadius = 2.5;
    SIM_STATE.playerInSmoke = false;
    SIM_STATE.inventory = { respirator: false, flashlight: false, medkit: false };
    playerPos.set(-15.5, 1.7, 0);
    yaw = Math.PI / 2;
    pitch = 0;

    SIM_STATE.stepMetrics = DRILL_STEPS.map(s => ({ stepNumber: s.number, stepKey: s.key, title: s.title, timeSpent: 0, mistakes: 0, completed: false }));
    SIM_STATE.casualties = [
      { id: 'cas1', name: 'Meera Sen (Accountant)', room: 'Room 2 (Office)', pos: [7.5, 0.8, 6.0], state: 'waiting', health: 100 },
      { id: 'cas2', name: 'Rohan Gupta (Facilities Staff)', room: 'Room 2 (Office)', pos: [9.0, 0.8, 4.0], state: 'waiting', health: 100 },
      { id: 'cas3', name: 'Sunita Rao (Building Visitor)', room: 'Room 4 (Conference)', pos: [8.0, 0.8, -6.0], state: 'waiting', health: 100 }
    ];

    if (camera) {
      camera.position.copy(playerPos);
      camera.rotation.set(0, yaw, 0);
    }

    buildInteractiveProps();
    buildCasualties();
    buildSmokeSystem();
    renderUIState();
    logSimEvent('Simulation reset to initial Command Post state.');
  }

  function setTrainee(name, email) {
    SIM_STATE.activeTrainee = name;
    if (email) SIM_STATE.activeTraineeEmail = email;

    // Automatically synchronize AI difficulty with trainee's adaptive profile
    let targetTier = 'normal';
    if (email === 'aarav@drillvr.edu') targetTier = 'harder';
    else if (email === 'kabir@drillvr.edu') targetTier = 'easier';
    else if (email === 'diya@drillvr.edu' || email === 'meera@drillvr.edu') targetTier = 'normal';

    const diffSelect = document.getElementById('simDifficultySelect');
    if (diffSelect && email !== 'demo@drillvr.edu') {
      diffSelect.value = targetTier;
      setDifficulty(targetTier);
    }

    logSimEvent(`Selected evaluation profile: ${name} (${SIM_STATE.activeTraineeEmail || 'Demo'}) - Adaptive Tier: ${targetTier.toUpperCase()}`);
  }

  // Exact synchronization with backend/src/services/difficultyService.js
  function setDifficulty(tier) {
    SIM_STATE.difficultyTier = tier;
    if (tier === 'harder') {
      SIM_STATE.difficultyMultiplier = 1.35;
      SIM_STATE.smokeGrowthRate = 0.25 * 1.35;
    } else if (tier === 'easier') {
      SIM_STATE.difficultyMultiplier = 0.70;
      SIM_STATE.smokeGrowthRate = 0.25 * 0.70;
    } else {
      SIM_STATE.difficultyMultiplier = 1.00;
      SIM_STATE.smokeGrowthRate = 0.25;
    }
    logSimEvent(`AI difficulty set to ${tier.toUpperCase()}: ${SIM_STATE.difficultyMultiplier.toFixed(2)}x smoke growth rate.`);
    renderHUDTelemetry();
  }

  // ---------------------------------------------------------------------------
  // 9. Bi-directional JavaScript Bridge & Accurate Sync
  // ---------------------------------------------------------------------------
  window.DrillVRBridge = {
    dispatchStepProgress: function (stepNumber, currentScore, mistakes, timeSpent) {
      const detail = { stepNumber, score: currentScore, mistakes, timeSpent, timestamp: Date.now() };
      window.dispatchEvent(new CustomEvent('drillvr:steplog', { detail }));

      if (window.DrillVRApp && typeof window.DrillVRApp.onLiveStepTelemetry === 'function') {
        window.DrillVRApp.onLiveStepTelemetry(detail);
      }
    },

    submitTestSessionExplicit: async function () {
      const breakdown = SIM_STATE.lastDebriefData || calculateDetailedScoreBreakdown();
      const apiBase = getApiBaseUrl();

      // Check current session auth token from both storage keys
      const token = localStorage.getItem('DrillVR_AuthToken') || localStorage.getItem('drillvr_instructor_token');
      const currentUserStr = localStorage.getItem('DrillVR_CurrentUser') || localStorage.getItem('drillvr_instructor_user');
      let currentUser = null;
      try { currentUser = JSON.parse(currentUserStr || '{}'); } catch (e) {}

      // If user selected sandbox preview profile, do not pollute database
      if (SIM_STATE.activeTraineeEmail === 'demo@drillvr.edu') {
        alert(
          'SANDBOX / PREVIEW MODE ACTIVE:\n\n' +
          'Interactive Sandbox sessions run entirely locally in the browser to prevent polluting real trainee cohort statistics.\n\n' +
          'To record an official evaluated drill session, select a registered Trainee Profile (e.g., Diya Patel, Aarav Sharma, Kabir Verma) from the TRAINEE PROFILE dropdown.'
        );
        logSimEvent('Preview sandbox session completed locally. No database sync performed.', 'info');
        const modal = document.getElementById('simDebriefModal');
        if (modal) modal.remove();
        return;
      }

      // If logged in as instructor, confirm attribution to the selected trainee
      if (currentUser && (currentUser.role === 'instructor' || currentUser.role === 'admin')) {
        const confirmSync = confirm(
          `CONFIRM EVALUATION SYNC:\n\n` +
          `You are recording this drill session for Trainee:\n` +
          `• Name: ${SIM_STATE.activeTrainee}\n` +
          `• Email: ${SIM_STATE.activeTraineeEmail}\n` +
          `• Score: ${breakdown.totalScore} / 100 (${breakdown.passed ? 'PASSED' : 'FAILED'})\n` +
          `• Evaluator: ${currentUser.name} (${currentUser.role})\n\n` +
          `Proceed with syncing to MySQL database?`
        );
        if (!confirmSync) {
          logSimEvent('MySQL drill session sync cancelled by instructor.', 'info');
          return;
        }
      }

      const payload = {
        scenarioCode: 'building_emergency_v1',
        targetUserEmail: SIM_STATE.activeTraineeEmail,
        difficultyMultiplier: SIM_STATE.difficultyMultiplier,
        totalScore: breakdown.totalScore,
        stepsScore: breakdown.stepsScore,
        evacuationScore: breakdown.evacuationScore,
        timeBonusScore: breakdown.timeBonusScore,
        mistakePenalty: breakdown.mistakePenalty,
        totalTimeSeconds: breakdown.totalTimeSeconds,
        totalMistakes: breakdown.totalMistakes,
        casualtiesEvacuated: breakdown.casualtiesEvacuated,
        casualtiesTotal: breakdown.casualtiesTotal,
        passed: breakdown.passed,
        weakestStepKey: breakdown.weakestStepKey,
        stepLogs: SIM_STATE.stepMetrics.map(s => ({
          stepKey: s.stepKey,
          timeSpentSeconds: Math.round(s.timeSpent),
          mistakesInStep: s.mistakes || 0,
          completed: s.completed
        }))
      };

      try {
        logSimEvent(`Submitting drill session to ${apiBase}/api/sessions...`);
        const res = await fetch(`${apiBase}/api/sessions`, {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
            'Authorization': token ? `Bearer ${token}` : ''
          },
          body: JSON.stringify(payload)
        });

        if (!res.ok) {
          const errData = await res.json().catch(() => ({}));
          throw new Error(errData.message || `Server returned HTTP ${res.status}: ${res.statusText}`);
        }

        const data = await res.json();
        logSimEvent(`Session verified and saved in MySQL (Session ID: #${data.sessionId}).`, 'success');
        const modal = document.getElementById('simDebriefModal');
        if (modal) modal.remove();

        // Refresh dashboard tables and charts
        if (window.DrillVRApp && typeof window.DrillVRApp.refreshTelemetry === 'function') {
          window.DrillVRApp.refreshTelemetry();
        }
      } catch (err) {
        // Real local offline queue storage
        const queue = JSON.parse(localStorage.getItem('DrillVR_SimOfflineQueue') || '[]');
        queue.push({ payload, queuedAt: new Date().toISOString() });
        localStorage.setItem('DrillVR_SimOfflineQueue', JSON.stringify(queue));

        logSimEvent(`Upload error: ${err.message}. Session queued locally in localStorage (${queue.length} pending).`, 'error');
        alert(`Failed to upload session to backend at ${apiBase}.\n\nError: ${err.message}\n\nThe session has been saved to your browser offline queue.`);
      }
    },

    start: startSimulation,
    pause: pauseSimulation,
    reset: resetSimulation,
    setTrainee: setTrainee,
    setDifficulty: setDifficulty,
    toggleCamera: toggleCameraMode,
    toggleLock: togglePointerLock
  };

  // Auto-init on page load if canvas exists
  window.addEventListener('DOMContentLoaded', () => {
    const canvasWrap = document.getElementById('simCanvasContainer');
    if (canvasWrap) {
      initThreeScene();
    }
  });

  // Re-check when view switches to simulation
  window.addEventListener('drillvr:view_switched', (e) => {
    if (e.detail === 'simulation' && !renderer) {
      setTimeout(initThreeScene, 50);
    } else if (e.detail === 'simulation' && renderer) {
      setTimeout(onWindowResize, 50);
    }
  });

})();
