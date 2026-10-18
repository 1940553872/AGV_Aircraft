using System.Collections.Generic;
using System.IO;
using RivetSim.Core.Config;
using RivetSim.Core.Logging;
using RivetSim.Editor.Generators;
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
                CaptureOverview(scene, temp);
                CaptureAircraftSide(layout, temp);
                CaptureAgv(scene, temp);
                CaptureDemoStation(layout, temp);
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

        private static void CaptureOverview(Scene scene, List<Object> temp)
        {
            Transform cameras = GeneratorUtility.FindRoot(scene, "Cameras");
            Transform anchor = cameras != null ? cameras.Find(MainSceneBuilder.CameraAnchorPrefix + "1_Overview") : null;
            if (anchor == null)
            {
                SimLog.Error(LogCategory.System, "场景中没有总览相机锚点，跳过总览透视图。");
                return;
            }
            Camera camera = CreateTempCamera(temp, anchor.position, anchor.rotation, MainSceneBuilder.MainCameraFovDeg);
            Render(camera, CloseUpSizePx, "02_Overview.png");
        }

        private static void CaptureAircraftSide(SceneLayoutConfig layout, List<Object> temp)
        {
            Vector3 target = new Vector3(layout.aircraftCenterM.x, layout.fuselageCenterlineHeightM, layout.aircraftCenterM.y);
            Vector3 position = new Vector3(layout.demoZone.MaxM.x + SideViewInsetM, SideViewEyeHeightM, layout.aircraftCenterM.y);
            Camera camera = CreateTempCamera(temp, position, Quaternion.LookRotation(target - position), SideViewFovDeg);
            Render(camera, CloseUpSizePx, "03_AircraftSide.png");
        }

        private static void CaptureAgv(Scene scene, List<Object> temp)
        {
            Transform robot = GeneratorUtility.FindRoot(scene, "Robot");
            Transform agv = robot != null ? robot.Find(MainSceneBuilder.AgvPreviewName) : null;
            if (agv == null)
            {
                SimLog.Error(LogCategory.System, "场景中没有 AGV_Preview，跳过 AGV 近景。");
                return;
            }
            Vector3 target = agv.TransformPoint(new Vector3(0f, 0.55f, 0.05f));
            Vector3 position = agv.TransformPoint(new Vector3(1.5f, 1.05f, 1.9f));
            Camera camera = CreateTempCamera(temp, position, Quaternion.LookRotation(target - position), 35f);
            Render(camera, CloseUpSizePx, "04_AGV.png");
        }

        private static void CaptureDemoStation(SceneLayoutConfig layout, List<Object> temp)
        {
            PlanarPose desk = layout.demoDeskPose;
            Vector3 position = desk.WorldPosition + desk.WorldRotation * new Vector3(1.5f, 1.75f, 2.3f);
            Vector3 target = desk.WorldPosition + desk.WorldRotation * new Vector3(-0.1f, 1.0f, -0.9f);
            Camera camera = CreateTempCamera(temp, position, Quaternion.LookRotation(target - position), 50f);
            Render(camera, CloseUpSizePx, "05_DemoStation.png");
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
