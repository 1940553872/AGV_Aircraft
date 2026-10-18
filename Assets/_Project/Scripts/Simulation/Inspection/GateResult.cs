namespace RivetSim.Simulation.Inspection
{
    /// <summary>
    /// 闸门判读结果（教学简化模型）：结论与原因、三个闸门的时间范围（显示用，单位 µs）、各闸门内的回波幅值（满屏高度比例）与缺陷回波深度。
    /// </summary>
    public readonly struct GateResult
    {
        /// <summary>结论。</summary>
        public readonly InspectionVerdict Verdict;

        /// <summary>判读原因。</summary>
        public readonly string Reason;

        /// <summary>闸门 A（界面）起止时刻，单位 µs。</summary>
        public readonly float GateAStartUs, GateAEndUs;

        /// <summary>闸门 B（缺陷）起止时刻，单位 µs。</summary>
        public readonly float GateBStartUs, GateBEndUs;

        /// <summary>底面闸门起止时刻，单位 µs。</summary>
        public readonly float BackwallStartUs, BackwallEndUs;

        /// <summary>界面回波幅值。</summary>
        public readonly float InterfaceAmplitude;

        /// <summary>闸门 B 内最大回波峰值（无峰值为 0）。</summary>
        public readonly float FlawAmplitude;

        /// <summary>闸门 B 内最大回波峰值时刻，单位 µs。</summary>
        public readonly float FlawTimeUs;

        /// <summary>底面闸门内回波幅值。</summary>
        public readonly float BackwallAmplitude;

        /// <summary>缺陷回波深度（无缺陷回波为 0），单位 mm。</summary>
        public readonly float FlawDepthMm;

        /// <summary>创建结果。</summary>
        public GateResult(InspectionVerdict verdict, string reason, float gateAStartUs, float gateAEndUs, float gateBStartUs, float gateBEndUs,
            float backwallStartUs, float backwallEndUs, float interfaceAmplitude, float flawAmplitude, float flawTimeUs, float backwallAmplitude,
            float flawDepthMm)
        {
            Verdict = verdict;
            Reason = reason;
            GateAStartUs = gateAStartUs;
            GateAEndUs = gateAEndUs;
            GateBStartUs = gateBStartUs;
            GateBEndUs = gateBEndUs;
            BackwallStartUs = backwallStartUs;
            BackwallEndUs = backwallEndUs;
            InterfaceAmplitude = interfaceAmplitude;
            FlawAmplitude = flawAmplitude;
            FlawTimeUs = flawTimeUs;
            BackwallAmplitude = backwallAmplitude;
            FlawDepthMm = flawDepthMm;
        }
    }
}
