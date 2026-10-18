using NUnit.Framework;
using RivetSim.Core.Config;
using RivetSim.Simulation.Inspection;
using RivetSim.Simulation.Robot;
using RivetSim.Simulation.World;
using UnityEngine;

namespace RivetSim.Tests.EditMode
{
    /// <summary>
    /// 检测教学简化模型测试：A 扫底面回波位置与厚度对应（Architecture §8）、缺陷回波模型、闸门判读四种结论、接触力虚拟弹簧与耦合质量。
    /// 参数取 CFG_Inspection 默认值（2 mm 铝蒙皮、6320 m/s、10 MHz）。
    /// </summary>
    public sealed class InspectionModelTests
    {
        private const int Seed = 42;
        private InspectionConfig _config;

        [SetUp]
        public void SetUp() => _config = ScriptableObject.CreateInstance<InspectionConfig>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_config);

        [Test]
        public void BackwallEcho_AtTwiceThicknessOverVelocity()
        {
            var synth = new AScanSynthesizer(_config, new System.Random(Seed));
            Assert.That(synth.NominalRoundTripUs, Is.EqualTo(2f * 0.002f / 6320f * 1e6f).Within(1e-4f));
            AScanWaveform w = synth.Synthesize(DefectType.None, 1f);
            float peak = PeakTimeUs(w, _config.interfaceEchoTimeUs + 0.3f, _config.interfaceEchoTimeUs + 0.9f);
            Assert.That(peak - _config.interfaceEchoTimeUs, Is.EqualTo(synth.NominalRoundTripUs).Within(0.02f));
        }

        [Test]
        public void Thinning_MovesBackwallEarlier()
        {
            var synth = new AScanSynthesizer(_config, new System.Random(Seed));
            AScanWaveform w = synth.Synthesize(DefectType.CorrosionThinning, 1f);
            float expected = synth.RoundTripUs(_config.skinThicknessMm * _config.thinningRemainingRatio);
            float peak = PeakTimeUs(w, _config.interfaceEchoTimeUs + 0.25f, _config.interfaceEchoTimeUs + 0.55f);
            Assert.That(peak - _config.interfaceEchoTimeUs, Is.EqualTo(expected).Within(0.02f));
        }

        [TestCase(DefectType.None, 1f, InspectionVerdict.Pass)]
        [TestCase(DefectType.CorrosionThinning, 1f, InspectionVerdict.Defect)]
        [TestCase(DefectType.EdgeCrack, 1f, InspectionVerdict.Defect)]
        [TestCase(DefectType.LooseRivet, 1f, InspectionVerdict.Defect)]
        [TestCase(DefectType.None, 0.6f, InspectionVerdict.Suspect)]
        [TestCase(DefectType.None, 0.2f, InspectionVerdict.Invalid)]
        public void GateJudge_Verdicts(DefectType defect, float coupling, InspectionVerdict expected)
        {
            var synth = new AScanSynthesizer(_config, new System.Random(Seed));
            GateResult r = new GateJudge(_config).Judge(synth.Synthesize(defect, coupling));
            Assert.That(r.Verdict, Is.EqualTo(expected), r.Reason);
        }

        [Test]
        public void GateJudge_CrackDepthFromEchoTime()
        {
            var synth = new AScanSynthesizer(_config, new System.Random(Seed));
            GateResult r = new GateJudge(_config).Judge(synth.Synthesize(DefectType.EdgeCrack, 1f));
            Assert.That(r.FlawDepthMm, Is.EqualTo(_config.crackDepthMm).Within(0.1f));
        }

        [Test]
        public void ContactModel_TargetForceAtTwoMillimetres_HardStopBeyondTravel()
        {
            var contact = new ContactModel(_config, 0.003f);
            contact.SetSurface(Vector3.zero, Vector3.up);
            contact.Update(new Vector3(0f, -0.002f, 0f));
            Assert.That(contact.ForceN, Is.EqualTo(10f).Within(1e-3f));
            Assert.That(contact.IsWithinAllowedRange, Is.True);
            Assert.That(contact.Coupling, Is.EqualTo(1f));
            contact.Update(new Vector3(0f, -0.0032f, 0f));
            Assert.That(contact.ForceN, Is.EqualTo(15f + 50000f * 0.0002f).Within(1e-3f));
            Assert.That(contact.IsOverLimit, Is.True);
            contact.Update(new Vector3(0f, 0.001f, 0f));
            Assert.That(contact.InContact, Is.False);
        }

        [Test]
        public void ContactModel_CouplingFallsBelowAllowedForce()
        {
            Assert.That(ContactModel.CouplingFromForce(5f, 8f, 2f), Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(ContactModel.CouplingFromForce(1f, 8f, 2f), Is.EqualTo(0f));
        }

        [Test]
        public void ContactModel_SurfaceFaultRaisesForce()
        {
            var contact = new ContactModel(_config, 0.003f) { SurfaceOffsetFaultM = 0.003f };
            contact.SetSurface(Vector3.zero, Vector3.up);
            contact.Update(new Vector3(0f, -0.002f, 0f));
            Assert.That(contact.IsOverLimit, Is.True);
        }

        private static float PeakTimeUs(AScanWaveform w, float startUs, float endUs)
        {
            float[] env = GateJudge.Envelope(w, 10f, out _);
            int a = w.IndexAt(startUs), b = w.IndexAt(endUs), best = a;
            for (int i = a; i <= b; i++)
                if (env[i] > env[best]) best = i;
            return w.TimeUs(best);
        }
    }
}
