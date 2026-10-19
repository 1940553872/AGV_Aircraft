using System;
using System.Collections.Generic;

namespace RivetSim.Simulation.Mission
{
    /// <summary>
    /// 阶段内单元的前后依赖（M5 已定决策 3，见 Docs/Architecture.md §5.1）：未列出的单元在阶段开始时并行启动；
    /// 列出的单元等全部前置单元结束后才启动，任一前置失败则跳过。依赖只表达设备互斥与数据先后，不改变 JSON 中的流程。
    /// 规则：同一设备的动作串行（机械臂、底盘）；T5 为 S3 力控接触 → S4 采集 → S5 有效性 → S8 保存。
    /// </summary>
    public static class PhaseDependencyTable
    {
        private static readonly Dictionary<string, string[]> Predecessors = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            // T1：确认运输姿态 → 底盘运动许可 → 全局导航；调度下发任务后才出发。
            ["t(1,6)"] = new[] { "t(1,3)" },
            ["t(1,1)"] = new[] { "t(1,6)", "t(1,7)" },
            // T2：运输姿态 → 底盘运动许可 → 对接停车；停车后记录轨迹。
            ["t(2,6)"] = new[] { "t(2,3)" },
            ["t(2,1)"] = new[] { "t(2,6)" },
            ["t(2,8)"] = new[] { "t(2,1)" },
            // T3：零速锁定 → 机械臂运动许可 → 升柱并到观察位姿 → 基准注册 → 保存注册。
            ["t(3,6)"] = new[] { "t(3,1)" },
            ["t(3,3)"] = new[] { "t(3,6)" },
            ["t(3,2)"] = new[] { "t(3,3)" },
            ["t(3,8)"] = new[] { "t(3,2)" },
            // T4：资源锁 → 近距离限速 → 机械臂规划与视觉修正（钉心复测在机械臂到位后由执行器内部衔接）；复测后保存图像与位姿。
            ["t(4,6)"] = new[] { "t(4,7)" },
            ["t(4,3)"] = new[] { "t(4,6)" },
            ["t(4,8)"] = new[] { "t(4,2)" },
            // T5：S3 力控接触 → S4 接触采集 → S5 有效性 → S8 保存。
            ["t(5,4)"] = new[] { "t(5,3)" },
            ["t(5,5)"] = new[] { "t(5,4)" },
            ["t(5,8)"] = new[] { "t(5,5)" },
            // T6：回退（末颗收臂降柱）→ 释放确认（需结论）→ 核验提交 → 提交记录；回退后发放释放许可 → 倒车换站；基准复测在回退后用机械臂。
            ["t(6,2)"] = new[] { "t(6,3)" },
            ["t(6,4)"] = new[] { "t(6,3)", "t(6,5)" },
            ["t(6,6)"] = new[] { "t(6,3)" },
            ["t(6,1)"] = new[] { "t(6,6)", "t(6,2)" },
            ["t(6,7)"] = new[] { "t(6,4)" },
            ["t(6,8)"] = new[] { "t(6,7)" },
        };

        /// <summary>单元的前置单元 id（无依赖时为空）。</summary>
        public static IReadOnlyList<string> PredecessorsOf(string cellId) =>
            Predecessors.TryGetValue(cellId ?? "", out string[] list) ? list : Array.Empty<string>();

        /// <summary>全部有依赖的单元 id。</summary>
        public static IEnumerable<string> DependentCells => Predecessors.Keys;
    }
}
