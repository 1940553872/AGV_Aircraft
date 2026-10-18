using System.Collections.Generic;
using NUnit.Framework;
using RivetSim.Core.Config;
using RivetSim.Simulation.Navigation;
using UnityEngine;

namespace RivetSim.Tests.EditMode
{
    /// <summary>
    /// 导航几何测试：栅格坐标换算、激光射线写入占据栅格、路径平滑不穿障碍、纯追踪前视点、标记视场判定、停靠误差分解、接近通道包含判定。
    /// </summary>
    public sealed class NavigationGeometryTests
    {
        [Test]
        public void GridGeometry_CoveringHall_AlignsToOrigin()
        {
            GridGeometry g = GridGeometry.Covering(Vector2.zero, new Vector2(64f, 44f), 0.1f);
            Assert.AreEqual(640, g.width);
            Assert.AreEqual(440, g.height);
            Assert.AreEqual(-32f, g.originM.x, 1e-4f);
            Assert.AreEqual(new Vector2Int(320, 220), g.WorldToCell(new Vector2(0.05f, 0.05f)));
            Assert.AreEqual(0.05f, g.CellCenter(320, 220).x, 1e-4f);
        }

        [Test]
        public void IntegrateRay_MarksHitOccupiedAndPassageFree()
        {
            var map = new OccupancyGridMap(new GridGeometry(Vector2.zero, 0.1f, 50, 10));
            map.IntegrateRay(new Vector2(0.15f, 0.55f), new Vector2(3.05f, 0.55f), true);
            map.CommitScan();
            Assert.AreEqual(OccupancyState.Occupied, map.GetState(30, 5));
            Assert.AreEqual(OccupancyState.Free, map.GetState(10, 5));
            Assert.AreEqual(OccupancyState.Unknown, map.GetState(40, 5));
            Assert.AreEqual(30, map.KnownCellCount);
            Assert.IsTrue(map.ConsumeDirtyRect(out RectInt dirty));
            Assert.AreEqual(1, dirty.xMin);
            Assert.AreEqual(31, dirty.xMax);
        }

        [Test]
        public void PathSmoother_RemovesRedundantVerticesWithoutCrossingObstacles()
        {
            var g = new GridGeometry(Vector2.zero, 0.1f, 40, 40);
            var map = new OccupancyGridMap(g);
            for (int y = 0; y < 40; y++)
            for (int x = 0; x < 40; x++)
                map.MarkFree(x, y);
            for (int y = 0; y < 30; y++) map.MarkOccupied(20, y);
            var cost = new CostMap(g);
            cost.Rebuild(map, null, 0.15f, 0f, 0f);
            var cells = new List<Vector2Int>();
            Assert.IsTrue(new GridAStar().TryPlan(cost, new Vector2Int(5, 5), new Vector2Int(35, 5), cells, out _));

            List<Vector2> smooth = PathSmoother.Smooth(cost, cells, g.CellCenter(5, 5), g.CellCenter(35, 5));
            Assert.Less(smooth.Count, cells.Count);
            for (int i = 1; i < smooth.Count; i++)
                Assert.IsTrue(PathSmoother.LineOfSight(cost, smooth[i - 1], smooth[i]), $"第 {i} 段穿过障碍");
        }

        [Test]
        public void PurePursuit_LookaheadOnStraightPath()
        {
            var tracker = new PurePursuitTracker();
            tracker.Reset(new[] { new Vector2(0f, 0f), new Vector2(0f, 10f) });
            Vector2 target = tracker.Compute(new Vector2(0.2f, 2f), 0f, 0.8f, out float error, out float curvature);
            Assert.AreEqual(0f, target.x, 1e-4f);
            Assert.AreEqual(2.8f, target.y, 1e-4f);
            Assert.Less(error, 0f, "车在路径右侧，应向左转");
            Assert.Less(curvature, 0f);
            Assert.AreEqual(8f, tracker.RemainingM, 1e-4f);
        }

        [Test]
        public void MarkerVisibility_RespectsFovRangeAndFacing()
        {
            Vector2 fov = new Vector2(90f, 60f);
            Vector3 cam = new Vector3(0f, 0.4f, 0f);
            Quaternion forward = Quaternion.identity;
            Vector3 facingCamera = Vector3.back;
            Assert.IsTrue(MarkerVisibility.IsVisible(cam, forward, fov, 0.3f, 4f, new Vector3(0.8f, 0.95f, 2.5f), facingCamera));
            Assert.IsFalse(MarkerVisibility.IsVisible(cam, forward, fov, 0.3f, 4f, new Vector3(0.8f, 0.95f, 0.4f), facingCamera), "超出水平视场");
            Assert.IsFalse(MarkerVisibility.IsVisible(cam, forward, fov, 0.3f, 4f, new Vector3(0f, 0.95f, 5f), facingCamera), "超出识别距离");
            Assert.IsFalse(MarkerVisibility.IsVisible(cam, forward, fov, 0.3f, 4f, new Vector3(0.8f, 0.95f, 2.5f), Vector3.forward), "背面");
        }

        [Test]
        public void DockingError_DecomposesInTargetFrame()
        {
            var target = new PlanarPose(new Vector2(6.222f, -3.540f), 0f);
            var actual = new PlanarPose(new Vector2(6.225f, -3.538f), 0.3f);
            DockingError e = DockingError.Between(target, actual);
            Assert.AreEqual(0.002f, e.LongitudinalM, 1e-5f);
            Assert.AreEqual(0.003f, e.LateralM, 1e-5f);
            Assert.AreEqual(0.3f, e.YawDeg, 1e-4f);
            Assert.IsTrue(e.IsWithin(0.010f, 0.5f));
            Assert.IsFalse(DockingError.Between(target, new PlanarPose(target.positionM, 1f)).IsWithin(0.010f, 0.5f));
        }

        [Test]
        public void KeepOutChannel_ContainsOrientedRectangle()
        {
            var channel = new KeepOutChannel("R1-S1", new Vector2(5.622f, -4.34f), 0f, new Vector2(2.4f, 3.2f));
            Assert.IsTrue(channel.Contains(new Vector2(6.222f, -5.04f)), "预停靠位姿在通道内");
            Assert.IsTrue(channel.Contains(new Vector2(6.80f, -3.0f)), "内侧 0.6 m 以内");
            Assert.IsFalse(channel.Contains(new Vector2(6.90f, -3.0f)), "内侧超过 0.6 m");
            Assert.IsFalse(channel.Contains(new Vector2(6.222f, -2.70f)), "超出车头前 0.2 m");
        }
    }
}
