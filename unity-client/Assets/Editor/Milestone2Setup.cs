using System.Collections.Generic;
using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SpatialTracking;
using UnityEngine.UI;
using DrillVR.Core;
using DrillVR.Environment;
using DrillVR.Player;
using DrillVR.UI;

namespace DrillVR.EditorTools
{
    /// <summary>
    /// Automates Milestone 2: builds the single-floor emergency drill building
    /// (Command Post, Corridor, 4 Rooms, Stairwell, Exits, Props, Signs, Baked Lighting,
    /// Ambient Audio, NavMeshSurface) and saves M2_BuildingEnvironment.unity.
    /// </summary>
    public static class Milestone2Setup
    {
        private const string ScenePath = "Assets/Scenes/M2_BuildingEnvironment.unity";
        private const string MaterialsFolder = "Assets/Materials/M2";

        private static readonly StaticEditorFlags ArchStaticFlags =
            StaticEditorFlags.ContributeGI |
            StaticEditorFlags.BatchingStatic |
            StaticEditorFlags.OccluderStatic |
            StaticEditorFlags.OccludeeStatic;

        private static Font _uiFont;
        private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();

        [MenuItem("DrillVR/Setup Milestone 2 Environment")]
        public static void RunSetup()
        {
            EnsureMaterialsFolder();
            LoadOrCreateMaterials();
            _uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject buildingRoot = new GameObject("BuildingEnvironment");
            BuildFloors(buildingRoot.transform);
            BuildPerimeterAndCorridorWalls(buildingRoot.transform);
            BuildInteriorDividers(buildingRoot.transform);
            BuildCeiling(buildingRoot.transform);
            BuildStairwell(buildingRoot.transform);
            BuildDoors(buildingRoot.transform);
            BuildSigns(buildingRoot.transform);
            BuildProps(buildingRoot.transform);
            BuildBakedLighting(buildingRoot.transform);

            // Bake NavMesh over all building colliders
            NavMeshSurface navSurface = buildingRoot.AddComponent<NavMeshSurface>();
            navSurface.collectObjects = CollectObjects.Children;
            navSurface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            navSurface.BuildNavMesh();

            BuildPlayerRig();
            BuildBootstrapAndAudio();

            Lightmapping.giWorkflowMode = Lightmapping.GIWorkflowMode.OnDemand;

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true)
            };

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "DrillVR Milestone 2 Setup Complete",
                "Built Assets/Scenes/M2_BuildingEnvironment.unity and baked the NavMesh!\n\n" +
                "Next steps:\n" +
                "1. Click DrillVR > Bake Milestone 2 Lighting (or Window > Rendering > Lighting > Generate Lighting).\n" +
                "2. Select BuildingEnvironment to inspect the blue NavMesh overlay.\n" +
                "3. Press Play: walk with WASD + Right-Mouse look, or Build And Run to check 30+ FPS on phone.",
                "OK"
            );
        }

        [MenuItem("DrillVR/Bake Milestone 2 Lighting")]
        public static void BakeLighting()
        {
            Lightmapping.giWorkflowMode = Lightmapping.GIWorkflowMode.OnDemand;
            if (!Lightmapping.BakeAsync())
            {
                Debug.LogWarning("Lightmapping.BakeAsync() did not start (a bake may already be running).");
            }
            else
            {
                Debug.Log("Milestone 2 async lightmap bake started. Watch the bottom-right progress bar.");
            }
        }

        private static void EnsureMaterialsFolder()
        {
            if (!Directory.Exists("Assets/Materials"))
            {
                Directory.CreateDirectory("Assets/Materials");
            }
            if (!Directory.Exists(MaterialsFolder))
            {
                Directory.CreateDirectory(MaterialsFolder);
            }
            AssetDatabase.Refresh();
        }

        private static void LoadOrCreateMaterials()
        {
            Materials.Clear();
            CreateOrLoadMat("Floor", new Color(0.72f, 0.72f, 0.70f));
            CreateOrLoadMat("ExteriorPad", new Color(0.42f, 0.45f, 0.48f));
            CreateOrLoadMat("Wall", new Color(0.88f, 0.87f, 0.83f));
            CreateOrLoadMat("Ceiling", new Color(0.78f, 0.78f, 0.78f));
            CreateOrLoadMat("DoorWood", new Color(0.55f, 0.36f, 0.22f));
            CreateOrLoadMat("DoorExit", new Color(0.78f, 0.18f, 0.16f));
            CreateOrLoadMat("DoorSafeExit", new Color(0.16f, 0.62f, 0.28f));
            CreateOrLoadMat("PropMetal", new Color(0.28f, 0.32f, 0.36f));
            CreateOrLoadMat("PropWood", new Color(0.62f, 0.44f, 0.26f));
            CreateOrLoadMat("ExtinguisherRed", new Color(0.85f, 0.12f, 0.12f));
            CreateOrLoadMat("MedicalWhite", new Color(0.95f, 0.95f, 0.95f));
            CreateOrLoadMat("StairStep", new Color(0.58f, 0.60f, 0.62f));
            CreateOrLoadMat("SignPanel", new Color(0.10f, 0.14f, 0.18f));
            CreateOrLoadMat("SignSafeGreen", new Color(0.08f, 0.45f, 0.18f));
            CreateOrLoadMat("SignHazardRed", new Color(0.62f, 0.12f, 0.12f));
        }

        private static void CreateOrLoadMat(string key, Color color)
        {
            string path = $"{MaterialsFolder}/{key}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Standard"));
                mat.color = color;
                mat.SetFloat("_Glossiness", 0.15f);
                AssetDatabase.CreateAsset(mat, path);
            }
            Materials[key] = mat;
        }

        private static void BuildFloors(Transform parent)
        {
            GameObject floors = new GameObject("Floors");
            floors.transform.SetParent(parent, false);

            // Command Post exterior pad (West: X [-19, -12], Z [-5, 5])
            CreateBox("CommandPostPad", floors.transform, new Vector3(-15.5f, -0.1f, 0f), new Vector3(7f, 0.2f, 10f), "ExteriorPad", true);
            // Main Building Floor (X [-12, 12], Z [-10, 10])
            CreateBox("MainFloor", floors.transform, new Vector3(0f, -0.1f, 0f), new Vector3(24f, 0.2f, 20f), "Floor", true);
            // East Main Exit exterior landing (X [12, 16], Z [-3, 3])
            CreateBox("EastExitPad", floors.transform, new Vector3(14f, -0.1f, 0f), new Vector3(4f, 0.2f, 6f), "ExteriorPad", true);
            // North Safe Assembly pad at top of stairwell landing (Y top = 0.60)
            CreateBox("SafeAssemblyPad", floors.transform, new Vector3(0f, 0.5f, 12.5f), new Vector3(8f, 0.2f, 5f), "ExteriorPad", true);
        }

        private static void BuildPerimeterAndCorridorWalls(Transform parent)
        {
            GameObject walls = new GameObject("Walls");
            walls.transform.SetParent(parent, false);

            // West exterior wall (X = -12) with 2.4m entrance at Z = 0
            CreateWallSegment("Wall_West_S", walls.transform, -12f, -5.6f, 0.25f, 8.8f);
            CreateWallSegment("Wall_West_N", walls.transform, -12f, 5.6f, 0.25f, 8.8f);
            CreateDoorHeader("Header_WestEntrance", walls.transform, -12f, 0f, 0.25f, 2.4f);

            // East exterior wall (X = +12) with 2.4m Main Exit at Z = 0
            CreateWallSegment("Wall_East_S", walls.transform, 12f, -5.6f, 0.25f, 8.8f);
            CreateWallSegment("Wall_East_N", walls.transform, 12f, 5.6f, 0.25f, 8.8f);
            CreateDoorHeader("Header_EastExit", walls.transform, 12f, 0f, 0.25f, 2.4f);

            // South perimeter wall (Z = -10, solid X [-12, 12])
            CreateWallSegment("Wall_SouthPerimeter", walls.transform, 0f, -10f, 24f, 0.25f);

            // North perimeter wall (Z = +10) with 2.2m Safe Exit opening at X = 0
            CreateWallSegment("Wall_North_W", walls.transform, -6.55f, 10f, 10.9f, 0.25f);
            CreateWallSegment("Wall_North_E", walls.transform, 6.55f, 10f, 10.9f, 0.25f);
            CreateDoorHeader("Header_SafeExit", walls.transform, 0f, 10f, 2.2f, 0.25f, 0.6f);

            // North Corridor Wall (Z = +2): doorways at X = -8 (Equip), X = 0 (Stairwell), X = +8 (Office)
            CreateWallSegment("Wall_CorridorN_1", walls.transform, -10.55f, 2f, 2.9f, 0.25f);
            CreateWallSegment("Wall_CorridorN_2", walls.transform, -4.0f, 2f, 5.8f, 0.25f);
            CreateWallSegment("Wall_CorridorN_3", walls.transform, 4.0f, 2f, 5.8f, 0.25f);
            CreateWallSegment("Wall_CorridorN_4", walls.transform, 10.55f, 2f, 2.9f, 0.25f);
            CreateDoorHeader("Header_Room1_Equip", walls.transform, -8f, 2f, 2.2f, 0.25f);
            CreateDoorHeader("Header_Stairwell", walls.transform, 0f, 2f, 2.2f, 0.25f);
            CreateDoorHeader("Header_Room2_Office", walls.transform, 8f, 2f, 2.2f, 0.25f);

            // South Corridor Wall (Z = -2): doorways at X = -8 (Utility), X = +8 (Conference)
            CreateWallSegment("Wall_CorridorS_1", walls.transform, -10.55f, -2f, 2.9f, 0.25f);
            CreateWallSegment("Wall_CorridorS_2", walls.transform, 0f, -2f, 13.8f, 0.25f);
            CreateWallSegment("Wall_CorridorS_3", walls.transform, 10.55f, -2f, 2.9f, 0.25f);
            CreateDoorHeader("Header_Room3_Utility", walls.transform, -8f, -2f, 2.2f, 0.25f);
            CreateDoorHeader("Header_Room4_Conf", walls.transform, 8f, -2f, 2.2f, 0.25f);
        }

        private static void BuildInteriorDividers(Transform parent)
        {
            GameObject dividers = new GameObject("Dividers");
            dividers.transform.SetParent(parent, false);

            // North rooms divided around Stairwell (X = -4 and X = +4, Z [2, 10])
            CreateWallSegment("Divider_Equip_Stairwell", dividers.transform, -4f, 6f, 0.25f, 8f);
            CreateWallSegment("Divider_Stairwell_Office", dividers.transform, 4f, 6f, 0.25f, 8f);

            // South rooms divided at X = 0 (Z [-10, -2])
            CreateWallSegment("Divider_Utility_Conf", dividers.transform, 0f, -6f, 0.25f, 8f);
        }

        private static void BuildCeiling(Transform parent)
        {
            // Slightly elevated over stairwell/roof so interior baked lighting stays enclosed
            CreateBox("BuildingCeiling", parent, new Vector3(0f, 3.3f, 0f), new Vector3(24.25f, 0.2f, 20.25f), "Ceiling", true);
        }

        private static void BuildStairwell(Transform parent)
        {
            GameObject stairwell = new GameObject("Stairwell");
            stairwell.transform.SetParent(parent, false);

            // 4 low-poly steps rising 0.15m each (total 0.60m), easy for CharacterController and NavMeshAgent
            for (int i = 0; i < 4; i++)
            {
                float stepTop = (i + 1) * 0.15f;
                float zCenter = 4.2f + i * 0.65f;
                CreateBox(
                    $"StairStep_{i + 1}",
                    stairwell.transform,
                    new Vector3(0f, stepTop * 0.5f, zCenter),
                    new Vector3(7.7f, stepTop, 0.65f),
                    "StairStep",
                    true
                );
            }

            // Upper stairwell landing connecting top step (Z ~ 6.475) to Safe Exit (Z = 10)
            CreateBox(
                "StairwellUpperLanding",
                stairwell.transform,
                new Vector3(0f, 0.3f, 8.25f),
                new Vector3(7.7f, 0.6f, 3.55f),
                "StairStep",
                true
            );
        }

        private static void BuildDoors(Transform parent)
        {
            GameObject doorsRoot = new GameObject("Doors");
            doorsRoot.transform.SetParent(parent, false);

            // Interior doors start open (85 deg) so NavMesh connects seamlessly into every room
            CreateHingedDoor("Door_WestEntrance", doorsRoot.transform, new Vector3(-12f, 0f, -1.1f), 2.2f, 0f, true, false, "DoorWood");
            CreateHingedDoor("Door_EquipmentRoom", doorsRoot.transform, new Vector3(-9.05f, 0f, 2f), 2.0f, 90f, true, false, "DoorWood");
            CreateHingedDoor("Door_OfficeRoom", doorsRoot.transform, new Vector3(6.95f, 0f, 2f), 2.0f, 90f, true, false, "DoorWood");
            CreateHingedDoor("Door_UtilityRoom", doorsRoot.transform, new Vector3(-9.05f, 0f, -2f), 2.0f, -90f, true, false, "DoorWood");
            CreateHingedDoor("Door_ConferenceRoom", doorsRoot.transform, new Vector3(6.95f, 0f, -2f), 2.0f, -90f, true, false, "DoorWood");

            // Exits
            CreateHingedDoor("Door_MainExit_East", doorsRoot.transform, new Vector3(12f, 0f, -1.1f), 2.2f, 0f, false, false, "DoorExit");
            CreateHingedDoor("Door_SafeExit_Stairwell", doorsRoot.transform, new Vector3(-1.05f, 0.6f, 10f), 2.0f, 90f, true, false, "DoorSafeExit");
        }

        private static void CreateHingedDoor(
            string name,
            Transform parent,
            Vector3 hingePos,
            float width,
            float baseYaw,
            bool startOpen,
            bool isLocked,
            string matKey)
        {
            GameObject pivot = new GameObject(name);
            pivot.transform.SetParent(parent, false);
            pivot.transform.position = hingePos;
            float initialYaw = startOpen && !isLocked ? baseYaw + 85f : baseYaw;
            pivot.transform.rotation = Quaternion.Euler(0f, initialYaw, 0f);

            GameObject leaf = CreateBox("Leaf", pivot.transform, Vector3.zero, new Vector3(0.08f, 2.35f, width), matKey, false);
            leaf.transform.localPosition = new Vector3(0f, 1.175f, width * 0.5f);

            DoorController controller = pivot.AddComponent<DoorController>();
            SerializedObject so = new SerializedObject(controller);
            so.FindProperty("doorPivot").objectReferenceValue = pivot.transform;
            so.FindProperty("closedAngleY").floatValue = baseYaw;
            so.FindProperty("openAngleY").floatValue = baseYaw + 85f;
            so.FindProperty("startOpen").boolValue = startOpen;
            so.FindProperty("isLocked").boolValue = isLocked;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildSigns(Transform parent)
        {
            GameObject signs = new GameObject("Signs");
            signs.transform.SetParent(parent, false);

            CreateWorldSign("Sign_CommandPost", signs.transform, new Vector3(-14.5f, 1.85f, -2.5f), new Vector3(0f, 135f, 0f), "COMMAND POST\nBRIEFING & REPORT", "SignSafeGreen", 2.2f, 0.7f);
            CreateWorldSign("Sign_MainEntrance", signs.transform, new Vector3(-12.2f, 2.75f, 0f), new Vector3(0f, 90f, 0f), "BUILDING ENTRANCE", "SignPanel", 2.2f, 0.5f);
            CreateWorldSign("Sign_Equipment", signs.transform, new Vector3(-8f, 2.75f, 1.82f), Vector3.zero, "ROOM 1: EQUIPMENT", "SignPanel", 2.0f, 0.45f);
            CreateWorldSign("Sign_Office", signs.transform, new Vector3(8f, 2.75f, 1.82f), Vector3.zero, "ROOM 2: OFFICE", "SignPanel", 2.0f, 0.45f);
            CreateWorldSign("Sign_Utility", signs.transform, new Vector3(-8f, 2.75f, -1.82f), new Vector3(0f, 180f, 0f), "ROOM 3: UTILITY", "SignPanel", 2.0f, 0.45f);
            CreateWorldSign("Sign_Conference", signs.transform, new Vector3(8f, 2.75f, -1.82f), new Vector3(0f, 180f, 0f), "ROOM 4: CONFERENCE", "SignPanel", 2.0f, 0.45f);
            CreateWorldSign("Sign_Stairwell", signs.transform, new Vector3(0f, 2.75f, 1.82f), Vector3.zero, "STAIRWELL -> SAFE EXIT", "SignSafeGreen", 2.2f, 0.5f);
            CreateWorldSign("Sign_SafeExit", signs.transform, new Vector3(0f, 2.85f, 9.82f), Vector3.zero, "SAFE EXIT", "SignSafeGreen", 1.8f, 0.5f);
            CreateWorldSign("Sign_MainExit", signs.transform, new Vector3(11.82f, 2.75f, 0f), new Vector3(0f, 90f, 0f), "EXIT (MAIN)", "SignHazardRed", 1.8f, 0.5f);
        }

        private static void CreateWorldSign(
            string name,
            Transform parent,
            Vector3 pos,
            Vector3 euler,
            string labelText,
            string bgMatKey,
            float width,
            float height)
        {
            GameObject signRoot = new GameObject(name);
            signRoot.transform.SetParent(parent, false);
            signRoot.transform.position = pos;
            signRoot.transform.rotation = Quaternion.Euler(euler);

            CreateBox("Board", signRoot.transform, Vector3.zero, new Vector3(width, height, 0.06f), bgMatKey, true);

            GameObject canvasGo = new GameObject("LabelCanvas");
            canvasGo.transform.SetParent(signRoot.transform, false);
            canvasGo.transform.localPosition = new Vector3(0f, 0f, -0.04f);

            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            RectTransform canvasRect = canvasGo.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(width * 200f, height * 200f);
            canvasRect.localScale = Vector3.one * 0.005f;

            GameObject textGo = new GameObject("Text");
            textGo.transform.SetParent(canvasGo.transform, false);
            Text txt = textGo.AddComponent<Text>();
            txt.font = _uiFont;
            txt.text = labelText;
            txt.fontSize = 32;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            txt.raycastTarget = false;

            RectTransform txtRect = textGo.GetComponent<RectTransform>();
            txtRect.anchorMin = Vector2.zero;
            txtRect.anchorMax = Vector2.one;
            txtRect.offsetMin = Vector2.zero;
            txtRect.offsetMax = Vector2.zero;
        }

        private static void BuildProps(Transform parent)
        {
            GameObject props = new GameObject("Props");
            props.transform.SetParent(parent, false);

            // Command Post desk + radio + stand
            CreateBox("CommandTable", props.transform, new Vector3(-14.5f, 0.4f, 2.5f), new Vector3(1.8f, 0.8f, 1.0f), "PropWood", true);
            CreateBox("CommandRadio", props.transform, new Vector3(-14.5f, 0.92f, 2.5f), new Vector3(0.35f, 0.24f, 0.25f), "PropMetal", true);
            CreateBox("BriefingPost", props.transform, new Vector3(-14.5f, 0.8f, -2.5f), new Vector3(0.2f, 1.6f, 0.2f), "PropMetal", true);

            // Room 1 (Equipment): Table + Medkit + Mask + Flashlight props
            CreateBox("EquipTable", props.transform, new Vector3(-8f, 0.4f, 7.5f), new Vector3(3.2f, 0.8f, 1.2f), "PropWood", true);
            CreateBox("Prop_MedKit", props.transform, new Vector3(-9f, 0.92f, 7.5f), new Vector3(0.45f, 0.24f, 0.35f), "MedicalWhite", true);
            CreateBox("Prop_Respirator", props.transform, new Vector3(-8f, 0.92f, 7.5f), new Vector3(0.3f, 0.24f, 0.3f), "PropMetal", true);
            CreateBox("Prop_Flashlight", props.transform, new Vector3(-7f, 0.88f, 7.5f), new Vector3(0.35f, 0.12f, 0.12f), "SignHazardRed", true);

            // Room 2 (Office): Two desks
            CreateBox("OfficeDesk_1", props.transform, new Vector3(6.5f, 0.38f, 7.5f), new Vector3(1.8f, 0.76f, 0.9f), "PropWood", true);
            CreateBox("OfficeDesk_2", props.transform, new Vector3(10.0f, 0.38f, 7.5f), new Vector3(1.8f, 0.76f, 0.9f), "PropWood", true);

            // Room 3 (Utility / Server): Server racks
            CreateBox("ServerRack_1", props.transform, new Vector3(-10.5f, 1.0f, -8.5f), new Vector3(1.2f, 2.0f, 1.0f), "PropMetal", true);
            CreateBox("ServerRack_2", props.transform, new Vector3(-8.5f, 1.0f, -8.5f), new Vector3(1.2f, 2.0f, 1.0f), "PropMetal", true);

            // Room 4 (Conference): Large central table
            CreateBox("ConferenceTable", props.transform, new Vector3(6f, 0.38f, -6f), new Vector3(3.6f, 0.76f, 1.6f), "PropWood", true);

            // Fire Extinguishers (corridor + equipment room + utility room)
            CreateExtinguisher("Extinguisher_CorridorW", props.transform, new Vector3(-10.5f, 0f, 1.65f));
            CreateExtinguisher("Extinguisher_CorridorE", props.transform, new Vector3(10.5f, 0f, -1.65f));
            CreateExtinguisher("Extinguisher_EquipRoom", props.transform, new Vector3(-11.4f, 0f, 8.8f));
            CreateExtinguisher("Extinguisher_UtilityRoom", props.transform, new Vector3(-1.0f, 0f, -9.3f));
        }

        private static void CreateExtinguisher(string name, Transform parent, Vector3 floorPos)
        {
            GameObject ext = new GameObject(name);
            ext.transform.SetParent(parent, false);
            ext.transform.position = floorPos;

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.name = "Cylinder";
            body.transform.SetParent(ext.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.35f, 0f);
            body.transform.localScale = new Vector3(0.22f, 0.35f, 0.22f);
            body.GetComponent<Renderer>().sharedMaterial = Materials["ExtinguisherRed"];
            GameObjectUtility.SetStaticEditorFlags(body, ArchStaticFlags);

            CreateBox("Nozzle", ext.transform, new Vector3(0f, 0.75f, 0f), new Vector3(0.08f, 0.12f, 0.16f), "PropMetal", true);
        }

        private static void BuildBakedLighting(Transform parent)
        {
            GameObject lights = new GameObject("Lights");
            lights.transform.SetParent(parent, false);

            // Baked Directional Light (for exterior Command Post + GI bounce)
            GameObject dirGo = new GameObject("Directional Light");
            dirGo.transform.SetParent(lights.transform, false);
            dirGo.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
            Light dirLight = dirGo.AddComponent<Light>();
            dirLight.type = LightType.Directional;
            dirLight.lightmapBakeType = LightmapBakeType.Baked;
            dirLight.intensity = 1.0f;
            dirLight.color = new Color(1f, 0.97f, 0.92f);

            // Baked Point Lights inside building zones (zero runtime shadow cost on phone)
            CreateBakedPointLight("Light_CorridorW", lights.transform, new Vector3(-6f, 2.8f, 0f), 14f, 1.4f);
            CreateBakedPointLight("Light_CorridorE", lights.transform, new Vector3(6f, 2.8f, 0f), 14f, 1.4f);
            CreateBakedPointLight("Light_Room1_Equip", lights.transform, new Vector3(-8f, 2.8f, 6f), 12f, 1.3f);
            CreateBakedPointLight("Light_Stairwell", lights.transform, new Vector3(0f, 2.8f, 6f), 12f, 1.3f);
            CreateBakedPointLight("Light_Room2_Office", lights.transform, new Vector3(8f, 2.8f, 6f), 12f, 1.3f);
            CreateBakedPointLight("Light_Room3_Utility", lights.transform, new Vector3(-6f, 2.8f, -6f), 14f, 1.2f);
            CreateBakedPointLight("Light_Room4_Conf", lights.transform, new Vector3(6f, 2.8f, -6f), 14f, 1.3f);
        }

        private static void CreateBakedPointLight(string name, Transform parent, Vector3 pos, float range, float intensity)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            Light light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.lightmapBakeType = LightmapBakeType.Baked;
            light.range = range;
            light.intensity = intensity;
            light.color = new Color(1f, 0.96f, 0.88f);
        }

        private static void BuildPlayerRig()
        {
            GameObject playerRig = new GameObject("PlayerRig");
            playerRig.transform.position = new Vector3(-15.5f, 0f, 0f);
            playerRig.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

            CharacterController cc = playerRig.AddComponent<CharacterController>();
            cc.height = 1.7f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0f, 0.85f, 0f);
            cc.stepOffset = 0.45f;

            EditorWalkController walk = playerRig.AddComponent<EditorWalkController>();

            GameObject mainCam = new GameObject("Main Camera");
            mainCam.tag = "MainCamera";
            mainCam.transform.SetParent(playerRig.transform, false);
            mainCam.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            mainCam.transform.localRotation = Quaternion.identity;

            Camera cam = mainCam.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 60f;
            mainCam.AddComponent<AudioListener>();
            mainCam.AddComponent<TrackedPoseDriver>();

            SerializedObject walkSo = new SerializedObject(walk);
            walkSo.FindProperty("cameraTransform").objectReferenceValue = mainCam.transform;
            walkSo.ApplyModifiedPropertiesWithoutUndo();

            // World-Space HUD (Reticle + FPS Overlay)
            GameObject hudCanvasGo = new GameObject("VrHudCanvas");
            hudCanvasGo.transform.SetParent(mainCam.transform, false);
            Canvas canvas = hudCanvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            hudCanvasGo.AddComponent<CanvasScaler>();

            RectTransform hudRect = hudCanvasGo.GetComponent<RectTransform>();
            hudRect.localPosition = new Vector3(0f, 0f, 2f);
            hudRect.sizeDelta = new Vector2(400f, 240f);
            hudRect.localScale = new Vector3(0.002f, 0.002f, 0.002f);

            GameObject dotGo = new GameObject("ReticleDot");
            dotGo.transform.SetParent(hudCanvasGo.transform, false);
            Image dotImg = dotGo.AddComponent<Image>();
            dotImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            dotImg.raycastTarget = false;
            RectTransform dotRect = dotGo.GetComponent<RectTransform>();
            dotRect.localPosition = Vector3.zero;
            dotRect.sizeDelta = new Vector2(16f, 16f);

            GameObject fpsGo = new GameObject("FpsCounter");
            fpsGo.transform.SetParent(hudCanvasGo.transform, false);
            Text fpsText = fpsGo.AddComponent<Text>();
            fpsText.font = _uiFont;
            fpsText.text = "FPS: --";
            fpsText.fontSize = 22;
            fpsText.fontStyle = FontStyle.Bold;
            fpsText.alignment = TextAnchor.UpperLeft;
            fpsText.color = Color.green;
            fpsText.raycastTarget = false;
            RectTransform fpsRect = fpsGo.GetComponent<RectTransform>();
            fpsRect.localPosition = new Vector3(-135f, 95f, 0f);
            fpsRect.sizeDelta = new Vector2(120f, 36f);

            FpsOverlay fpsOverlay = fpsGo.AddComponent<FpsOverlay>();
            SerializedObject fpsSo = new SerializedObject(fpsOverlay);
            fpsSo.FindProperty("fpsText").objectReferenceValue = fpsText;
            fpsSo.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildBootstrapAndAudio()
        {
            GameObject bootstrap = new GameObject("VRBootstrap");
            bootstrap.AddComponent<CardboardBootstrap>();
            bootstrap.AddComponent<AmbientAudioController>();
        }

        private static void CreateWallSegment(string name, Transform parent, float x, float z, float sizeX, float sizeZ)
        {
            CreateBox(name, parent, new Vector3(x, 1.6f, z), new Vector3(sizeX, 3.2f, sizeZ), "Wall", true);
        }

        private static void CreateDoorHeader(string name, Transform parent, float x, float z, float sizeX, float sizeZ, float floorOffsetY = 0f)
        {
            float bottomY = floorOffsetY + 2.4f;
            float topY = 3.2f;
            float height = Mathf.Max(0.2f, topY - bottomY);
            CreateBox(name, parent, new Vector3(x, bottomY + height * 0.5f, z), new Vector3(sizeX, height, sizeZ), "Wall", true);
        }

        private static GameObject CreateBox(string name, Transform parent, Vector3 pos, Vector3 scale, string matKey, bool isStatic)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = pos;
            box.transform.localScale = scale;

            if (Materials.TryGetValue(matKey, out Material mat))
            {
                box.GetComponent<Renderer>().sharedMaterial = mat;
            }

            if (isStatic)
            {
                GameObjectUtility.SetStaticEditorFlags(box, ArchStaticFlags);
            }

            return box;
        }
    }
}
