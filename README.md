# DrillVR

An AI-driven mobile VR educational prototype for live building emergency response training drills (built for Google Cardboard on Android with Unity 2022.3 LTS, Node.js, Express, and MySQL 8).

> **Educational Prototype Disclaimer:** DrillVR is an academic prototype inspired by the problem statement *"Augmented/Virtual Reality system for the live training of troops"* (Ministry of Home Affairs, 2023, Smart Automation, Software). It contains no weapons or combat content. Simulation scores are for educational demonstration only and must not be presented as proof of real-world qualification.

## Repository Layout
- `unity-client/` — Unity 2022.3 LTS mobile VR project (Google Cardboard XR Plugin)
- `backend/` — Node.js + Express + MySQL 8 REST API (Milestone 7)
- `dashboard/` — Plain HTML + Chart.js instructor dashboard (Milestone 9)

## Milestones
1. Project setup, packages, git repo, Cardboard config, first APK running on phone
2. Environment with placeholder assets (baked lighting, ambient audio, NavMesh)
3. Gaze/tap interaction + editor test mode
4. Hazard spread, equipment selection, safe-route logic
5. Casualty NPCs, search, evacuation
6. Scoring and on-screen performance report
7. Backend: schema, seed data, REST API, Jest + Supertest + Postman
8. Unity connects to backend with offline retry queue
9. Instructor dashboard, full testing, performance check, demo preparation
