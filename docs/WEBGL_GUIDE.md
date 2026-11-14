# DrillVR WebGL Build & Deployment Guide

This guide details how to build, run, and test the native Unity WebGL simulation within the DrillVR project ecosystem.

---

## 1. Prerequisites

* **Unity Version:** Unity `6000.6.7f1` (Unity 6).
* **Unity Modules:** **WebGL Build Support** must be installed in Unity Hub under Unity 6000.6.7f1 (`Unity Hub > Installs > 6000.6.7f1 > Add modules > WebGL Build Support`).
* **Node.js & Express API:** Running on port `4000` (`cd backend && npm start`).
* **Web Dashboard:** Served on port `8080` (e.g., `python3 -m http.server 8080 --directory dashboard`).

---

## 2. API Base URL Resolution in WebGL

When the Unity simulation runs inside a web browser under WebGL:
1. **Dynamic Derivation:** If no saved URL exists in `PlayerPrefs`, `ApiClient.cs` dynamically parses `Application.absoluteURL` to derive the backend URL using the browser's current hostname and default backend port `4000`:
   ```csharp
   Uri pageUri = new Uri(Application.absoluteURL);
   initialUrl = $"{pageUri.Scheme}://{pageUri.Host}:4000";
   ```
   > [!NOTE]
   > The derived URL assumes the DrillVR Node.js/Express backend runs on port `4000`.

2. **PlayerPrefs Override:** If you enter a custom API URL into the in-player login screen (`BackendFlowController`), the URL is saved to `PlayerPrefs` (`DrillVR_ApiBaseUrl`). A saved `PlayerPrefs` URL always takes precedence over the dynamically derived URL.
3. **Editable Input Field:** The login panel in the 3D scene contains a visible `Base URL` input field that allows the user or instructor to manually reconfigure the API target at any point before signing in.

---

## 3. How to Build WebGL

### Automated Build Pipeline
1. Open the project in Unity 6000.6.7f1.
2. In the top Unity menu, click:
   ```text
   DrillVR > Build WebGL for Dashboard
   ```
3. Unity will compile `Assets/Scenes/M8_FullOnlineLoop.unity` using `WebGLBuildPipeline.cs` and export the generated files to:
   ```text
   dashboard/unity-webgl/
   ```

### Command Line / Batchmode Build
You can also trigger the build headlessly from the terminal:
```bash
/Applications/Unity/Hub/Editor/6000.6.7f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit \
  -projectPath unity-client \
  -executeMethod DrillVR.EditorTools.WebGLBuildPipeline.BuildForDashboard \
  -logFile -
```

---

## 4. Local Server Compression Setting

* **Setting in `WebGLBuildPipeline.cs`:**
  ```csharp
  PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
  PlayerSettings.WebGL.decompressionFallback = true;
  ```
* **Why Compression is Disabled:** Standard local static development servers (such as Python's `python3 -m http.server 8080` or basic Node.js static middleware) do not send the necessary `Content-Encoding: gzip` or `Content-Encoding: br` headers. 
* Disabling compression generates uncompressed `.wasm` and `.data` files that load immediately on any static HTTP server without header configuration errors.

---

## 5. Offline Queue & Hardware Divergence

* **In-Memory Buffering (WebGL):** In WebGL, `OfflineSessionQueue.cs` buffers any failed session uploads in-memory (`_cache`) for the duration of the active browser session.
* **Test Case E2E-02:** Test case `E2E-02` (persisting session data across physical device restarts and offline airplane mode to `pending_sessions.json`) applies exclusively to physical mobile Android APK hardware. WebGL builds running in browser tabs do not write to physical local disks.

---

## 6. Browser Input, Focus, & Pointer Lock

* **`WebGLInput.captureAllKeyboardInput = false`:** Configured in `EditorWalkController.cs:Awake()` under `#if UNITY_WEBGL && !UNITY_EDITOR`.
* **Canvas Focus Requirement:** When `captureAllKeyboardInput` is disabled, the browser only sends keyboard events (`W A S D`, `L`, `Esc`) to Unity if the canvas element has keyboard focus. The user must click the canvas once to direct focus to the simulation.
* **Pointer Lock via `L`:** Pressing `L` locks the cursor for continuous mouselook. Web browsers (Chrome, Firefox, Safari) enforce that pointer lock requests must originate from a user interaction gesture (clicking inside the canvas first). Pressing `Esc` unlocks the cursor.

---

## 7. Demo-Only Test Credentials (Seeded)

> [!NOTE]
> The following accounts are seeded exclusively for local development, automated testing, and demonstration evaluation by `backend/db/seed.sql` and `backend/src/scripts/initDb.js`. They are demo-only and must never be used in production.

| Role | Name | Email | Password | Purpose |
|---|---|---|---|---|
| **Instructor** | Dr. Vikram Rao | `instructor@drillvr.edu` | `Instructor@123` | Dashboard evaluation, cohort analytics, and live session monitoring |
| **Admin** | System Admin | `admin@drillvr.edu` | `Admin@123` | Master administrative API access |
| **Trainee** | Aarav Sharma | `aarav@drillvr.edu` | `Trainee@123` | High Performer profile (Adaptive Tier: Harder, 1.35x hazard rate) |
| **Trainee** | Diya Patel | `diya@drillvr.edu` | `Trainee@123` | Standard Performer profile (Adaptive Tier: Normal, 1.00x hazard rate) |
| **Trainee** | Kabir Verma | `kabir@drillvr.edu` | `Trainee@123` | Struggling Trainee profile (Adaptive Tier: Easier, 0.70x hazard rate + forced hints) |
| **Trainee** | Meera Nair | `meera@drillvr.edu` | `Trainee@123` | Developing Trainee profile (Adaptive Tier: Normal, 1.00x hazard rate) |
