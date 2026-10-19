using System;
using System.Collections.Generic;
using RivetSim.Simulation.Mission;

namespace RivetSim.Presentation.App
{
    /// <summary>
    /// 启动参数覆盖（仅编辑器验证工具在进入 Play 前设置；正常运行时为空）：快速推进、计划故障注入、完成回调。
    /// </summary>
    public static class MissionLaunchOptions
    {
        /// <summary>是否按 CFG_SimSettings.fastVerifySimSecondsPerFrame 快速推进。</summary>
        public static bool FastForward { get; set; }

        /// <summary>计划故障注入点。</summary>
        public static List<FaultInjectionPoint> Faults { get; } = new List<FaultInjectionPoint>();

        /// <summary>流程结束（含失败）后调用。</summary>
        public static Action<MissionRunReport> Completed { get; set; }

        /// <summary>流程开始后调用（参数为事件总线所在的运行时与流程）。</summary>
        public static Action<RobotRuntime, MissionRoutine> Started { get; set; }

        /// <summary>是否设置了任何覆盖。</summary>
        public static bool IsActive => FastForward || Faults.Count > 0 || Completed != null || Started != null;

        /// <summary>清除全部覆盖。</summary>
        public static void Clear()
        {
            FastForward = false;
            Faults.Clear();
            Completed = null;
            Started = null;
        }
    }
}
