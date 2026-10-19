using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using RivetSim.Simulation.World;
using UnityEngine;

namespace RivetSim.Simulation.Inspection
{
    /// <summary>
    /// 检测记录 CSV 导出（M5）：区域、工位、铆钉 ID、位置、真值缺陷、判读结论、接触力、耦合质量、时间戳；UTF-8（带 BOM，便于 Excel 打开中文）。
    /// </summary>
    public static class InspectionCsvExporter
    {
        /// <summary>表头。</summary>
        public const string Header = "区域,工位,铆钉ID,位置X_m,位置Y_m,位置Z_m,真值缺陷,判读结论,接触力_N,耦合质量,时间戳_s";

        /// <summary>生成 CSV 文本（registry 用于查区域与位置）。</summary>
        public static string Build(IReadOnlyList<InspectionRecord> records, RivetRegistry registry)
        {
            CultureInfo inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder(Header).Append('\n');
            foreach (InspectionRecord r in records)
            {
                Rivet rivet = registry != null ? registry.FindRivet(r.RivetId) : null;
                string region = rivet != null && rivet.RegionIndex < registry.Regions.Count ? registry.Regions[rivet.RegionIndex].RegionId : "";
                Vector3 p = rivet != null ? rivet.PositionM : Vector3.zero;
                sb.Append(region).Append(',')
                  .Append(r.StationId).Append(',')
                  .Append(r.RivetId).Append(',')
                  .Append(p.x.ToString("0.0000", inv)).Append(',')
                  .Append(p.y.ToString("0.0000", inv)).Append(',')
                  .Append(p.z.ToString("0.0000", inv)).Append(',')
                  .Append(r.Truth).Append(',')
                  .Append(r.Verdict).Append(',')
                  .Append(r.ContactForceN.ToString("0.00", inv)).Append(',')
                  .Append(r.Coupling.ToString("0.000", inv)).Append(',')
                  .Append(r.TimeS.ToString("0.00", inv)).Append('\n');
            }
            return sb.ToString();
        }

        /// <summary>写入文件（自动创建目录），返回完整路径。</summary>
        public static string Write(string path, IReadOnlyList<InspectionRecord> records, RivetRegistry registry)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(path, Build(records, registry), new UTF8Encoding(true));
            return Path.GetFullPath(path);
        }
    }
}
