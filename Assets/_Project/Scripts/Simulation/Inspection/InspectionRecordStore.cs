using System.Collections.Generic;
using System.Text;
using RivetSim.Simulation.World;

namespace RivetSim.Simulation.Inspection
{
    /// <summary>
    /// 检测记录（主体 S8，M4 只存内存；CSV 导出在 M5）：按完成顺序保存，并按工位统计检出、漏检、误报、疑似、无效与平均节拍。
    /// </summary>
    public sealed class InspectionRecordStore
    {
        private readonly List<InspectionRecord> _records = new List<InspectionRecord>();

        /// <summary>全部记录。</summary>
        public IReadOnlyList<InspectionRecord> Records => _records;

        /// <summary>追加一条记录。</summary>
        public void Add(InspectionRecord record) => _records.Add(record);

        /// <summary>清空。</summary>
        public void Clear() => _records.Clear();

        /// <summary>某工位（stationId 为空时为全部）的统计文字。</summary>
        public string Summarize(string stationId)
        {
            int count = 0, truth = 0, detected = 0, missed = 0, falseAlarm = 0, suspect = 0, invalid = 0;
            float cycleSum = 0f, cycleMax = 0f, forceMin = float.MaxValue, forceMax = 0f, centerMax = 0f;
            var types = new StringBuilder();
            foreach (InspectionRecord r in _records)
            {
                if (!string.IsNullOrEmpty(stationId) && r.StationId != stationId) continue;
                count++;
                if (r.Truth != DefectType.None) truth++;
                if (r.IsDetection) detected++;
                if (r.IsMiss) missed++;
                if (r.IsFalseAlarm) falseAlarm++;
                if (r.Verdict == InspectionVerdict.Suspect) suspect++;
                if (r.Verdict == InspectionVerdict.Invalid) invalid++;
                cycleSum += r.CycleTimeS;
                if (r.CycleTimeS > cycleMax) cycleMax = r.CycleTimeS;
                if (r.ContactForceN < forceMin) forceMin = r.ContactForceN;
                if (r.ContactForceN > forceMax) forceMax = r.ContactForceN;
                if (r.CenteringErrorM > centerMax) centerMax = r.CenteringErrorM;
                if (r.Truth != DefectType.None) types.Append($"{r.RivetId} 真值 {r.Truth} → {r.Verdict}（{r.Reason}）；");
            }
            if (count == 0) return "无检测记录";
            return $"{(string.IsNullOrEmpty(stationId) ? "全部" : stationId)}：{count} 颗，真值缺陷 {truth} 颗，检出 {detected}、漏检 {missed}、误报 {falseAlarm}、" +
                   $"疑似 {suspect}、无效 {invalid}；平均单钉节拍 {cycleSum / count:0.00} s（最长 {cycleMax:0.00} s）；" +
                   $"接触力 {forceMin:0.00}～{forceMax:0.00} N；最大对中误差 {centerMax * 1000f:0.00} mm\n  {types}";
        }

        /// <summary>某工位的平均单钉节拍，单位 s（无记录为 0）。</summary>
        public float MeanCycleTimeS(string stationId)
        {
            float sum = 0f;
            int n = 0;
            foreach (InspectionRecord r in _records)
            {
                if (!string.IsNullOrEmpty(stationId) && r.StationId != stationId) continue;
                sum += r.CycleTimeS;
                n++;
            }
            return n > 0 ? sum / n : 0f;
        }
    }
}
