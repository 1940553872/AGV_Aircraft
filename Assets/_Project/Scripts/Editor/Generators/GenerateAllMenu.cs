using RivetSim.Core.Logging;
using RivetSim.Editor.Tools;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace RivetSim.Editor.Generators
{
    /// <summary>
    /// 按依赖顺序执行全部 M1 生成器，最后打开主场景。
    /// </summary>
    public static class GenerateAllMenu
    {
        /// <summary>依次执行：材质库 → 地面文字 → 道具 → 模型机占位 → 探头末端 → 占位臂与 AGV → 演示台 → 沙盒场景 → 主场景。</summary>
        [MenuItem("RivetSim/Generate/All", priority = 0)]
        public static void GenerateAll()
        {
            if (!MainSceneBuilder.EnsureNoUnsavedScenes()) return;

            MaterialLibraryBuilder.Build();
            ZoneLabelBaker.Build();
            PropsGenerator.Build();
            AircraftPlaceholderGenerator.Build();
            EndEffectorGenerator.Build();
            AgvGenerator.Build();
            DemoStationGenerator.Build();
            SandboxSceneBuilder.Build();
            MainSceneBuilder.Build();

            EditorSceneManager.OpenScene(ProjectPaths.MainScene, OpenSceneMode.Single);
            AssetDatabase.SaveAssets();
            SimLog.Info(LogCategory.System, "M1 全部生成完成。");
        }
    }
}
