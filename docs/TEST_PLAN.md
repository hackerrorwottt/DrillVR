# DrillVR Test Plan & Evidence Log

> **Important:** Mark `PASS` / `FAIL` and fill actual metrics (`FPS`, `ms`, classmate ratings) **only after executing each check** on your machine and Android phones.

## 1. Master Test Case Table

| ID | Area | Test Case | Expected Result | Status | Evidence / Notes |
|---|---|---|---|---|---|
| DB-01 | Database | Run `npm run db:seed` (`schema.sql` + `seed.sql`) | 5 tables (`users`, `scenarios`, `scenario_steps`, `sessions`, `session_step_logs`), FKs, indexes, 20 sessions | `PENDING` | Run after `brew install mysql` |
| DB-02 | Database | Attempt inserting `sessions` row with invalid `user_id` | MySQL FK rejects insert (`ER_NO_REFERENCED_ROW_2`) | `PENDING` | Verify in MySQL CLI |
| API-01 | Backend API | `POST /api/auth/register` & `/login` | Returns JWT; `password_hash` never returned | `PENDING` | Covered in `tests/api.test.js` |
| API-02 | Backend API | Trainee calls `GET /api/instructor/trainees/summary` | `403 FORBIDDEN`; instructor gets `200 OK` | `PENDING` | Covered in `tests/api.test.js` |
| API-03 | Backend API | `POST /api/sessions` with invalid `stepKey` | `400 INVALID_STEP_LOG`; entire transaction rolled back | `PENDING` | Covered in `tests/api.test.js` |
| API-04 | Adaptive AI | Difficulty logic (`<=2`, `3-5`, `>5` avg mistakes) | `harder (1.35x)`, `normal (1.0x)`, `easier (0.70x + extraHints)` | `PASS (Unit)` | 5/5 Node assertions passed; re-run full `npm test` with MySQL |
| VR-01 | Unity M1-M3 | Cardboard split-screen + gaze dwell (`1.5s`) + tap + teleport | Reticle ring fills, cube/buttons select, teleport moves smoothly | `PENDING` | Requires Unity 2022.3 LTS + Android phone |
| VR-02 | Unity M4 | Out-of-order step & wrong equipment (`Laptop`, `PaperBox`) | Step blocked; `Mistakes` increments; HUD & hint update | `PENDING` | Test in `M8_FullOnlineLoop` |
| VR-03 | Unity M4 | Walk into smoke without Respirator / enter Blocked East Exit | `Mistakes` increments with clear warning | `PENDING` | Test in Editor + APK |
| VR-04 | Unity M5 | `CasualtyAI` transitions (`Idle -> Panic -> Following -> Safe`) | All 3 NPCs navigate doorways & stairs without getting stuck | `PENDING` | Observe state badges |
| VR-05 | Unity M6 | Same actions produce identical deterministic score; hints toggle | Score matches formula; hints hidden when `HINTS: OFF` | `PENDING` | Compare two identical runs |
| E2E-01 | End-to-End | Phone login (`Aarav`) -> full drill -> Debrief -> MySQL -> Dashboard | Session row + 7 step logs saved; visible on Chart.js chart | `PENDING` | Verify in `http://localhost:4000/dashboard` |
| E2E-02 | Offline Queue | Stop backend, complete drill, restart backend, click Retry Sync | Queued in `pending_sessions.json` and uploaded on reconnect | `PENDING` | Verify `Session #` toast |

---

## 2. AI Behavior Table (Before vs. After Adaptive Difficulty)

| Trainee Profile | Last 5 Avg Mistakes | Returned Tier | Smoke Growth Speed | Extra Hints | Observed Drill Behavior |
|---|---:|---|---:|---|---|
| Fresh / Baseline (`diya@drillvr.edu`) | `3.8` | `normal` | `0.25 * 1.00 = 0.25 m/s` | `false` | `[RECORD TIME & SCORE]` |
| High Performer (`aarav@drillvr.edu`) | `1.2` | `harder` | `0.25 * 1.35 = 0.3375 m/s` | `false` | `[RECORD TIME & SCORE]` |
| Struggling (`kabir@drillvr.edu`) | `6.8` | `easier` | `0.25 * 0.70 = 0.175 m/s` | `true` | `[RECORD TIME & SCORE]` |

---

## 3. Performance Check (2 Android Phones)

| Device Model & Android Version | Scene Tested | Target FPS | Measured Min / Avg FPS | Crashes / Thermal Issues |
|---|---|---:|---:|---|
| Phone 1: `[Model / API Level]` | `M8_FullOnlineLoop` | `30+ FPS` | `[e.g. 42 / 55 FPS]` | `[None / Notes]` |
| Phone 2: `[Model / API Level]` | `M8_FullOnlineLoop` | `30+ FPS` | `[e.g. 34 / 46 FPS]` | `[None / Notes]` |

---

## 4. User Testing Log (5–8 Classmates) & Changes Made

| Tester # | VR Comfort (1–5) | Interaction Clarity (1–5) | Completed Without Help? | Top Feedback / Friction Point |
|---:|---:|---:|---|---|
| 1 | | | | |
| 2 | | | | |
| 3 | | | | |
| 4 | | | | |
| 5 | | | | |

### Bugs Fixed / Improvements Made from Testing
- *(Record each bug you discovered during classmate/phone testing and the commit SHA of your fix.)*
