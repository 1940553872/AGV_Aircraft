using System;
using System.Collections.Generic;
using RivetSim.Core.Config;
using RivetSim.Simulation.World;
using UnityEngine;

namespace RivetSim.Simulation.Inspection
{
    /// <summary>
    /// A 扫波形合成（教学简化模型，不做声场仿真，ADR-016）：高斯包络 × 中心频率余弦的脉冲叠加——界面回波、底面多次回波
    /// （声时 = 2 × 厚度 / 声速，幅值逐次按比例衰减）与缺陷回波，再乘以耦合质量并加高斯噪声。
    /// 缺陷模型：腐蚀减薄 → 厚度按剩余比例减小（底面回波提前）；孔边裂纹 → 指定深度处出现缺陷回波、底面回波减弱；
    /// 铆钉松动 → 底面回波减弱且多次回波衰减更快。参数取自 CFG_Inspection。
    /// </summary>
    public sealed class AScanSynthesizer
    {
        // 高斯谱 −6 dB 全宽与 σ 之比：2·√(2 ln 2)。
        private const float FullWidthToSigma = 2.3548f;
        // 脉冲包络截断范围（σ 的倍数）。
        private const float PulseExtentSigmas = 4f;
        private const float MmToM = 0.001f;
        private const float SToUs = 1e6f;

        private readonly InspectionConfig _config;
        private readonly System.Random _random;
        private readonly List<(float TimeUs, float Amplitude)> _echoes = new List<(float, float)>();

        /// <summary>名义底面声时，单位 µs。</summary>
        public float NominalRoundTripUs => RoundTripUs(_config.skinThicknessMm);

        /// <summary>脉冲包络 σ，单位 µs。</summary>
        public float PulseSigmaUs
        {
            get
            {
                float sigmaF = _config.pulseFractionalBandwidth * _config.probeFrequencyMHz / FullWidthToSigma;
                return 1f / (2f * Mathf.PI * sigmaF);
            }
        }

        /// <summary>创建合成器；random 为噪声随机源（固定种子）。</summary>
        public AScanSynthesizer(InspectionConfig config, System.Random random)
        {
            _config = config;
            _random = random;
        }

        /// <summary>厚度（mm）对应的往返声时，单位 µs。</summary>
        public float RoundTripUs(float thicknessMm) => 2f * thicknessMm * MmToM / _config.longitudinalVelocityMps * SToUs;

        /// <summary>按真值缺陷与耦合质量（0～1）合成一条 A 扫。</summary>
        public AScanWaveform Synthesize(DefectType defect, float coupling)
        {
            BuildEchoes(defect);
            int count = Mathf.RoundToInt(_config.aScanWindowUs * _config.aScanSampleRateMHz);
            var samples = new float[count];
            float sigma = PulseSigmaUs;
            float extent = PulseExtentSigmas * sigma;
            float omega = 2f * Mathf.PI * _config.probeFrequencyMHz;
            for (int i = 0; i < count; i++)
            {
                float t = i / _config.aScanSampleRateMHz;
                float s = 0f;
                foreach ((float t0, float a) in _echoes)
                {
                    float dt = t - t0;
                    if (dt < -extent || dt > extent) continue;
                    s += a * Mathf.Exp(-dt * dt / (2f * sigma * sigma)) * Mathf.Cos(omega * dt);
                }
                samples[i] = s * Mathf.Clamp01(coupling) + GaussianNoise.Next(_random, _config.noiseSigmaFs);
            }
            return new AScanWaveform(samples, _config.aScanSampleRateMHz, _config.interfaceEchoTimeUs, NominalRoundTripUs);
        }

        private void BuildEchoes(DefectType defect)
        {
            _echoes.Clear();
            float t0 = _config.interfaceEchoTimeUs;
            _echoes.Add((t0, _config.interfaceEchoAmplitude));
            float thickness = _config.skinThicknessMm;
            float backwall = _config.backwallEchoAmplitude;
            float decay = _config.multipleEchoDecay;
            switch (defect)
            {
                case DefectType.CorrosionThinning:
                    thickness *= _config.thinningRemainingRatio;
                    break;
                case DefectType.EdgeCrack:
                    _echoes.Add((t0 + RoundTripUs(_config.crackDepthMm), _config.crackEchoAmplitude));
                    backwall *= _config.crackBackwallFactor;
                    break;
                case DefectType.LooseRivet:
                    backwall *= _config.looseBackwallFactor;
                    decay = _config.looseMultipleEchoDecay;
                    break;
            }
            float roundTrip = RoundTripUs(thickness);
            float amplitude = backwall;
            for (float t = t0 + roundTrip; t <= _config.aScanWindowUs + PulseExtentSigmas * PulseSigmaUs; t += roundTrip)
            {
                _echoes.Add((t, amplitude));
                amplitude *= decay;
            }
        }
    }
}
