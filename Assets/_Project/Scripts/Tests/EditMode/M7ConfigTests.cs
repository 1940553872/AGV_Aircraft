using NUnit.Framework;
using RivetSim.Core.Config;
using RivetSim.Core.Diagnostics;
using UnityEngine;

namespace RivetSim.Tests.EditMode
{
    /// <summary>M7：默认倍速、画面观感配置与模块 GC 探针。</summary>
    public sealed class M7ConfigTests
    {
        [Test]
        public void DefaultSpeed_IsTwoAndInOptions()
        {
            var settings = ScriptableObject.CreateInstance<SimSettings>();
            try
            {
                Assert.AreEqual(2f, settings.defaultSpeedMultiplier);
                Assert.IsTrue(settings.IsDefaultSpeedValid());
                settings.defaultSpeedMultiplier = 3f;
                Assert.IsFalse(settings.IsDefaultSpeedValid());
            }
            finally
            {
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void RenderLook_DefaultsAreSubtle()
        {
            var look = ScriptableObject.CreateInstance<RenderLookConfig>();
            try
            {
                Assert.That(look.bloomIntensity, Is.InRange(0f, 0.5f), "Bloom 应轻微");
                Assert.That(look.vignetteIntensity, Is.InRange(0f, 0.35f), "Vignette 应轻微");
                Assert.IsTrue(look.softShadows);
                Assert.That(look.shadowCascadeCount, Is.InRange(1, 4));
                Assert.Greater(look.demoKeyLightIntensity, 0f);
            }
            finally
            {
                Object.DestroyImmediate(look);
            }
        }

        [Test]
        public void GcProbe_DisabledRecordsNothing()
        {
            GcProbe.Enabled = false;
            GcProbe.Reset();
            GcProbe.Begin(ProbeModule.LidarView);
            GcProbe.End(ProbeModule.LidarView);
            Assert.AreEqual(0, GcProbe.CallCount(ProbeModule.LidarView));
        }

        [Test]
        public void GcProbe_EnabledCountsCalls()
        {
            GcProbe.Reset();
            GcProbe.Enabled = true;
            try
            {
                GcProbe.Begin(ProbeModule.GridOverlay);
                GcProbe.End(ProbeModule.GridOverlay);
                GcProbe.Begin(ProbeModule.GridOverlay);
                GcProbe.End(ProbeModule.GridOverlay);
                Assert.AreEqual(2, GcProbe.CallCount(ProbeModule.GridOverlay));
                Assert.AreEqual(0, GcProbe.CallCount(ProbeModule.MiniMapDraw));
            }
            finally
            {
                GcProbe.Enabled = false;
                GcProbe.Reset();
            }
        }
    }
}
