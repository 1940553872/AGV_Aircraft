using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace RivetSim.Editor.Tools
{
    /// <summary>
    /// v1.1 Windows 64 位独立程序构建：入口场景 Main_TeachingHall，输出 Builds/v1.1/（不入库）。
    /// 结果写入 Builds/v1.1/build_summary.txt。帧率复测用启动参数 -perfRun（见 BuildPerfProbe）。
    /// </summary>
    public static class BuildV11
    {
        private const string Version = "1.1.0";
        private const string ScenePath = "Assets/_Project/Scenes/Main_TeachingHall.unity";
        private const string OutDir = "Builds/v1.1";
        private const string ExeName = "AGV_Aircraft.exe";

        [MenuItem("RivetSim/Build/Windows v1.1")]
        public static void Build()
        {
            PlayerSettings.bundleVersion = Version;
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = Path.Combine(OutDir, ExeName),
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary s = report.summary;

            var sb = new StringBuilder();
            sb.AppendLine($"结果：{s.result}");
            sb.AppendLine($"版本：{Version}  平台：{s.platform}  后端：{PlayerSettings.GetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone)}");
            sb.AppendLine($"用时：{s.totalTime.TotalSeconds:0.0} s  大小：{s.totalSize / (1024.0 * 1024.0):0.0} MB");
            sb.AppendLine($"错误：{s.totalErrors}  警告：{s.totalWarnings}");
            foreach (BuildStep step in report.steps)
            foreach (BuildStepMessage m in step.messages)
                if (m.type == LogType.Error || m.type == LogType.Exception)
                    sb.AppendLine($"- [{m.type}] {m.content}");
            Directory.CreateDirectory(OutDir);
            File.WriteAllText(Path.Combine(OutDir, "build_summary.txt"), sb.ToString());
        }
    }
}
