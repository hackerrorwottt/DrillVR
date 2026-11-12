-- DrillVR Seed Data: users, 3 scenarios (1 active + 2 future scope),
-- 7 scenario_steps, and 20 sample sessions with session_step_logs.
-- Note: Run `npm run db:seed` to populate live bcrypt password hashes for test accounts.

USE drillvr;

SET FOREIGN_KEY_CHECKS = 0;
TRUNCATE TABLE session_step_logs;
TRUNCATE TABLE sessions;
TRUNCATE TABLE scenario_steps;
TRUNCATE TABLE scenarios;
TRUNCATE TABLE users;
SET FOREIGN_KEY_CHECKS = 1;

-- 1. Sample Users (id 1..6)
INSERT INTO users (id, name, email, password_hash, role) VALUES
(1, 'Dr. Vikram Rao', 'instructor@drillvr.edu', 'SEED_HASH_REPLACED_BY_INITDB', 'instructor'),
(2, 'System Admin', 'admin@drillvr.edu', 'SEED_HASH_REPLACED_BY_INITDB', 'admin'),
(3, 'Aarav Sharma', 'aarav@drillvr.edu', 'SEED_HASH_REPLACED_BY_INITDB', 'trainee'),
(4, 'Diya Patel', 'diya@drillvr.edu', 'SEED_HASH_REPLACED_BY_INITDB', 'trainee'),
(5, 'Kabir Verma', 'kabir@drillvr.edu', 'SEED_HASH_REPLACED_BY_INITDB', 'trainee'),
(6, 'Meera Nair', 'meera@drillvr.edu', 'SEED_HASH_REPLACED_BY_INITDB', 'trainee');

-- 2. Scenarios (1 active prototype + 2 future scope entries)
INSERT INTO scenarios (id, code, title, description, base_smoke_growth_rate, time_limit_seconds, max_casualties_lost, is_active) VALUES
(1, 'building_emergency_v1', 'Building Emergency Response Drill',
 'Single-floor smoke hazard assessment, equipment selection, casualty rescue, safe stairwell evacuation, and command post debrief.',
 0.250, 300, 1, TRUE),
(2, 'tunnel_smoke_evacuation', 'Subway Tunnel Smoke Evacuation (Future Scope)',
 'Future scenario: underground low-visibility ventilation failure drill.',
 0.300, 360, 1, FALSE),
(3, 'warehouse_hazmat_response', 'Industrial Warehouse Chemical Leak (Future Scope)',
 'Future scenario: containment perimeter and PPE selection drill.',
 0.220, 420, 1, FALSE);

-- 3. Scenario Steps for building_emergency_v1 (id 1..7)
INSERT INTO scenario_steps (id, scenario_id, step_number, step_key, title, instruction, default_hint, mistake_hint, expected_time_seconds, weight_percent) VALUES
(1, 1, 1, 'briefing', 'Briefing',
 'Review emergency protocol and confirm the Briefing Board at the Command Post.',
 'Look at the green Briefing Board beside you and select Confirm Briefing.',
 'Confirm the briefing at the Command Post before entering the building.',
 20, 10.00),
(2, 1, 2, 'assess_situation', 'Assess Situation',
 'Enter the corridor cautiously. Inspect the Smoke Zone and the Blocked Main Exit.',
 'Look toward the East Corridor to inspect both the Smoke Warning and the Blocked Exit.',
 'Assess both the smoke hazard and the blocked exit before picking equipment.',
 35, 15.00),
(3, 1, 3, 'equipment_selection', 'Equipment Selection',
 'Enter Room 1 (Equipment) and collect the Respirator, Flashlight, and First Aid Kit.',
 'Avoid unrelated items like the Laptop or Paper Box — pick only emergency response gear.',
 'Select only required emergency gear (Respirator, Flashlight, First Aid Kit).',
 45, 20.00),
(4, 1, 4, 'search_casualties', 'Search & Locate Casualties',
 'Search the rooms to locate all casualties and guide them toward safety.',
 'Check Room 2 (Office), Room 3 (Utility), and Room 4 (Conference) carefully.',
 'Locate surviving casualties before evacuating the building.',
 60, 20.00),
(5, 1, 5, 'evacuate_safe_route', 'Evacuate via Safe Route',
 'Main East Exit is blocked! Evacuate up the North Stairwell to the Safe Assembly Zone.',
 'Head to Corridor Center -> Stairwell Base -> Stairwell Landing -> Safe Assembly Zone.',
 'Do not use the blocked East Exit or enter dense smoke without a respirator.',
 50, 20.00),
(6, 1, 6, 'report_command_post', 'Report to Command Post',
 'Return to the Command Post Radio outside the West Entrance to report completion.',
 'Use the West Entrance teleport pads to return to the Command Post table and select the Radio.',
 'Select the Command Post Radio only after evacuating via the Safe Assembly Zone.',
 30, 15.00),
(7, 1, 7, 'debrief', 'Debrief & Score',
 'Review your performance summary and areas for improvement.',
 'Check your step times, mistakes, and overall score.',
 '',
 15, 0.00);

-- 4. 20 Sample Sessions covering all 3 adaptive difficulty tiers:
--    User 3 (Aarav): avg mistakes <= 2 (1.2 -> harder)
--    User 4 (Diya):  avg mistakes 3-5 (3.8 -> same)
--    User 5 (Kabir): avg mistakes > 5 (6.8 -> easier with more hints)
--    User 6 (Meera): mixed progression (avg 2.8 -> same)
INSERT INTO sessions (id, user_id, scenario_id, difficulty_multiplier, total_score, steps_score, evacuation_score, time_bonus_score, mistake_penalty, total_time_seconds, total_mistakes, casualties_evacuated, casualties_total, passed, weakest_step_key, completed_at) VALUES
(1,  3, 1, 1.00, 86.50, 40.00, 40.00, 11.50,  5.00, 127.50, 1, 3, 3, TRUE,  'equipment_selection', DATE_SUB(NOW(), INTERVAL 10 DAY)),
(2,  3, 1, 1.00, 82.00, 40.00, 40.00, 12.00, 10.00, 120.00, 2, 3, 3, TRUE,  'assess_situation',    DATE_SUB(NOW(), INTERVAL 8 DAY)),
(3,  3, 1, 1.35, 88.40, 40.00, 40.00, 13.40,  5.00,  99.00, 1, 3, 3, TRUE,  'search_casualties',   DATE_SUB(NOW(), INTERVAL 6 DAY)),
(4,  3, 1, 1.35, 93.20, 40.00, 40.00, 13.20,  0.00, 102.00, 0, 3, 3, TRUE,  NULL,                  DATE_SUB(NOW(), INTERVAL 3 DAY)),
(5,  3, 1, 1.35, 84.10, 40.00, 40.00, 14.10, 10.00,  88.50, 2, 3, 3, TRUE,  'evacuate_safe_route', DATE_SUB(NOW(), INTERVAL 1 DAY)),

(6,  4, 1, 1.00, 71.00, 40.00, 40.00, 11.00, 20.00, 135.00, 4, 3, 3, TRUE,  'equipment_selection', DATE_SUB(NOW(), INTERVAL 9 DAY)),
(7,  4, 1, 1.00, 66.50, 40.00, 40.00, 11.50, 25.00, 127.50, 5, 3, 3, TRUE,  'evacuate_safe_route', DATE_SUB(NOW(), INTERVAL 7 DAY)),
(8,  4, 1, 1.00, 76.20, 40.00, 40.00, 11.20, 15.00, 132.00, 3, 3, 3, TRUE,  'search_casualties',   DATE_SUB(NOW(), INTERVAL 5 DAY)),
(9,  4, 1, 1.00, 72.80, 40.00, 40.00, 12.80, 20.00, 108.00, 4, 3, 3, TRUE,  'equipment_selection', DATE_SUB(NOW(), INTERVAL 3 DAY)),
(10, 4, 1, 1.00, 78.00, 40.00, 40.00, 13.00, 15.00, 105.00, 3, 3, 3, TRUE,  'assess_situation',    DATE_SUB(NOW(), INTERVAL 1 DAY)),

(11, 5, 1, 1.00, 34.00, 28.00, 26.67,  9.33, 30.00, 160.00, 6, 2, 3, TRUE,  'equipment_selection', DATE_SUB(NOW(), INTERVAL 10 DAY)),
(12, 5, 1, 0.70, 25.00, 20.00, 13.33,  0.00, 40.00, 240.00, 8, 1, 3, FALSE, 'evacuate_safe_route', DATE_SUB(NOW(), INTERVAL 8 DAY)),
(13, 5, 1, 0.70, 44.50, 40.00, 26.67, 12.83, 35.00, 107.50, 7, 2, 3, TRUE,  'equipment_selection', DATE_SUB(NOW(), INTERVAL 5 DAY)),
(14, 5, 1, 0.70, 51.00, 40.00, 40.00,  6.00, 35.00, 210.00, 7, 3, 3, TRUE,  'search_casualties',   DATE_SUB(NOW(), INTERVAL 3 DAY)),
(15, 5, 1, 0.70, 56.40, 40.00, 40.00,  6.40, 30.00, 204.00, 6, 3, 3, TRUE,  'evacuate_safe_route', DATE_SUB(NOW(), INTERVAL 1 DAY)),

(16, 6, 1, 1.00, 68.00, 40.00, 40.00,  8.00, 20.00, 180.00, 4, 3, 3, TRUE,  'assess_situation',    DATE_SUB(NOW(), INTERVAL 9 DAY)),
(17, 6, 1, 1.00, 74.50, 40.00, 40.00,  9.50, 15.00, 157.50, 3, 3, 3, TRUE,  'equipment_selection', DATE_SUB(NOW(), INTERVAL 7 DAY)),
(18, 6, 1, 1.00, 79.00, 40.00, 40.00,  9.00, 10.00, 165.00, 2, 3, 3, TRUE,  'search_casualties',   DATE_SUB(NOW(), INTERVAL 4 DAY)),
(19, 6, 1, 1.00, 75.50, 40.00, 40.00, 10.50, 15.00, 142.50, 3, 3, 3, TRUE,  'evacuate_safe_route', DATE_SUB(NOW(), INTERVAL 2 DAY)),
(20, 6, 1, 1.00, 85.20, 40.00, 40.00, 15.20, 10.00,  72.00, 2, 3, 3, TRUE,  'equipment_selection', DATE_SUB(NOW(), INTERVAL 12 HOUR));

-- 5. Sample session_step_logs across key sessions (plus automated expansion for all 20 sessions)
INSERT INTO session_step_logs (session_id, step_id, time_spent_seconds, mistakes_in_step, completed)
SELECT s.id, st.id,
       ROUND(st.expected_time_seconds * (0.7 + (s.id % 5) * 0.1), 2),
       CASE
         WHEN st.step_key = s.weakest_step_key THEN GREATEST(1, FLOOR(s.total_mistakes * 0.6))
         WHEN st.step_number = 3 AND s.total_mistakes > 1 THEN FLOOR(s.total_mistakes * 0.4)
         ELSE 0
       END,
       CASE WHEN s.passed OR st.step_number <= 4 THEN TRUE ELSE FALSE END
FROM sessions s
CROSS JOIN scenario_steps st
WHERE st.scenario_id = s.scenario_id;
