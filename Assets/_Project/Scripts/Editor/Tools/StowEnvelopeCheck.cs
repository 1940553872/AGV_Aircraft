using System.IO;
using System.Text;
using RivetSim.Core.Config;
using RivetSim.Core.Logging;
using RivetSim.Editor.Generators;
using RivetSim.Simulation.Robot;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RivetSim.Editor.Tools
{
    /// <summary>
    /// 运输姿态 Stow 外廓校核（M4，ADR-016）：在预览场景中放一台 PF_AGV（升降柱最低位、机械臂 Stow），用网格顶点量测
    /// 机械臂水平投影是否在车体 1.20 × 0.80 m 投影内、整机最高点是否 ≤ 移动时高度上限，以及连杆自干涉距离（非相邻连杆）；
    /// 同时给出零位作对比。结果写入 Docs/Reference/M4_StowEnvelope.md。
    /// </summary>
    public static class StowEnvelopeCheck
    {
        private const string ReportPath = "Docs/Reference/M4_StowEnvelope.md";
        // 自干涉最小距离要求，单位 m（计划经用户确认）。
        private const float SelfClearanceMinM = 0.01f;

        /// <summary>校核结果。</summary>
        public readonly struct Result
        {
            /// <summary>Stow 外廓。</summary>
            public readonly AgvEnvelope Stow;
            /// <summary>零位外廓（对比）。</summary>
            public readonly AgvEnvelope Zero;
            /// <summary>Stow 自干涉最小距离，单位 m。</summary>
            public readonly float SelfDistanceM;
            /// <summary>最近的连杆对。</summary>
            public readonly string SelfPair;
            /// <summary>是否全部合格。</summary>
            public readonly bool Passed;

            /// <summary>创建结果。</summary>
            public Result(AgvEnvelope stow, AgvEnvelope zero, float selfDistanceM, string selfPair, bool passed)
            {
                Stow = stow;
                Zero = zero;
                SelfDistanceM = selfDistanceM;
                SelfPair = selfPair;
                Passed = passed;
            }
        }

        /// <summary>执行校核并写报告。</summary>
        [MenuItem("RivetSim/Debug/Check Stow Envelope")]
        public static void Run()
        {
            var agv = GeneratorUtility.LoadConfig<AgvConfig>(ProjectPaths.AgvAsset);
            GameObject prefab = GeneratorUtility.LoadPrefab(AgvGenerator.AgvPrefabPath);
            if (agv == null || prefab == null) return;
            Result r = Measure(prefab, agv);
            var sb = new StringBuilder();
            sb.Append("# M4 运输姿态 Stow 外廓校核（自动生成，RivetSim/Debug/Check Stow Envelope）\n\n");
            sb.Append("方法：PF_AGV 放在原点（车体坐标 x 右、y 上、z 前），升降柱最低位，统计全部网格顶点。");
            sb.Append($"要求：机械臂水平投影在车体 {agv.bodySizeM.x:0.00} × {agv.bodySizeM.y:0.00} m 投影内（横向 ±{0.5f * agv.bodySizeM.y:0.00} m、纵向 ±{0.5f * agv.bodySizeM.x:0.00} m），");
            sb.Append($"整机最高点 ≤ {agv.transportHeightMaxM:0.00} m，非相邻连杆自干涉距离 ≥ {SelfClearanceMinM * 1000f:0} mm。\n\n");
            sb.Append($"- Stow：{r.Stow}\n- 零位（对比）：{r.Zero}\n");
            sb.Append($"- Stow 自干涉最小距离：{r.SelfDistanceM * 1000f:0.0} mm（{r.SelfPair}）\n");
            sb.Append($"- 车体外接圆（导航膨胀半径）：{agv.BodyCircumscribedRadiusM:0.000} m\n");
            sb.Append($"- 结论：{(r.Passed ? "合格" : "不合格")}\n");
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath) ?? ".");
            File.WriteAllText(ReportPath, sb.ToString(), new UTF8Encoding(false));
            SimLog.Write(LogCategory.System, r.Passed ? SimLogLevel.Info : SimLogLevel.Error, sb.ToString());
        }

        /// <summary>量测 PF_AGV 在 Stow 与零位时的外廓（预览场景中进行，不改动当前场景）。</summary>
        public static Result Measure(GameObject agvPrefab, AgvConfig agv)
        {
            var preview = EditorSceneManager.NewPreviewScene();
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(agvPrefab, preview);
            try
            {
                instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                instance.GetComponent<LiftColumnController>().SetExtensionImmediate(0f);
                ArmController arm = instance.GetComponentInChildren<ArmController>();
                arm.SetJointsImmediate(new float[arm.Rig.JointCount]);
                AgvEnvelope zero = AgvFootprint.Measure(instance.transform, arm.transform, true);
                arm.SetJointsImmediate(arm.NamedPose(ArmConfig.StowPoseName));
                AgvEnvelope stow = AgvFootprint.Measure(instance.transform, arm.transform, true);
                float self = ArmClearance.MinSelfDistanceM(arm.Rig, out string pair);
                bool passed = stow.ArmInsideBody(agv.bodySizeM.y, agv.bodySizeM.x) && stow.TopM <= agv.transportHeightMaxM && self >= SelfClearanceMinM;
                return new Result(stow, zero, self, pair, passed);
            }
            finally
            {
                Object.DestroyImmediate(instance);
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }
    }
}
