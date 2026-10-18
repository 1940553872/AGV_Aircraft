using System.Collections.Generic;
using NUnit.Framework;
using RivetSim.Simulation.Navigation;
using UnityEngine;

namespace RivetSim.Tests.EditMode
{
    /// <summary>
    /// GridAStar 测试：直线可达、绕墙可达、被围住不可达、终点在膨胀区内拒绝、起点在膨胀区内可逃离、斜向不切角。
    /// </summary>
    public sealed class GridAStarTests
    {
        private const float Res = 0.1f;

        private static OccupancyGridMap EmptyMap(int w, int h)
        {
            var map = new OccupancyGridMap(new GridGeometry(Vector2.zero, Res, w, h));
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                map.MarkFree(x, y);
            return map;
        }

        private static CostMap Build(OccupancyGridMap map, float inflationM)
        {
            var cost = new CostMap(map.Geometry);
            cost.Rebuild(map, null, inflationM, 0f, 1f);
            return cost;
        }

        [Test]
        public void StraightLine_IsReachable_WithOptimalLength()
        {
            CostMap cost = Build(EmptyMap(30, 10), 0f);
            var path = new List<Vector2Int>();
            Assert.IsTrue(new GridAStar().TryPlan(cost, new Vector2Int(2, 5), new Vector2Int(25, 5), path, out _));
            Assert.AreEqual(24, path.Count);
            Assert.AreEqual(new Vector2Int(2, 5), path[0]);
            Assert.AreEqual(new Vector2Int(25, 5), path[path.Count - 1]);
        }

        [Test]
        public void WallWithGap_PathGoesThroughGap()
        {
            OccupancyGridMap map = EmptyMap(30, 30);
            for (int y = 0; y < 30; y++)
                if (y != 25) map.MarkOccupied(15, y);
            CostMap cost = Build(map, 0f);
            var path = new List<Vector2Int>();
            Assert.IsTrue(new GridAStar().TryPlan(cost, new Vector2Int(5, 5), new Vector2Int(25, 5), path, out _));
            Assert.IsTrue(path.Contains(new Vector2Int(15, 25)), "路径应穿过墙上的缺口");
            foreach (Vector2Int c in path) Assert.IsFalse(cost.IsBlocked(c), $"路径经过障碍格 {c}");
        }

        [Test]
        public void EnclosedGoal_IsUnreachable()
        {
            OccupancyGridMap map = EmptyMap(30, 30);
            for (int i = 10; i <= 20; i++)
            {
                map.MarkOccupied(i, 10);
                map.MarkOccupied(i, 20);
                map.MarkOccupied(10, i);
                map.MarkOccupied(20, i);
            }
            CostMap cost = Build(map, 0f);
            var path = new List<Vector2Int>();
            Assert.IsFalse(new GridAStar().TryPlan(cost, new Vector2Int(2, 2), new Vector2Int(15, 15), path, out string failure));
            Assert.AreEqual(0, path.Count);
            StringAssert.Contains("不可达", failure);
        }

        [Test]
        public void GoalInsideInflation_IsRejected()
        {
            OccupancyGridMap map = EmptyMap(30, 30);
            map.MarkOccupied(20, 15);
            CostMap cost = Build(map, 0.3f);
            Assert.IsFalse(new GridAStar().TryPlan(cost, new Vector2Int(2, 15), new Vector2Int(18, 15), new List<Vector2Int>(), out string failure));
            StringAssert.Contains("膨胀区", failure);
        }

        [Test]
        public void StartInsideInflation_CanEscape()
        {
            OccupancyGridMap map = EmptyMap(40, 20);
            map.MarkOccupied(5, 10);
            CostMap cost = Build(map, 0.3f);
            var start = new Vector2Int(7, 10);
            Assert.IsTrue(cost.IsBlocked(start), "前提：起点在膨胀区内");
            var path = new List<Vector2Int>();
            Assert.IsTrue(new GridAStar().TryPlan(cost, start, new Vector2Int(35, 10), path, out _));
            foreach (Vector2Int c in path) Assert.IsFalse(cost.IsLethal(cost.Geometry.Index(c)), "不得进入致命区");
        }

        [Test]
        public void Diagonal_DoesNotCutObstacleCorner()
        {
            OccupancyGridMap map = EmptyMap(10, 10);
            map.MarkOccupied(5, 4);
            map.MarkOccupied(4, 5);
            CostMap cost = Build(map, 0f);
            var path = new List<Vector2Int>();
            // (4,4) → (5,5) 的斜向两侧正交格都被占据，不能直接斜穿。
            Assert.IsTrue(new GridAStar().TryPlan(cost, new Vector2Int(4, 4), new Vector2Int(5, 5), path, out _));
            Assert.Greater(path.Count, 2);
        }
    }
}
