using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace DrillVR.EditorTools
{
    /// <summary>
    /// Automated Android Cardboard APK build pipeline with ARM64, OpenGLES3,
    /// and HTTP LAN cleartext permissions preconfigured for hardware testing.
    /// </summary>
    public static class AndroidBuildPipeline
    {
        private const string OutputApkPath = "Builds/Android/DrillVR.apk";
        private const string ScenePath = "Assets/Scenes/M8_FullOnlineLoop.unity";

        [MenuItem("DrillVR/Build Android Cardboard APK")]
        public static void BuildAndroidCardboardApk()
        {
            Debug.Log("[DrillVR] Preparing Android Cardboard APK build...");

            // Ensure destination directory exists
            string dir = Path.GetDirectoryName(OutputApkPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            // Ensure M8 scene exists, or fallback to active scene
            string buildScene = File.Exists(ScenePath) ? ScenePath : EditorBuildSettings.scenes[0].path;

            // Enforce PlayerSettings for Android Cardboard
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

            var buildOptions = new BuildPlayerOptions
            {
                scenes = new[] { buildScene },
                locationPathName = OutputApkPath,
                target = BuildTarget.Android,
                options = BuildOptions.None
            };

            Debug.Log($"[DrillVR] Starting Android build targeting: {OutputApkPath}");
            BuildReport report = BuildPipeline.BuildPlayer(buildOptions);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[DrillVR] Android APK build SUCCEEDED: {summary.totalSize / (1024 * 1024):F1} MB in {summary.totalTime.TotalSeconds:F1}s.");
                if (!Application.isBatchMode)
                {
                    EditorUtility.DisplayDialog("DrillVR Build Success",
                        $"Android APK generated successfully at:\n{OutputApkPath}\n\nInstall via adb:\n  adb install -r {OutputApkPath}",
                        "OK");
                }
            }
            else
            {
                Debug.LogError($"[DrillVR] Android build FAILED with {summary.totalErrors} error(s).");
            }
        }

        public static void BuildCli()
        {
            BuildAndroidCardboardApk();
        }
    }
}
