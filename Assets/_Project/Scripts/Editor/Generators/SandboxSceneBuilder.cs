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
    /// 沙盒场景生成器：Sandbox_Robot（M3 导航开发场地：地面、矮围墙、窄通道、模拟机身虚拟禁行区、测试工位、AGV、导航演示入口与可视化；
    /// M2 升降柱联锁演示入口保留但停用）与 Sandbox_UI（空场景 + UIDocument）。重复执行覆盖上次结果。
    /// </summary>
    public static class SandboxSceneBuilder
    {
        /// <summary>沙盒 PanelSettings 资产路径。</summary>
        public const string PanelSettingsPath = ProjectPaths.UiFolder + "/UI_PanelSettings_Sandbox.asset";

        /// <summary>运行时主题样式表路径。</summary>
        public const string ThemePath = ProjectPaths.UiFolder + "/UI_Theme_Runtime.tss";

        private static readonly Vector2Int ReferenceResolution = new Vector2Int(1920, 1080);
        // 沙盒布局（M3 导航开发场地，外观与测试布置常量）：地面 20 × 14 m，矮围墙供激光扫描。
        private static readonly Vector2 SandboxFloorSizeM = new Vector2(20f, 14f);
        private const float SandboxWallHeightM = 0.5f;
        private const float SandboxWallThicknessM = 0.1f;
        // 两堆货箱夹出 1.8 m 窄通道（与主场景 O1 / O2 相同宽度）。
        private const float CrateCenterXM = -4f;
        private const float NarrowPassageWidthM = 1.8f;
        private static readonly Vector3 CrateSizeM = new Vector3(1.0f, 1.0f, 3.0f);
        // 模拟机身：地面投影 2.6 × 8 m（虚拟禁行区），外观为悬空长方体（底面离地 1.3 m，激光扫不到）。
        private static readonly Vector2 FuselageMockCenterM = new Vector2(5f, 0f);
        private static readonly Vector2 FuselageMockSizeM = new Vector2(2.6f, 8f);
        private const float FuselageMockCenterHeightM = 2.6f;
        // 测试工位：车道中心线距模拟机身投影 0.46 m（与主场景 R1-S1 相同），车头朝 +Z。
        private const string SandboxStationId = "SB-S1";
        private static readonly PlanarPose SandboxDockPose = new PlanarPose(new Vector2(3.24f, 1.0f), 0f);
        private static readonly PlanarPose SandboxStartPose = new PlanarPose(new Vector2(-8f, 0f), 90f);
        private static readonly Vector2[] SandboxRouteM =
        {
            new Vector2(-8f, 0f), new Vector2(-2f, 0f), new Vector2(-2f, -5.5f), new Vector2(8.5f, -5.5f),
            new Vector2(8.5f, 5.5f), new Vector2(-2f, 5.5f), new Vector2(-2f, 0f), new Vector2(-8f, 0f),
        };
        // 斜俯视机位：整个沙盒入画。
        private const float SandboxCameraFovDeg = 50f;
        private static readonly Vector3 SandboxCameraPositionM = new Vector3(-1f, 15f, -14f);
        private static readonly Vector3 SandboxCameraLookAtM = new Vector3(0.5f, 0f, 0f);

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
            var nav = GeneratorUtility.LoadConfig<NavigationConfig>(ProjectPaths.NavigationAsset);
            var inspection = GeneratorUtility.LoadConfig<InspectionConfig>(ProjectPaths.InspectionAsset);

            SceneLightingSetup.ApplyAmbient();
            SceneLightingSetup.CreateSun(system);
            SceneLightingSetup.CreateGlobalVolume(system);
            BuildSandboxEnvironment(environment);

            GameObject agvPrefab = GeneratorUtility.LoadPrefab(AgvGenerator.AgvPrefabPath);
            GameObject agv = agvPrefab != null
                ? GeneratorUtility.InstantiatePrefab(agvPrefab, robot, SandboxStartPose.WorldPosition, SandboxStartPose.WorldRotation, "AGV")
                : null;
            Camera camera = SceneLightingSetup.CreateCamera(cameras, "Main Camera", SandboxCameraPositionM, SandboxCameraLookAtM, SandboxCameraFovDeg, true);
            if (agv == null || settings == null || agvConfig == null || nav == null || inspection == null)
            {
                SimLog.Error(LogCategory.System, "Sandbox_Robot 生成不完整：缺少 PF_AGV 或 CFG（含 CFG_Navigation）。");
                Save(scene, ProjectPaths.SandboxRobotScene);
                return;
            }

            // 模拟机身：外观悬空（激光扫不到），地面投影作为虚拟禁行区；测试工位停在其左侧，停靠标记板在前方 1.0 m、外侧 0.8 m。
            InspectionStation station = BuildTestStation(work, inspection);
            GridGeometry mapGrid = GridGeometry.Covering(Vector2.zero, SandboxFloorSizeM, nav.gridResolutionM);
            GridGeometry maskGrid = KeepOutZoneGenerator.AlignedCovering(new Rect(FuselageMockCenterM - 0.5f * FuselageMockSizeM, FuselageMockSizeM), mapGrid);
            Transform keepOutNode = GeneratorUtility.CreateNode(KeepOutZoneGenerator.NodeName, work, Vector3.zero);
            var keepOut = keepOutNode.gameObject.AddComponent<VirtualKeepOutZone>();
            keepOut.Configure(maskGrid, KeepOutZoneGenerator.RasterizeRectangle(maskGrid, FuselageMockCenterM, FuselageMockSizeM),
                new[] { KeepOutZoneGenerator.ComputeChannel(station.StationId, station.DockingPose, FuselageMockCenterM, nav, agvConfig) });

            // 升降柱联锁演示（M2 第二部分）保留但停用；导航演示（M3）为默认入口。
            var liftDemo = new GameObject("SandboxLiftDemo");
            liftDemo.transform.SetParent(system, false);
            liftDemo.AddComponent<SandboxLiftDemo>().Configure(settings, agv.GetComponent<AgvController>(), agv.GetComponent<LiftColumnController>());
            liftDemo.SetActive(false);

            var navDemoObject = new GameObject("SandboxNavDemo");
            navDemoObject.transform.SetParent(system, false);
            var navDemo = navDemoObject.AddComponent<SandboxNavDemo>();
            navDemo.Configure(settings, agvConfig, nav, agv.GetComponent<AgvController>(), station, keepOut, Vector2.zero, SandboxFloorSizeM, SandboxRouteM);
            NavigationSceneSetup.Views views = NavigationSceneSetup.Build(visualization, ui, navDemoObject, camera);
            navDemo.ConfigureViews(views.Map, views.KeepOut, views.Lidar, views.Path, views.Overlay, views.Teleop);

            Save(scene, ProjectPaths.SandboxRobotScene);
        }

        // 地面、矮围墙（激光可见）、两堆货箱（夹出 1.8 m 窄通道）与悬空的模拟机身。
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

            GameObject fuselage = GeneratorUtility.CreateBox("FuselageMock", environment,
                new Vector3(FuselageMockCenterM.x, FuselageMockCenterHeightM, FuselageMockCenterM.y),
                new Vector3(FuselageMockSizeM.x, FuselageMockSizeM.x, FuselageMockSizeM.y), MaterialLibraryBuilder.Get(MaterialLibraryBuilder.AircraftSkin));
            fuselage.layer = SimLayers.AircraftIndex;
        }

        private static InspectionStation BuildTestStation(Transform work, InspectionConfig inspection)
        {
            Transform node = GeneratorUtility.CreateNode("TestStation", work, Vector3.zero);
            var station = node.gameObject.AddComponent<InspectionStation>();
            RegionMarker marker = null;
            GameObject boardPrefab = GeneratorUtility.LoadPrefab(PropsGenerator.MarkerBoardPath);
            if (boardPrefab != null)
            {
                float yaw = SandboxDockPose.yawDeg * Mathf.Deg2Rad;
                var heading = new Vector2(Mathf.Sin(yaw), Mathf.Cos(yaw));
                Vector2 outward = SandboxDockPose.positionM.x < FuselageMockCenterM.x ? Vector2.left : Vector2.right;
                Vector2 p = SandboxDockPose.positionM + heading * inspection.dockingMarkerAheadM + outward * inspection.dockingMarkerLateralM;
                GameObject board = GeneratorUtility.InstantiatePrefab(boardPrefab, node, Vector3.zero, Quaternion.identity, "DockingMarker");
                board.transform.SetPositionAndRotation(new Vector3(p.x, 0f, p.y), Quaternion.LookRotation(-new Vector3(heading.x, 0f, heading.y), Vector3.up));
                int tagId = inspection.dockingTagIds.Length > 0 ? inspection.dockingTagIds[0] : 0;
                Transform tag = board.transform.Find("Tag");
                if (tag != null)
                {
                    Material material = AprilTagMaterialBuilder.Get(tagId);
                    if (material != null) tag.GetComponent<Renderer>().sharedMaterial = material;
                    tag.localScale = new Vector3(inspection.dockingTagSizeM, inspection.dockingTagSizeM, 1f);
                }
                marker = board.AddComponent<RegionMarker>();
                marker.Configure(tagId, inspection.dockingTagSizeM, MarkerRole.Docking,
                    tag != null ? tag.localPosition : new Vector3(0f, PropsGenerator.MarkerBoardTagHeightM, 0f));
            }
            station.Configure(SandboxStationId, 0, 0, SandboxDockPose, new Rivet[0], marker, null);
            return station;
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
