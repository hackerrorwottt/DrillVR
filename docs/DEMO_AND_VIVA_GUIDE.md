# DrillVR Demo Video, APK & Viva Preparation Guide

## 1. Final APK Build Checklist (`unity-client`)
1. Open `unity-client` in **Unity 6 (6000.6.7f1)**.
2. Run **DrillVR → Setup Milestone 8 Backend Integration** (ensures `M8_FullOnlineLoop.unity` is active in Build Settings and `InsecureHttpOption.AlwaysAllowed` is set).
3. Click **DrillVR → Bake Milestone 2 Lighting** (or **Window → Rendering → Lighting → Generate Lighting**) and save the scene (`Cmd + S`).
4. On `DrillSystems → ApiClient → Default Base Url`, set `http://<YOUR_MAC_LAN_IP>:4000` (find via `ipconfig getifaddr en0`).
5. **File → Build Settings → Android → Build** → save as `unity-client/Builds/DrillVR_Final.apk`.

---

## 2. Demo Video Script (3–5 Minutes)
- **0:00–0:35 — Problem & Scope:** State MHA 2023 problem statement, non-combat building emergency response scenario, and the educational prototype disclaimer.
- **0:35–1:20 — Phone Login & Adaptive Difficulty:** Show `BackendFlowController` logging into the Node/Express API, fetching scenarios (active + future scope), and displaying adaptive difficulty from MySQL.
- **1:20–2:50 — Full VR Drill Playthrough (Screen-record Phone or Split-Screen):**
  - Confirm Briefing → Assess Smoke + Blocked East Exit → Room 1 Equipment Selection (show 1 intentional wrong pick increasing mistake count & triggering `HintSystem`) → Search 3 `CasualtyAI` NPCs (`Idle`/`Panic` → `Following`) → Evacuate via North Stairwell to Safe Assembly Zone (`Safe`) → Report at Command Post Radio.
- **2:50–3:40 — Debrief & Offline Queue:** Show World-Space Debrief score breakdown + disclaimer, and show `Synced to MySQL (Session #...)` (plus brief offline retry demo if time permits).
- **3:40–4:30 — Instructor Dashboard & Tests:** Open `http://localhost:4000/dashboard`, log in as `instructor@drillvr.edu`, inspect trainee table, Chart.js score/mistake trend chart, leaderboard, and `npm test` green output.

---

## 3. Viva Q&A Notes (Key Concepts to Explain Clearly)
1. **Why ScriptableObjects (`ScenarioConfig`, `StepData`, `EquipmentData`)?**
   - Separates training content and scoring weights from C# code so instructors/designers can configure drills without editing scripts, and lets the API hydrate runtime steps dynamically.
2. **How does `CasualtyAI` avoid getting stuck on walls?**
   - Uses a baked `NavMeshSurface` (`PhysicsColliders`), narrow agent radius (`0.28m`), `MedQualityObstacleAvoidance`, and `NavMesh.SamplePosition` across candidate away-angles during `Panic`.
3. **Why World-Space UI instead of Screen-Space Overlay?**
   - Google Cardboard renders two stereoscopic eye cameras; `Screen Space - Overlay` does not project properly into binocular VR depth, whereas `World Space` canvases render cleanly to both eyes.
4. **How is database integrity guaranteed when saving a drill session?**
   - `POST /api/sessions` wraps the `sessions` insert and all `session_step_logs` inserts inside a single MySQL transaction (`beginTransaction` / `commit` / `rollback`), using parameterized queries only (`?`) and foreign keys in 3NF.
5. **How does adaptive difficulty work?**
   - Queries the trainee's last 5 sessions (`idx_sessions_user_completed`): `avgMistakes <= 2` sets `1.35x` smoke speed (`harder`), `<= 5` keeps `1.0x` (`normal`), and `> 5` lowers to `0.70x` (`easier`) while enabling `extraHints`.
6. **How is security enforced?**
   - Passwords hashed with `bcrypt`, stateless auth via signed `JWT`, `password_hash` never selected/returned in responses, public registration locked to `trainee` role, and instructor endpoints protected by `authorizeRoles('instructor', 'admin')`.

---

## 4. Physical Hardware Testing Protocol (VR-01 to VR-04)

When presenting to examiners or capturing viva evidence, execute and record these 4 VR test cases on your physical Android phone mounted inside the Google Cardboard viewer:

| Test ID | Target System | Physical Test Procedure | Expected Pass Evidence |
|---|---|---|---|
| **VR-01** | Cardboard Stereoscopic Dual-Camera Rig | Insert phone into Cardboard viewer. Look around 360°. Verify binocular depth perception and zero double-vision. | Left and right eye frustums align cleanly with correct IPD (interpupillary distance) and 60 FPS head tracking. |
| **VR-02** | Gaze Reticle & Dwell Selection | Focus center reticle on Briefing Board or Teleport Pad for 1.5 seconds without pressing any buttons. | Circular progress ring fills over 1.5s; action triggers upon 100% completion with green feedback ring. |
| **VR-03** | Capacitive Touch Trigger / Screen Tap | Look at an interactive object (e.g., Door or Respirator) and pull the Cardboard top conductive button (or tap screen). | Immediate interaction triggers without waiting for the 1.5s dwell timer. |
| **VR-04** | Spatial Audio & Hazard Cues | Walk into the East Corridor near the Smoke Zone with stereo headphones on. | Smoke hiss audio pans dynamically to the right ear; smoke density fog dims visibility and triggers breathing audio. |

---

## 5. End-to-End & Airplane Mode Offline Queue Testing (E2E-01 & E2E-02)

To demonstrate the resilience of the offline queueing architecture to examiners:

### E2E-01: Online Connected Drill Execution
1. Ensure phone is on the same Wi-Fi network as the laptop.
2. Launch DrillVR app. Select **Diya Patel** preset.
3. Complete the drill steps (Briefing → Equipment → Rescue → Evacuate → Command Post).
4. Verify debrief screen displays: `Synced to MySQL (Session #XX)`.
5. Refresh instructor dashboard (`http://localhost:8080`) and verify session appears in the ledger with verified step times.

### E2E-02: Airplane Mode Disconnection & Reconnection Sync
1. On your phone, turn on **Airplane Mode** (disconnecting Wi-Fi).
2. In DrillVR, click **Play Offline (Queue)** or run the drill while disconnected.
3. Complete the drill to the debrief screen.
4. Observe the on-screen notice: `Network unreachable. Queued locally (1 pending)`.
5. Inspect phone storage: payload is persisted in `Application.persistentDataPath/pending_sessions.json`.
6. Turn **Airplane Mode OFF** (reconnecting Wi-Fi).
7. Observe the automatic background retry: `Reconnected: uploaded 1 queued offline session(s)!`.
8. Refresh the instructor dashboard and confirm the session was ingested into MySQL without data loss.

---

## 6. Hardware Performance Benchmarking Protocol

Enable the built-in on-screen FPS counter (`FpsOverlay.cs`):
1. Launch `M8_FullOnlineLoop` on your phone.
2. Observe the green FPS readout displayed in the bottom-left corner of the HUD.
3. Record real measurements across 3 test runs:
   - Baseline Command Post (Open Air): Measure steady-state FPS.
   - Dense Smoke Particle Zone (Corridor): Measure minimum FPS under particle load.
   - Casualty Following (AI NavMesh Active): Measure average FPS with 3 active agents.
4. Record phone specifications: Model Name, SoC (e.g. Snapdragon 8 Gen 2 / Dimensity), Android Version, and Average Measured FPS.

---

## 7. Adaptive Difficulty Verification Protocol

Run three sessions across the seeded trainee profiles and observe real-time parameter adaptation:

1. **Aarav Sharma (`aarav@drillvr.edu`) - High Performer**:
   - Seed average mistakes: `1.2` (<= 2).
   - Resulting tier: **HARDER** (`1.35x` smoke expansion speed, standard hints).
2. **Diya Patel (`diya@drillvr.edu`) - Standard Performer**:
   - Seed average mistakes: `3.8` (2 < avg <= 5).
   - Resulting tier: **NORMAL** (`1.00x` smoke expansion speed, standard hints).
3. **Kabir Verma (`kabir@drillvr.edu`) - Struggling Trainee**:
   - Seed average mistakes: `6.8` (> 5).
   - Resulting tier: **EASIER** (`0.70x` smoke expansion speed, extra hints enabled).

Verify in MySQL after runs:
```sql
SELECT u.name, s.difficulty_multiplier, s.total_mistakes, s.weakest_step_key, s.completed_at
FROM sessions s
INNER JOIN users u ON u.id = s.user_id
ORDER BY s.id DESC LIMIT 3;
```
