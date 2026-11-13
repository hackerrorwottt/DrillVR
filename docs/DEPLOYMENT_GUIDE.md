# DrillVR - Hardware Deployment & LAN Testing Guide

This guide describes the step-by-step procedure for deploying and testing DrillVR across a host development machine and an Android smartphone mounted in a Google Cardboard VR headset.

---

## 1. Prerequisites

- **Host Machine**: macOS, Linux, or Windows with Node.js 20+, MySQL 8+, and Unity 6 (6000.x) with Android Build Support.
- **Android Device**: Android 8.0+ (API 26+) with hardware gyroscope/accelerometer.
- **Cardboard Headset**: Standard Google Cardboard v2 or compatible viewer with capacitive touch trigger.
- **Local Network**: Both the laptop and smartphone must be connected to the **same Wi-Fi network** or smartphone Wi-Fi hotspot.

---

## 2. Host Backend & Database Setup

1. **Start MySQL Server**:
   ```bash
   mysql.server start
   ```

2. **Initialize Schema & Seed Trainee Cohort**:
   ```bash
   cd backend
   npm install
   npm run db:seed
   ```

3. **Verify Database Health**:
   ```bash
   npm run db:check
   ```

4. **Start REST API Server**:
   ```bash
   npm start
   ```
   *The backend will listen on `http://0.0.0.0:4000`.*

5. **Start Instructor Dashboard**:
   ```bash
   cd ../dashboard
   python3 -m http.server 8080
   ```
   *Open `http://localhost:8080` in your browser.*

---

## 3. Find Your Laptop's LAN IP Address

Because Android devices run in their own networking namespace, `http://127.0.0.1` refers to the phone itself. You must use your host machine's Wi-Fi IP address:

- **macOS / Linux**:
  ```bash
  ifconfig | grep "inet " | grep -v 127.0.0.1
  # Example output: inet 192.168.1.45 netmask 0xffffff00 broadcast 192.168.1.255
  ```
- **Windows**:
  ```cmd
  ipconfig
  # Look for "IPv4 Address" under your active Wi-Fi adapter (e.g., 192.168.1.45)
  ```

---

## 4. Unity Project & Android Player Settings

1. Open `unity-client` in **Unity Editor 6000.x**.
2. Verify settings in **Project Settings > Player > Android**:
   - **Package Name**: `com.drillvr.prototype`
   - **Minimum API Level**: `Android 8.0 'Oreo' (API level 26)`
   - **Target API Level**: `Automatic (highest installed)`
   - **Scripting Backend**: `IL2CPP`
   - **Target Architectures**: `ARM64`
   - **Graphics API**: `OpenGLES3`
   - **Insecure HTTP Option**: `Always Allowed` (Permits cleartext HTTP traffic to your LAN IP without requiring HTTPS SSL certificates during local evaluation)
3. Open `Assets/Scenes/M8_FullOnlineLoop.unity`.
4. In the hierarchy, locate `BackendFlowCanvas` or `ApiClient`:
   - Set **Default Base Url** to: `http://<YOUR_LAPTOP_IP>:4000` (e.g., `http://192.168.1.45:4000`).

---

## 5. Build & Sideload APK

1. In the Unity Editor menu, select:
   **DrillVR > Build Android Cardboard APK**
   *(Or press `Ctrl+Shift+B` / `Cmd+Shift+B` and click Build).*
2. Output path: `Builds/Android/DrillVR.apk`.
3. Connect your Android phone via USB with **USB Debugging** enabled.
4. Install via ADB:
   ```bash
   adb install -r Builds/Android/DrillVR.apk
   ```

---

## 6. End-to-End Verification Loop

1. **Launch App on Phone**: Place phone into Google Cardboard headset.
2. **Gaze Authentication**:
   - Focus reticle on **Diya Patel** (or Aarav Sharma / Kabir Verma) preset button.
   - Hold gaze for 1.5s to trigger auto-login.
   - Verify green confirmation: `Logged in as Diya Patel (Trainee)`.
3. **Scenario Start**:
   - Gaze at `Load building_emergency_v1` and confirm.
   - Observe HUD showing current step: `Step 1/7: Confirm Briefing`.
4. **Execute Drill Steps**:
   - **Step 1**: Gaze at Briefing Board at West Command Post table.
   - **Step 2**: Teleport into corridor, inspect smoke zone and blocked East exit.
   - **Step 3**: Enter Room 1 and pick up Respirator, Flashlight, and MedKit.
   - **Step 4**: Search rooms and locate all 3 casualties.
   - **Step 5**: Guide casualties through North Stairwell to Safe Assembly Zone.
   - **Step 6**: Teleport back to Command Post and select the Radio.
5. **Debrief & Database Sync**:
   - Debrief panel displays final score, mistake penalties, and weakest step.
   - ApiClient automatically uploads session via `POST /api/sessions`.
   - Status updates: `Synced to MySQL (Session #XX)`.
6. **Live Dashboard Check**:
   - On your laptop browser at `http://localhost:8080`, click **Refresh Telemetry**.
   - Verify Diya's newly completed session appears in the ledger with verified step times.
