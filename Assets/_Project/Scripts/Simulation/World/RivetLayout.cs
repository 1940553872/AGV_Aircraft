using System;

namespace RivetSim.Simulation.World
{
    /// <summary>
    /// 检测区排布计算（纯函数，单位 m）：沿机身轴线（模型机本地 Z，机头为 +）的工位中心与铆钉偏移，以及 ID 命名。
    /// </summary>
    public static class RivetLayout
    {
        /// <summary>区域内各工位中心相对区域中心的偏移，按机头方向在前排序（如 2 个工位、间距 1.5 → +0.75, −0.75）。</summary>
        public static float[] StationOffsetsM(int stationCount, float spacingM)
        {
            if (stationCount <= 0) throw new ArgumentOutOfRangeException(nameof(stationCount));
            var offsets = new float[stationCount];
            for (int i = 0; i < stationCount; i++) offsets[i] = (stationCount - 1) * 0.5f * spacingM - i * spacingM;
            return offsets;
        }

        /// <summary>工位内各铆钉相对工位中心的偏移，沿机头方向递增、居中排列。</summary>
        public static float[] RivetOffsetsM(int rivetCount, float pitchM)
        {
            if (rivetCount <= 0) throw new ArgumentOutOfRangeException(nameof(rivetCount));
            var offsets = new float[rivetCount];
            for (int i = 0; i < rivetCount; i++) offsets[i] = (i - (rivetCount - 1) * 0.5f) * pitchM;
            return offsets;
        }

        /// <summary>区域 ID（从 1 起）。</summary>
        public static string RegionId(int regionIndex) => $"R{regionIndex + 1}";

        /// <summary>工位 ID。</summary>
        public static string StationId(int regionIndex, int stationIndex) => $"{RegionId(regionIndex)}-S{stationIndex + 1}";

        /// <summary>铆钉 ID。</summary>
        public static string RivetId(int regionIndex, int stationIndex, int rivetIndex) =>
            $"{StationId(regionIndex, stationIndex)}-{rivetIndex + 1:00}";
    }
}
