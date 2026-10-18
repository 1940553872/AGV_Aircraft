using RivetSim.Core.Config;
using UnityEngine;

namespace RivetSim.Simulation.Inspection
{
    /// <summary>
    /// 闸门判读（教学简化模型，ADR-016）：先对射频波形全波整流并按一个周期滑动平均得到包络，再按三个闸门判读——
    /// 闸门 A（界面回波）低于阈值 → 无效；闸门 B（界面盲区之后到底面闸门之前）内出现不低于阈值的回波峰 → 缺陷（给出深度）；
    /// 底面闸门（名义声时 ±比例）内回波低于缺陷 / 疑似阈值 → 缺陷 / 疑似；否则合格。闸门 B 只认包络的局部峰值，
    /// 避免把底面回波的前沿当成缺陷回波。
    /// </summary>
    public sealed class GateJudge
    {
        private readonly InspectionConfig _config;

        /// <summary>创建判读器。</summary>
        public GateJudge(InspectionConfig config) => _config = config;

        /// <summary>按包络判读。</summary>
        public GateResult Judge(AScanWaveform waveform)
        {
            float[] env = Envelope(waveform, _config.probeFrequencyMHz, out int halfPeriod);
            float ti = waveform.InterfaceTimeUs;
            float t = waveform.NominalRoundTripUs;
            float hw = _config.backwallGateHalfWidthRatio;
            float aStart = ti - _config.flawGateDeadZoneUs, aEnd = ti + _config.flawGateDeadZoneUs;
            float bStart = aEnd, bEnd = ti + t * (1f - hw);
            float wStart = bEnd, wEnd = ti + t * (1f + hw);

            float interfaceAmp = MaxIn(env, waveform, aStart, aEnd, out _);
            float flawAmp = MaxPeakIn(env, waveform, bStart, bEnd, halfPeriod, out float flawTime);
            float backwallAmp = MaxIn(env, waveform, wStart, wEnd, out _);
            float depthMm = flawAmp > 0f ? 0.5f * (flawTime - ti) * 1e-6f * _config.longitudinalVelocityMps * 1000f : 0f;

            InspectionVerdict verdict;
            string reason;
            if (interfaceAmp < _config.gateAThresholdRatio)
            {
                verdict = InspectionVerdict.Invalid;
                reason = $"界面回波 {interfaceAmp:P0} 低于闸门 A {_config.gateAThresholdRatio:P0}（耦合不良）";
            }
            else if (flawAmp >= _config.gateBThresholdRatio)
            {
                verdict = InspectionVerdict.Defect;
                reason = $"闸门 B 内回波 {flawAmp:P0}（深度 {depthMm:0.00} mm）";
            }
            else if (backwallAmp < _config.backwallDefectRatio)
            {
                verdict = InspectionVerdict.Defect;
                reason = $"底面回波 {backwallAmp:P0} 低于 {_config.backwallDefectRatio:P0}";
            }
            else if (backwallAmp < _config.backwallSuspectRatio)
            {
                verdict = InspectionVerdict.Suspect;
                reason = $"底面回波 {backwallAmp:P0} 偏低（{_config.backwallDefectRatio:P0}～{_config.backwallSuspectRatio:P0}）";
            }
            else
            {
                verdict = InspectionVerdict.Pass;
                reason = $"底面回波 {backwallAmp:P0}，闸门 B 无回波";
            }
            return new GateResult(verdict, reason, aStart, aEnd, bStart, bEnd, wStart, wEnd, interfaceAmp, flawAmp, flawTime, backwallAmp, depthMm);
        }

        /// <summary>包络：全波整流后按一个周期滑动平均，再乘 π/2 还原峰值。halfPeriod 为半周期采样数。</summary>
        public static float[] Envelope(AScanWaveform waveform, float frequencyMHz, out int halfPeriod)
        {
            float[] s = waveform.Samples;
            int period = Mathf.Max(1, Mathf.RoundToInt(waveform.SampleRateMHz / frequencyMHz));
            halfPeriod = Mathf.Max(1, period / 2);
            var env = new float[s.Length];
            for (int i = 0; i < s.Length; i++)
            {
                int from = Mathf.Max(0, i - halfPeriod), to = Mathf.Min(s.Length - 1, i - halfPeriod + period - 1);
                float sum = 0f;
                for (int j = from; j <= to; j++) sum += Mathf.Abs(s[j]);
                env[i] = sum / (to - from + 1) * (0.5f * Mathf.PI);
            }
            return env;
        }

        private static float MaxIn(float[] env, AScanWaveform w, float startUs, float endUs, out float timeUs)
        {
            int a = w.IndexAt(startUs), b = w.IndexAt(endUs);
            float best = 0f;
            timeUs = startUs;
            for (int i = a; i <= b; i++)
            {
                if (env[i] <= best) continue;
                best = env[i];
                timeUs = w.TimeUs(i);
            }
            return best;
        }

        // 区间内的局部峰值（不低于前后半周期内各点）中最大者。
        private static float MaxPeakIn(float[] env, AScanWaveform w, float startUs, float endUs, int halfPeriod, out float timeUs)
        {
            int a = w.IndexAt(startUs), b = w.IndexAt(endUs);
            float best = 0f;
            timeUs = 0f;
            for (int i = a + 1; i < b; i++)
            {
                bool peak = true;
                for (int k = 1; k <= halfPeriod && peak; k++)
                {
                    if (i - k >= 0 && env[i - k] > env[i]) peak = false;
                    if (i + k < env.Length && env[i + k] > env[i]) peak = false;
                }
                if (!peak || env[i] <= best) continue;
                best = env[i];
                timeUs = w.TimeUs(i);
            }
            return best;
        }
    }
}
