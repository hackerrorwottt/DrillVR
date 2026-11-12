# DrillVR Demo Video, APK & Viva Preparation Guide

## 1. Final APK Build Checklist (`unity-client`)
1. Open `/Users/shloksharma/Desktop/DrillVR/unity-client` in **Unity 2022.3 LTS**.
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
