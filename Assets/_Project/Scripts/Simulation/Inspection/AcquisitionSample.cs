using RivetSim.Simulation.World;

namespace RivetSim.Simulation.Inspection
{
    /// <summary>
    /// 一次 T5 接触采集的结果（M5 把 T5 拆为 S3 接触 → S4 采集 → S5 有效性 → S8 保存，各执行器经此传递数据）。
    /// A 扫与接触力均为教学简化模型。
    /// </summary>
    public sealed class AcquisitionSample
    {
        /// <summary>被采集的铆钉。</summary>
        public Rivet Rivet { get; }

        /// <summary>采集开始时的接触力，单位 N。</summary>
        public float ForceStartN { get; }

        /// <summary>采集结束时的接触力，单位 N。</summary>
        public float ForceEndN { get; }

        /// <summary>耦合质量（0～1）。</summary>
        public float Coupling { get; }

        /// <summary>保持接触的时长，单位 s。</summary>
        public float DurationS { get; }

        /// <summary>A 扫波形。</summary>
        public AScanWaveform Waveform { get; }

        /// <summary>闸门判读。</summary>
        public GateResult Gates { get; }

        /// <summary>创建结果。</summary>
        public AcquisitionSample(Rivet rivet, float forceStartN, float forceEndN, float coupling, float durationS, AScanWaveform waveform, GateResult gates)
        {
            Rivet = rivet;
            ForceStartN = forceStartN;
            ForceEndN = forceEndN;
            Coupling = coupling;
            DurationS = durationS;
            Waveform = waveform;
            Gates = gates;
        }
    }
}
