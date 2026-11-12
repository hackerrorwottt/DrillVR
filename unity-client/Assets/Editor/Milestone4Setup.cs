using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using DrillVR.Core;
using DrillVR.Environment;
using DrillVR.Equipment;
using DrillVR.Hazards;
using DrillVR.Interactions;
using DrillVR.UI;

namespace DrillVR.EditorTools
{
    /// <summary>
    /// Automates Milestone 4: creates ScenarioConfig, 7 StepData, and 5 EquipmentData
    /// ScriptableObject assets, and builds M4_CoreMechanics.unity with MissionManager,
    /// growing SmokeHazardZone, Equipment room interactables, safe/blocked route triggers,
    /// and the live Drill HUD.
    /// </summary>
    public static class Milestone4Setup
    {
        private const string ScenePath = "Assets/Scenes/M4_CoreMechanics.unity";
        private const string SoFolder = "Assets/ScriptableObjects";
        private const string StepsFolder = "Assets/ScriptableObjects/Steps";
        private const string EquipFolder = "Assets/ScriptableObjects/Equipment";
        private const string ScenarioAssetPath = "Assets/ScriptableObjects/Scenario_BuildingEmergency.asset";

        [MenuItem("DrillVR/Setup Milestone 4 Core Mechanics")]
        public static void RunSetup()
        {
            ScenarioConfig scenario = CreateOrUpdateScriptableObjects(out List<EquipmentData> equipmentAssets);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            BuildMilestone4IntoCurrentScene(scenario, equipmentAssets, out _, out _, out _, out _, out _, out _);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true)
            };

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "DrillVR Milestone 4 Setup Complete",
                "Generated ScriptableObjects in Assets/ScriptableObjects/ and built Assets/Scenes/M4_CoreMechanics.unity!\n\n" +
                "Test in Play Mode:\n" +
                "1. Select START DRILL.\n" +
                "2. Step 1: Gaze/click Briefing Board to confirm.\n" +
                "3. Step 2: Inspect Smoke Zone and Blocked East Exit.\n" +
                "4. Step 3: Teleport to Room 1 (Equipment) and pick Respirator, Flashlight, MedKit (wrong items log mistakes!).\n" +
                "5. Step 4: Click temporary Casualty Marker in Room 2 (replaced by AI NPCs in M5).\n" +
                "6. Step 5: Evacuate via North Stairwell to Safe Assembly Zone.\n" +
                "7. Step 6: Report at Command Post Radio.",
                "OK"
            );
        }

        public static void BuildMilestone4IntoCurrentScene(
            ScenarioConfig scenario,
            List<EquipmentData> equipmentAssets,
            out GameObject buildingRoot,
            out GameObject playerRig,
            out MissionManager missionManager,
            out HazardSystem hazardSystem,
            out EquipmentSelectionSystem equipSystem,
            out DrillHudController drillHud)
        {
            Milestone3Setup.BuildMilestone3IntoCurrentScene(out buildingRoot, out playerRig, out _, out UIManager uiManager);

            // Core Drill Systems container
            GameObject systemsRoot = new GameObject("DrillSystems");
            missionManager = systemsRoot.AddComponent<MissionManager>();
            missionManager.SetScenarioConfig(scenario);

            SerializedObject mmSo = new SerializedObject(missionManager);
            mmSo.FindProperty("uiManager").objectReferenceValue = uiManager;
            mmSo.ApplyModifiedPropertiesWithoutUndo();

            equipSystem = systemsRoot.AddComponent<EquipmentSelectionSystem>();
            SerializedObject eqSo = new SerializedObject(equipSystem);
            eqSo.FindProperty("missionManager").objectReferenceValue = missionManager;
            eqSo.ApplyModifiedPropertiesWithoutUndo();

            SituationAssessmentTracker assessmentTracker = systemsRoot.AddComponent<SituationAssessmentTracker>();
            SerializedObject asSo = new SerializedObject(assessmentTracker);
            asSo.FindProperty("missionManager").objectReferenceValue = missionManager;
            asSo.ApplyModifiedPropertiesWithoutUndo();

            // Growing Smoke Hazard Zone in East Corridor
            hazardSystem = CreateSmokeHazardZone(missionManager, equipSystem, playerRig.transform);

            // Lock East Main Exit & add Blocked Exit barricade + Safe/Unsafe Route triggers
            ConfigureExitsAndSafeRoute(buildingRoot.transform, missionManager, assessmentTracker, playerRig.transform);

            // Step 1 Briefing Confirm, Step 2 Smoke Inspect, Step 4 Temp Marker, Step 6 Command Radio
            CreateStepCheckpoints(buildingRoot.transform, missionManager, assessmentTracker);

            // Step 3 Equipment Props in Room 1
            CreateEquipmentProps(buildingRoot.transform, equipmentAssets, equipSystem);

            // Live Drill HUD on VrHudCanvas
            drillHud = AttachDrillHud(playerRig, missionManager, assessmentTracker, equipSystem, uiManager);
        }

        public static ScenarioConfig CreateOrUpdateScriptableObjects(out List<EquipmentData> equipmentAssets)
        {
            EnsureFolders();

            List<StepData> steps = new List<StepData>
            {
                CreateOrUpdateStep("Step_01_Briefing", 1, "briefing", "Briefing",
                    "Review emergency protocol and confirm the Briefing Board at the Command Post.",
                    "Look at the green Briefing Board beside you and select Confirm Briefing.",
                    "Confirm the briefing at the Command Post before entering the building.", 20f, 10f),

                CreateOrUpdateStep("Step_02_AssessSituation", 2, "assess_situation", "Assess Situation",
                    "Enter the corridor cautiously. Inspect the Smoke Zone and the Blocked Main Exit.",
                    "Look toward the East Corridor to inspect both the Smoke Warning and the Blocked Exit.",
                    "Assess both the smoke hazard and the blocked exit before picking equipment.", 35f, 15f),

                CreateOrUpdateStep("Step_03_EquipmentSelection", 3, "equipment_selection", "Equipment Selection",
                    "Enter Room 1 (Equipment) and collect the Respirator, Flashlight, and First Aid Kit.",
                    "Avoid unrelated items like the Laptop or Paper Box — pick only emergency response gear.",
                    "Select only required emergency gear (Respirator, Flashlight, First Aid Kit).", 45f, 20f),

                CreateOrUpdateStep("Step_04_SearchCasualties", 4, "search_casualties", "Search & Locate Casualties",
                    "Search the rooms to locate all casualties and guide them toward safety.",
                    "Check Room 2 (Office) and Room 4 (Conference) carefully while avoiding unprotected smoke.",
                    "Locate casualties before evacuating the building.", 60f, 20f),

                CreateOrUpdateStep("Step_05_EvacuateSafeRoute", 5, "evacuate_safe_route", "Evacuate via Safe Route",
                    "Main East Exit is blocked! Evacuate up the North Stairwell to the Safe Assembly Zone.",
                    "Head to Corridor Center -> Stairwell Base -> Stairwell Landing -> Safe Assembly Zone.",
                    "Do not use the blocked East Exit or enter dense smoke without a respirator.", 50f, 20f),

                CreateOrUpdateStep("Step_06_ReportCommandPost", 6, "report_command_post", "Report to Command Post",
                    "Return to the Command Post Radio outside the West Entrance to report completion.",
                    "Use the West Entrance teleport pads to return to the Command Post table and select the Radio.",
                    "Select the Command Post Radio only after evacuating via the Safe Assembly Zone.", 30f, 15f),

                CreateOrUpdateStep("Step_07_Debrief", 7, "debrief", "Debrief & Score",
                    "Review your performance summary and areas for improvement.",
                    "Check your step times, mistakes, and overall score.",
                    "", 15f, 0f)
            };

            equipmentAssets = new List<EquipmentData>
            {
                CreateOrUpdateEquipment("Equipment_Respirator", "respirator", "Respirator Mask",
                    "Protects against smoke inhalation.", true, true, ""),
                CreateOrUpdateEquipment("Equipment_Flashlight", "flashlight", "Emergency Flashlight",
                    "Illuminates smoke-filled corridors.", true, false, ""),
                CreateOrUpdateEquipment("Equipment_MedKit", "medkit", "First Aid Kit",
                    "Essential medical supplies for casualties.", true, false, ""),
                CreateOrUpdateEquipment("Equipment_Laptop", "laptop", "Office Laptop",
                    "Non-essential office electronics.", false, false, "Wrong item: Office Laptop is not emergency response gear."),
                CreateOrUpdateEquipment("Equipment_PaperBox", "paper_box", "Cardboard Paper Box",
                    "Flammable clutter.", false, false, "Wrong item: Cardboard Box slows evacuation and adds fire hazard.")
            };

            ScenarioConfig scenario = AssetDatabase.LoadAssetAtPath<ScenarioConfig>(ScenarioAssetPath);
            if (scenario == null)
            {
                scenario = ScriptableObject.CreateInstance<ScenarioConfig>();
                AssetDatabase.CreateAsset(scenario, ScenarioAssetPath);
            }

            scenario.scenarioCode = "building_emergency_v1";
            scenario.scenarioTitle = "Building Emergency Response Drill";
            scenario.briefingText =
                "Smoke has been reported in the East Corridor. Assess the hazard, equip protective gear, " +
                "locate casualties, evacuate via the safest route, and report back to the Command Post.";
            scenario.disclaimerText =
                "Educational prototype only. Simulation scores must not be presented as proof of real-world qualification.";
            scenario.steps = steps;
            scenario.availableEquipment = equipmentAssets;
            scenario.baseSmokeGrowthRate = 0.25f;
            scenario.difficultyMultiplier = 1.0f;
            scenario.timeLimitSeconds = 300f;
            scenario.maxSmokeRadius = 11f;
            scenario.stepsWeight = 40f;
            scenario.evacuationWeight = 40f;
            scenario.timeBonusWeight = 20f;
            scenario.mistakePenaltyPoints = 5f;
            EditorUtility.SetDirty(scenario);

            return scenario;
        }

        private static StepData CreateOrUpdateStep(
            string fileName,
            int number,
            string key,
            string title,
            string instruction,
            string defaultHint,
            string mistakeHint,
            float expectedTime,
            float weight)
        {
            string path = $"{StepsFolder}/{fileName}.asset";
            StepData step = AssetDatabase.LoadAssetAtPath<StepData>(path);
            if (step == null)
            {
                step = ScriptableObject.CreateInstance<StepData>();
                AssetDatabase.CreateAsset(step, path);
            }

            step.stepNumber = number;
            step.stepKey = key;
            step.title = title;
            step.instruction = instruction;
            step.defaultHint = defaultHint;
            step.mistakeHint = mistakeHint;
            step.expectedTimeSeconds = expectedTime;
            step.weightPercent = weight;
            EditorUtility.SetDirty(step);
            return step;
        }

        private static EquipmentData CreateOrUpdateEquipment(
            string fileName,
            string id,
            string displayName,
            string description,
            bool required,
            bool smokeProtection,
            string wrongReason)
        {
            string path = $"{EquipFolder}/{fileName}.asset";
            EquipmentData eq = AssetDatabase.LoadAssetAtPath<EquipmentData>(path);
            if (eq == null)
            {
                eq = ScriptableObject.CreateInstance<EquipmentData>();
                AssetDatabase.CreateAsset(eq, path);
            }

            eq.equipmentId = id;
            eq.displayName = displayName;
            eq.description = description;
            eq.isRequiredForScenario = required;
            eq.grantsSmokeProtection = smokeProtection;
            eq.wrongChoiceReason = wrongReason;
            EditorUtility.SetDirty(eq);
            return eq;
        }

        private static void EnsureFolders()
        {
            if (!Directory.Exists(SoFolder)) Directory.CreateDirectory(SoFolder);
            if (!Directory.Exists(StepsFolder)) Directory.CreateDirectory(StepsFolder);
            if (!Directory.Exists(EquipFolder)) Directory.CreateDirectory(EquipFolder);
            AssetDatabase.Refresh();
        }

        private static HazardSystem CreateSmokeHazardZone(
            MissionManager missionManager,
            EquipmentSelectionSystem equipSystem,
            Transform playerTransform)
        {
            GameObject smokeGo = new GameObject("SmokeHazardZone");
            smokeGo.layer = 2; // Ignore Raycast
            smokeGo.transform.position = new Vector3(9.0f, 1.2f, 0f);

            SphereCollider col = smokeGo.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 1.6f;

            // Low-poly particle system tuned for mobile 30+ FPS (maxParticles = 60)
            ParticleSystem ps = smokeGo.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startColor = new Color(0.28f, 0.28f, 0.30f, 0.55f);
            main.startSize = 1.8f;
            main.startLifetime = 3.2f;
            main.startSpeed = 0.25f;
            main.maxParticles = 60;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 16f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 1.4f;

            ParticleSystemRenderer psRenderer = smokeGo.GetComponent<ParticleSystemRenderer>();
            Material defaultParticle = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Particle.mat");
            if (defaultParticle != null)
            {
                psRenderer.sharedMaterial = defaultParticle;
            }

            HazardSystem hazard = smokeGo.AddComponent<HazardSystem>();
            SerializedObject hSo = new SerializedObject(hazard);
            hSo.FindProperty("missionManager").objectReferenceValue = missionManager;
            hSo.FindProperty("equipmentSystem").objectReferenceValue = equipSystem;
            hSo.FindProperty("playerTransform").objectReferenceValue = playerTransform;
            hSo.FindProperty("smokeParticles").objectReferenceValue = ps;
            hSo.ApplyModifiedPropertiesWithoutUndo();

            return hazard;
        }

        private static void ConfigureExitsAndSafeRoute(
            Transform buildingRoot,
            MissionManager missionManager,
            SituationAssessmentTracker assessmentTracker,
            Transform playerTransform)
        {
            // Lock East Main Exit door
            Transform eastDoorTr = buildingRoot.Find("Doors/Door_MainExit_East");
            if (eastDoorTr != null)
            {
                DoorController doorCtrl = eastDoorTr.GetComponent<DoorController>();
                if (doorCtrl != null)
                {
                    SerializedObject dSo = new SerializedObject(doorCtrl);
                    dSo.FindProperty("startOpen").boolValue = false;
                    dSo.FindProperty("isLocked").boolValue = true;
                    dSo.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            // Visual barricade + Blocked Exit Interactable at East Exit
            GameObject barricade = GameObject.CreatePrimitive(PrimitiveType.Cube);
            barricade.name = "BlockedExit_Barricade";
            barricade.transform.position = new Vector3(11.35f, 0.9f, 0f);
            barricade.transform.localScale = new Vector3(0.45f, 1.4f, 2.0f);
            Material redMat = CreateOrLoadMaterial("Assets/Materials/M4_BlockedExit.mat", new Color(0.8f, 0.16f, 0.14f));
            barricade.GetComponent<Renderer>().sharedMaterial = redMat;

            DrillStepTriggerInteractable blockedTrigger = barricade.AddComponent<DrillStepTriggerInteractable>();
            blockedTrigger.SetPromptText("Inspect Blocked Main Exit");
            ConfigureStepTrigger(blockedTrigger, DrillStepTriggerInteractable.TriggerAction.BlockedHazardMistake,
                "assess_situation", "blocked_exit", "Main East Exit is blocked by smoke! Evacuate via North Stairwell.", false,
                missionManager, assessmentTracker);

            // Unsafe route trigger zone at East Exit
            GameObject unsafeZone = new GameObject("BlockedExitRouteZone");
            unsafeZone.transform.position = new Vector3(11.0f, 1.0f, 0f);
            BoxCollider unsafeCol = unsafeZone.AddComponent<BoxCollider>();
            unsafeCol.size = new Vector3(2.4f, 2.5f, 3.2f);
            SafeRouteZone unsafeRoute = unsafeZone.AddComponent<SafeRouteZone>();
            ConfigureSafeRouteZone(unsafeRoute, false, "evacuate_safe_route", missionManager, playerTransform);

            // Safe alternative route trigger zone at Safe Assembly Pad (North of Stairwell)
            GameObject safeZone = new GameObject("SafeExitRouteZone");
            safeZone.transform.position = new Vector3(0f, 1.5f, 12.2f);
            BoxCollider safeCol = safeZone.AddComponent<BoxCollider>();
            safeCol.size = new Vector3(5.5f, 3.0f, 4.0f);
            SafeRouteZone safeRoute = safeZone.AddComponent<SafeRouteZone>();
            ConfigureSafeRouteZone(safeRoute, true, "evacuate_safe_route", missionManager, playerTransform);
        }

        private static void CreateStepCheckpoints(
            Transform buildingRoot,
            MissionManager missionManager,
            SituationAssessmentTracker assessmentTracker)
        {
            // Step 1: Briefing Confirm Board at Command Post
            GameObject briefingBoard = GameObject.CreatePrimitive(PrimitiveType.Cube);
            briefingBoard.name = "BriefingConfirm_Beacon";
            briefingBoard.transform.position = new Vector3(-14.5f, 1.35f, -2.1f);
            briefingBoard.transform.localScale = new Vector3(0.6f, 0.6f, 0.25f);
            Material cyanMat = CreateOrLoadMaterial("Assets/Materials/M4_CheckpointCyan.mat", new Color(0.15f, 0.68f, 0.92f));
            briefingBoard.GetComponent<Renderer>().sharedMaterial = cyanMat;

            DrillStepTriggerInteractable briefTrig = briefingBoard.AddComponent<DrillStepTriggerInteractable>();
            briefTrig.SetPromptText("Confirm Briefing");
            ConfigureStepTrigger(briefTrig, DrillStepTriggerInteractable.TriggerAction.CompleteStep,
                "briefing", "", "", false, missionManager, assessmentTracker);

            // Step 2: Smoke Hazard Inspection Beacon in Corridor
            GameObject smokeBeacon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            smokeBeacon.name = "Inspect_SmokeHazard";
            smokeBeacon.transform.position = new Vector3(4.5f, 1.4f, 0.9f);
            smokeBeacon.transform.localScale = Vector3.one * 0.55f;
            Material amberMat = CreateOrLoadMaterial("Assets/Materials/M4_InspectAmber.mat", new Color(0.95f, 0.65f, 0.12f));
            smokeBeacon.GetComponent<Renderer>().sharedMaterial = amberMat;

            DrillStepTriggerInteractable smokeTrig = smokeBeacon.AddComponent<DrillStepTriggerInteractable>();
            smokeTrig.SetPromptText("Assess Smoke Hazard");
            ConfigureStepTrigger(smokeTrig, DrillStepTriggerInteractable.TriggerAction.LogAssessmentObservation,
                "assess_situation", "smoke_hazard", "", false, missionManager, assessmentTracker);

            // Step 4: Temporary Casualty Marker in Room 2 (replaced by CasualtyAI in Milestone 5)
            GameObject tempCasualty = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            tempCasualty.name = "TempCasualtyMarker_M4";
            tempCasualty.transform.position = new Vector3(8.0f, 1.0f, 6.2f);
            tempCasualty.GetComponent<Renderer>().sharedMaterial = cyanMat;

            DrillStepTriggerInteractable casTrig = tempCasualty.AddComponent<DrillStepTriggerInteractable>();
            casTrig.SetPromptText("Locate Casualty (M4 Temp)");
            ConfigureStepTrigger(casTrig, DrillStepTriggerInteractable.TriggerAction.CompleteStep,
                "search_casualties", "", "", true, missionManager, assessmentTracker);

            // Step 6: Command Post Radio on CommandTable
            Transform radioTr = buildingRoot.Find("Props/CommandRadio");
            GameObject radioGo = radioTr != null ? radioTr.gameObject : GameObject.CreatePrimitive(PrimitiveType.Cube);
            if (radioTr == null)
            {
                radioGo.name = "CommandRadio";
                radioGo.transform.position = new Vector3(-14.5f, 0.95f, 2.5f);
                radioGo.transform.localScale = new Vector3(0.4f, 0.3f, 0.3f);
            }

            DrillStepTriggerInteractable radioTrig = radioGo.AddComponent<DrillStepTriggerInteractable>();
            radioTrig.SetPromptText("Report to Command Post");
            ConfigureStepTrigger(radioTrig, DrillStepTriggerInteractable.TriggerAction.CompleteStep,
                "report_command_post", "", "", false, missionManager, assessmentTracker);
        }

        private static void CreateEquipmentProps(
            Transform buildingRoot,
            List<EquipmentData> equipmentAssets,
            EquipmentSelectionSystem equipSystem)
        {
            // Remove static decorative M2 props on EquipTable so live interactive props replace them cleanly
            string[] oldProps = { "Props/Prop_MedKit", "Props/Prop_Respirator", "Props/Prop_Flashlight" };
            foreach (string relPath in oldProps)
            {
                Transform found = buildingRoot.Find(relPath);
                if (found != null)
                {
                    Object.DestroyImmediate(found.gameObject);
                }
            }

            GameObject equipRoot = new GameObject("EquipmentInteractables");
            Material reqMat = CreateOrLoadMaterial("Assets/Materials/M4_EquipRequired.mat", new Color(0.20f, 0.75f, 0.92f));
            Material wrongMat = CreateOrLoadMaterial("Assets/Materials/M4_EquipDistractor.mat", new Color(0.75f, 0.45f, 0.25f));

            for (int i = 0; i < equipmentAssets.Count; i++)
            {
                EquipmentData data = equipmentAssets[i];
                GameObject prop = GameObject.CreatePrimitive(PrimitiveType.Cube);
                prop.name = $"Equip_{data.equipmentId}";
                prop.transform.SetParent(equipRoot.transform, false);
                prop.transform.position = new Vector3(-9.3f + i * 0.65f, 0.98f, 7.5f);
                prop.transform.localScale = new Vector3(0.38f, 0.28f, 0.38f);
                prop.GetComponent<Renderer>().sharedMaterial = data.isRequiredForScenario ? reqMat : wrongMat;

                EquipmentInteractable ei = prop.AddComponent<EquipmentInteractable>();
                ei.Configure(data, equipSystem);
            }
        }

        private static DrillHudController AttachDrillHud(
            GameObject playerRig,
            MissionManager missionManager,
            SituationAssessmentTracker assessmentTracker,
            EquipmentSelectionSystem equipSystem,
            UIManager uiManager)
        {
            Transform hudTransform = playerRig.transform.Find("Main Camera/VrHudCanvas");
            Font uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            Text stepText = CreateHudLabel("StepTitleText", hudTransform, uiFont, "Step: Waiting to Start", 16,
                new Vector2(0f, 112f), new Vector2(390f, 26f), Color.cyan, TextAnchor.MiddleCenter);
            Text mistakeText = CreateHudLabel("MistakeCountText", hudTransform, uiFont, "Mistakes: 0", 16,
                new Vector2(-135f, 70f), new Vector2(120f, 26f), Color.white, TextAnchor.UpperLeft);
            Text alertText = CreateHudLabel("AlertBannerText", hudTransform, uiFont, "", 16,
                new Vector2(0f, -85f), new Vector2(390f, 44f), Color.yellow, TextAnchor.MiddleCenter);
            alertText.gameObject.SetActive(false);

            DrillHudController hud = hudTransform.gameObject.AddComponent<DrillHudController>();
            SerializedObject hSo = new SerializedObject(hud);
            hSo.FindProperty("missionManager").objectReferenceValue = missionManager;
            hSo.FindProperty("assessmentTracker").objectReferenceValue = assessmentTracker;
            hSo.FindProperty("equipmentSystem").objectReferenceValue = equipSystem;
            hSo.FindProperty("uiManager").objectReferenceValue = uiManager;
            hSo.FindProperty("stepTitleText").objectReferenceValue = stepText;
            hSo.FindProperty("mistakeCountText").objectReferenceValue = mistakeText;
            hSo.FindProperty("alertBannerText").objectReferenceValue = alertText;
            hSo.ApplyModifiedPropertiesWithoutUndo();

            return hud;
        }

        private static Text CreateHudLabel(
            string name,
            Transform parent,
            Font font,
            string text,
            int fontSize,
            Vector2 pos,
            Vector2 size,
            Color color,
            TextAnchor align)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Text txt = go.AddComponent<Text>();
            txt.font = font;
            txt.text = text;
            txt.fontSize = fontSize;
            txt.fontStyle = FontStyle.Bold;
            txt.color = color;
            txt.alignment = align;
            txt.raycastTarget = false;

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.localPosition = pos;
            rect.sizeDelta = size;
            return txt;
        }

        private static Material CreateOrLoadMaterial(string path, Color color)
        {
            if (!Directory.Exists("Assets/Materials"))
            {
                Directory.CreateDirectory("Assets/Materials");
            }

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Standard"));
                mat.color = color;
                mat.SetFloat("_Glossiness", 0.2f);
                AssetDatabase.CreateAsset(mat, path);
            }
            return mat;
        }

        private static void ConfigureStepTrigger(
            DrillStepTriggerInteractable trig,
            DrillStepTriggerInteractable.TriggerAction action,
            string stepKey,
            string obsId,
            string mistakeMsg,
            bool hideOnSuccess,
            MissionManager mm,
            SituationAssessmentTracker tracker)
        {
            SerializedObject so = new SerializedObject(trig);
            so.FindProperty("actionType").enumValueIndex = (int)action;
            so.FindProperty("targetStepKey").stringValue = stepKey;
            so.FindProperty("observationId").stringValue = obsId;
            so.FindProperty("blockedMistakeMessage").stringValue = mistakeMsg;
            so.FindProperty("hideAfterSuccess").boolValue = hideOnSuccess;
            so.FindProperty("missionManager").objectReferenceValue = mm;
            so.FindProperty("assessmentTracker").objectReferenceValue = tracker;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureSafeRouteZone(
            SafeRouteZone zone,
            bool isSafe,
            string stepKey,
            MissionManager mm,
            Transform player)
        {
            SerializedObject so = new SerializedObject(zone);
            so.FindProperty("isSafeAlternativeRoute").boolValue = isSafe;
            so.FindProperty("evacuationStepKey").stringValue = stepKey;
            so.FindProperty("missionManager").objectReferenceValue = mm;
            so.FindProperty("playerTransform").objectReferenceValue = player;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
