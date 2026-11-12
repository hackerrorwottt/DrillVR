using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using DrillVR.Environment;
using DrillVR.Interactions;
using DrillVR.Player;
using DrillVR.UI;

namespace DrillVR.EditorTools
{
    /// <summary>
    /// Automates Milestone 3: upgrades the building environment with GazeInteractor,
    /// radial dwell reticle, TeleportPoints across all rooms/exits, DoorInteractables,
    /// and a World-Space Main Menu + Instructions UI, saving M3_GazeAndUI.unity.
    /// </summary>
    public static class Milestone3Setup
    {
        private const string ScenePath = "Assets/Scenes/M3_GazeAndUI.unity";
        private const string RingTexturePath = "Assets/Textures/ReticleRing.png";
        private const string TeleportMatPath = "Assets/Materials/M3_TeleportPad.mat";

        [MenuItem("DrillVR/Setup Milestone 3 Interactions")]
        public static void RunSetup()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            BuildMilestone3IntoCurrentScene(out _, out _, out _, out _);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true)
            };

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "DrillVR Milestone 3 Setup Complete",
                "Built Assets/Scenes/M3_GazeAndUI.unity with GazeInteractor, dwell fill ring, " +
                "12 TeleportPoints, DoorInteractables, and World-Space Menus!\n\n" +
                "Press Play to test:\n" +
                "- Hold Right-Mouse (or press L) to look; Left-Click or dwell 1.5s to select.\n" +
                "- Click 'Instructions' then 'Back', then 'Start Drill'.\n" +
                "- Gaze at green pads to teleport through the building.",
                "OK"
            );
        }

        public static void BuildMilestone3IntoCurrentScene(
            out GameObject buildingRoot,
            out GameObject playerRig,
            out GazeInteractor gazeInteractor,
            out UIManager uiManager)
        {
            Sprite ringSprite = EnsureReticleRingSprite();
            Material teleportMat = EnsureTeleportMaterial();
            Font uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            Milestone2Setup.BuildMilestone2IntoCurrentScene(out buildingRoot, out playerRig);

            gazeInteractor = ConfigureCameraGazeAndHud(playerRig, ringSprite, uiFont, out Text hoverPrompt, out Text hudStatus);
            AttachDoorInteractables(buildingRoot.transform);
            CreateTeleportPoints(playerRig.GetComponent<EditorWalkController>(), teleportMat);
            uiManager = CreateWorldSpaceMenus(gazeInteractor, hoverPrompt, hudStatus, uiFont);
        }

        private static Sprite EnsureReticleRingSprite()
        {
            if (!Directory.Exists("Assets/Textures"))
            {
                Directory.CreateDirectory("Assets/Textures");
            }

            if (!File.Exists(RingTexturePath))
            {
                const int size = 64;
                Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                float center = (size - 1) * 0.5f;
                float outerR = size * 0.46f;
                float innerR = size * 0.30f;

                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                        bool inRing = dist >= innerR && dist <= outerR;
                        tex.SetPixel(x, y, inRing ? Color.white : Color.clear);
                    }
                }

                tex.Apply();
                File.WriteAllBytes(RingTexturePath, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(RingTexturePath, ImportAssetOptions.ForceUpdate);
            }

            TextureImporter importer = AssetImporter.GetAtPath(RingTexturePath) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(RingTexturePath);
        }

        private static Material EnsureTeleportMaterial()
        {
            if (!Directory.Exists("Assets/Materials"))
            {
                Directory.CreateDirectory("Assets/Materials");
            }

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(TeleportMatPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Standard"));
                mat.color = new Color(0.15f, 0.78f, 0.42f, 1f);
                mat.SetFloat("_Glossiness", 0.25f);
                AssetDatabase.CreateAsset(mat, TeleportMatPath);
            }

            return mat;
        }

        private static GazeInteractor ConfigureCameraGazeAndHud(
            GameObject playerRig,
            Sprite ringSprite,
            Font uiFont,
            out Text hoverPromptText,
            out Text hudStatusText)
        {
            Transform camTransform = playerRig.transform.Find("Main Camera");
            Transform hudTransform = camTransform.Find("VrHudCanvas");
            Image reticleDot = hudTransform.Find("ReticleDot").GetComponent<Image>();

            // Radial Dwell Fill Ring around ReticleDot
            GameObject ringGo = new GameObject("DwellFillRing");
            ringGo.transform.SetParent(hudTransform, false);
            Image ringImg = ringGo.AddComponent<Image>();
            ringImg.sprite = ringSprite;
            ringImg.type = Image.Type.Filled;
            ringImg.fillMethod = Image.FillMethod.Radial360;
            ringImg.fillOrigin = (int)Image.Origin360.Top;
            ringImg.fillClockwise = true;
            ringImg.fillAmount = 0f;
            ringImg.color = new Color(0.2f, 0.95f, 0.45f, 0.95f);
            ringImg.raycastTarget = false;
            ringImg.enabled = false;

            RectTransform ringRect = ringGo.GetComponent<RectTransform>();
            ringRect.localPosition = Vector3.zero;
            ringRect.sizeDelta = new Vector2(40f, 40f);

            // Hover Prompt label below reticle
            hoverPromptText = CreateHudText("HoverPromptText", hudTransform, uiFont, "", 18, new Vector2(0f, -45f), new Vector2(360f, 34f), Color.yellow, TextAnchor.MiddleCenter);
            hoverPromptText.gameObject.SetActive(false);

            // Top status bar
            hudStatusText = CreateHudText("HudStatusText", hudTransform, uiFont, "", 16, new Vector2(35f, 95f), new Vector2(260f, 36f), Color.white, TextAnchor.UpperRight);

            GazeInteractor interactor = camTransform.gameObject.AddComponent<GazeInteractor>();
            SerializedObject so = new SerializedObject(interactor);
            so.FindProperty("reticleDot").objectReferenceValue = reticleDot;
            so.FindProperty("dwellFillImage").objectReferenceValue = ringImg;
            so.ApplyModifiedPropertiesWithoutUndo();

            return interactor;
        }

        private static Text CreateHudText(
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
            txt.color = color;
            txt.alignment = alignment;
            txt.raycastTarget = false;

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.localPosition = pos;
            rect.sizeDelta = size;
            return txt;
        }

        private static void AttachDoorInteractables(Transform buildingRoot)
        {
            DoorController[] doors = buildingRoot.GetComponentsInChildren<DoorController>();
            foreach (DoorController door in doors)
            {
                if (door.GetComponent<DoorInteractable>() == null)
                {
                    door.gameObject.AddComponent<DoorInteractable>();
                }
            }
        }

        private static void CreateTeleportPoints(EditorWalkController playerRig, Material padMat)
        {
            GameObject tpRoot = new GameObject("TeleportPoints");

            var points = new (string name, Vector3 pos)[]
            {
                ("Command Post", new Vector3(-15.5f, 0.02f, 0f)),
                ("Building Entrance", new Vector3(-11.0f, 0.02f, 0f)),
                ("Corridor West", new Vector3(-8.0f, 0.02f, 0f)),
                ("Corridor Center", new Vector3(0.0f, 0.02f, 0f)),
                ("Corridor East", new Vector3(8.0f, 0.02f, 0f)),
                ("Equipment Room", new Vector3(-8.0f, 0.02f, 5.2f)),
                ("Office Room", new Vector3(8.0f, 0.02f, 5.2f)),
                ("Utility Room", new Vector3(-8.0f, 0.02f, -5.2f)),
                ("Conference Room", new Vector3(8.0f, 0.02f, -5.2f)),
                ("Stairwell Base", new Vector3(0.0f, 0.02f, 3.3f)),
                ("Stairwell Landing", new Vector3(0.0f, 0.62f, 8.2f)),
                ("Safe Assembly Zone", new Vector3(0.0f, 0.62f, 12.2f))
            };

            foreach (var p in points)
            {
                GameObject pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pad.name = $"TP_{p.name.Replace(" ", "")}";
                pad.transform.SetParent(tpRoot.transform, false);
                pad.transform.position = p.pos;
                pad.transform.localScale = new Vector3(0.9f, 0.03f, 0.9f);
                pad.GetComponent<Renderer>().sharedMaterial = padMat;

                // Replace thin capsule collider with generous BoxCollider for easy distant gaze targeting
                Collider defaultCol = pad.GetComponent<Collider>();
                if (defaultCol != null)
                {
                    Object.DestroyImmediate(defaultCol);
                }
                BoxCollider hitBox = pad.AddComponent<BoxCollider>();
                hitBox.isTrigger = true;
                hitBox.size = new Vector3(1.1f, 8.0f, 1.1f);
                hitBox.center = new Vector3(0f, 3.5f, 0f);

                TeleportPoint tp = pad.AddComponent<TeleportPoint>();
                tp.Configure(p.name, playerRig);
            }
        }

        private static UIManager CreateWorldSpaceMenus(
            GazeInteractor gazeInteractor,
            Text hoverPromptText,
            Text hudStatusText,
            Font uiFont)
        {
            GameObject menuRoot = new GameObject("WorldMenuCanvas");
            menuRoot.transform.position = new Vector3(-13.1f, 1.55f, 0f);
            menuRoot.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

            Canvas canvas = menuRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            menuRoot.AddComponent<CanvasScaler>();
            RectTransform canvasRect = menuRoot.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(640f, 400f);
            canvasRect.localScale = Vector3.one * 0.0032f;

            // Main Menu Panel
            GameObject mainPanel = CreatePanel("MainMenuPanel", menuRoot.transform, new Color(0.07f, 0.10f, 0.15f, 0.94f));
            CreatePanelText("TitleText", mainPanel.transform, uiFont, "DRILLVR\nBuilding Emergency Response", 34, new Vector2(0f, 110f), new Vector2(580f, 100f), Color.white);
            CreatePanelText("DisclaimerText", mainPanel.transform, uiFont, "Educational prototype only — simulation scores are not real-world qualification.", 17, new Vector2(0f, 35f), new Vector2(560f, 48f), new Color(0.95f, 0.80f, 0.35f));

            GazeButton startBtn = CreateGazeButton("Btn_StartDrill", mainPanel.transform, uiFont, "START DRILL", new Vector2(0f, -45f), new Vector2(300f, 58f));
            GazeButton instrBtn = CreateGazeButton("Btn_Instructions", mainPanel.transform, uiFont, "INSTRUCTIONS", new Vector2(0f, -120f), new Vector2(300f, 58f));

            // Instructions Panel
            GameObject instrPanel = CreatePanel("InstructionPanel", menuRoot.transform, new Color(0.07f, 0.10f, 0.15f, 0.96f));
            CreatePanelText("InstrTitle", instrPanel.transform, uiFont, "HOW TO PLAY (VR & EDITOR)", 30, new Vector2(0f, 130f), new Vector2(580f, 56f), Color.white);
            CreatePanelText(
                "InstrBody",
                instrPanel.transform,
                uiFont,
                "1. PHONE VR: Look around to aim the center dot.\n" +
                "2. SELECT: Dwell on any pad/button for 1.5s OR tap the Cardboard trigger/screen.\n" +
                "3. MOVE: Gaze at green teleport pads to move safely.\n" +
                "4. EDITOR TEST: Hold Right-Mouse (or press L) to look, WASD to walk, Left-Click to select.",
                20,
                new Vector2(0f, 10f),
                new Vector2(560f, 165f),
                new Color(0.88f, 0.92f, 0.96f)
            );
            GazeButton backBtn = CreateGazeButton("Btn_BackToMenu", instrPanel.transform, uiFont, "BACK TO MENU", new Vector2(0f, -125f), new Vector2(300f, 58f));
            instrPanel.SetActive(false);

            UIManager uiMgr = menuRoot.AddComponent<UIManager>();
            SerializedObject uiSo = new SerializedObject(uiMgr);
            uiSo.FindProperty("mainMenuPanel").objectReferenceValue = mainPanel;
            uiSo.FindProperty("instructionPanel").objectReferenceValue = instrPanel;
            uiSo.FindProperty("startDrillButton").objectReferenceValue = startBtn;
            uiSo.FindProperty("instructionsButton").objectReferenceValue = instrBtn;
            uiSo.FindProperty("backToMenuButton").objectReferenceValue = backBtn;
            uiSo.FindProperty("gazeInteractor").objectReferenceValue = gazeInteractor;
            uiSo.FindProperty("hoverPromptText").objectReferenceValue = hoverPromptText;
            uiSo.FindProperty("hudStatusText").objectReferenceValue = hudStatusText;
            uiSo.ApplyModifiedPropertiesWithoutUndo();

            return uiMgr;
        }

        private static GameObject CreatePanel(string name, Transform parent, Color bgColor)
        {
            GameObject panel = new GameObject(name);
            panel.transform.SetParent(parent, false);
            Image img = panel.AddComponent<Image>();
            img.color = bgColor;
            img.raycastTarget = false;

            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return panel;
        }

        private static Text CreatePanelText(
            string name,
            Transform parent,
            Font font,
            string text,
            int fontSize,
            Vector2 anchoredPos,
            Vector2 size,
            Color color)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Text t = go.AddComponent<Text>();
            t.font = font;
            t.text = text;
            t.fontSize = fontSize;
            t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = color;
            t.raycastTarget = false;

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;
            return t;
        }

        private static GazeButton CreateGazeButton(
            string name,
            Transform parent,
            Font font,
            string label,
            Vector2 anchoredPos,
            Vector2 size)
        {
            GameObject btnGo = new GameObject(name);
            btnGo.transform.SetParent(parent, false);
            Image bg = btnGo.AddComponent<Image>();
            bg.raycastTarget = false;

            RectTransform rect = btnGo.GetComponent<RectTransform>();
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;

            BoxCollider col = btnGo.AddComponent<BoxCollider>();
            col.size = new Vector3(size.x, size.y, 12f);

            GazeButton gazeBtn = btnGo.AddComponent<GazeButton>();
            gazeBtn.SetPromptText(label);

            CreatePanelText("Label", btnGo.transform, font, label, 24, Vector2.zero, size, Color.white);
            return gazeBtn;
        }
    }
}
