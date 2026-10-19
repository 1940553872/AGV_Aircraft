using RivetSim.Core.Config;
using RivetSim.Core.Logging;
using RivetSim.Editor.Tools;
using UnityEditor;
using UnityEngine;

namespace RivetSim.Editor.Generators
{
    /// <summary>
    /// 演示台生成器：演示台、主机（S8 数据设备）、双显示器、键鼠、座椅，以及独立的大屏预制体。
    /// 尺寸取自 CFG_SceneLayout 演示区参数。演示台根节点在桌面中心的地面投影，本地 +Z 为显示器屏幕朝向（操作员一侧）。
    /// 屏幕网格命名为 Screen_*，使用 M6 演示屏 RenderTexture 材质（DemoUiGenerator.EnsureScreenMaterial）。
    /// </summary>
    public static class DemoStationGenerator
    {
        /// <summary>演示台预制体路径。</summary>
        public const string DeskPrefabPath = ProjectPaths.PrefabsDemoStation + "/PF_DemoStation.prefab";

        /// <summary>大屏预制体路径。</summary>
        public const string BigScreenPrefabPath = ProjectPaths.PrefabsDemoStation + "/PF_BigScreen.prefab";

        private const float InchToM = 0.0254f;

        // 外观尺寸，单位 m / °。
        private const float DeskTopThicknessM = 0.04f;
        private const float PanelThicknessM = 0.04f;
        private const float BezelM = 0.012f;
        private const float MonitorDepthM = 0.03f;
        private const float MonitorGapM = 0.02f;
        private const float MonitorToeInDeg = 12f;
        private const float MonitorSetbackM = 0.18f;
        private const float BigScreenBezelM = 0.03f;
        private const float BigScreenDepthM = 0.06f;

        /// <summary>生成演示台与大屏预制体。</summary>
        [MenuItem("RivetSim/Generate/Demo Station", priority = 17)]
        public static void Build()
        {
            var layout = GeneratorUtility.LoadConfig<SceneLayoutConfig>(ProjectPaths.SceneLayoutAsset);
            if (layout == null) return;
            BuildDesk(layout);
            BuildBigScreen(layout);
            SimLog.Info(LogCategory.System, "演示台与大屏已生成。");
        }

        /// <summary>显示器可视尺寸（宽, 高），单位 m。</summary>
        public static Vector2 MonitorSizeM(SceneLayoutConfig layout)
        {
            float diagonal = layout.monitorDiagonalInch * InchToM;
            float k = diagonal / layout.monitorAspect.magnitude;
            return layout.monitorAspect * k;
        }

        private static void BuildDesk(SceneLayoutConfig layout)
        {
            Material deskMat = MaterialLibraryBuilder.Get(MaterialLibraryBuilder.Desk);
            Material top = MaterialLibraryBuilder.Get(MaterialLibraryBuilder.DeskTop);
            Material bezel = MaterialLibraryBuilder.Get(MaterialLibraryBuilder.Bezel);
            Material screen = DemoUiGenerator.EnsureScreenMaterial();
            Material dark = MaterialLibraryBuilder.Get(MaterialLibraryBuilder.RobotDark);
            Material accent = MaterialLibraryBuilder.Get(MaterialLibraryBuilder.RobotAccent);
            Material indicator = MaterialLibraryBuilder.Get(MaterialLibraryBuilder.IndicatorOff);

            var root = new GameObject("PF_DemoStation");
            Transform t = root.transform;
            Vector3 desk = SizeAxes.LengthAlongX(layout.demoDeskSizeM);
            float topY = desk.y;

            GeneratorUtility.CreateBox("DeskTop", t, new Vector3(0f, topY - DeskTopThicknessM * 0.5f, 0f),
                new Vector3(desk.x, DeskTopThicknessM, desk.z), top, true);
            foreach (float side in new[] { -1f, 1f })
            {
                GeneratorUtility.CreateBox(side < 0f ? "Leg_L" : "Leg_R", t,
                    new Vector3(side * (desk.x * 0.5f - PanelThicknessM), (topY - DeskTopThicknessM) * 0.5f, 0f),
                    new Vector3(PanelThicknessM, topY - DeskTopThicknessM, desk.z - 0.1f), deskMat, true);
            }
            GeneratorUtility.CreateBox("ModestyPanel", t, new Vector3(0f, topY * 0.55f, -desk.z * 0.5f + 0.08f),
                new Vector3(desk.x - PanelThicknessM * 4f, topY * 0.5f, 0.02f), deskMat);
            GeneratorUtility.CreateBox("EdgeAccent", t, new Vector3(0f, topY - DeskTopThicknessM * 0.5f, desk.z * 0.5f + 0.003f),
                new Vector3(desk.x, 0.012f, 0.006f), accent);

            // 显示器：并排、轻微内收，屏幕中心高度取配置。
            Vector2 size = MonitorSizeM(layout);
            int count = Mathf.Max(1, layout.monitorCount);
            float pitch = size.x + BezelM * 2f + MonitorGapM;
            for (int i = 0; i < count; i++)
            {
                float offset = (i - (count - 1) * 0.5f) * pitch;
                float toeIn = count > 1 ? -Mathf.Sign(offset) * MonitorToeInDeg : 0f;
                string name = count == 2 ? (i == 0 ? "Monitor_Left" : "Monitor_Right") : $"Monitor_{i}";
                Transform monitor = GeneratorUtility.CreateNode(name, t,
                    new Vector3(offset, layout.monitorCenterHeightM, -desk.z * 0.5f + MonitorSetbackM), Quaternion.Euler(0f, toeIn, 0f));
                GeneratorUtility.CreateBox("Bezel", monitor, Vector3.zero, new Vector3(size.x + BezelM * 2f, size.y + BezelM * 2f, MonitorDepthM), bezel);
                GeneratorUtility.CreateScreenQuad(name.Replace("Monitor", "Screen"), monitor, new Vector3(0f, 0f, MonitorDepthM * 0.5f + 0.001f), size, screen);
                float neckBottom = topY - monitor.localPosition.y;
                GeneratorUtility.CreateBox("Neck", monitor, new Vector3(0f, neckBottom * 0.5f, -MonitorDepthM), new Vector3(0.05f, -neckBottom, 0.03f), dark);
                GeneratorUtility.CreateBox("Foot", monitor, new Vector3(0f, neckBottom + 0.006f, -MonitorDepthM), new Vector3(0.24f, 0.012f, 0.18f), dark);
            }

            GeneratorUtility.CreateBox("Keyboard", t, new Vector3(0f, topY + 0.01f, desk.z * 0.5f - 0.18f), new Vector3(0.44f, 0.02f, 0.14f), dark);
            GeneratorUtility.CreateBox("Mouse", t, new Vector3(0.32f, topY + 0.01f, desk.z * 0.5f - 0.18f), new Vector3(0.06f, 0.02f, 0.1f), dark);

            Transform pc = GeneratorUtility.CreateNode("Device_S8_PC", t, new Vector3(desk.x * 0.5f - 0.25f, 0f, -0.05f));
            GeneratorUtility.CreateBox("Box", pc, new Vector3(0f, 0.225f, 0f), new Vector3(0.2f, 0.45f, 0.45f), dark);
            GeneratorUtility.CreateBox("Indicator", pc, new Vector3(0f, 0.4f, 0.226f), new Vector3(0.12f, 0.012f, 0.004f), indicator);

            Transform chair = GeneratorUtility.CreateNode("Chair", t, new Vector3(-0.35f, 0f, desk.z * 0.5f + 0.35f));
            GeneratorUtility.CreateBox("Seat", chair, new Vector3(0f, 0.47f, 0f), new Vector3(0.46f, 0.06f, 0.44f), dark);
            GeneratorUtility.CreateBox("Back", chair, new Vector3(0f, 0.78f, 0.21f), new Vector3(0.44f, 0.5f, 0.05f), dark);
            GeneratorUtility.CreateCylinder("Column", chair, new Vector3(0f, 0.25f, 0f), Vector3.up, 0.05f, 0.4f, bezel);
            GeneratorUtility.CreateBox("Base", chair, new Vector3(0f, 0.03f, 0f), new Vector3(0.6f, 0.04f, 0.06f), bezel);
            GeneratorUtility.CreateBox("Base2", chair, new Vector3(0f, 0.03f, 0f), new Vector3(0.06f, 0.04f, 0.6f), bezel);

            GeneratorUtility.SetLayerRecursive(root, SimLayers.ObstacleIndex);
            GeneratorUtility.SavePrefab(root, DeskPrefabPath);
            DeviceIndicatorGenerator.ApplyToDemoStation();
        }

        private static void BuildBigScreen(SceneLayoutConfig layout)
        {
            Material bezel = MaterialLibraryBuilder.Get(MaterialLibraryBuilder.Bezel);
            Material screen = DemoUiGenerator.EnsureScreenMaterial();
            Material dark = MaterialLibraryBuilder.Get(MaterialLibraryBuilder.RobotDark);

            var root = new GameObject("PF_BigScreen");
            Vector2 size = layout.bigScreenSizeM;
            float centerY = layout.bigScreenBottomHeightM + size.y * 0.5f;
            GeneratorUtility.CreateBox("WallBracket", root.transform, new Vector3(0f, centerY, -BigScreenDepthM),
                new Vector3(size.x * 0.4f, size.y * 0.4f, BigScreenDepthM), dark);
            GeneratorUtility.CreateBox("Bezel", root.transform, new Vector3(0f, centerY, 0f),
                new Vector3(size.x + BigScreenBezelM * 2f, size.y + BigScreenBezelM * 2f, BigScreenDepthM), bezel);
            GeneratorUtility.CreateScreenQuad("Screen_Big", root.transform, new Vector3(0f, centerY, BigScreenDepthM * 0.5f + 0.001f), size, screen);
            GeneratorUtility.SetLayerRecursive(root, SimLayers.EnvironmentIndex);
            GeneratorUtility.SavePrefab(root, BigScreenPrefabPath);
        }
    }
}
