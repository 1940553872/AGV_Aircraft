using System.IO;
using RivetSim.Core.Config;
using RivetSim.Core.Logging;
using RivetSim.Editor.Tools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace RivetSim.Editor.Generators
{
    /// <summary>
    /// 沙盒场景生成器：Sandbox_Robot（地面 + 灯光 + AGV）与 Sandbox_UI（空场景 + UIDocument）。重复执行覆盖上次结果。
    /// </summary>
    public static class SandboxSceneBuilder
    {
        /// <summary>沙盒 PanelSettings 资产路径。</summary>
        public const string PanelSettingsPath = ProjectPaths.UiFolder + "/UI_PanelSettings_Sandbox.asset";

        /// <summary>运行时主题样式表路径。</summary>
        public const string ThemePath = ProjectPaths.UiFolder + "/UI_Theme_Runtime.tss";

        private static readonly Vector2Int ReferenceResolution = new Vector2Int(1920, 1080);
        private const float SandboxFloorSizeM = 12f;
        private const float SandboxCameraFovDeg = 40f;

        /// <summary>生成两个沙盒场景。</summary>
        [MenuItem("RivetSim/Generate/Sandbox Scenes", priority = 31)]
        public static void Build()
        {
            if (!MainSceneBuilder.EnsureNoUnsavedScenes()) return;
            BuildRobotSandbox();
            BuildUiSandbox();
            SimLog.Info(LogCategory.System, "沙盒场景已生成：Sandbox_Robot、Sandbox_UI。");
        }

        private static void BuildRobotSandbox()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Transform system = GeneratorUtility.CreateRoot(scene, "_System");
            Transform environment = GeneratorUtility.CreateRoot(scene, "Environment");
            Transform robot = GeneratorUtility.CreateRoot(scene, "Robot");
            Transform cameras = GeneratorUtility.CreateRoot(scene, "Cameras");

            SceneLightingSetup.ApplyAmbient();
            SceneLightingSetup.CreateSun(system);
            SceneLightingSetup.CreateGlobalVolume(system);
            GeneratorUtility.CreateBox("Floor", environment, new Vector3(0f, -0.05f, 0f),
                new Vector3(SandboxFloorSizeM, 0.1f, SandboxFloorSizeM), MaterialLibraryBuilder.Get(MaterialLibraryBuilder.FloorEpoxy), true);
            GeneratorUtility.SetLayerRecursive(environment.gameObject, SimLayers.EnvironmentIndex);

            GameObject agv = GeneratorUtility.LoadPrefab(AgvGenerator.AgvPrefabPath);
            if (agv != null) GeneratorUtility.InstantiatePrefab(agv, robot, Vector3.zero, Quaternion.identity, "AGV");
            SceneLightingSetup.CreateCamera(cameras, "Main Camera", new Vector3(1.8f, 1.6f, 2.4f), new Vector3(0f, 0.5f, 0f), SandboxCameraFovDeg, true);

            Save(scene, ProjectPaths.SandboxRobotScene);
        }

        private static void BuildUiSandbox()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Transform cameras = GeneratorUtility.CreateRoot(scene, "Cameras");
            Transform ui = GeneratorUtility.CreateRoot(scene, "UI");
            SceneLightingSetup.CreateCamera(cameras, "Main Camera", new Vector3(0f, 1f, -10f), new Vector3(0f, 1f, 0f), 60f, true);

            Transform document = GeneratorUtility.CreateNode("UIDocument", ui, Vector3.zero);
            var uiDocument = document.gameObject.AddComponent<UIDocument>();
            uiDocument.panelSettings = EnsurePanelSettings();

            Save(scene, ProjectPaths.SandboxUiScene);
        }

        /// <summary>生成或更新沙盒 PanelSettings（1920×1080 参考分辨率，默认运行时主题）。</summary>
        public static PanelSettings EnsurePanelSettings()
        {
            GeneratorUtility.EnsureFolder(ProjectPaths.UiFolder);
            if (!File.Exists(ThemePath))
            {
                File.WriteAllText(ThemePath, "@import url(\"unity-theme://default\");\n");
                AssetDatabase.ImportAsset(ThemePath);
            }

            var settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(settings, PanelSettingsPath);
            }
            settings.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = ReferenceResolution;
            settings.match = 0.5f;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            return settings;
        }

        private static void Save(Scene scene, string path)
        {
            GeneratorUtility.EnsureFolder(path.Substring(0, path.LastIndexOf('/')));
            EditorSceneManager.SaveScene(scene, path);
        }
    }
}
