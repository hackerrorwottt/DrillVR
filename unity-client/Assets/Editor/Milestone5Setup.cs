using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using DrillVR.AI;
using DrillVR.Core;
using DrillVR.Equipment;
using DrillVR.Hazards;
using DrillVR.Rescue;
using DrillVR.UI;

namespace DrillVR.EditorTools
{
    /// <summary>
    /// Automates Milestone 5: replaces temporary M4 casualty marker with 3 NavMeshAgent
    /// CasualtyAI NPCs, wires RescueSystem and EvacuationSystem, updates the HUD,
    /// and saves M5_CasualtyAI.unity.
    /// </summary>
    public static class Milestone5Setup
    {
        private const string ScenePath = "Assets/Scenes/M5_CasualtyAI.unity";

        [MenuItem("DrillVR/Setup Milestone 5 Casualty AI")]
        public static void RunSetup()
        {
            ScenarioConfig scenario = Milestone4Setup.CreateOrUpdateScriptableObjects(out List<EquipmentData> equipmentAssets);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            BuildMilestone5IntoCurrentScene(scenario, equipmentAssets, out _, out _, out _, out _, out _, out _, out _);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true)
            };

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "DrillVR Milestone 5 Setup Complete",
                "Built Assets/Scenes/M5_CasualtyAI.unity with 3 CasualtyAI NPCs, RescueSystem, " +
                "EvacuationSystem, and smoke-exposure / time-limit failure checks!\n\n" +
                "Test in Play Mode:\n" +
                "1. Complete Steps 1-3 (Briefing, Assess, Equipment).\n" +
                "2. In Step 4, watch Casualty_02_Conference switch from IDLE to PANIC! as smoke nears.\n" +
                "3. Gaze/tap all 3 casualties to switch them to FOLLOWING.\n" +
                "4. Lead them up the North Stairwell to Safe Assembly Zone -> SAFE state & Step 5 completion.",
                "OK"
            );
        }

        public static void BuildMilestone5IntoCurrentScene(
            ScenarioConfig scenario,
            List<EquipmentData> equipmentAssets,
            out GameObject buildingRoot,
            out GameObject playerRig,
            out MissionManager missionManager,
            out HazardSystem hazardSystem,
            out RescueSystem rescueSystem,
            out EvacuationSystem evacuationSystem,
            out DrillHudController drillHud)
        {
            Milestone4Setup.BuildMilestone4IntoCurrentScene(
                scenario,
                equipmentAssets,
                out buildingRoot,
                out playerRig,
                out missionManager,
                out hazardSystem,
                out _,
                out drillHud
            );

            // Remove temporary M4 casualty marker
            GameObject tempMarker = GameObject.Find("TempCasualtyMarker_M4");
            if (tempMarker != null)
            {
                Object.DestroyImmediate(tempMarker);
            }

            // Add RescueSystem to DrillSystems
            GameObject drillSystems = GameObject.Find("DrillSystems");
            rescueSystem = drillSystems.AddComponent<RescueSystem>();
            SerializedObject rSo = new SerializedObject(rescueSystem);
            rSo.FindProperty("missionManager").objectReferenceValue = missionManager;
            rSo.FindProperty("playerTransform").objectReferenceValue = playerRig.transform;
            rSo.ApplyModifiedPropertiesWithoutUndo();

            // Spawn 3 CasualtyAI NPCs on walkable NavMesh rooms
            GameObject casualtiesRoot = new GameObject("Casualties");
            Font uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            CreateCasualtyNpc("Casualty_01_Office", casualtiesRoot.transform, new Vector3(8.0f, 0f, 5.8f), missionManager, hazardSystem, rescueSystem, uiFont);
            CreateCasualtyNpc("Casualty_02_Conference", casualtiesRoot.transform, new Vector3(7.5f, 0f, -5.8f), missionManager, hazardSystem, rescueSystem, uiFont);
            CreateCasualtyNpc("Casualty_03_Utility", casualtiesRoot.transform, new Vector3(-7.5f, 0f, -5.8f), missionManager, hazardSystem, rescueSystem, uiFont);

            // Re-register spawned casualties on RescueSystem
            rescueSystem.RegisterSceneCasualties();

            // Attach EvacuationSystem onto SafeExitRouteZone
            GameObject safeZone = GameObject.Find("SafeExitRouteZone");
            evacuationSystem = safeZone.AddComponent<EvacuationSystem>();
            SerializedObject eSo = new SerializedObject(evacuationSystem);
            eSo.FindProperty("missionManager").objectReferenceValue = missionManager;
            eSo.FindProperty("playerTransform").objectReferenceValue = playerRig.transform;
            eSo.ApplyModifiedPropertiesWithoutUndo();

            // Wire SafeRouteZone to EvacuationSystem
            SafeRouteZone safeRoute = safeZone.GetComponent<SafeRouteZone>();
            if (safeRoute != null)
            {
                SerializedObject srSo = new SerializedObject(safeRoute);
                srSo.FindProperty("evacuationSystem").objectReferenceValue = evacuationSystem;
                srSo.ApplyModifiedPropertiesWithoutUndo();
            }

            // Add Casualty Status label onto VrHudCanvas and wire to DrillHudController
            Transform hudTransform = playerRig.transform.Find("Main Camera/VrHudCanvas");
            GameObject casStatusGo = new GameObject("CasualtyStatusText");
            casStatusGo.transform.SetParent(hudTransform, false);
            Text casText = casStatusGo.AddComponent<Text>();
            casText.font = uiFont;
            casText.text = "Found: 0/3 | Safe: 0/3";
            casText.fontSize = 15;
            casText.fontStyle = FontStyle.Bold;
            casText.color = new Color(0.85f, 0.95f, 1f);
            casText.alignment = TextAnchor.UpperLeft;
            casText.raycastTarget = false;
            RectTransform casRect = casStatusGo.GetComponent<RectTransform>();
            casRect.localPosition = new Vector2(-105f, 44f);
            casRect.sizeDelta = new Vector2(180f, 24f);

            SerializedObject hudSo = new SerializedObject(drillHud);
            hudSo.FindProperty("rescueSystem").objectReferenceValue = rescueSystem;
            hudSo.FindProperty("evacuationSystem").objectReferenceValue = evacuationSystem;
            hudSo.FindProperty("casualtyStatusText").objectReferenceValue = casText;
            hudSo.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateCasualtyNpc(
            string name,
            Transform parent,
            Vector3 floorPos,
            MissionManager missionManager,
            HazardSystem hazardSystem,
            RescueSystem rescueSystem,
            Font uiFont)
        {
            GameObject npc = new GameObject(name);
            npc.transform.SetParent(parent, false);
            npc.transform.position = floorPos;

            NavMeshAgent agent = npc.AddComponent<NavMeshAgent>();
            agent.radius = 0.28f;
            agent.height = 1.6f;
            agent.speed = 3.2f;
            agent.angularSpeed = 360f;
            agent.acceleration = 12f;
            agent.stoppingDistance = 1.8f;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;

            BoxCollider gazeBox = npc.AddComponent<BoxCollider>();
            gazeBox.isTrigger = true;
            gazeBox.size = new Vector3(0.85f, 1.8f, 0.85f);
            gazeBox.center = new Vector3(0f, 0.9f, 0f);

            // Visual capsule body
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(npc.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            body.transform.localScale = new Vector3(0.55f, 0.9f, 0.55f);
            Collider bodyCol = body.GetComponent<Collider>();
            if (bodyCol != null) Object.DestroyImmediate(bodyCol);
            Renderer bodyRenderer = body.GetComponent<Renderer>();

            // Floating State Badge above head
            GameObject badgeCanvasGo = new GameObject("StateBadge");
            badgeCanvasGo.transform.SetParent(npc.transform, false);
            badgeCanvasGo.transform.localPosition = new Vector3(0f, 2.05f, 0f);
            Canvas canvas = badgeCanvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            RectTransform cRect = badgeCanvasGo.GetComponent<RectTransform>();
            cRect.sizeDelta = new Vector2(260f, 60f);
            cRect.localScale = Vector3.one * 0.0045f;

            GameObject txtGo = new GameObject("Text");
            txtGo.transform.SetParent(badgeCanvasGo.transform, false);
            Text badgeText = txtGo.AddComponent<Text>();
            badgeText.font = uiFont;
            badgeText.text = "IDLE";
            badgeText.fontSize = 32;
            badgeText.fontStyle = FontStyle.Bold;
            badgeText.alignment = TextAnchor.MiddleCenter;
            badgeText.color = Color.cyan;
            badgeText.raycastTarget = false;
            RectTransform tRect = txtGo.GetComponent<RectTransform>();
            tRect.anchorMin = Vector2.zero;
            tRect.anchorMax = Vector2.one;
            tRect.offsetMin = Vector2.zero;
            tRect.offsetMax = Vector2.zero;

            CasualtyAI ai = npc.AddComponent<CasualtyAI>();
            SerializedObject aiSo = new SerializedObject(ai);
            aiSo.FindProperty("missionManager").objectReferenceValue = missionManager;
            aiSo.FindProperty("hazardSystem").objectReferenceValue = hazardSystem;
            aiSo.FindProperty("bodyRenderer").objectReferenceValue = bodyRenderer;
            aiSo.FindProperty("stateBadgeText").objectReferenceValue = badgeText;
            aiSo.FindProperty("badgeTransform").objectReferenceValue = badgeCanvasGo.transform;
            aiSo.ApplyModifiedPropertiesWithoutUndo();

            CasualtyInteractable interactable = npc.AddComponent<CasualtyInteractable>();
            interactable.Configure(rescueSystem);
        }
    }
}
