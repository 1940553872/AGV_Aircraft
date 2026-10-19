using NUnit.Framework;
using RivetSim.Core.Config;
using RivetSim.Simulation.Mission;
using UnityEngine;

namespace RivetSim.Tests.EditMode
{
    /// <summary>
    /// 操作员初值通道测试（M6，t(1,8) 小地图点选）：未打开时拒绝点选、点选后结束等待、自动给出的超时判定。
    /// </summary>
    public sealed class OperatorPoseChannelTests
    {
        private static readonly PlanarPose Pick = new PlanarPose(new Vector2(1.5f, -2f), 30f);

        [Test]
        public void Submit_WhenClosed_IsRejected()
        {
            var channel = new OperatorPoseChannel();
            Assert.That(channel.Submit(Pick), Is.False);
            Assert.That(channel.TryTake(out _), Is.False);
        }

        [Test]
        public void Submit_WhenOpen_ResolvesWithPickedPose()
        {
            var channel = new OperatorPoseChannel { AutoProvide = false };
            float openedDelay = -1f;
            bool closedWithPick = false;
            channel.Opened += d => openedDelay = d;
            channel.Closed += used => closedWithPick = used;

            channel.Open(10d, 3f);
            Assert.That(openedDelay, Is.EqualTo(3f));
            Assert.That(channel.IsResolved(100d), Is.False, "非自动模式下不应超时给出");
            Assert.That(channel.Submit(Pick), Is.True);
            Assert.That(channel.Submit(Pick), Is.False, "同一次请求只接受一次点选");
            Assert.That(channel.IsResolved(10d), Is.True);
            Assert.That(channel.TryTake(out PlanarPose pose), Is.True);
            Assert.That(pose.positionM, Is.EqualTo(Pick.positionM));
            channel.Close();
            Assert.That(closedWithPick, Is.True);
            Assert.That(channel.IsOpen, Is.False);
        }

        [Test]
        public void AutoProvide_ResolvesAfterDelay()
        {
            var channel = new OperatorPoseChannel { AutoProvide = true };
            channel.Open(5d, 3f);
            Assert.That(channel.IsResolved(7.9d), Is.False);
            Assert.That(channel.IsResolved(8d), Is.True);
            Assert.That(channel.TryTake(out _), Is.False, "超时给出时没有操作员点选");
        }

        [Test]
        public void RivetCycleTime_ExcludesStowAndLowerTime()
        {
            var board = new MissionBlackboard(null);
            var cursor = new MissionCursor();
            cursor.Reset(3);
            board.Sync(cursor, 100d, true);
            board.CycleExcludedS += 40d;
            Assert.That(board.RivetCycleTimeS(160d), Is.EqualTo(20f).Within(1e-4f));

            cursor.RivetIndex = 1;
            board.Sync(cursor, 200d, true);
            Assert.That(board.CycleExcludedS, Is.EqualTo(0d), "新的一颗应清零扣除时长");
            Assert.That(board.RivetCycleTimeS(230d), Is.EqualTo(30f).Within(1e-4f));
        }
    }
}
