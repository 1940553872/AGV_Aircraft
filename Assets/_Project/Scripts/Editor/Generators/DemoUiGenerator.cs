using System.Collections.Generic;
using RivetSim.Core.Config;
using RivetSim.Core.Logging;
using RivetSim.Editor.Tools;
using RivetSim.Presentation.App;
using RivetSim.Presentation.CameraControl;
using RivetSim.Presentation.Input;
using RivetSim.Presentation.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace RivetSim.Editor.Generators
{
    /// <summary>
    /// M6 演示界面生成器：演示屏 RenderTexture（T_DemoConsoleRT）、显示器材质 M_DemoScreen、三份 PanelSettings
    /// （渲染到贴图 / 全屏叠加 / HUD），并在主场景与 Sandbox_UI 中挂载演示屏、HUD、CameraRig 与入口引用。重复执行覆盖上次结果。
    /// </summary>
    public static class DemoUiGenerator
    {
        /// <summary>演示屏 RenderTexture。</summary>
        public const string RenderTexturePath = ProjectPaths.UiFolder + "/T_DemoConsoleRT.renderTexture";

        /// <summary>显示器屏幕材质。</summary>
        public const string ScreenMaterialPath = ProjectPaths.MaterialsFolder + "/M_DemoScreen.mat";

        /// <summary>渲染到贴图的演示屏面板。</summary>
        public const string ConsoleTexturePanelPath = ProjectPaths.UiFolder + "/UI_PanelSettings_Console.asset";

        /// <summary>全屏演示屏面板。</summary>
        public const string ConsoleFullscreenPanelPath = ProjectPaths.UiFolder + "/UI_PanelSettings_ConsoleFullscreen.asset";

        /// <summary>HUD 面板。</summary>
        public const string HudPanelPath = ProjectPaths.UiFolder + "/UI_PanelSettings_Hud.asset";

        private const int RenderTextureDepthBits = 24;
        private const int FullscreenSortingOrder = 10;
        private const int HudSortingOrder = 20;
        private const float PanelMatch = 0.5f;

        /// <summary>生成或更新演示界面资产。</summary>
        [MenuItem("RivetSim/Generate/Demo UI Assets", priority = 18)]
        public static void BuildAssets()
        {
            EnsureRenderTexture();
            EnsureScreenMaterial();
            EnsurePanels();
            AssetDatabase.SaveAssets();
            SimLog.Info(LogCategory.System, "演示界面资产已生成：RenderTexture、显示器材质、PanelSettings。");
        }

        /// <summary>显示器材质（不存在时生成）。DemoStationGenerator 用它替换屏幕网格材质。</summary>
        public static Material EnsureScreenMaterial()
        {
            RenderTexture rt = EnsureRenderTexture();
            var material = AssetDatabase.LoadAssetAtPath<Material>(ScreenMaterialPath);
            if (material == null)
            {
                GeneratorUtility.EnsureFolder(ProjectPaths.MaterialsFolder);
                material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(material, ScreenMaterialPath);
            }
            material.shader = Shader.Find("Universal Render Pipeline/Unlit");
            material.SetTexture("_BaseMap", rt);
            material.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static RenderTexture EnsureRenderTexture()
        {
            var theme = GeneratorUtility.LoadConfig<UiThemeConfig>(ProjectPaths.UiThemeAsset);
            Vector2Int size = theme != null ? theme.screenResolutionPx : new Vector2Int(1920, 1080);
            var rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(RenderTexturePath);
            if (rt == null)
            {
                GeneratorUtility.EnsureFolder(ProjectPaths.UiFolder);
                rt = new RenderTexture(size.x, size.y, RenderTextureDepthBits, RenderTextureFormat.ARGB32);
                AssetDatabase.CreateAsset(rt, RenderTexturePath);
            }
            if (rt.width != size.x || rt.height != size.y)
            {
                rt.Release();
                rt.width = size.x;
                rt.height = size.y;
            }
            rt.depth = RenderTextureDepthBits;
            rt.useMipMap = false;
            rt.filterMode = FilterMode.Bilinear;
            rt.antiAliasing = 1;
            EditorUtility.SetDirty(rt);
            return rt;
        }

        private static void EnsurePanels()
        {
            PanelSettings sandbox = SandboxSceneBuilder.EnsurePanelSettings();
            var theme = GeneratorUtility.LoadConfig<UiThemeConfig>(ProjectPaths.UiThemeAsset);
            Vector2Int reference = theme != null ? theme.screenResolutionPx : new Vector2Int(1920, 1080);
            Color clear = theme != null ? theme.background : Color.black;

            PanelSettings texture = EnsurePanel(ConsoleTexturePanelPath, sandbox.themeStyleSheet, reference, 0);
            texture.targetTexture = EnsureRenderTexture();
            texture.clearColor = true;
            texture.colorClearValue = clear;
            EditorUtility.SetDirty(texture);

            EnsurePanel(ConsoleFullscreenPanelPath, sandbox.themeStyleSheet, reference, FullscreenSortingOrder);
            EnsurePanel(HudPanelPath, sandbox.themeStyleSheet, reference, HudSortingOrder);
        }

        private static PanelSettings EnsurePanel(string path, ThemeStyleSheet themeSheet, Vector2Int reference, int sortingOrder)
        {
            var settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(path);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(settings, path);
            }
            settings.themeStyleSheet = themeSheet;
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = reference;
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = PanelMatch;
            settings.sortingOrder = sortingOrder;
            settings.targetTexture = null;
            settings.clearColor = false;
            EditorUtility.SetDirty(settings);
            return settings;
        }

        /// <summary>在 uiRoot 下生成演示屏与 HUD 文档节点。</summary>
        public static (DemoConsoleView console, HudView hud) BuildDocuments(Transform uiRoot)
        {
            BuildAssets();
            Transform consoleNode = GeneratorUtility.CreateNode("DemoConsole", uiRoot, Vector3.zero);
            var consoleDoc = consoleNode.gameObject.AddComponent<UIDocument>();
            consoleDoc.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(ConsoleTexturePanelPath);
            consoleDoc.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(ProjectPaths.DemoConsoleUxml);
            var console = consoleNode.gameObject.AddComponent<DemoConsoleView>();
            console.Configure(consoleDoc.panelSettings, AssetDatabase.LoadAssetAtPath<PanelSettings>(ConsoleFullscreenPanelPath));

            Transform hudNode = GeneratorUtility.CreateNode("Hud", uiRoot, Vector3.zero);
            var hudDoc = hudNode.gameObject.AddComponent<UIDocument>();
            hudDoc.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(HudPanelPath);
            hudDoc.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(ProjectPaths.HudUxml);
            var hud = hudNode.gameObject.AddComponent<HudView>();
            return (console, hud);
        }

        /// <summary>主场景：演示屏、HUD、CameraRig（主相机 + CamAnchor_1～4），写入 AppBootstrap。</summary>
        public static void BuildMain(Dictionary<string, Transform> roots, AppBootstrap bootstrap, Camera mainCamera)
        {
            (DemoConsoleView console, HudView hud) = BuildDocuments(roots["UI"]);
            var rig = mainCamera.gameObject.AddComponent<CameraRig>();
            Transform cameras = roots["Cameras"];
            string[] names = { "1_Overview", "2_FollowAgv", "3_EndEffector", "4_DemoStation" };
            var anchors = new Transform[names.Length];
            for (int i = 0; i < names.Length; i++) anchors[i] = cameras.Find(MainSceneBuilder.CameraAnchorPrefix + names[i]);
            rig.Configure(anchors);
            bootstrap.ConfigureDemoUi(GeneratorUtility.LoadConfig<UiThemeConfig>(ProjectPaths.UiThemeAsset), console, hud, rig,
                bootstrap.GetComponent<VisualizationToggleInput>());
        }

        /// <summary>Sandbox_UI：演示屏（全屏）、HUD 与假数据入口。</summary>
        public static void BuildSandbox(Transform systemRoot, Transform uiRoot)
        {
            (DemoConsoleView console, HudView hud) = BuildDocuments(uiRoot);
            var entry = new GameObject("SandboxUiBootstrap");
            entry.transform.SetParent(systemRoot, false);
            entry.AddComponent<SandboxUiBootstrap>().Configure(
                GeneratorUtility.LoadConfig<SimSettings>(ProjectPaths.SimSettingsAsset),
                GeneratorUtility.LoadConfig<InspectionConfig>(ProjectPaths.InspectionAsset),
                GeneratorUtility.LoadConfig<UiThemeConfig>(ProjectPaths.UiThemeAsset),
                console, hud);
        }
    }
}
