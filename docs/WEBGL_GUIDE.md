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
