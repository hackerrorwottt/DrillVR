# DrillVR

An AI-driven mobile VR educational prototype for live building emergency response training drills, built with **Unity 2022.3 LTS (Google Cardboard XR)**, **Node.js + Express**, **MySQL 8**, and a **Chart.js Instructor Dashboard**.

> **Educational Prototype Disclaimer:** DrillVR is an academic prototype inspired by the problem statement *"Augmented/Virtual Reality system for the live training of troops"* (Ministry of Home Affairs, 2023, Smart Automation, Software). It contains **no weapons or combat content**. Simulation scores are for educational demonstration only and must not be presented as proof of real-world qualification. Augmented Reality (AR) is future scope only.

---

## Course Requirements Demonstrated
1. **Mobile VR Application Development (Unity 3D):** Stereoscopic Google Cardboard XR on Android with gaze-dwell (`1.5s`) + single-tap interaction, comfortable teleport locomotion, baked lighting, ambient audio, and a `30+ FPS` mobile target — plus an Editor mouse-look/WASD test mode.
2. **Basic AI Inside VR:**
   - **Casualty NPCs (`CasualtyAI`):** `NavMeshAgent` + state machine (`Idle`, `Panic`, `Following`, `Safe`) with smoke-proximity flee behavior and doorway/stairwell pathfinding.
   - **Dynamic Hazard (`HazardSystem`):** Expanding smoke zone (`ParticleSystem` + trigger `SphereCollider`) whose spread speed scales with difficulty.
   - **Virtual Instructor (`HintSystem`):** Context-aware hint panel driven by the active step and the player's last mistake (toggleable ON/OFF).
   - **Adaptive Difficulty (`difficultyService.js`):** Evaluates the trainee's last 5 sessions (`avg mistakes <= 2` → harder `1.35x`, `3–5` → normal `1.00x`, `> 5` → easier `0.70x` with extra hints).
3. **Sound Application Design with Backend & DBMS:** 3NF MySQL 8 schema (5 tables, foreign keys, indexes), parameterized SQL queries, JWT + bcrypt authentication, server-side RBAC (`trainee`, `instructor`, `admin`), single-transaction session + step-log persistence, offline retry queue in Unity, Jest + Supertest integration tests, Postman collection, and an HTML + Chart.js Instructor Dashboard.

---

## Tech Stack
- **Mobile VR Client:** Unity `2022.3 LTS` (Built-in Render Pipeline, C#), `com.google.xr.cardboard` (`#v1.30.0`), `com.unity.xr.management` (`4.5.4`), `com.unity.xr.legacyinputhelpers` (`2.1.11`), `com.unity.ai.navigation` (`1.1.7`)
- **Backend API:** Node.js `20+`, Express `4`, `mysql2`, `jsonwebtoken`, `bcrypt`, `Jest` + `Supertest`
- **Database:** MySQL `8`
- **Instructor Dashboard:** Plain HTML5, CSS3, Vanilla JS, Chart.js `4.4.4`

---

## Repository Layout
```text
DrillVR/
├── unity-client/
│   ├── Packages/manifest.json          # Pinned Cardboard XR & AI Navigation packages
│   └── Assets/
│       ├── Editor/Milestone1..8Setup.cs # Automated Unity scene & ScriptableObject builders
│       ├── ScriptableObjects/          # Generated ScenarioConfig, StepData, EquipmentData assets
│       └── Scripts/
│           ├── AI/                     # CasualtyAI, HintSystem
│           ├── Assessment/             # AssessmentManager (deterministic weighted formula)
│           ├── Core/                   # CardboardBootstrap, MissionManager, ScenarioConfig, StepData
│           ├── Environment/            # DoorController, AmbientAudioController, SafeRouteZone
│           ├── Equipment/              # EquipmentData, EquipmentSelectionSystem, EquipmentInteractable
│           ├── Hazards/                # HazardSystem, SituationAssessmentTracker
│           ├── Interactions/           # GazeInteractor, Interactable, TeleportPoint, GazeButton, DoorInteractable
│           ├── Network/                # ApiClient, ApiDtos, OfflineSessionQueue, DrillBackendSync
│           ├── Player/                 # EditorWalkController
│           ├── Rescue/                 # RescueSystem, EvacuationSystem, CasualtyInteractable
│           └── UI/                     # UIManager, DrillHudController, DebriefPanelController, BackendFlowController, FpsOverlay
├── backend/
│   ├── db/
│   │   ├── schema.sql                  # 3NF 5-table MySQL 8 schema with FKs & indexes
│   │   └── seed.sql                    # 6 users, 3 scenarios, 7 steps, 20 sample sessions
│   ├── docs/
│   │   ├── ER_DIAGRAM.md               # Mermaid ER diagram & 3NF notes
│   │   └── DrillVR_API.postman_collection.json
│   ├── src/
│   │   ├── app.js & server.js
│   │   ├── config/db.js
│   │   ├── middleware/                 # auth.js (JWT + RBAC), errorHandler.js ({ error: { code, message } })
│   │   ├── routes/                     # authRoutes, scenarioRoutes, sessionRoutes, userRoutes, instructorRoutes
│   │   ├── services/difficultyService.js
│   │   └── scripts/initDb.js
│   └── tests/                          # difficulty.test.js & api.test.js (Jest + Supertest)
├── dashboard/
│   ├── index.html                      # Instructor login, trainee table, Chart.js chart, leaderboard
│   ├── styles.css
│   └── app.js
└── docs/
    ├── ARCHITECTURE_DIAGRAMS.md        # System flow, CasualtyAI FSM, 7-step flowchart
    ├── TEST_PLAN.md                    # Test-case matrix, AI difficulty matrix, 2-phone FPS & user log
    └── DEMO_AND_VIVA_GUIDE.md          # APK checklist, 3-5 min demo shotlist, viva Q&A notes
```

---

## Core Scenario (Building Emergency Response Drill)
All steps, equipment items, hints, and scoring weights live in `ScenarioConfig`, `StepData`, and `EquipmentData` ScriptableObjects (and can be hydrated from the backend API):
1. **Briefing** — Confirm the briefing at the Command Post.
2. **Assess Situation** — Inspect the growing smoke zone and the blocked East Main Exit.
3. **Equipment Selection** — Collect required gear (Respirator, Emergency Flashlight, First Aid Kit) in Room 1 while avoiding distractors (Office Laptop, Cardboard Box).
4. **Search & Locate Casualties** — Find all 3 `CasualtyAI` NPCs across the rooms so they switch to `Following`.
5. **Evacuate via Safe Route** — Lead casualties through the North Stairwell to the Safe Assembly Zone while smoke spreads.
6. **Report to Command Post** — Return to the Command Post Radio to report completion.
7. **Debrief with Score** — Review total score, step times, mistakes, areas to improve, educational disclaimer, and backend sync status.

*(Two additional scenarios—`tunnel_smoke_evacuation` and `warehouse_hazmat_response`—exist in MySQL as inactive future-scope entries.)*

---

## Database & REST API

### 3NF Tables (`backend/db/schema.sql`)
- `users` (`trainee`, `instructor`, `admin`)
- `scenarios`
- `scenario_steps`
- `sessions`
- `session_step_logs`

### Key Endpoints
| Method | Path | Access | Purpose |
|---|---|---|---|
| `GET` | `/api/health` | Public | API + MySQL health check |
| `POST` | `/api/auth/register` | Public | Register new `trainee` (returns JWT, never `password_hash`) |
| `POST` | `/api/auth/login` | Public | Authenticate user (returns JWT, never `password_hash`) |
| `GET` | `/api/scenarios` | Auth | List active & future-scope scenarios |
| `GET` | `/api/scenarios/:idOrCode/steps` | Auth | Fetch ordered scenario steps & hints |
| `POST` | `/api/sessions` | Auth | Save session + step logs in **one transaction** |
| `GET` | `/api/users/me/history` (or `/:id/history`) | Self / Instructor / Admin | Session history with step logs |
| `GET` | `/api/users/me/difficulty` (or `/:id/difficulty`) | Self / Instructor / Admin | Adaptive difficulty from last 5 sessions |
| `GET` | `/api/leaderboard` | Auth | Ranked trainee scores |
| `GET` | `/api/instructor/trainees/summary` | Instructor / Admin | Per-trainee averages & weakest step |

### Seeded Test Accounts (after `npm run db:seed`)
| Role | Email | Password | Seeded Difficulty Tier |
|---|---|---|---|
| Instructor | `instructor@drillvr.edu` | `Instructor@123` | — |
| Admin | `admin@drillvr.edu` | `Admin@123` | — |
| Trainee | `aarav@drillvr.edu` | `Trainee@123` | `harder` (`1.35x`, avg mistakes `1.2`) |
| Trainee | `diya@drillvr.edu` | `Trainee@123` | `normal` (`1.00x`, avg mistakes `3.8`) |
| Trainee | `kabir@drillvr.edu` | `Trainee@123` | `easier` (`0.70x` + extra hints, avg mistakes `6.8`) |
| Trainee | `meera@drillvr.edu` | `Trainee@123` | `normal` (`1.00x`, avg mistakes `2.8`) |

---

## Quick Start

### 1. Backend, MySQL 8 & Instructor Dashboard
```bash
# Install and start MySQL 8 (macOS Homebrew)
brew install mysql
brew services start mysql

# Configure, install, seed, and test backend
cd backend
cp .env.example .env    # Update DB_PASSWORD and JWT_SECRET
npm install
npm run db:seed
npm test                # Runs Jest + Supertest suite
npm start               # Starts API on http://0.0.0.0:4000
```
- Open **`http://localhost:4000/dashboard`** in your browser and sign in with `instructor@drillvr.edu` / `Instructor@123`.
- Import [DrillVR_API.postman_collection.json](backend/docs/DrillVR_API.postman_collection.json) into Postman to test endpoints manually.

### 2. Unity 2022.3 LTS & Android Cardboard APK
1. Install **Unity 2022.3 LTS** via Unity Hub with **Android Build Support** (**Android SDK & NDK Tools** + **OpenJDK**).
2. Add `/Users/shloksharma/Desktop/DrillVR/unity-client` in **Unity Hub → Projects → Add project from disk** and open it.
3. Open **Edit → Project Settings → XR Plug-in Management → Android** and enable **Initialize XR on Startup** + **Cardboard XR Plugin**.
4. In the top menu bar, click **DrillVR → Setup Milestone 8 Backend Integration**, then **DrillVR → Bake Milestone 2 Lighting**.
5. **Editor Test Mode:** Press **Play**, hold **Right Mouse Button** (or press **`L`**) to look, **WASD** to walk (or gaze at green teleport pads), and **Left-Click** / dwell `1.5s` to interact.
6. **Android Phone Build:** Find your Mac's LAN IP (`ipconfig getifaddr en0`), set `ApiClient` base URL to `http://<YOUR_MAC_IP>:4000`, switch platform to **Android** in **File → Build Settings**, and click **Build And Run**.

---

## Milestones Completed
1. Setup, Cardboard XR config, and gaze-tap test scene (`Milestone1Setup.cs`)
2. Building environment with primitives, baked lighting, ambient audio, FPS overlay, and NavMesh (`Milestone2Setup.cs`)
3. GazeInteractor with radial dwell ring, `Interactable` events, `TeleportPoint`s, and World-Space menus (`Milestone3Setup.cs`)
4. `ScenarioConfig` / `StepData` / `EquipmentData` ScriptableObjects, `MissionManager`, `HazardSystem`, `EquipmentSelectionSystem`, and `SafeRouteZone` (`Milestone4Setup.cs`)
5. `CasualtyAI` NavMesh state machine (`Idle`, `Panic`, `Following`, `Safe`), `RescueSystem`, `EvacuationSystem`, and failure conditions (`Milestone5Setup.cs`)
6. `AssessmentManager`, `HintSystem` virtual instructor, and `DebriefPanelController` with restart (`Milestone6Setup.cs`)
7. 3NF MySQL 8 schema, 20-session seed data, Express REST API, Jest + Supertest suite, ER diagram, and Postman collection
8. Unity `ApiClient`, `DrillBackendSync`, login/scenario UI, adaptive difficulty loop, and persistent `OfflineSessionQueue` (`Milestone8Setup.cs`)
9. Plain HTML + Chart.js Instructor Dashboard, architecture diagrams, test plan, and demo/viva guide

---

## Documentation
- [Architecture & State Diagrams](docs/ARCHITECTURE_DIAGRAMS.md)
- [ER Diagram & 3NF Design](backend/db/schema.sql) | [Mermaid ER](backend/docs/ER_DIAGRAM.md)
- [Master Test Plan & Evidence Log](docs/TEST_PLAN.md)
- [Demo Video & Viva Prep Guide](docs/DEMO_AND_VIVA_GUIDE.md)
