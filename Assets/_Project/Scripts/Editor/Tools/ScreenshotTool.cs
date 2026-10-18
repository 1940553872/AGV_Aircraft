using System.Collections.Generic;
using System.IO;
using RivetSim.Core.Config;
using RivetSim.Core.Logging;
using RivetSim.Editor.Generators;
using RivetSim.Simulation.Robot;
using RivetSim.Simulation.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RivetSim.Editor.Tools
{
    /// <summary>
    /// 布局验收截图：俯视总览（地面文字含三区尺寸；标注巡游路线、障碍物编号、窄通道与最小距离）、总览机位透视、
    /// 从演示区看模型机侧面、AGV 近景、演示台近景。标注只在截图时临时创建，截完即删除，不保存进场景。
    /// 输出到 Docs/Screenshots/M2/（M1 的截图保留在 Docs/Screenshots/M1/）。
    /// </summary>
    public static class ScreenshotTool
    {
        private const string OutputFolder = ProjectPaths.ScreenshotsFolder + "/M2";
        private static readonly Vector2Int TopDownSizePx = new Vector2Int(2400, 1440);
        private static readonly Vector2Int CloseUpSizePx = new Vector2Int(1920, 1080);

        // 俯视相机：高于场景、近裁剪面切掉 9.5 m 以上的桁架与顶灯（模型机尾翼顶 7.84 m）。
        private const float TopDownHeightM = 40f;
        private const float TopDownCutHeightM = 9.5f;
        private const float TopDownMarginM = 1.6f;
        // 标注画在尾翼顶与裁剪面之间，俯视时不被模型机遮挡。
        private const float AnnotationHeightM = 8.5f;
        private const float AnnotationTextHeightM = 1.0f;
        private const float RouteWidthM = 0.18f;
        // 从演示区看模型机侧面的机位。
        private const float SideViewInsetM = 1.0f;
        private const float SideViewEyeHeightM = 3.5f;
        private const float SideViewFovDeg = 35f;
        private const int TextFontSize = 96;

        private static readonly Color RouteColor = new Color(0.18f, 0.77f, 0.90f);
        private static readonly Color TextColor = new Color(1f, 0.85f, 0.3f);

        // 升降柱近景机位（相对 AGV 根节点），单位 m。
        private static readonly Vector3 LiftViewOffsetM = new Vector3(2.7f, 2.0f, 3.3f);
        private static readonly Vector3 LiftViewTargetM = new Vector3(0f, 1.5f, 0.1f);
        private const float LiftViewFovDeg = 50f;

        /// <summary>升降柱最低位与最高位两张近景（需打开 Sandbox_Robot），截完复位到最低位。</summary>
        [MenuItem("RivetSim/Debug/Capture Lift Screenshots")]
        public static void CaptureLift() => CaptureLift("06_Lift_Lowest.png", "07_Lift_Highest.png");

        /// <summary>AGV 带 UR5e 的升降柱最低位 / 最高位近景（M2 第三部分 3A，需打开 Sandbox_Robot）。</summary>
        [MenuItem("RivetSim/Debug/Capture UR5e Lift Screenshots")]
        public static void CaptureUr5eLift() => CaptureLift("11a_AGV_UR5e_Lowest.png", "11b_AGV_UR5e_Highest.png");

        /// <summary>A320 与材质验收截图（M2 第三部分 3A，需打开主场景）：总览、从演示区看机身侧面、演示区材质近景。</summary>
        [MenuItem("RivetSim/Debug/Capture Asset Import Screenshots")]
        public static void CaptureAssetImport()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ProjectPaths.MainScene)
            {
                SimLog.Error(LogCategory.System, $"请先打开 {ProjectPaths.MainScene}。");
                return;
            }
            var layout = GeneratorUtility.LoadConfig<SceneLayoutConfig>(ProjectPaths.SceneLayoutAsset);
            if (layout == null) return;
            bool wasDirty = scene.isDirty;
            Directory.CreateDirectory(OutputFolder);
            var temp = new List<Object>();
            try
            {
                CaptureOverview(scene, temp, "08_A320_Overview.png");
                CaptureAircraftSide(layout, temp, "09_A320_Side.png");
                CaptureDemoStation(layout, temp, "12_Materials.png");
            }
            finally
            {
                foreach (Object o in temp)
                {
                    if (o != null) Object.DestroyImmediate(o);
                }
                if (!wasDirty) EditorSceneManager.SaveScene(scene);
            }
            SimLog.Info(LogCategory.System, $"资产导入截图已输出到 {OutputFolder}/");
        }

        // 检测区截图机位：工位近景（相对 AGV 停靠位姿）、铆钉特写（相对铆钉排中心，沿法向外移）、工位俯视（相对模型机根节点）。
        private static readonly Vector3 StationViewOffsetM = new Vector3(-2.6f, 2.3f, -2.4f);
        private static readonly Vector3 StationViewTargetM = new Vector3(0.6f, 1.6f, 0.2f);
        private const float StationViewFovDeg = 50f;
        private const float RivetViewDistanceM = 0.55f;
        private const float RivetViewFovDeg = 40f;
        private const float StationsTopDownHalfHeightM = 4.5f;
        private const float DefectMarkerDiameterM = 0.012f;

        /// <summary>检测区验收截图（M2 第三部分 3B，需打开主场景并已生成检测区）：工位近景、铆钉特写、工位俯视。</summary>
        [MenuItem("RivetSim/Debug/Capture Rivet Zone Screenshots")]
        public static void CaptureRivetZones()
        {
            Scene scene = SceneManager.GetActiveScene();
            Transform work = GeneratorUtility.FindRoot(scene, "Zone_Work");
            RivetRegistry registry = work != null ? work.Find(RivetZoneGenerator.RootName)?.GetComponent<RivetRegistry>() : null;
            Transform aircraft = work != null ? work.Find("Aircraft") : null;
            var agvConfig = GeneratorUtility.LoadConfig<AgvConfig>(ProjectPaths.AgvAsset);
            var armConfig = GeneratorUtility.LoadConfig<ArmConfig>(ProjectPaths.ArmAsset);
            GameObject agvPrefab = GeneratorUtility.LoadPrefab(AgvGenerator.AgvPrefabPath);
            if (registry == null || aircraft == null || agvConfig == null || armConfig == null || agvPrefab == null)
            {
                SimLog.Error(LogCategory.System, "请先打开主场景并生成检测区。");
                return;
            }
            bool wasDirty = scene.isDirty;
            Directory.CreateDirectory(OutputFolder);
            var temp = new List<Object>();
            try
            {
                InspectionStation first = registry.Regions[0].Stations[0];
                Rivet middle = first.Rivets[first.Rivets.Count / 2];

                // 13：AGV 停在 R1-S1，升降柱取规划高度，探头压在中间一颗铆钉上。
                GameObject agv = TempInstance(agvPrefab, temp);
                bool posed = ReachabilityChecker.PoseAtRivet(agv, first, middle, agvConfig, armConfig);
                Transform dock = agv.transform;
                Vector3 position = dock.TransformPoint(StationViewOffsetM);
                Vector3 target = dock.TransformPoint(StationViewTargetM);
                Render(CreateTempCamera(temp, position, Quaternion.LookRotation(target - position), StationViewFovDeg), CloseUpSizePx, "13_Station.png");
                Object.DestroyImmediate(agv);

                // 14：R1-S1 铆钉排与基准标记特写，缺陷钉叠加红色圆片（仅截图时）。
                var red = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = Color.red };
                temp.Add(red);
                foreach (Rivet rivet in registry.GetAllRivets())
                {
                    if (rivet.Defect == DefectType.None) continue;
                    GameObject mark = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    mark.hideFlags = HideFlags.DontSave;
                    temp.Add(mark);
                    Object.DestroyImmediate(mark.GetComponent<Collider>());
                    mark.transform.SetPositionAndRotation(rivet.PositionM + rivet.Normal * 0.001f, Quaternion.FromToRotation(Vector3.up, rivet.Normal));
                    mark.transform.localScale = new Vector3(DefectMarkerDiameterM, 0.0005f, DefectMarkerDiameterM);
                    mark.GetComponent<Renderer>().sharedMaterial = red;
                }
                Vector3 rowCenter = (first.Rivets[0].PositionM + first.Rivets[first.Rivets.Count - 1].PositionM) * 0.5f;
                Vector3 look = rowCenter + aircraft.forward * 0.05f;
                Vector3 eye = look + middle.Normal * RivetViewDistanceM;
                Render(CreateTempCamera(temp, eye, Quaternion.LookRotation(look - eye, Vector3.up), RivetViewFovDeg), CloseUpSizePx, "14_Rivets.png");

                // 15：4 个工位各放一台 AGV（升降柱降到最低），从正上方俯视后机身。
                foreach (InspectionRegion region in registry.Regions)
                {
                    foreach (InspectionStation station in region.Stations)
                    {
                        GameObject copy = TempInstance(agvPrefab, temp);
                        copy.transform.SetPositionAndRotation(station.DockingPose.WorldPosition, station.DockingPose.WorldRotation);
                    }
                }
                Vector3 center = (registry.Regions[0].Stations[0].DockingPose.WorldPosition + registry.Regions[registry.Regions.Count - 1].Stations[0].DockingPose.WorldPosition) * 0.5f;
                Camera top = CreateTempCamera(temp, center + Vector3.up * TopDownHeightM, Quaternion.Euler(90f, aircraft.eulerAngles.y, 0f), 30f);
                top.orthographic = true;
                top.orthographicSize = StationsTopDownHalfHeightM;
                top.nearClipPlane = 1f;
                top.farClipPlane = TopDownHeightM + 1f;
                Render(top, CloseUpSizePx, "15_Stations_TopDown.png");
                SimLog.Info(LogCategory.System, $"检测区截图已输出到 {OutputFolder}/（13 号图探头逆解{(posed ? "成功" : "失败")}）");
            }
            finally
            {
                foreach (Object o in temp)
                {
                    if (o != null) Object.DestroyImmediate(o);
                }
                if (!wasDirty) EditorSceneManager.SaveScene(scene);
            }
        }

        private static GameObject TempInstance(GameObject prefab, List<Object> temp)
        {
            var go = (GameObject)Object.Instantiate(prefab);
            go.hideFlags = HideFlags.DontSave;
            temp.Add(go);
            return go;
        }

        private static void CaptureLift(string lowestFile, string highestFile)
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ProjectPaths.SandboxRobotScene)
            {
                SimLog.Error(LogCategory.System, $"请先打开 {ProjectPaths.SandboxRobotScene}。");
                return;
            }
            Transform robot = GeneratorUtility.FindRoot(scene, "Robot");
            Transform agv = robot != null ? robot.Find("AGV") : null;
            LiftColumnController lift = agv != null ? agv.GetComponent<LiftColumnController>() : null;
            if (lift == null)
            {
                SimLog.Error(LogCategory.System, "Sandbox_Robot 中没有带 LiftColumnController 的 AGV。");
                return;
            }

            bool wasDirty = scene.isDirty;
            Directory.CreateDirectory(OutputFolder);
            var temp = new List<Object>();
            try
            {
                Vector3 position = agv.TransformPoint(LiftViewOffsetM);
                Vector3 target = agv.TransformPoint(LiftViewTargetM);
                Camera camera = CreateTempCamera(temp, position, Quaternion.LookRotation(target - position), LiftViewFovDeg);
                lift.SetExtensionImmediate(0f);
                Render(camera, CloseUpSizePx, lowestFile);
                SimLog.Info(LogCategory.System, $"最低位：安装面离地 {lift.MountHeightM:0.000} m");
                lift.SetExtensionImmediate(lift.Config.liftStrokeM);
                Render(camera, CloseUpSizePx, highestFile);
                SimLog.Info(LogCategory.System, $"最高位：安装面离地 {lift.MountHeightM:0.000} m");
            }
            finally
            {
                lift.SetExtensionImmediate(0f);
                foreach (Object o in temp)
                {
                    if (o != null) Object.DestroyImmediate(o);
                }
                if (!wasDirty) EditorSceneManager.SaveScene(scene);
            }
            SimLog.Info(LogCategory.System, $"升降柱截图已输出到 {OutputFolder}/");
        }

        /// <summary>生成 5 张验收截图（需打开主场景）。</summary>
        [MenuItem("RivetSim/Debug/Capture Layout Screenshots")]
        public static void CaptureAll()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ProjectPaths.MainScene)
            {
                SimLog.Error(LogCategory.System, $"请先打开 {ProjectPaths.MainScene}。");
                return;
            }
            var layout = GeneratorUtility.LoadConfig<SceneLayoutConfig>(ProjectPaths.SceneLayoutAsset);
            var agv = GeneratorUtility.LoadConfig<AgvConfig>(ProjectPaths.AgvAsset);
            if (layout == null || agv == null) return;

            bool wasDirty = scene.isDirty;
            Directory.CreateDirectory(OutputFolder);
            var temp = new List<Object>();
            try
            {
                Transform annotations = BuildAnnotations(layout, agv, temp);
                CaptureTopDown(layout, temp);
                Object.DestroyImmediate(annotations.gameObject);
                CaptureOverview(scene, temp, "02_Overview.png");
                CaptureAircraftSide(layout, temp, "03_AircraftSide.png");
                CaptureAgv(scene, temp);
                CaptureDemoStation(layout, temp, "05_DemoStation.png");
            }
            finally
            {
                foreach (Object o in temp)
                {
                    if (o != null) Object.DestroyImmediate(o);
                }
                if (!wasDirty) EditorSceneManager.SaveScene(scene);
            }
            SimLog.Info(LogCategory.System, $"布局截图已输出到 {OutputFolder}/");
        }

        private static void CaptureTopDown(SceneLayoutConfig layout, List<Object> temp)
        {
            Camera camera = CreateTempCamera(temp, new Vector3(0f, TopDownHeightM, 0f), Quaternion.Euler(90f, 0f, 0f), 30f);
            camera.orthographic = true;
            camera.orthographicSize = layout.hallSizeM.y * 0.5f + TopDownMarginM;
            camera.nearClipPlane = TopDownHeightM - TopDownCutHeightM;
            camera.farClipPlane = TopDownHeightM + 1f;
            Render(camera, TopDownSizePx, "01_TopDown.png");
        }

        private static void CaptureOverview(Scene scene, List<Object> temp, string fileName)
        {
            Transform cameras = GeneratorUtility.FindRoot(scene, "Cameras");
            Transform anchor = cameras != null ? cameras.Find(MainSceneBuilder.CameraAnchorPrefix + "1_Overview") : null;
            if (anchor == null)
            {
                SimLog.Error(LogCategory.System, "场景中没有总览相机锚点，跳过总览透视图。");
                return;
            }
            Camera camera = CreateTempCamera(temp, anchor.position, anchor.rotation, MainSceneBuilder.MainCameraFovDeg);
            Render(camera, CloseUpSizePx, fileName);
        }

        private static void CaptureAircraftSide(SceneLayoutConfig layout, List<Object> temp, string fileName)
        {
            Vector3 target = new Vector3(layout.aircraftCenterM.x, layout.fuselageCenterlineHeightM, layout.aircraftCenterM.y);
            Vector3 position = new Vector3(layout.demoZone.MaxM.x + SideViewInsetM, SideViewEyeHeightM, layout.aircraftCenterM.y);
            Camera camera = CreateTempCamera(temp, position, Quaternion.LookRotation(target - position), SideViewFovDeg);
            Render(camera, CloseUpSizePx, fileName);
        }

        private static void CaptureAgv(Scene scene, List<Object> temp)
        {
            // AGV 由 AppBootstrap 运行时生成（M3），截图时在起始位临时放一台。
            var layout = GeneratorUtility.LoadConfig<SceneLayoutConfig>(ProjectPaths.SceneLayoutAsset);
            GameObject prefab = GeneratorUtility.LoadPrefab(AgvGenerator.AgvPrefabPath);
            if (layout == null || prefab == null) return;
            Transform agv = TempInstance(prefab, temp).transform;
            agv.SetPositionAndRotation(layout.startPose.WorldPosition, layout.startPose.WorldRotation);
            Vector3 target = agv.TransformPoint(new Vector3(0f, 0.55f, 0.05f));
            Vector3 position = agv.TransformPoint(new Vector3(1.5f, 1.05f, 1.9f));
            Camera camera = CreateTempCamera(temp, position, Quaternion.LookRotation(target - position), 35f);
            Render(camera, CloseUpSizePx, "04_AGV.png");
        }

        private static void CaptureDemoStation(SceneLayoutConfig layout, List<Object> temp, string fileName)
        {
            PlanarPose desk = layout.demoDeskPose;
            Vector3 position = desk.WorldPosition + desk.WorldRotation * new Vector3(1.5f, 1.75f, 2.3f);
            Vector3 target = desk.WorldPosition + desk.WorldRotation * new Vector3(-0.1f, 1.0f, -0.9f);
            Camera camera = CreateTempCamera(temp, position, Quaternion.LookRotation(target - position), 50f);
            Render(camera, CloseUpSizePx, fileName);
        }

        private static Camera CreateTempCamera(List<Object> temp, Vector3 position, Quaternion rotation, float fovDeg)
        {
            var go = new GameObject("__ScreenshotCamera") { hideFlags = HideFlags.DontSave };
            temp.Add(go);
            go.transform.SetPositionAndRotation(position, rotation);
            var camera = go.AddComponent<Camera>();
            SceneLightingSetup.ConfigureCamera(camera, fovDeg);
            camera.enabled = false;
            return camera;
        }

        private static void Render(Camera camera, Vector2Int size, string fileName)
        {
            var rt = new RenderTexture(size.x, size.y, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            camera.targetTexture = rt;
            camera.Render();

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            var image = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0);
            image.Apply();
            RenderTexture.active = previous;

            camera.targetTexture = null;
            File.WriteAllBytes($"{OutputFolder}/{fileName}", image.EncodeToPNG());
            Object.DestroyImmediate(image);
            rt.Release();
            Object.DestroyImmediate(rt);
        }

        // 俯视标注：巡游路线、障碍物编号、窄通道宽度、最小距离与大厅尺寸。数值来自 SceneLayoutValidator。
        private static Transform BuildAnnotations(SceneLayoutConfig layout, AgvConfig agv, List<Object> temp)
        {
            SceneLayoutReport report = SceneLayoutValidator.Validate(layout, agv);
            var root = new GameObject("__ScreenshotAnnotations") { hideFlags = HideFlags.DontSave };
            Transform t = root.transform;
            var font = AssetDatabase.LoadAssetAtPath<Font>(ProjectPaths.NotoSansScBold);

            var lineMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = RouteColor };
            temp.Add(lineMaterial);
            var line = new GameObject("Route") { hideFlags = HideFlags.DontSave };
            line.transform.SetParent(t, false);
            var renderer = line.AddComponent<LineRenderer>();
            renderer.sharedMaterial = lineMaterial;
            renderer.widthMultiplier = RouteWidthM;
            renderer.positionCount = layout.mappingRouteM.Length;
            for (int i = 0; i < layout.mappingRouteM.Length; i++)
            {
                Vector2 p = layout.mappingRouteM[i];
                renderer.SetPosition(i, new Vector3(p.x, AnnotationHeightM, p.y));
            }

            float hz = layout.hallSizeM.y * 0.5f, y = AnnotationHeightM, h = AnnotationTextHeightM;
            foreach (ObstacleSpec spec in layout.obstacles)
            {
                Vector2 c = spec.footprint.centerM;
                string code = spec.name.Split(' ')[0];
                Text(t, font, code, new Vector3(c.x, y, c.y + spec.footprint.sizeM.y * 0.5f + h * 0.6f), h * 0.8f);
            }

            // 三区尺寸已烘焙在地面区域文字中（ZoneLabelBaker），此处不重复标注。
            Vector2 passage = (layout.obstacles[0].footprint.centerM + layout.obstacles[1].footprint.centerM) * 0.5f;
            float passageLabelZ = layout.obstacles[1].footprint.MaxM.y + h * 1.8f;
            Text(t, font, $"窄通道 {report.NarrowestPassageM:0.00} m", new Vector3(passage.x, y, passageLabelZ), h * 0.8f);
            Vector2 nose = layout.aircraftCenterM + new Vector2(0f, layout.aircraftLengthM * 0.5f);
            Text(t, font, $"巡游时 AGV 距机身 ≥ {report.RouteToFuselageM:0.00} m", new Vector3(nose.x, y, nose.y + h * 1.5f), h * 0.8f);
            Vector2 north = (layout.mappingRouteM[4] + layout.mappingRouteM[5]) * 0.5f;
            Text(t, font, $"巡游路线 {report.RouteLengthM:0.0} m", new Vector3(north.x, y, north.y - h * 1.2f), h * 0.8f);
            Text(t, font, $"大厅 {layout.hallSizeM.x:0} × {layout.hallSizeM.y:0} × {layout.hallSizeM.z:0} m（X 向右，Z 向上，机头朝 +Z）",
                new Vector3(0f, y, hz + TopDownMarginM * 0.5f), h);
            Text(t, font, $"模型机到障碍物 ≥ {report.AircraftToObstacleM:0.00} m　模型机到作业区边界 ≥ {report.AircraftToWorkZoneEdgeM:0.00} m　" +
                $"充电位到作业区 {report.ChargingDockToWorkZoneM:0.0} m　观众区进深 {report.DemoAudienceDepthM:0.0} m",
                new Vector3(0f, y, -hz - TopDownMarginM * 0.5f), h * 0.8f);
            return t;
        }

        private static void Text(Transform parent, Font font, string text, Vector3 position, float heightM)
        {
            var go = new GameObject($"Text_{text}") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(90f, 0f, 0f));
            var mesh = go.AddComponent<TextMesh>();
            mesh.font = font;
            mesh.fontSize = TextFontSize;
            // TextMesh 世界行高约为 characterSize × fontSize / 10。
            mesh.characterSize = heightM * 10f / TextFontSize;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = TextColor;
            mesh.text = text;
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        }
    }
}
