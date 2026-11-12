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
using DrillVR.Player;
using DrillVR.Rescue;
using DrillVR.UI;

namespace DrillVR.EditorTools
{
    /// <summary>
    /// Automates Milestone 6: wires AssessmentManager (weighted formula), HintSystem
    /// (instructor panel + enable/disable toggle), and DebriefPanelController (score breakdown,
    /// step times, areas to improve, disclaimer, restart), saving M6_ScoringAndDebrief.unity.
    /// </summary>
    public static class Milestone6Setup
    {
        private const string ScenePath = "Assets/Scenes/M6_ScoringAndDebrief.unity";

        [MenuItem("DrillVR/Setup Milestone 6 Scoring & Debrief")]
        public static void RunSetup()
        {
            ScenarioConfig scenario = Milestone4Setup.CreateOrUpdateScriptableObjects(out List<EquipmentData> equipmentAssets);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            BuildMilestone6IntoCurrentScene(scenario, equipmentAssets, out _, out _, out _, out _, out _, out _);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true)
            };

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "DrillVR Milestone 6 Setup Complete",
                "Built Assets/Scenes/M6_ScoringAndDebrief.unity with AssessmentManager, " +
                "HintSystem (HINTS: ON/OFF toggle on Main Menu), and Debrief Screen with Restart!\n\n" +
                "Test in Play Mode:\n" +
                "1. Toggle HINTS ON/OFF on Main Menu to verify hints appear only when enabled.\n" +
                "2. Complete a full drill (or fail) to inspect Total Score, Step Times, Mistakes, Areas to Improve, and Disclaimer.\n" +
                "3. Click RESTART DRILL to verify clean reset.",
                "OK"
            );
        }

        public static void BuildMilestone6IntoCurrentScene(
            ScenarioConfig scenario,
            List<EquipmentData> equipmentAssets,
            out MissionManager missionManager,
            out AssessmentManager assessmentManager,
            out HintSystem hintSystem,
            out DebriefPanelController debriefController,
            out UIManager uiManager,
            out GameObject playerRig)
        {
            Milestone5Setup.BuildMilestone5IntoCurrentScene(
                scenario,
                equipmentAssets,
                out _,
                out playerRig,
                out missionManager,
                out _,
                out _,
                out EvacuationSystem evacuationSystem,
                out _
            );

            uiManager = Object.FindObjectOfType<UIManager>();
            Font uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // 1. AssessmentManager on DrillSystems
            GameObject drillSystems = GameObject.Find("DrillSystems");
            assessmentManager = drillSystems.AddComponent<AssessmentManager>();
            SerializedObject aSo = new SerializedObject(assessmentManager);
            aSo.FindProperty("missionManager").objectReferenceValue = missionManager;
            aSo.FindProperty("evacuationSystem").objectReferenceValue = evacuationSystem;
            aSo.ApplyModifiedPropertiesWithoutUndo();

            // 2. HintSystem + Instructor Hint Panel on VrHudCanvas + Toggle Button on MainMenuPanel
            hintSystem = BuildHintSystemAndPanel(drillSystems, playerRig, missionManager, uiFont);

            // 3. World-Space Debrief Screen
            debriefController = BuildDebriefCanvas(assessmentManager, missionManager, uiManager, playerRig, uiFont);
        }

        private static HintSystem BuildHintSystemAndPanel(
            GameObject drillSystems,
            GameObject playerRig,
            MissionManager missionManager,
            Font uiFont)
        {
            Transform hudTransform = playerRig.transform.Find("Main Camera/VrHudCanvas");

            GameObject hintPanel = new GameObject("InstructorHintPanel");
            hintPanel.transform.SetParent(hudTransform, false);
            Image bg = hintPanel.AddComponent<Image>();
            bg.color = new Color(0.06f, 0.12f, 0.22f, 0.88f);
            bg.raycastTarget = false;

            RectTransform pRect = hintPanel.GetComponent<RectTransform>();
            pRect.localPosition = new Vector2(0f, -130f);
            pRect.sizeDelta = new Vector2(390f, 64f);

            Text header = CreateText("HintHeader", hintPanel.transform, uiFont, "INSTRUCTOR HINT", 13,
                new Vector2(0f, 18f), new Vector2(376f, 20f), new Color(0.45f, 0.88f, 1f), TextAnchor.MiddleCenter);
            Text body = CreateText("HintBody", hintPanel.transform, uiFont, "", 13,
                new Vector2(0f, -10f), new Vector2(376f, 38f), Color.white, TextAnchor.MiddleCenter);

            // Add HINTS: ON/OFF GazeButton onto WorldMenuCanvas/MainMenuPanel
            GazeButton toggleBtn = null;
            Text toggleLabel = null;
            GameObject mainMenu = GameObject.Find("WorldMenuCanvas/MainMenuPanel");
            if (mainMenu != null)
            {
                Transform startTr = mainMenu.transform.Find("Btn_StartDrill");
                Transform instrTr = mainMenu.transform.Find("Btn_Instructions");
                if (startTr != null)
                {
                    RectTransform r = startTr.GetComponent<RectTransform>();
                    r.anchoredPosition = new Vector2(-95f, -45f);
                    r.sizeDelta = new Vector2(260f, 54f);
                }
                if (instrTr != null)
                {
                    RectTransform r = instrTr.GetComponent<RectTransform>();
                    r.anchoredPosition = new Vector2(-95f, -115f);
                    r.sizeDelta = new Vector2(260f, 54f);
                }

                GameObject btnGo = new GameObject("Btn_ToggleHints");
                btnGo.transform.SetParent(mainMenu.transform, false);
                btnGo.AddComponent<Image>().raycastTarget = false;
                RectTransform bRect = btnGo.GetComponent<RectTransform>();
                bRect.anchoredPosition = new Vector2(180f, -80f);
                bRect.sizeDelta = new Vector2(200f, 64f);

                BoxCollider col = btnGo.AddComponent<BoxCollider>();
                col.size = new Vector3(200f, 64f, 12f);

                toggleBtn = btnGo.AddComponent<GazeButton>();
                toggleBtn.SetPromptText("Toggle Instructor Hints");
                toggleLabel = CreateText("Label", btnGo.transform, uiFont, "HINTS: ON", 22,
                    Vector2.zero, new Vector2(190f, 56f), Color.white, TextAnchor.MiddleCenter);
            }

            HintSystem hintSystem = drillSystems.AddComponent<HintSystem>();
            SerializedObject hSo = new SerializedObject(hintSystem);
            hSo.FindProperty("missionManager").objectReferenceValue = missionManager;
            hSo.FindProperty("hintPanel").objectReferenceValue = hintPanel;
            hSo.FindProperty("hintHeaderText").objectReferenceValue = header;
            hSo.FindProperty("hintBodyText").objectReferenceValue = body;
            hSo.FindProperty("hintToggleButton").objectReferenceValue = toggleBtn;
            hSo.FindProperty("toggleButtonLabel").objectReferenceValue = toggleLabel;
            hSo.ApplyModifiedPropertiesWithoutUndo();

            return hintSystem;
        }

        private static DebriefPanelController BuildDebriefCanvas(
            AssessmentManager assessmentManager,
            MissionManager missionManager,
            UIManager uiManager,
            GameObject playerRig,
            Font uiFont)
        {
            GameObject debriefCanvas = new GameObject("DebriefCanvas");
            debriefCanvas.transform.position = new Vector3(-13.1f, 1.55f, 0f);
            debriefCanvas.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

            Canvas canvas = debriefCanvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            debriefCanvas.AddComponent<CanvasScaler>();
            RectTransform cRect = debriefCanvas.GetComponent<RectTransform>();
            cRect.sizeDelta = new Vector2(760f, 540f);
            cRect.localScale = Vector3.one * 0.0030f;

            GameObject panel = new GameObject("DebriefPanel");
            panel.transform.SetParent(debriefCanvas.transform, false);
            Image bg = panel.AddComponent<Image>();
            bg.color = new Color(0.06f, 0.09f, 0.14f, 0.96f);
            bg.raycastTarget = false;
            RectTransform pRect = panel.GetComponent<RectTransform>();
            pRect.anchorMin = Vector2.zero;
            pRect.anchorMax = Vector2.one;
            pRect.offsetMin = Vector2.zero;
            pRect.offsetMax = Vector2.zero;

            Text headerScore = CreateText("HeaderScoreText", panel.transform, uiFont, "SCORE: -- / 100", 30,
                new Vector2(0f, 225f), new Vector2(720f, 44f), Color.green, TextAnchor.MiddleCenter);
            Text breakdown = CreateText("BreakdownText", panel.transform, uiFont, "", 16,
                new Vector2(0f, 180f), new Vector2(720f, 40f), new Color(0.85f, 0.92f, 1f), TextAnchor.MiddleCenter);
            Text stepTimes = CreateText("StepTimesText", panel.transform, uiFont, "", 15,
                new Vector2(-175f, 20f), new Vector2(345f, 240f), Color.white, TextAnchor.UpperLeft);
            Text improve = CreateText("ImproveText", panel.transform, uiFont, "", 15,
                new Vector2(180f, 20f), new Vector2(345f, 240f), new Color(1f, 0.85f, 0.45f), TextAnchor.UpperLeft);
            Text disclaimer = CreateText("DisclaimerText", panel.transform, uiFont, "", 14,
                new Vector2(0f, -135f), new Vector2(720f, 42f), new Color(0.95f, 0.72f, 0.35f), TextAnchor.MiddleCenter);

            GazeButton restartBtn = CreateDebriefButton("Btn_RestartDrill", panel.transform, uiFont, "RESTART DRILL",
                new Vector2(-150f, -205f), new Vector2(260f, 56f));
            GazeButton menuBtn = CreateDebriefButton("Btn_MainMenu", panel.transform, uiFont, "MAIN MENU",
                new Vector2(150f, -205f), new Vector2(260f, 56f));

            DebriefPanelController controller = debriefCanvas.AddComponent<DebriefPanelController>();
            SerializedObject dSo = new SerializedObject(controller);
            dSo.FindProperty("assessmentManager").objectReferenceValue = assessmentManager;
            dSo.FindProperty("missionManager").objectReferenceValue = missionManager;
            dSo.FindProperty("uiManager").objectReferenceValue = uiManager;
            dSo.FindProperty("playerRig").objectReferenceValue = playerRig.GetComponent<EditorWalkController>();
            dSo.FindProperty("cameraTransform").objectReferenceValue = playerRig.transform.Find("Main Camera");
            dSo.FindProperty("debriefPanel").objectReferenceValue = panel;
            dSo.FindProperty("headerScoreText").objectReferenceValue = headerScore;
            dSo.FindProperty("breakdownText").objectReferenceValue = breakdown;
            dSo.FindProperty("stepTimesText").objectReferenceValue = stepTimes;
            dSo.FindProperty("improveText").objectReferenceValue = improve;
            dSo.FindProperty("disclaimerText").objectReferenceValue = disclaimer;
            dSo.FindProperty("restartButton").objectReferenceValue = restartBtn;
            dSo.FindProperty("mainMenuButton").objectReferenceValue = menuBtn;
            dSo.ApplyModifiedPropertiesWithoutUndo();

            return controller;
        }

        private static GazeButton CreateDebriefButton(
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
            CreateText("Label", btnGo.transform, font, label, 22, Vector2.zero, size, Color.white, TextAnchor.MiddleCenter);
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
