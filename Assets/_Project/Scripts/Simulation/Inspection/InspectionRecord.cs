using RivetSim.Simulation.World;

namespace RivetSim.Simulation.Inspection
{
    /// <summary>
    /// 单钉检测记录：铆钉与真值缺陷、判读结论与原因、接触力与耦合质量、对中误差、各闸门幅值、单钉节拍（T4～T6，仿真时间）与完成时刻。
    /// </summary>
    public sealed class InspectionRecord
    {
        /// <summary>铆钉 ID。</summary>
        public string RivetId { get; }

        /// <summary>工位 ID。</summary>
        public string StationId { get; }

        /// <summary>真值缺陷。</summary>
        public DefectType Truth { get; }

        /// <summary>判读结论。</summary>
        public InspectionVerdict Verdict { get; }

        /// <summary>判读原因。</summary>
        public string Reason { get; }

        /// <summary>采集时接触力，单位 N。</summary>
        public float ContactForceN { get; }

        /// <summary>耦合质量（0～1）。</summary>
        public float Coupling { get; }

        /// <summary>对中误差（探头轴线到真实钉心，蒙皮切平面内），单位 m。</summary>
        public float CenteringErrorM { get; }

        /// <summary>闸门结果。</summary>
        public GateResult Gates { get; }

        /// <summary>单钉节拍（T4 对中 → T6 回退结束），单位 s。</summary>
        public float CycleTimeS { get; set; }

        /// <summary>完成时刻（仿真时间），单位 s。</summary>
        public double TimeS { get; set; }

        /// <summary>创建记录。</summary>
        public InspectionRecord(string rivetId, string stationId, DefectType truth, InspectionVerdict verdict, string reason, float contactForceN,
            float coupling, float centeringErrorM, GateResult gates)
        {
            RivetId = rivetId;
            StationId = stationId;
            Truth = truth;
            Verdict = verdict;
            Reason = reason;
            ContactForceN = contactForceN;
            Coupling = coupling;
            CenteringErrorM = centeringErrorM;
            Gates = gates;
        }

        /// <summary>是否检出（真值有缺陷且判为缺陷或疑似）。</summary>
        public bool IsDetection => Truth != DefectType.None && (Verdict == InspectionVerdict.Defect || Verdict == InspectionVerdict.Suspect);

        /// <summary>是否误报（真值无缺陷却判为缺陷）。</summary>
        public bool IsFalseAlarm => Truth == DefectType.None && Verdict == InspectionVerdict.Defect;

        /// <summary>是否漏检（真值有缺陷却判为合格）。</summary>
        public bool IsMiss => Truth != DefectType.None && Verdict == InspectionVerdict.Pass;
    }
}
