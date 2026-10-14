using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace FifMan.Editor
{
    /// <summary>One-click project configuration and Android (Quest) build.</summary>
    public static class ProjectSetup
    {
        const string ScenePath = "Assets/Scenes/Main.unity";

        [InitializeOnLoadMethod]
        static void AutoSetup()
        {
            if (File.Exists(ScenePath) || Application.isBatchMode) return;
            EditorApplication.delayCall += Setup;
        }

        [MenuItem("Fif-Man/Setup Project")]
        public static void Setup()
        {
            ConfigurePlayerSettings();
            CreateScene();
            Debug.Log("Fif-Man: project configured. Make sure OpenXR is ticked under " +
                      "Project Settings > XR Plug-in Management (Android + PC tabs), and add the " +
                      "Oculus Touch Controller Profile under OpenXR > Enabled Interaction Profiles.");
        }

        [MenuItem("Fif-Man/Build Android (Quest) APK")]
        public static void BuildAndroid()
        {
            Setup();
            Directory.CreateDirectory("Builds");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/FifMan.apk",
                target = BuildTarget.Android,
                options = BuildOptions.None,
            });
            if (report.summary.result != BuildResult.Succeeded)
            {
                Debug.LogError("Fif-Man: Android build failed.");
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        static void ConfigurePlayerSettings()
        {
            PlayerSettings.companyName = "FifMan";
            PlayerSettings.productName = "Fif-Man VR";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.fifman.vr");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLES3 });

            // The game reads controllers through the Input System package.
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (assets.Length > 0)
            {
                var so = new SerializedObject(assets[0]);
                var handler = so.FindProperty("activeInputHandler");
                if (handler != null && handler.intValue == 0)
                {
                    handler.intValue = 1;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    Debug.LogWarning("Fif-Man: switched Active Input Handling to 'Input System Package'. Restart the editor to apply.");
                }
            }
        }

        static void CreateScene()
        {
            if (!File.Exists(ScenePath))
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                Directory.CreateDirectory("Assets/Scenes");
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
                AssetDatabase.Refresh();
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }
    }
}
