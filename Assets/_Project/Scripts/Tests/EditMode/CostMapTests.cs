using System.Collections.Generic;
using NUnit.Framework;
using RivetSim.Simulation.Navigation;
using UnityEngine;

namespace RivetSim.Tests.EditMode
{
    /// <summary>
    /// 代价地图测试：虚拟禁行区生效（含外扩与膨胀）、接近通道内不叠加禁行区但激光障碍照常膨胀、膨胀半径、未知格附加代价。
    /// </summary>
    public sealed class CostMapTests
    {
        private const float Res = 0.1f;
        private static readonly GridGeometry Grid = new GridGeometry(Vector2.zero, Res, 100, 60);

        private static OccupancyGridMap FreeMap()
        {
            var map = new OccupancyGridMap(Grid);
            for (int y = 0; y < Grid.height; y++)
            for (int x = 0; x < Grid.width; x++)
                map.MarkFree(x, y);
            return map;
        }

        // “机身”投影：x 6.0～7.0 m 的竖条。
        private static KeepOutMask Fuselage(params KeepOutChannel[] channels)
        {
            var blocked = new bool[Grid.CellCount];
            for (int y = 0; y < Grid.height; y++)
            for (int x = 0; x < Grid.width; x++)
            {
                Vector2 p = Grid.CellCenter(x, y);
                blocked[Grid.Index(x, y)] = p.x >= 6f && p.x <= 7f;
            }
            return new KeepOutMask(Grid, blocked, channels);
        }

        private static bool Blocked(CostMap cost, float x, float z) => cost.IsBlocked(Grid.WorldToCell(new Vector2(x, z)));

        [Test]
        public void KeepOut_BlocksProjectionDilationAndInflation()
        {
            var cost = new CostMap(Grid);
            cost.Rebuild(FreeMap(), Fuselage(), 0.7f, 0.3f, 0f);
            Assert.IsTrue(Blocked(cost, 6.5f, 3f), "投影内");
            Assert.IsTrue(cost.IsLethal(Grid.Index(Grid.WorldToCell(new Vector2(5.82f, 3f)))), "外扩 0.3 m 内为致命区");
            Assert.IsTrue(Blocked(cost, 5.15f, 3f), "外扩 + 膨胀 1.0 m 内不可进入");
            Assert.IsFalse(Blocked(cost, 4.85f, 3f), "1.0 m 以外可通行");
        }

        [Test]
        public void Channel_OpensKeepOut_ButLidarStillInflates()
        {
            var channel = new KeepOutChannel("T", new Vector2(5.5f, 3f), 0f, new Vector2(2.4f, 3.2f));
            OccupancyGridMap map = FreeMap();
            map.MarkOccupied(Grid.WorldToCell(new Vector2(5.0f, 4.0f)).x, Grid.WorldToCell(new Vector2(5.0f, 4.0f)).y);
            var cost = new CostMap(Grid);
            cost.Rebuild(map, Fuselage(channel), 0.7f, 0.3f, 0f);

            Assert.IsFalse(Blocked(cost, 5.6f, 2.0f), "通道内虚拟禁行区不生效");
            Assert.IsTrue(Blocked(cost, 6.5f, 0.5f), "通道外投影仍不可进入");
            Assert.IsTrue(Blocked(cost, 5.0f, 4.5f), "通道内激光障碍仍按膨胀半径阻挡");
        }

        [Test]
        public void Inflation_UsesCellCenterDistance()
        {
            OccupancyGridMap map = FreeMap();
            map.MarkOccupied(50, 30);
            var cost = new CostMap(Grid);
            cost.Rebuild(map, null, 0.72f, 0f, 0f);
            Assert.IsTrue(cost.IsBlocked(new Vector2Int(57, 30)), "0.70 m 处在膨胀区内");
            Assert.IsFalse(cost.IsBlocked(new Vector2Int(58, 30)), "0.80 m 处在膨胀区外");
            Assert.IsTrue(cost.IsLethal(Grid.Index(50, 30)));
            Assert.IsFalse(cost.IsLethal(Grid.Index(51, 30)), "激光层只有障碍格本身是致命区");
        }

        [Test]
        public void UnknownCells_ArePassableWithExtraCost()
        {
            var map = new OccupancyGridMap(Grid);
            var cost = new CostMap(Grid);
            cost.Rebuild(map, null, 0.5f, 0f, 1.0f);
            Assert.IsFalse(cost.IsBlocked(Grid.Index(10, 10)));
            Assert.AreEqual(1.0f, cost.ExtraCost(Grid.Index(10, 10)));
            Assert.IsTrue(new GridAStar().TryPlan(cost, new Vector2Int(5, 5), new Vector2Int(90, 50), new List<Vector2Int>(), out _));
        }
    }
}
