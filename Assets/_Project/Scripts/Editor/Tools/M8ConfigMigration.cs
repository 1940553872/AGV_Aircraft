using System.Text;
using RivetSim.Core.Config;
using RivetSim.Core.Logging;
using RivetSim.Editor.Generators;
using UnityEditor;
using UnityEngine;

namespace RivetSim.Editor.Tools
{
    /// <summary>
    /// M8 配置迁移：把 v1.1 的参数写入现有配置资产（倍速 ×8、演示台与显示器尺寸、场景明度）。
    /// 新增字段由类型默认值带入；这里只改写已存在字段的值。重复执行结果相同。调整前后的对照见 <see cref="ChangeTable"/>（写入 Docs/Reports/M8.md）。
    /// </summary>
    public static class M8ConfigMigration
    {
        private static readonly float[] SpeedOptions = { 0.5f, 1f, 2f, 4f, 8f };
        private static readonly Vector3 DeskSizeM = new Vector3(2.00f, 0.80f, 0.80f);
        private const float MonitorCenterHeightM = 1.22f;
        private const float SunIntensity = 1.9f;
        private static readonly Color AmbientSky = new Color(0.86f, 0.89f, 0.93f);
        private static readonly Color AmbientEquator = new Color(0.62f, 0.65f, 0.70f);
        private static readonly Color AmbientGround = new Color(0.34f, 0.36f, 0.39f);
        private static readonly Color CameraBackground = new Color(0.78f, 0.81f, 0.85f);
        private const float BloomThreshold = 1.3f;
        private const float DemoKeyLightIntensity = 22f;

        // 调整前（M7 验收值）与调整后，供报告对照。
        private static readonly string[,] Changes =
        {
            { "CFG_SimSettings.speedOptions", "0.5 / 1 / 2 / 4", "0.5 / 1 / 2 / 4 / 8" },
            { "CFG_SceneLayout.demoDeskSizeM", "1.60 × 0.80 × 0.75 m", "2.00 × 0.80 × 0.80 m" },
            { "CFG_SceneLayout.monitorCenterHeightM", "1.10 m", "1.22 m" },
            { "CFG_SceneLayout.monitorScale（新增）", "—（1.0）", "1.4" },
            { "CFG_RenderLook.sunIntensity", "1.6", "1.9" },
            { "CFG_RenderLook.ambientSky", "(0.74, 0.79, 0.85)", "(0.86, 0.89, 0.93)" },
            { "CFG_RenderLook.ambientEquator", "(0.46, 0.50, 0.56)", "(0.62, 0.65, 0.70)" },
            { "CFG_RenderLook.ambientGround", "(0.22, 0.24, 0.27)", "(0.34, 0.36, 0.39)" },
            { "CFG_RenderLook.cameraBackground（透过屋顶桁架可见）", "(0.10, 0.12, 0.15)", "(0.78, 0.81, 0.85)" },
            { "CFG_RenderLook.postExposureEv（新增）", "0 EV", "+0.3 EV" },
            { "CFG_RenderLook.bloomThreshold", "1.1", "1.3（避免提亮后屏幕文字泛光）" },
            { "CFG_RenderLook.demoKeyLightIntensity", "28", "22（曝光提高后防止桌面过曝）" },
            { "墙面（M_Wall）", "基础色 (0.55, 0.60, 0.66) × 波纹钢板贴图，金属度贴图", "贴图按平均色定标到 wallTargetColor #E0E3E8，金属度 0.1、光滑度 0.3" },
            { "屋顶内侧（桁架、檩条）", "M_Steel #2F3A46", "M_Roof_Inner roofInnerColor #D9DDE2" },
            { "演示屏材质 M_DemoScreen / M_DemoScreenStatus", "基础色 1.0", "基础色 2^−0.3 ≈ 0.81（抵消曝光补偿，屏幕文字亮度不变）" },
        };

        /// <summary>写入 v1.1 参数。</summary>
        [MenuItem("RivetSim/Config/Apply M8 Values", priority = 60)]
        public static void Apply()
        {
            var sim = GeneratorUtility.LoadConfig<SimSettings>(ProjectPaths.SimSettingsAsset);
            var layout = GeneratorUtility.LoadConfig<SceneLayoutConfig>(ProjectPaths.SceneLayoutAsset);
            var look = GeneratorUtility.LoadConfig<RenderLookConfig>(ProjectPaths.RenderLookAsset);
            if (sim == null || layout == null || look == null) return;

            sim.speedOptions = (float[])SpeedOptions.Clone();
            EditorUtility.SetDirty(sim);

            layout.demoDeskSizeM = DeskSizeM;
            layout.monitorCenterHeightM = MonitorCenterHeightM;
            EditorUtility.SetDirty(layout);

            look.sunIntensity = SunIntensity;
            look.ambientSky = AmbientSky;
            look.ambientEquator = AmbientEquator;
            look.ambientGround = AmbientGround;
            look.cameraBackground = CameraBackground;
            look.bloomThreshold = BloomThreshold;
            look.demoKeyLightIntensity = DemoKeyLightIntensity;
            EditorUtility.SetDirty(look);

            AssetDatabase.SaveAssets();
            SimLog.Info(LogCategory.System, "M8 配置已写入：倍速 ×8、演示台 2.00 m、显示器 ×1.4、场景明度参数。");
        }

        /// <summary>调整前后参数对照（Markdown 表格）。</summary>
        public static string ChangeTable()
        {
            var sb = new StringBuilder();
            sb.AppendLine("| 参数 | 调整前（M7） | 调整后（M8） |");
            sb.AppendLine("|---|---|---|");
            for (int i = 0; i < Changes.GetLength(0); i++) sb.AppendLine($"| {Changes[i, 0]} | {Changes[i, 1]} | {Changes[i, 2]} |");
            return sb.ToString();
        }
    }
}
