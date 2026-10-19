using System.Collections.Generic;
using System.IO;
using RivetSim.Core.Config;
using RivetSim.Core.Logging;
using RivetSim.Editor.Tools;
using RivetSim.Presentation.App;
using RivetSim.Simulation.Navigation;
using RivetSim.Simulation.Robot;
using RivetSim.Simulation.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace RivetSim.Editor.Generators
{
    /// <summary>
    /// 沙盒场景生成器：Sandbox_Robot（M4 机械臂与检测开发场地：地面、矮围墙、1.8 m 窄通道、A320 机身段（PF_FuselageSection，
    /// 摆放方式与整机一致）及其两侧各一个工位（R1-S1 / R2-S1，铆钉与真值缺陷与主场景一致）、机身段虚拟禁行区、停在 R1-S1 的 AGV、
    /// 检测演示入口与可视化；M3 导航演示与 M2 升降柱联锁演示保留但停用）与 Sandbox_UI（M6 演示屏 + HUD，假数据驱动）。重复执行覆盖上次结果。
    /// </summary>
    public static class SandboxSceneBuilder
    {
        /// <summary>沙盒 PanelSettings 资产路径。</summary>
        public const string PanelSettingsPath = ProjectPaths.UiFolder + "/UI_PanelSettings_Sandbox.asset";

        /// <summary>运行时主题样式表路径。</summary>
        public const string ThemePath = ProjectPaths.UiFolder + "/UI_Theme_Runtime.tss";

        private static readonly Vector2Int ReferenceResolution = new Vector2Int(1920, 1080);
        // 沙盒布局（外观与测试布置常量）：地面 20 × 14 m，矮围墙供激光扫描。
        private static readonly Vector2 SandboxFloorSizeM = new Vector2(20f, 14f);
        private const float SandboxWallHeightM = 0.5f;
        private const float SandboxWallThicknessM = 0.1f;
        // 两堆货箱夹出 1.8 m 窄通道（与主场景 O1 / O2 相同宽度）。
        private const float CrateCenterXM = -4f;
        private const float NarrowPassageWidthM = 1.8f;
        private static readonly Vector3 CrateSizeM = new Vector3(1.0f, 1.0f, 3.0f);
        // 机身段根节点（= 整机主起落架中点地面投影）位置：机身段落在原模拟机身处（x ≈ 3.7～6.3 m），R1-S1 停靠位约在 (3.2, 0.8)。
        private static readonly Vector3 SectionOriginM = new Vector3(5f, 0f, 5f);
        private const int SandboxStationIndex = 0;
        private static readonly Vector2[] SandboxRouteM =
        {
            new Vector2(-8f, 0f), new Vector2(-2f, 0f), new Vector2(-2f, -5.5f), new Vector2(8.5f, -5.5f),
            new Vector2(8.5f, 5.5f), new Vector2(-2f, 5.5f), new Vector2(-2f, 0f), new Vector2(-8f, 0f),
        };
        // 近景机位：从机身段左前方斜看 R1-S1 的铆钉排与机械臂。
        private const float SandboxCameraFovDeg = 40f;
        private static readonly Vector3 SandboxCameraPositionM = new Vector3(0.9f, 2.9f, 3.6f);
        private static readonly Vector3 SandboxCameraLookAtM = new Vector3(3.6f, 1.8f, 0.9f);

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
            Transform work = GeneratorUtility.CreateRoot(scene, "Zone_Work");
            Transform robot = GeneratorUtility.CreateRoot(scene, "Robot");
            Transform cameras = GeneratorUtility.CreateRoot(scene, "Cameras");
            Transform ui = GeneratorUtility.CreateRoot(scene, "UI");
            Transform visualization = GeneratorUtility.CreateRoot(scene, "Visualization");
            var settings = GeneratorUtility.LoadConfig<SimSettings>(ProjectPaths.SimSettingsAsset);
            var agvConfig = GeneratorUtility.LoadConfig<AgvConfig>(ProjectPaths.AgvAsset);
            var armConfig = GeneratorUtility.LoadConfig<ArmConfig>(ProjectPaths.ArmAsset);
            var nav = GeneratorUtility.LoadConfig<NavigationConfig>(ProjectPaths.NavigationAsset);
            var inspection = GeneratorUtility.LoadConfig<InspectionConfig>(ProjectPaths.InspectionAsset);

            SceneLightingSetup.ApplyAmbient();
            SceneLightingSetup.CreateSun(system);
            SceneLightingSetup.CreateGlobalVolume(system);
            BuildSandboxEnvironment(environment);
            Camera camera = SceneLightingSetup.CreateCamera(cameras, "Main Camera", SandboxCameraPositionM, SandboxCameraLookAtM, SandboxCameraFovDeg, true);

            GameObject sectionPrefab = GeneratorUtility.LoadPrefab(FuselageSectionBuilder.PrefabPath);
            GameObject agvPrefab = GeneratorUtility.LoadPrefab(AgvGenerator.AgvPrefabPath);
            if (sectionPrefab == null || agvPrefab == null || settings == null || agvConfig == null || armConfig == null || nav == null || inspection == null)
            {
                SimLog.Error(LogCategory.System, "Sandbox_Robot 生成不完整：缺少 PF_FuselageSection / PF_AGV 或 CFG。");
                Save(scene, ProjectPaths.SandboxRobotScene);
                return;
            }

            // 机身段与检测区：每侧只生成工位 1（R1-S1 在左侧、R2-S1 在右侧），真值缺陷按全部 48 颗的序号分配。
            Transform aircraft = GeneratorUtility.InstantiatePrefab(sectionPrefab, work, SectionOriginM, Quaternion.identity, "Aircraft").transform;
            Physics.SyncTransforms();
            RivetRegistry registry = RivetZoneGenerator.Build(work, aircraft, (r, s) => s == SandboxStationIndex);
            if (registry == null || registry.Regions.Count == 0 || registry.Regions[0].Stations.Count == 0)
            {
                SimLog.Error(LogCategory.System, "Sandbox_Robot 生成不完整：机身段上没有生成工位。");
                Save(scene, ProjectPaths.SandboxRobotScene);
                return;
            }
            InspectionStation station = registry.Regions[0].Stations[0];
            VirtualKeepOutZone keepOut = BuildKeepOut(work, aircraft, registry, nav, agvConfig);

            GameObject agv = GeneratorUtility.InstantiatePrefab(agvPrefab, robot, station.DockingPose.WorldPosition, station.DockingPose.WorldRotation, "AGV");

            // 升降柱联锁演示（M2）与导航演示（M3）保留但停用；检测演示（M4）为默认入口。
            var liftDemo = new GameObject("SandboxLiftDemo");
            liftDemo.transform.SetParent(system, false);
            liftDemo.AddComponent<SandboxLiftDemo>().Configure(settings, agv.GetComponent<AgvController>(), agv.GetComponent<LiftColumnController>());
            liftDemo.SetActive(false);

            var inspectionObject = new GameObject("SandboxInspectionDemo");
            inspectionObject.transform.SetParent(system, false);
            var inspectionDemo = inspectionObject.AddComponent<SandboxInspectionDemo>();
            inspectionDemo.Configure(settings, agvConfig, nav, armConfig, inspection, agv.GetComponent<AgvController>(), station, keepOut,
                Vector2.zero, SandboxFloorSizeM);
            NavigationSceneSetup.Views views = NavigationSceneSetup.Build(visualization, ui, inspectionObject, camera);
            NavigationSceneSetup.InspectionViews inspectionViews = NavigationSceneSetup.BuildInspectionViews(visualization, ui);
            inspectionDemo.ConfigureViews(views.Overlay, inspectionViews.Overlay, inspectionViews.Rings, inspectionViews.Frustum, inspectionViews.Contact);

            var navDemoObject = new GameObject("SandboxNavDemo");
            navDemoObject.transform.SetParent(system, false);
            var navDemo = navDemoObject.AddComponent<SandboxNavDemo>();
            navDemo.Configure(settings, agvConfig, nav, armConfig, inspection, agv.GetComponent<AgvController>(), station, keepOut,
                Vector2.zero, SandboxFloorSizeM, SandboxRouteM);
            navDemo.ConfigureViews(views.Map, views.KeepOut, views.Lidar, views.Path, views.Overlay, views.Teleop);
            navDemoObject.SetActive(false);

            Save(scene, ProjectPaths.SandboxRobotScene);
        }

        // 机身段全部网格三角形的地面投影（与主场景同法保守栅格化），每个工位一条接近通道。
        private static VirtualKeepOutZone BuildKeepOut(Transform work, Transform aircraft, RivetRegistry registry, NavigationConfig nav, AgvConfig agv)
        {
            GridGeometry mapGrid = GridGeometry.Covering(Vector2.zero, SandboxFloorSizeM, nav.gridResolutionM);
            GridGeometry maskGrid = KeepOutZoneGenerator.AlignedCovering(KeepOutZoneGenerator.RendererBoundsXz(aircraft.gameObject), mapGrid);
            bool[] mask = KeepOutZoneGenerator.RasterizeMeshes(aircraft.gameObject, maskGrid, out _);
            var center = new Vector2(aircraft.position.x, aircraft.position.z);
            var channels = new List<KeepOutChannel>();
            foreach (InspectionRegion region in registry.Regions)
            foreach (InspectionStation s in region.Stations)
                channels.Add(KeepOutZoneGenerator.ComputeChannel(s.StationId, s.DockingPose, center, nav, agv));
            Transform node = GeneratorUtility.CreateNode(KeepOutZoneGenerator.NodeName, work, Vector3.zero);
            var keepOut = node.gameObject.AddComponent<VirtualKeepOutZone>();
            keepOut.Configure(maskGrid, mask, channels.ToArray());
            return keepOut;
        }

        // 地面、矮围墙（激光可见）与两堆货箱（夹出 1.8 m 窄通道）。
        private static void BuildSandboxEnvironment(Transform environment)
        {
            Material floor = MaterialLibraryBuilder.Get(MaterialLibraryBuilder.FloorEpoxy);
            Material wall = MaterialLibraryBuilder.Get(MaterialLibraryBuilder.Wall);
            GeneratorUtility.CreateBox("Floor", environment, new Vector3(0f, -0.05f, 0f),
                new Vector3(SandboxFloorSizeM.x, 0.1f, SandboxFloorSizeM.y), floor, true);
            float hx = 0.5f * SandboxFloorSizeM.x, hz = 0.5f * SandboxFloorSizeM.y;
            GeneratorUtility.CreateBox("Wall_N", environment, new Vector3(0f, SandboxWallHeightM * 0.5f, hz),
                new Vector3(SandboxFloorSizeM.x, SandboxWallHeightM, SandboxWallThicknessM), wall, true);
            GeneratorUtility.CreateBox("Wall_S", environment, new Vector3(0f, SandboxWallHeightM * 0.5f, -hz),
                new Vector3(SandboxFloorSizeM.x, SandboxWallHeightM, SandboxWallThicknessM), wall, true);
            GeneratorUtility.CreateBox("Wall_E", environment, new Vector3(hx, SandboxWallHeightM * 0.5f, 0f),
                new Vector3(SandboxWallThicknessM, SandboxWallHeightM, SandboxFloorSizeM.y), wall, true);
            GeneratorUtility.CreateBox("Wall_W", environment, new Vector3(-hx, SandboxWallHeightM * 0.5f, 0f),
                new Vector3(SandboxWallThicknessM, SandboxWallHeightM, SandboxFloorSizeM.y), wall, true);
            GeneratorUtility.SetLayerRecursive(environment.gameObject, SimLayers.EnvironmentIndex);

            Material crate = MaterialLibraryBuilder.Get(MaterialLibraryBuilder.Crate);
            foreach (float side in new[] { -1f, 1f })
            {
                GameObject box = GeneratorUtility.CreateBox(side < 0f ? "Crates_S" : "Crates_N", environment,
                    new Vector3(CrateCenterXM, CrateSizeM.y * 0.5f, side * (0.5f * NarrowPassageWidthM + 0.5f * CrateSizeM.z)), CrateSizeM, crate, true);
                box.layer = SimLayers.ObstacleIndex;
            }
        }

        private static void BuildUiSandbox()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Transform system = GeneratorUtility.CreateRoot(scene, "_System");
            Transform cameras = GeneratorUtility.CreateRoot(scene, "Cameras");
            Transform ui = GeneratorUtility.CreateRoot(scene, "UI");
            SceneLightingSetup.CreateCamera(cameras, "Main Camera", new Vector3(0f, 1f, -10f), new Vector3(0f, 1f, 0f), 60f, true);
            // M6：演示屏（全屏）+ HUD，由 FakeMissionFeed 假数据驱动。
            DemoUiGenerator.BuildSandbox(system, ui);

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
