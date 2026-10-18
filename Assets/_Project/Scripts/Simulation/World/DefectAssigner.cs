using System;

namespace RivetSim.Simulation.World
{
    /// <summary>
    /// 真值缺陷分配（教学简化模型）：按比例四舍五入确定缺陷数，用固定随机种子不重复地抽取铆钉，缺陷类型在三种中均匀随机。
    /// 同一种子与参数总得到相同结果。
    /// </summary>
    public static class DefectAssigner
    {
        /// <summary>缺陷数 = 四舍五入（铆钉数 × 比例），限制在 0～铆钉数。</summary>
        public static int DefectCount(int rivetCount, float ratio) =>
            Math.Max(0, Math.Min(rivetCount, (int)Math.Round(rivetCount * (double)ratio, MidpointRounding.AwayFromZero)));

        /// <summary>为 rivetCount 颗铆钉分配真值缺陷（按铆钉顺序返回）。</summary>
        public static DefectType[] Assign(int rivetCount, float ratio, int seed)
        {
            if (rivetCount < 0) throw new ArgumentOutOfRangeException(nameof(rivetCount));
            var result = new DefectType[rivetCount];
            var random = new Random(seed);
            var order = new int[rivetCount];
            for (int i = 0; i < rivetCount; i++) order[i] = i;
            int count = DefectCount(rivetCount, ratio);
            // 部分 Fisher–Yates：前 count 个即抽中的铆钉。
            for (int i = 0; i < count; i++)
            {
                int j = random.Next(i, rivetCount);
                (order[i], order[j]) = (order[j], order[i]);
                result[order[i]] = (DefectType)random.Next(1, 4);
            }
            return result;
        }
    }
}
