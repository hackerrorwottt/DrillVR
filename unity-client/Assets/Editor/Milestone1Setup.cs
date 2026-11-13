using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SpatialTracking;
using UnityEngine.UI;
using DrillVR.Core;
using DrillVR.Interactions;

namespace DrillVR.EditorTools
{
    /// <summary>
    /// Automates Milestone 1 setup inside Unity: configures Android PlayerSettings,
    /// registers the InteractiveTest tag, and builds the M1_CardboardTest scene.
    /// </summary>
    public static class Milestone1Setup
    {
        private const string ScenePath = "Assets/Scenes/M1_CardboardTest.unity";
        private const string InteractiveTag = "InteractiveTest";

        [MenuItem("DrillVR/Setup Milestone 1")]
        public static void RunSetup()
        {
            EnsureDirectories();
            EnsureTag(InteractiveTag);
            ConfigureAndroidPlayerSettings();
            BuildTestScene();

            EditorUtility.DisplayDialog(
                "DrillVR Milestone 1 Setup Complete",
                "Configured Android PlayerSettings and built Assets/Scenes/M1_CardboardTest.unity.\n\n" +
                "Next manual check:\n" +
                "1. Open Edit > Project Settings > XR Plug-in Management > Android tab.\n" +
                "2. Check 'Initialize XR on Startup' and 'Cardboard XR Plugin'.\n" +
                "3. Switch platform to Android in File > Build Settings, then Build And Run.",
                "OK"
            );
        }

        private static void EnsureDirectories()
        {
            string[] folders =
            {
                "Assets/Scenes",
                "Assets/ScriptableObjects",
                "Assets/Scripts/Core",
                "Assets/Scripts/Player",
                "Assets/Scripts/Interactions",
                "Assets/Scripts/Environment",
                "Assets/Scripts/Hazards",
                "Assets/Scripts/Equipment",
                "Assets/Scripts/Rescue",
                "Assets/Scripts/Assessment",
                "Assets/Scripts/AI",
                "Assets/Scripts/Network",
                "Assets/Scripts/UI"
            };

            foreach (string folder in folders)
            {
                if (!Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                }
            }

            AssetDatabase.Refresh();
        }

        private static void EnsureTag(string tagName)
        {
            Object[] tagManagerAssets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (tagManagerAssets == null || tagManagerAssets.Length == 0)
            {
                Debug.LogWarning("Could not load ProjectSettings/TagManager.asset to add tag: " + tagName);
                return;
            }

            SerializedObject tagManager = new SerializedObject(tagManagerAssets[0]);
            SerializedProperty tagsProp = tagManager.FindProperty("tags");

            for (int i = 0; i < tagsProp.arraySize; i++)
            {
                if (tagsProp.GetArrayElementAtIndex(i).stringValue == tagName)
                {
                    return;
                }
            }

            tagsProp.InsertArrayElementAtIndex(tagsProp.arraySize);
            tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1).stringValue = tagName;
            tagManager.ApplyModifiedProperties();
        }

        private static void ConfigureAndroidPlayerSettings()
        {
            PlayerSettings.companyName = "DrillVRTeam";
            PlayerSettings.productName = "DrillVR";
            PlayerSettings.colorSpace = ColorSpace.Gamma;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;

            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.drillvr.prototype");
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.forceInternetPermission = true;
            PlayerSettings.insecureHttpOption = InsecureHttpOption.AlwaysAllowed;
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
        }

        private static void BuildTestScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            GameObject existingMainCam = GameObject.FindGameObjectWithTag("MainCamera");
            if (existingMainCam == null)
            {
                existingMainCam = new GameObject("Main Camera");
                existingMainCam.tag = "MainCamera";
                existingMainCam.AddComponent<Camera>();
                existingMainCam.AddComponent<AudioListener>();
            }

            GameObject playerRig = new GameObject("PlayerRig");
            playerRig.transform.position = new Vector3(0f, 1.6f, 0f);

            existingMainCam.transform.SetParent(playerRig.transform, false);
            existingMainCam.transform.localPosition = Vector3.zero;
            existingMainCam.transform.localRotation = Quaternion.identity;

            Camera cam = existingMainCam.GetComponent<Camera>();
            cam.nearClipPlane = 0.05f;

            if (existingMainCam.GetComponent<TrackedPoseDriver>() == null)
            {
                existingMainCam.AddComponent<TrackedPoseDriver>();
            }

            SimpleGazeTapTester gazeTester = existingMainCam.GetComponent<SimpleGazeTapTester>();
            if (gazeTester == null)
            {
                gazeTester = existingMainCam.AddComponent<SimpleGazeTapTester>();
            }

            // World-space reticle canvas attached to Main Camera (required for stereoscopic VR)
            GameObject canvasGo = new GameObject("ReticleCanvas");
            canvasGo.transform.SetParent(existingMainCam.transform, false);
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvasGo.AddComponent<CanvasScaler>();

            RectTransform canvasRect = canvasGo.GetComponent<RectTransform>();
            canvasRect.localPosition = new Vector3(0f, 0f, 2f);
            canvasRect.sizeDelta = new Vector2(100f, 100f);
            canvasRect.localScale = new Vector3(0.002f, 0.002f, 0.002f);

            GameObject dotGo = new GameObject("ReticleDot");
            dotGo.transform.SetParent(canvasGo.transform, false);
            Image dotImage = dotGo.AddComponent<Image>();
            dotImage.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            dotImage.raycastTarget = false;

            RectTransform dotRect = dotGo.GetComponent<RectTransform>();
            dotRect.localPosition = Vector3.zero;
            dotRect.sizeDelta = new Vector2(20f, 20f);

            SerializedObject testerSo = new SerializedObject(gazeTester);
            testerSo.FindProperty("reticleImage").objectReferenceValue = dotImage;
            testerSo.ApplyModifiedPropertiesWithoutUndo();

            GameObject bootstrapGo = new GameObject("VRBootstrap");
            bootstrapGo.AddComponent<CardboardBootstrap>();

            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.position = Vector3.zero;

            GameObject testCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            testCube.name = "TestCube";
            testCube.tag = InteractiveTag;
            testCube.transform.position = new Vector3(0f, 1.6f, 3.5f);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true)
            };

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}
