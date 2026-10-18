using RivetSim.Core.Logging;
using RivetSim.Editor.Tools;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace RivetSim.Editor.Generators
{
    /// <summary>
    /// 按依赖顺序执行全部生成器，最后打开主场景。第三方资产须先执行 RivetSim/Import/Third Party Assets。
    /// </summary>
    public static class GenerateAllMenu
    {
        /// <summary>依次执行：材质库 → Poly Haven 材质 → 地面文字 → 道具 → 模型机占位 → A320 → 探头末端 → UR5e → 占位臂与 AGV → 演示台 → 沙盒场景 → UI 字体 → 主场景。</summary>
        [MenuItem("RivetSim/Generate/All", priority = 0)]
        public static void GenerateAll()
        {
            if (!MainSceneBuilder.EnsureNoUnsavedScenes()) return;

            ProjectSetupMenu.CreateDefaultConfigs();
            MaterialLibraryBuilder.Build();
            PolyHavenMaterialBuilder.Build();
            ZoneLabelBaker.Build();
            PropsGenerator.Build();
            AircraftPlaceholderGenerator.Build();
            AircraftA320Builder.Build();
            EndEffectorGenerator.Build();
            UR5eArmGenerator.Build();
            AgvGenerator.Build();
            DemoStationGenerator.Build();
            SandboxSceneBuilder.Build();
            UiFontAssetBuilder.Build();
            MainSceneBuilder.Build();

            EditorSceneManager.OpenScene(ProjectPaths.MainScene, OpenSceneMode.Single);
            AssetDatabase.SaveAssets();
            SimLog.Info(LogCategory.System, "全部生成完成。");
        }
    }
}
