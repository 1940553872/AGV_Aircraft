using System.Collections.Generic;
using RivetSim.Simulation.Inspection;
using RivetSim.Simulation.Mission;

namespace RivetSim.Presentation.App
{
    /// <summary>
    /// 一次完整工序运行的汇总（MissionRoutine 产生；Verify 菜单据此写 Docs/Reports/M5.md）。时间均为仿真时间，单位 s。
    /// </summary>
    public sealed class MissionRunReport
    {
        /// <summary>任务矩阵流程结果。</summary>
        public MissionResult Result { get; internal set; }

        /// <summary>是否整体成功（建图、矩阵流程、返回充电位均完成）。</summary>
        public bool Success { get; internal set; }

        /// <summary>失败说明。</summary>
        public string FailureReason { get; internal set; } = "";

        /// <summary>建图准备用时。</summary>
        public double MappingS { get; internal set; }

        /// <summary>任务矩阵流程用时。</summary>
        public double MatrixS { get; internal set; }

        /// <summary>返回充电位用时。</summary>
        public double ReturnS { get; internal set; }

        /// <summary>总用时。</summary>
        public double TotalS { get; internal set; }

        /// <summary>检测记录。</summary>
        public IReadOnlyList<InspectionRecord> Records { get; internal set; } = new List<InspectionRecord>();

        /// <summary>CSV 路径（persistentDataPath/Reports）。</summary>
        public string CsvPath { get; internal set; } = "";

        /// <summary>JSON 中的执行器键名数。</summary>
        public int ExecutorKeyCount { get; internal set; }

        /// <summary>启动核对发现的问题（缺失键名等）。</summary>
        public IReadOnlyList<string> ValidationProblems { get; internal set; } = new List<string>();

        /// <summary>条件判定次数与未满足次数。</summary>
        public int ConditionChecks { get; internal set; }

        /// <summary>条件未满足次数。</summary>
        public int ConditionFailures { get; internal set; }

        /// <summary>运行记录。</summary>
        public MissionTrace Trace { get; internal set; }
    }
}
