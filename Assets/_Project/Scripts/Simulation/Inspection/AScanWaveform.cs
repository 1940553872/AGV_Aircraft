using System;

namespace RivetSim.Simulation.Inspection
{
    /// <summary>
    /// 合成 A 扫波形（教学简化模型）：射频波形采样（满屏高度比例，可正可负）、采样率、显示时间窗，以及界面回波时刻与名义底面声时，
    /// 供闸门判读与显示。
    /// </summary>
    public sealed class AScanWaveform
    {
        /// <summary>射频采样（满屏高度比例）。</summary>
        public float[] Samples { get; }

        /// <summary>采样率，单位 MHz。</summary>
        public float SampleRateMHz { get; }

        /// <summary>界面回波时刻，单位 µs。</summary>
        public float InterfaceTimeUs { get; }

        /// <summary>名义底面声时（2 × 名义厚度 / 声速），单位 µs。</summary>
        public float NominalRoundTripUs { get; }

        /// <summary>显示时间窗，单位 µs。</summary>
        public float WindowUs => Samples.Length / SampleRateMHz;

        /// <summary>创建波形。</summary>
        public AScanWaveform(float[] samples, float sampleRateMHz, float interfaceTimeUs, float nominalRoundTripUs)
        {
            Samples = samples ?? Array.Empty<float>();
            SampleRateMHz = sampleRateMHz;
            InterfaceTimeUs = interfaceTimeUs;
            NominalRoundTripUs = nominalRoundTripUs;
        }

        /// <summary>采样序号对应的时刻，单位 µs。</summary>
        public float TimeUs(int index) => index / SampleRateMHz;

        /// <summary>时刻对应的采样序号（限制在范围内）。</summary>
        public int IndexAt(float timeUs) => Math.Max(0, Math.Min(Samples.Length - 1, (int)Math.Round(timeUs * SampleRateMHz)));
    }
}
