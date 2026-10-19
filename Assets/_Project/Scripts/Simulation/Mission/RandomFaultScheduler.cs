using System;
using System.Collections.Generic;

namespace RivetSim.Simulation.Mission
{
    /// <summary>
    /// 随机故障调度（M8）：按种子为每轮任务生成故障注入点，交给 <see cref="ScheduledFaultInjector"/> 在进入匹配阶段时注入。
    /// 每类故障每轮按概率发生至多一次：识别失败随机落在某个区域的 T1；力超限与采集无效随机落在不同工位（工位不足两个时为同一工位的不同铆钉）的某颗铆钉的 T5。
    /// 相同种子与任务规模得到相同的注入点，演示可复现。
    /// </summary>
    public static class RandomFaultScheduler
    {
        /// <summary>
        /// 生成注入点。plan 为任务规模（区域 → 工位 → 铆钉）；probability 为每类故障发生的概率（0～1）。
        /// </summary>
        public static List<FaultInjectionPoint> CreatePlan(IMissionPlan plan, int seed, float probability)
        {
            var points = new List<FaultInjectionPoint>();
            if (plan == null || plan.RegionCount <= 0) return points;
            var random = new Random(seed);

            // 工位全局序号与区域、工位内序号对应表（与 MissionCursor.StationGlobalIndex 的递增顺序一致）。
            var stations = new List<(int region, int station, int rivets)>();
            for (int r = 0; r < plan.RegionCount; r++)
                for (int s = 0; s < plan.StationCount(r); s++)
                    stations.Add((r, s, plan.RivetCount(r, s)));

            if (random.NextDouble() < probability)
                points.Add(new FaultInjectionPoint(FaultKind.RecognitionFailed, "T1", random.Next(plan.RegionCount), -1, -1));

            var usable = new List<int>();
            for (int i = 0; i < stations.Count; i++)
                if (stations[i].rivets > 0) usable.Add(i);
            if (usable.Count == 0) return points;

            int forceStation = -1, forceRivet = -1;
            if (random.NextDouble() < probability)
            {
                forceStation = usable[random.Next(usable.Count)];
                forceRivet = random.Next(stations[forceStation].rivets);
                points.Add(new FaultInjectionPoint(FaultKind.ContactForceExceeded, "T5", -1, forceStation, forceRivet));
            }
            if (random.NextDouble() < probability)
            {
                // 尽量与力超限落在不同工位；只有一个工位时换一颗铆钉。
                var others = usable.FindAll(i => i != forceStation);
                int station = others.Count > 0 ? others[random.Next(others.Count)] : usable[0];
                int rivets = stations[station].rivets;
                int rivet = random.Next(rivets);
                if (station == forceStation && rivets > 1 && rivet == forceRivet) rivet = (rivet + 1) % rivets;
                points.Add(new FaultInjectionPoint(FaultKind.AcquisitionInvalid, "T5", -1, station, rivet));
            }
            return points;
        }
    }
}
