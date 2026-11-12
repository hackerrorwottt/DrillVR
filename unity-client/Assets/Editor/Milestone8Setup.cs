using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using DrillVR.AI;
using DrillVR.Assessment;
using DrillVR.Core;
using DrillVR.Equipment;
using DrillVR.Interactions;
using DrillVR.Network;
using DrillVR.UI;

namespace DrillVR.EditorTools
{
    /// <summary>
    /// Automates Milestone 8: enables HTTP LAN access for Android, wires ApiClient,
    /// OfflineSessionQueue, DrillBackendSync, World-Space Login/Register/Scenario UI,
    /// and Debrief sync status, saving M8_FullOnlineLoop.unity.
    /// </summary>
    public static class Milestone8Setup
    {
        private const string ScenePath = "Assets/Scenes/M8_FullOnlineLoop.unity";

        [MenuItem("DrillVR/Setup Milestone 8 Backend Integration")]
        public static void RunSetup()
        {
            // Allow HTTP calls to http://<LAPTOP_IP>:4000 on Android 9+ devices
            PlayerSettings.insecureHttpOption = InsecureHttpOption.AlwaysAllowed;

            ScenarioConfig scenario = Milestone4Setup.CreateOrUpdateScriptableObjects(out List<EquipmentData> equipmentAssets);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Milestone6Setup.BuildMilestone6IntoCurrentScene(
                scenario,
                equipmentAssets,
                out MissionManager missionManager,
                out AssessmentManager assessmentManager,
                out HintSystem hintSystem,
                out DebriefPanelController debriefController,
                out UIManager uiManager,
                out _
            );

            // Let BackendFlowController own startup UI before showing MainMenuPanel
            SerializedObject uiSo = new SerializedObject(uiManager);
            uiSo.FindProperty("showMenuOnStart").boolValue = false;
            uiSo.ApplyModifiedPropertiesWithoutUndo();

            Font uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // 1. Add Network components onto DrillSystems
            GameObject drillSystems = GameObject.Find("DrillSystems");
            OfflineSessionQueue queue = drillSystems.AddComponent<OfflineSessionQueue>();
            ApiClient apiClient = drillSystems.AddComponent<ApiClient>();
            SerializedObject apiSo = new SerializedObject(apiClient);
            apiSo.FindProperty("offlineQueue").objectReferenceValue = queue;
            apiSo.ApplyModifiedPropertiesWithoutUndo();

            DrillBackendSync backendSync = drillSystems.AddComponent<DrillBackendSync>();
            SerializedObject syncSo = new SerializedObject(backendSync);
            syncSo.FindProperty("apiClient").objectReferenceValue = apiClient;
            syncSo.FindProperty("offlineQueue").objectReferenceValue = queue;
            syncSo.FindProperty("missionManager").objectReferenceValue = missionManager;
            syncSo.FindProperty("assessmentManager").objectReferenceValue = assessmentManager;
            syncSo.FindProperty("hintSystem").objectReferenceValue = hintSystem;
            syncSo.ApplyModifiedPropertiesWithoutUndo();

            // 2. Build World-Space Login / Register & Scenario Select Canvas
            BuildBackendFlowCanvas(apiClient, backendSync, uiManager, uiFont);

            // 3. Add Sync Status & Retry button onto DebriefPanel
            AttachDebriefSyncUi(debriefController, apiClient, backendSync, uiFont);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true)
            };

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "DrillVR Milestone 8 Setup Complete",
                "Enabled HTTP LAN option and built Assets/Scenes/M8_FullOnlineLoop.unity!\n\n" +
                "To test:\n" +
                "1. Start backend (`cd backend && npm start`).\n" +
                "2. In Unity Editor: click a preset trainee (Aarav/Diya/Kabir) or Register.\n" +
                "3. On Android phone: change API Base URL on ApiClient (or BaseUrl InputField) to http://<YOUR_LAPTOP_IP>:4000.\n" +
                "4. Verify difficulty changes after runs and offline runs sync after reconnecting.",
                "OK"
            );
        }

        private static void BuildBackendFlowCanvas(
            ApiClient apiClient,
            DrillBackendSync backendSync,
            UIManager uiManager,
            Font uiFont)
        {
            GameObject root = new GameObject("BackendFlowCanvas");
            root.transform.position = new Vector3(-13.1f, 1.55f, 0f);
            root.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            root.AddComponent<CanvasScaler>();
            root.AddComponent<GraphicRaycaster>();
            RectTransform cRect = root.GetComponent<RectTransform>();
            cRect.sizeDelta = new Vector2(760f, 520f);
            cRect.localScale = Vector3.one * 0.0031f;

            // Ensure EventSystem exists for Editor InputField typing
            if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                GameObject es = new GameObject("EventSystem");
                es.AddComponent<UnityEngine.EventSystems.EventSystem>();
                es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            // Auth Panel
            GameObject authPanel = CreatePanel("AuthPanel", root.transform, new Color(0.06f, 0.09f, 0.14f, 0.96f));
            CreateText("AuthTitle", authPanel.transform, uiFont, "DRILLVR ACCOUNT LOGIN", 28,
                new Vector2(0f, 215f), new Vector2(700f, 44f), Color.white, TextAnchor.MiddleCenter);

            InputField urlField = CreateInputField("Input_BaseUrl", authPanel.transform, uiFont, "http://127.0.0.1:4000",
                new Vector2(0f, 160f), new Vector2(560f, 38f), false);
            InputField nameField = CreateInputField("Input_Name", authPanel.transform, uiFont, "Name (for Register)",
                new Vector2(-190f, 110f), new Vector2(260f, 38f), false);
            InputField emailField = CreateInputField("Input_Email", authPanel.transform, uiFont, "Email",
                new Vector2(100f, 110f), new Vector2(290f, 38f), false);
            InputField passField = CreateInputField("Input_Password", authPanel.transform, uiFont, "Password",
                new Vector2(0f, 62f), new Vector2(560f, 38f), true);

            GazeButton loginBtn = CreateGazeBtn("Btn_Login", authPanel.transform, uiFont, "LOGIN", new Vector2(-145f, 4f), new Vector2(260f, 48f));
            GazeButton regBtn = CreateGazeBtn("Btn_Register", authPanel.transform, uiFont, "REGISTER", new Vector2(145f, 4f), new Vector2(260f, 48f));

            CreateText("PresetHint", authPanel.transform, uiFont, "1-Tap Cardboard VR Presets (or Offline):", 16,
                new Vector2(0f, -45f), new Vector2(680f, 26f), new Color(0.75f, 0.88f, 1f), TextAnchor.MiddleCenter);

            GazeButton aaravBtn = CreateGazeBtn("Btn_Aarav", authPanel.transform, uiFont, "AARAV (HARDER)", new Vector2(-240f, -92f), new Vector2(215f, 46f));
            GazeButton diyaBtn = CreateGazeBtn("Btn_Diya", authPanel.transform, uiFont, "DIYA (NORMAL)", new Vector2(0f, -92f), new Vector2(215f, 46f));
            GazeButton kabirBtn = CreateGazeBtn("Btn_Kabir", authPanel.transform, uiFont, "KABIR (EASIER)", new Vector2(240f, -92f), new Vector2(215f, 46f));
            GazeButton offlineBtn = CreateGazeBtn("Btn_Offline", authPanel.transform, uiFont, "PLAY OFFLINE (QUEUE)", new Vector2(0f, -152f), new Vector2(320f, 46f));

            Text authStatus = CreateText("AuthStatusText", authPanel.transform, uiFont, "", 16,
                new Vector2(0f, -210f), new Vector2(700f, 44f), Color.yellow, TextAnchor.MiddleCenter);

            // Scenario Select Panel
            GameObject scenPanel = CreatePanel("ScenarioSelectPanel", root.transform, new Color(0.06f, 0.09f, 0.14f, 0.96f));
            Text welcomeTxt = CreateText("UserWelcomeText", scenPanel.transform, uiFont, "Signed in", 26,
                new Vector2(0f, 195f), new Vector2(700f, 42f), Color.cyan, TextAnchor.MiddleCenter);
            Text diffTxt = CreateText("DifficultyBadgeText", scenPanel.transform, uiFont, "Adaptive Difficulty: ...", 19,
                new Vector2(0f, 140f), new Vector2(700f, 40f), new Color(0.35f, 0.95f, 0.55f), TextAnchor.MiddleCenter);
            Text scenListTxt = CreateText("ScenarioListText", scenPanel.transform, uiFont, "Loading scenarios...", 18,
                new Vector2(0f, 20f), new Vector2(680f, 170f), Color.white, TextAnchor.MiddleCenter);
            GazeButton loadScenBtn = CreateGazeBtn("Btn_LoadActiveScenario", scenPanel.transform, uiFont,
                "LOAD BUILDING EMERGENCY DRILL", new Vector2(0f, -135f), new Vector2(460f, 58f));
            scenPanel.SetActive(false);

            BackendFlowController ctrl = root.AddComponent<BackendFlowController>();
            SerializedObject so = new SerializedObject(ctrl);
            so.FindProperty("apiClient").objectReferenceValue = apiClient;
            so.FindProperty("backendSync").objectReferenceValue = backendSync;
            so.FindProperty("uiManager").objectReferenceValue = uiManager;
            so.FindProperty("authPanel").objectReferenceValue = authPanel;
            so.FindProperty("scenarioSelectPanel").objectReferenceValue = scenPanel;
            so.FindProperty("baseUrlInput").objectReferenceValue = urlField;
            so.FindProperty("nameInput").objectReferenceValue = nameField;
            so.FindProperty("emailInput").objectReferenceValue = emailField;
            so.FindProperty("passwordInput").objectReferenceValue = passField;
            so.FindProperty("authStatusText").objectReferenceValue = authStatus;
            so.FindProperty("loginButton").objectReferenceValue = loginBtn;
            so.FindProperty("registerButton").objectReferenceValue = regBtn;
            so.FindProperty("presetAaravButton").objectReferenceValue = aaravBtn;
            so.FindProperty("presetDiyaButton").objectReferenceValue = diyaBtn;
            so.FindProperty("presetKabirButton").objectReferenceValue = kabirBtn;
            so.FindProperty("offlineModeButton").objectReferenceValue = offlineBtn;
            so.FindProperty("userWelcomeText").objectReferenceValue = welcomeTxt;
            so.FindProperty("scenarioListText").objectReferenceValue = scenListTxt;
            so.FindProperty("difficultyBadgeText").objectReferenceValue = diffTxt;
            so.FindProperty("loadActiveScenarioButton").objectReferenceValue = loadScenBtn;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AttachDebriefSyncUi(
            DebriefPanelController debriefController,
            ApiClient apiClient,
            DrillBackendSync backendSync,
            Font uiFont)
        {
            if (debriefController == null)
            {
                return;
            }

            Transform panel = debriefController.transform.Find("DebriefPanel");
            if (panel == null)
            {
                return;
            }

            Text syncTxt = CreateText("SyncStatusText", panel, uiFont, "Sync: Ready", 15,
                new Vector2(0f, -166f), new Vector2(700f, 24f), Color.cyan, TextAnchor.MiddleCenter);

            // Nudge Restart & Main Menu slightly and add Retry Sync button
            Transform restartTr = panel.Find("Btn_RestartDrill");
            Transform menuTr = panel.Find("Btn_MainMenu");
            if (restartTr != null) restartTr.GetComponent<RectTransform>().anchoredPosition = new Vector2(-235f, -215f);
            if (menuTr != null) menuTr.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -215f);

            GazeButton retryBtn = CreateGazeBtn("Btn_RetrySync", panel, uiFont, "RETRY SYNC",
                new Vector2(235f, -215f), new Vector2(200f, 56f));

            SerializedObject dSo = new SerializedObject(debriefController);
            dSo.FindProperty("backendSync").objectReferenceValue = backendSync;
            dSo.FindProperty("apiClient").objectReferenceValue = apiClient;
            dSo.FindProperty("syncStatusText").objectReferenceValue = syncTxt;
            dSo.FindProperty("retrySyncButton").objectReferenceValue = retryBtn;
            dSo.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject CreatePanel(string name, Transform parent, Color color)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Image img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return go;
        }

        private static InputField CreateInputField(
            string name,
            Transform parent,
            Font font,
            string placeholder,
            Vector2 pos,
            Vector2 size,
            bool isPassword)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Image bg = go.AddComponent<Image>();
            bg.color = new Color(0.14f, 0.19f, 0.27f, 1f);

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;

            Text ph = CreateText("Placeholder", go.transform, font, placeholder, 16, Vector2.zero, size - new Vector2(16f, 4f), new Color(0.6f, 0.65f, 0.72f), TextAnchor.MiddleLeft);
            Text txt = CreateText("Text", go.transform, font, "", 16, Vector2.zero, size - new Vector2(16f, 4f), Color.white, TextAnchor.MiddleLeft);
            txt.supportRichText = false;

            InputField field = go.AddComponent<InputField>();
            field.textComponent = txt;
            field.placeholder = ph;
            field.contentType = isPassword ? InputField.ContentType.Password : InputField.ContentType.Standard;
            return field;
        }

        private static GazeButton CreateGazeBtn(
            string name,
            Transform parent,
            Font font,
            string label,
            Vector2 pos,
            Vector2 size)
        {
            GameObject btnGo = new GameObject(name);
            btnGo.transform.SetParent(parent, false);
            btnGo.AddComponent<Image>().raycastTarget = false;
            RectTransform rect = btnGo.GetComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;

            BoxCollider col = btnGo.AddComponent<BoxCollider>();
            col.size = new Vector3(size.x, size.y, 12f);

            GazeButton btn = btnGo.AddComponent<GazeButton>();
            btn.SetPromptText(label);
            CreateText("Label", btnGo.transform, font, label, 18, Vector2.zero, size, Color.white, TextAnchor.MiddleCenter);
            return btn;
        }

        private static Text CreateText(
            string name,
            Transform parent,
            Font font,
            string content,
            int fontSize,
            Vector2 pos,
            Vector2 size,
            Color color,
            TextAnchor alignment)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Text txt = go.AddComponent<Text>();
            txt.font = font;
            txt.text = content;
            txt.fontSize = fontSize;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = alignment;
            txt.color = color;
            txt.raycastTarget = false;

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            return txt;
        }
    }
}
