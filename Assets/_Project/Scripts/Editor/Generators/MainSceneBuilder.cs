using System.Collections.Generic;
using System.Linq;
using RivetSim.Core.Config;
using RivetSim.Core.Logging;
using RivetSim.Editor.Tools;
using RivetSim.Presentation.App;
using RivetSim.Simulation.Navigation;
using RivetSim.Simulation.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RivetSim.Editor.Generators
{
    /// <summary>
    /// 主场景装配：新建并覆盖 Main_TeachingHall（Architecture §4），按 CFG_SceneLayout 布置厂房、三区、模型机占位体、
    /// 障碍物、充电位、演示台、检测区、虚拟禁行区、灯光、后处理、相机锚点，以及启动入口 AppBootstrap 与导航可视化（M3）、检测可视化（M4）。
    /// AGV 由 AppBootstrap 在充电位运行时生成（ADR-009 / ADR-015）。场景完全由本生成器产出，重复执行结果一致。
    /// </summary>
    public static class MainSceneBuilder
    {
        /// <summary>根节点名称（Architecture §4）。</summary>
        public static readonly string[] RootNames =
            { "_System", "Environment", "Zone_Work", "Zone_Mobility", "Zone_Demo", "Robot", "Cameras", "UI", "Visualization" };

        /// <summary>相机锚点名称前缀。</summary>
        public const string CameraAnchorPrefix = "CamAnchor_";

        /// <summary>主相机（总览机位）竖直视场角，单位 °。</summary>
        public const float MainCameraFovDeg = 60f;
        private const float OverviewHeightRatio = 0.75f;
        private const float OverviewCornerInsetM = 2f;
        private const float OverviewLookOffsetXM = -10f;
        private const float OverviewLookZM = 6f;
        private const float ChockGapM = 0.08f;
        // 末端特写锚点：看向后机身侧壁检测区附近（铆钉排离地约 2.0～2.3 m，Architecture §6.1）。
        private const float EffectorLookHeightM = 2.15f;
        private const float EffectorLookZRatio = -0.2f;

        /// <summary>生成主场景。</summary>
        [MenuItem("RivetSim/Generate/Main Teaching Hall Scene", priority = 30)]
        public static void Build()
        {
            if (!EnsureNoUnsavedScenes()) return;

            // 新建场景会卸载未被引用的资产，配置须在新建场景之后读取。
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var layout = GeneratorUtility.LoadConfig<SceneLayoutConfig>(ProjectPaths.SceneLayoutAsset);
            var agv = GeneratorUtility.LoadConfig<AgvConfig>(ProjectPaths.AgvAsset);
            if (layout == null || agv == null) return;
            var roots = RootNames.ToDictionary(n => n, n => GeneratorUtility.CreateRoot(scene, n));

            SceneLightingSetup.ApplyAmbient();
            SceneLightingSetup.CreateSun(roots["_System"]);
            SceneLightingSetup.CreateGlobalVolume(roots["_System"]);

            HallGenerator.BuildHall(roots["Environment"], layout);
            HallGenerator.BuildZoneMarkings(roots["Zone_Work"], roots["Zone_Mobility"], roots["Zone_Demo"], layout, agv);
            BuildWorkZone(roots["Zone_Work"], layout);
            BuildMobilityZone(roots["Zone_Mobility"], layout);
            BuildDemoZone(roots["Zone_Demo"], layout);
            RivetZoneGenerator.Build(scene);
            VirtualKeepOutZone keepOut = KeepOutZoneGenerator.Build(scene);

            Camera mainCamera = BuildCameras(roots["Cameras"], layout);
            BuildBootstrap(roots, keepOut, mainCamera);

            GeneratorUtility.EnsureFolder(ProjectPaths.MainScene.Substring(0, ProjectPaths.MainScene.LastIndexOf('/')));
            EditorSceneManager.SaveScene(scene, ProjectPaths.MainScene);
            EnsureFirstInBuildSettings(ProjectPaths.MainScene);

            SceneLayoutReport report = SceneLayoutValidator.Validate(layout, agv);
            SimLogLevel level = report.IsValid ? SimLogLevel.Info : SimLogLevel.Error;
            SimLog.Write(LogCategory.System, level, report.ToText());
            SimLog.Info(LogCategory.System, $"主场景已生成：{ProjectPaths.MainScene}");
        }

        /// <summary>检查已打开场景没有未保存改动；有则报错并返回 false。</summary>
        public static bool EnsureNoUnsavedScenes()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene s = SceneManager.GetSceneAt(i);
                if (!s.isDirty) continue;
                SimLog.Error(LogCategory.System, $"场景 {s.path} 有未保存改动，请先保存或放弃后再生成。");
                return false;
            }
            return true;
        }

        private static void BuildWorkZone(Transform root, SceneLayoutConfig layout)
        {
            // 有 A320 预制体时使用它（M2 第三部分），否则退回占位模型机。
            GameObject a320 = AssetDatabase.LoadAssetAtPath<GameObject>(AircraftA320Builder.PrefabPath);
            if (a320 != null)
            {
                BuildA320(root, layout, a320);
                return;
            }
            GameObject aircraftPrefab = GeneratorUtility.LoadPrefab(AircraftPlaceholderGenerator.PrefabPath);
            GameObject standPrefab = GeneratorUtility.LoadPrefab(PropsGenerator.AircraftStandPath);
            GameObject chockPrefab = GeneratorUtility.LoadPrefab(PropsGenerator.WheelChockPath);
            if (aircraftPrefab == null || standPrefab == null || chockPrefab == null) return;

            Vector3 origin = new Vector3(layout.aircraftCenterM.x, 0f, layout.aircraftCenterM.y);
            GeneratorUtility.InstantiatePrefab(aircraftPrefab, root, origin, Quaternion.identity, "Aircraft");

            Transform supports = GeneratorUtility.CreateNode("AircraftSupports", root, origin);
            foreach (Vector3 p in AircraftPlaceholderGenerator.StandPositions(layout))
                GeneratorUtility.InstantiatePrefab(standPrefab, supports, p, Quaternion.identity, "Stand");
            float chockOffset = AircraftPlaceholderGenerator.MainWheelDiameterM(layout) * 0.5f + ChockGapM;
            foreach (Vector3 wheel in AircraftPlaceholderGenerator.MainWheelPositions(layout))
            {
                GeneratorUtility.InstantiatePrefab(chockPrefab, supports, wheel + Vector3.forward * chockOffset, Quaternion.Euler(0f, 180f, 0f), "Chock");
                GeneratorUtility.InstantiatePrefab(chockPrefab, supports, wheel - Vector3.forward * chockOffset, Quaternion.identity, "Chock");
            }
        }

        // A320 根节点在主起落架中点；按实测包围盒中心对齐 CFG_SceneLayout.aircraftCenterM。真机靠起落架支撑，不放机翼支撑架，只放轮挡。
        private static void BuildA320(Transform root, SceneLayoutConfig layout, GameObject prefab)
        {
            GameObject chockPrefab = GeneratorUtility.LoadPrefab(PropsGenerator.WheelChockPath);
            GameObject aircraft = GeneratorUtility.InstantiatePrefab(prefab, root, Vector3.zero, Quaternion.identity, "Aircraft");
            AircraftMeasurement m = AircraftA320Builder.Measure(aircraft);
            Vector3 origin = new Vector3(layout.aircraftCenterM.x - m.BoundsCenterOffsetM.x, 0f, layout.aircraftCenterM.y - m.BoundsCenterOffsetM.y);
            aircraft.transform.localPosition = origin;
            if (chockPrefab == null) return;

            Transform supports = GeneratorUtility.CreateNode("AircraftSupports", root, origin);
            float chockOffset = m.MainWheelRadiusM + ChockGapM;
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 wheel = new Vector3(side * m.MainGearSideOffsetM, 0f, 0f);
                GeneratorUtility.InstantiatePrefab(chockPrefab, supports, wheel + Vector3.forward * chockOffset, Quaternion.Euler(0f, 180f, 0f), "Chock");
                GeneratorUtility.InstantiatePrefab(chockPrefab, supports, wheel - Vector3.forward * chockOffset, Quaternion.identity, "Chock");
            }
        }

        private static void BuildMobilityZone(Transform root, SceneLayoutConfig layout)
        {
            Transform obstacles = GeneratorUtility.CreateNode("Obstacles", root, Vector3.zero);
            var prefabs = new Dictionary<ObstacleKind, GameObject>
            {
                [ObstacleKind.CrateStack] = GeneratorUtility.LoadPrefab(PropsGenerator.CratePath),
                [ObstacleKind.ToolCart] = GeneratorUtility.LoadPrefab(PropsGenerator.ToolCartPath),
                [ObstacleKind.Cone] = GeneratorUtility.LoadPrefab(PropsGenerator.ConePath),
            };
            foreach (ObstacleSpec spec in layout.obstacles)
                BuildObstacle(obstacles, spec, prefabs[spec.kind]);

            GameObject dock = GeneratorUtility.LoadPrefab(PropsGenerator.ChargingDockPath);
            if (dock != null)
            {
                GeneratorUtility.InstantiatePrefab(dock, root, layout.chargingDockPose.WorldPosition,
                    layout.chargingDockPose.WorldRotation, "ChargingDock");
            }
        }

        // 障碍物根节点带一个与占地矩形一致的盒碰撞体（Obstacle 层，激光可见），外观由道具预制体缩放拼成。
        private static void BuildObstacle(Transform parent, ObstacleSpec spec, GameObject prefab)
        {
            Vector2 c = spec.footprint.centerM;
            Vector2 s = spec.footprint.sizeM;
            Transform t = GeneratorUtility.CreateNode(spec.name, parent, new Vector3(c.x, 0f, c.y));
            t.gameObject.layer = SimLayers.ObstacleIndex;
            GeneratorUtility.AddBoxCollider(t.gameObject, new Vector3(0f, spec.heightM * 0.5f, 0f), new Vector3(s.x, spec.heightM, s.y));
            if (prefab == null) return;

            // 长边统一转到本地 X 方向再摆放。
            bool alongZ = s.y > s.x;
            float longSide = alongZ ? s.y : s.x;
            float shortSide = alongZ ? s.x : s.y;
            Quaternion rotation = alongZ ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.identity;
            Transform visual = GeneratorUtility.CreateNode("Visual", t, Vector3.zero, rotation);

            switch (spec.kind)
            {
                case ObstacleKind.CrateStack:
                    int count = Mathf.Max(1, Mathf.RoundToInt(longSide / shortSide));
                    float crateLength = longSide / count;
                    for (int i = 0; i < count; i++)
                    {
                        float x = -longSide * 0.5f + crateLength * (i + 0.5f);
                        GameObject crate = GeneratorUtility.InstantiatePrefab(prefab, visual, new Vector3(x, 0f, 0f), Quaternion.identity, $"Crate_{i}");
                        crate.transform.localScale = new Vector3(crateLength, spec.heightM, shortSide);
                    }
                    break;
                case ObstacleKind.ToolCart:
                    ScaleToFit(GeneratorUtility.InstantiatePrefab(prefab, visual, Vector3.zero, Quaternion.identity, "ToolCart"),
                        PropsGenerator.ToolCartNominalSizeM, longSide, spec.heightM, shortSide);
                    break;
                case ObstacleKind.Cone:
                    ScaleToFit(GeneratorUtility.InstantiatePrefab(prefab, visual, Vector3.zero, Quaternion.identity, "Cone"),
                        PropsGenerator.ConeNominalSizeM, longSide, spec.heightM, shortSide);
                    break;
            }
        }

        private static void ScaleToFit(GameObject instance, Vector3 nominal, float x, float y, float z)
        {
            instance.transform.localScale = new Vector3(x / nominal.x, y / nominal.y, z / nominal.z);
        }

        private static void BuildDemoZone(Transform root, SceneLayoutConfig layout)
        {
            GameObject desk = GeneratorUtility.LoadPrefab(DemoStationGenerator.DeskPrefabPath);
            if (desk != null)
                GeneratorUtility.InstantiatePrefab(desk, root, layout.demoDeskPose.WorldPosition, layout.demoDeskPose.WorldRotation, "DemoStation");
            SceneLightingSetup.CreateDemoLights(root, layout.demoDeskPose);
            if (!layout.bigScreenEnabled) return;
            GameObject screen = GeneratorUtility.LoadPrefab(DemoStationGenerator.BigScreenPrefabPath);
            if (screen != null)
                GeneratorUtility.InstantiatePrefab(screen, root, layout.bigScreenPose.WorldPosition, layout.bigScreenPose.WorldRotation, "BigScreen");
        }

        // 相机锚点供 M6 CameraRig 使用：1 总览、2 跟随 AGV、3 末端特写、4 演示台。主相机放在总览机位。
        // 启动入口与导航可视化：AGV 由 AppBootstrap 运行时生成，引用全部在此写入。
        private static void BuildBootstrap(Dictionary<string, Transform> roots, VirtualKeepOutZone keepOut, Camera mainCamera)
        {
            var bootstrapObject = new GameObject("AppBootstrap");
            bootstrapObject.transform.SetParent(roots["_System"], false);
            var bootstrap = bootstrapObject.AddComponent<AppBootstrap>();
            bootstrap.ConfigureConfigs(
                GeneratorUtility.LoadConfig<SimSettings>(ProjectPaths.SimSettingsAsset),
                GeneratorUtility.LoadConfig<SceneLayoutConfig>(ProjectPaths.SceneLayoutAsset),
                GeneratorUtility.LoadConfig<AgvConfig>(ProjectPaths.AgvAsset),
                GeneratorUtility.LoadConfig<ArmConfig>(ProjectPaths.ArmAsset),
                GeneratorUtility.LoadConfig<NavigationConfig>(ProjectPaths.NavigationAsset),
                GeneratorUtility.LoadConfig<InspectionConfig>(ProjectPaths.InspectionAsset));
            Transform regions = roots["Zone_Work"].Find(RivetZoneGenerator.RootName);
            bootstrap.ConfigureScene(GeneratorUtility.LoadPrefab(AgvGenerator.AgvPrefabPath), roots["Robot"], keepOut,
                regions != null ? regions.GetComponent<RivetRegistry>() : null);
            // M6：HUD 与演示屏取代 NavOverlay / InspectionOverlay（类保留，主场景不再挂载）。
            NavigationSceneSetup.Views views = NavigationSceneSetup.Build(roots["Visualization"], roots["UI"], bootstrapObject, mainCamera, false);
            bootstrap.ConfigureViews(views.Map, views.KeepOut, views.Lidar, views.Path, views.Overlay, views.Teleop);
            NavigationSceneSetup.InspectionViews inspection = NavigationSceneSetup.BuildInspectionViews(roots["Visualization"], roots["UI"], false);
            bootstrap.ConfigureInspectionViews(inspection.Overlay, inspection.Rings, inspection.Frustum, inspection.Contact);
            DemoUiGenerator.BuildMain(roots, bootstrap, mainCamera);
        }

        private static Camera BuildCameras(Transform root, SceneLayoutConfig layout)
        {
            Vector3 aircraft = new Vector3(layout.aircraftCenterM.x, 0f, layout.aircraftCenterM.y);
            float hz = layout.hallSizeM.y * 0.5f;
            // 总览机位在西南角、顶灯与桁架下方，斜向看厂房中部，使演示区、移动区与模型机同时入画。
            float hx = layout.hallSizeM.x * 0.5f;
            Vector3 overviewPos = new Vector3(-hx + OverviewCornerInsetM, layout.hallSizeM.z * OverviewHeightRatio, -hz + OverviewCornerInsetM);
            Vector3 overviewLook = new Vector3(aircraft.x + OverviewLookOffsetXM, 0f, OverviewLookZM);

            PlanarPose start = layout.startPose;
            Vector3 agvPos = start.WorldPosition;
            Vector3 followPos = agvPos + start.WorldRotation * new Vector3(1.6f, 1.6f, -2.4f);
            Vector3 followLook = agvPos + Vector3.up * 0.6f;

            Vector3 effectorLook = aircraft + new Vector3(-layout.fuselageDiameterM * 0.5f, EffectorLookHeightM,
                layout.aircraftLengthM * EffectorLookZRatio);
            Vector3 effectorPos = effectorLook + new Vector3(-1.8f, 0.5f, 0.8f);

            // 演示台机位（M7 拉近）：位置与注视点来自 CFG_UiTheme，兼顾桌面显示器与后方大屏。
            var theme = GeneratorUtility.LoadConfig<UiThemeConfig>(ProjectPaths.UiThemeAsset);
            PlanarPose desk = layout.demoDeskPose;
            Vector3 deskPos = desk.WorldPosition + desk.WorldRotation * (theme != null ? theme.demoCameraOffsetM : new Vector3(0.4f, 1.6f, 2.4f));
            Vector3 deskLook = desk.WorldPosition + desk.WorldRotation * (theme != null ? theme.demoCameraLookOffsetM : new Vector3(0f, 1.0f, -0.6f));

            Anchor(root, "1_Overview", overviewPos, overviewLook);
            Anchor(root, "2_FollowAgv", followPos, followLook);
            Anchor(root, "3_EndEffector", effectorPos, effectorLook);
            Anchor(root, "4_DemoStation", deskPos, deskLook);
            return SceneLightingSetup.CreateCamera(root, "Main Camera", overviewPos, overviewLook, MainCameraFovDeg, true);
        }

        private static void Anchor(Transform root, string name, Vector3 position, Vector3 lookAt)
        {
            GeneratorUtility.CreateNode(CameraAnchorPrefix + name, root, position, Quaternion.LookRotation(lookAt - position, Vector3.up));
        }

        private static void EnsureFirstInBuildSettings(string path)
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != path).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
