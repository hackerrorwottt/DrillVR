using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DrillVR.EditorTools
{
    /// <summary>
    /// Automates building the DrillVR Unity simulation for the Web Dashboard.
    /// Exports M8_FullOnlineLoop.unity to dashboard/unity-webgl.
    /// </summary>
    public static class WebGLBuildPipeline
    {
        private const string ScenePath = "Assets/Scenes/M8_FullOnlineLoop.unity";
        private const string OutputRelativePath = "../../dashboard/unity-webgl";

        [MenuItem("DrillVR/Build WebGL for Dashboard")]
        public static void BuildForDashboard()
        {
            string outputPath = Path.GetFullPath(Path.Combine(Application.dataPath, OutputRelativePath));
            Debug.Log($"[DrillVR] Starting WebGL export to: {outputPath}");

            if (!Directory.Exists(outputPath))
            {
                Directory.CreateDirectory(outputPath);
            }

            // Ensure Scene exists
            if (!File.Exists(ScenePath))
            {
                Debug.LogWarning($"[DrillVR] Scene {ScenePath} not found. Running Milestone 8 setup first...");
                Milestone8Setup.RunSetup();
            }

            // Ensure target is switched to WebGL
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
            {
                Debug.Log("[DrillVR] Switching active build target to WebGL...");
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);
            }

            // WebGL Configuration
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.runInBackground = true;

            BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = outputPath,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(buildPlayerOptions);
            var summary = report.summary;

            if (summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                Debug.Log($"[DrillVR] WebGL build succeeded! Output size: {summary.totalSize} bytes");
                if (!Application.isBatchMode)
                {
                    EditorUtility.DisplayDialog(
                        "WebGL Build Succeeded",
                        $"DrillVR simulation successfully exported to:\n{outputPath}\n\n" +
                        "Open the web dashboard at http://localhost:8080 and switch to 'Live 3D Simulation' to play in-browser.",
                        "OK"
                    );
                }
            }
            else
            {
                Debug.LogError($"[DrillVR] WebGL build failed with result: {summary.result}");
                if (!Application.isBatchMode)
                {
                    EditorUtility.DisplayDialog(
                        "WebGL Build Failed",
                        $"WebGL build failed: {summary.result}. Check the Unity Editor console for detailed errors.",
                        "OK"
                    );
                }
            }
        }
    }
}

