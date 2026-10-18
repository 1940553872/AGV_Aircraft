using System;
using RivetSim.Core.Config;
using UnityEngine;

namespace RivetSim.Simulation.World
{
    /// <summary>
    /// 场景布局校验：检查三区包含关系、障碍物与作业通道、起始位 / 充电位 / 巡游路线的可通行性，
    /// 充电位到作业区的距离与演示台前观众区进深，并计算最窄通道与 AGV 到机身的最小距离。所有计算在世界 X / Z 平面内进行，单位 m。
    /// </summary>
    public static class SceneLayoutValidator
    {
        /// <summary>校验布局并返回报告。</summary>
        public static SceneLayoutReport Validate(SceneLayoutConfig layout, AgvConfig agv)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            if (agv == null) throw new ArgumentNullException(nameof(agv));

            var report = new SceneLayoutReport
            {
                AgvWidthM = agv.bodySizeM.y,
                DockingFuselageClearanceM = agv.fuselageSafetyClearanceM,
            };
            float agvHalfWidthM = agv.bodySizeM.y * 0.5f;

            Rect hall = CenteredRect(Vector2.zero, new Vector2(layout.hallSizeM.x, layout.hallSizeM.y));
            Rect work = layout.workZone.ToRect();
            Rect mobility = layout.mobilityZone.ToRect();
            Rect demo = layout.demoZone.ToRect();
            Rect aircraftBounds = CenteredRect(layout.aircraftCenterM, new Vector2(layout.aircraftWingspanM, layout.aircraftLengthM));
            Rect fuselage = CenteredRect(layout.aircraftCenterM, new Vector2(layout.fuselageDiameterM, layout.aircraftLengthM));

            CheckZones(report, hall, work, mobility, demo, aircraftBounds, layout.aisleClearanceM);
            CheckObstacles(report, layout, hall, work, mobility, aircraftBounds);
            CheckPoint(report, layout, mobility, layout.startPose.positionM, agvHalfWidthM, "起始位");
            CheckPoint(report, layout, mobility, layout.chargingDockPose.positionM, agvHalfWidthM, "充电位");
            CheckRoute(report, layout, mobility, fuselage, aircraftBounds, agvHalfWidthM);
            CheckChargingDock(report, layout, work);
            CheckDemoAudience(report, layout, demo);
            return report;
        }

        private static void CheckChargingDock(SceneLayoutReport report, SceneLayoutConfig layout, Rect work)
        {
            report.ChargingDockToWorkZoneM = PointRectDistance(layout.chargingDockPose.positionM, work);
            if (report.ChargingDockToWorkZoneM < layout.chargingDockMinToWorkZoneM)
                report.Errors.Add($"充电位到作业区 {report.ChargingDockToWorkZoneM:0.00} m，小于要求 {layout.chargingDockMinToWorkZoneM:0.00} m");
        }

        // 观众站位进深：从演示台前缘（操作侧，沿演示台朝向）到演示区边界的距离。
        private static void CheckDemoAudience(SceneLayoutReport report, SceneLayoutConfig layout, Rect demo)
        {
            PlanarPose desk = layout.demoDeskPose;
            Vector3 forward3 = desk.WorldRotation * Vector3.forward;
            var forward = new Vector2(forward3.x, forward3.z);
            Vector2 frontEdge = desk.positionM + forward * (layout.demoDeskSizeM.y * 0.5f);
            if (!demo.Contains(frontEdge))
            {
                report.DemoAudienceDepthM = 0f;
                report.Errors.Add("演示台前缘不在演示区内");
                return;
            }
            report.DemoAudienceDepthM = RayExitDistance(frontEdge, forward, demo);
            if (report.DemoAudienceDepthM < layout.demoAudienceDepthMinM)
                report.Errors.Add($"演示台前观众区进深 {report.DemoAudienceDepthM:0.00} m，小于要求 {layout.demoAudienceDepthMinM:0.00} m");
        }

        /// <summary>矩形内一点沿单位方向射出到矩形边界的距离。</summary>
        internal static float RayExitDistance(Vector2 origin, Vector2 direction, Rect r)
        {
            float t = float.PositiveInfinity;
            if (direction.x > 1e-6f) t = Mathf.Min(t, (r.xMax - origin.x) / direction.x);
            else if (direction.x < -1e-6f) t = Mathf.Min(t, (r.xMin - origin.x) / direction.x);
            if (direction.y > 1e-6f) t = Mathf.Min(t, (r.yMax - origin.y) / direction.y);
            else if (direction.y < -1e-6f) t = Mathf.Min(t, (r.yMin - origin.y) / direction.y);
            return t;
        }

        private static void CheckZones(SceneLayoutReport report, Rect hall, Rect work, Rect mobility, Rect demo,
            Rect aircraftBounds, float aisleClearanceM)
        {
            if (!Contains(hall, mobility)) report.Errors.Add("移动区超出大厅范围");
            if (!Contains(hall, demo)) report.Errors.Add("演示区超出大厅范围");
            if (!Contains(mobility, work)) report.Errors.Add("作业区不在移动区内");
            if (OverlapArea(mobility, demo) > 0f) report.Errors.Add("演示区与移动区重叠");

            if (!Contains(work, aircraftBounds))
            {
                report.Errors.Add("模型机超出作业区");
                report.AircraftToWorkZoneEdgeM = 0f;
                return;
            }
            report.AircraftToWorkZoneEdgeM = Mathf.Min(
                Mathf.Min(aircraftBounds.xMin - work.xMin, work.xMax - aircraftBounds.xMax),
                Mathf.Min(aircraftBounds.yMin - work.yMin, work.yMax - aircraftBounds.yMax));
            if (report.AircraftToWorkZoneEdgeM < aisleClearanceM)
                report.Errors.Add($"模型机到作业区边界 {report.AircraftToWorkZoneEdgeM:0.00} m，小于作业通道 {aisleClearanceM:0.00} m");
        }

        private static void CheckObstacles(SceneLayoutReport report, SceneLayoutConfig layout, Rect hall, Rect work,
            Rect mobility, Rect aircraftBounds)
        {
            ObstacleSpec[] obstacles = layout.obstacles ?? Array.Empty<ObstacleSpec>();
            for (int i = 0; i < obstacles.Length; i++)
            {
                Rect a = obstacles[i].footprint.ToRect();
                string name = obstacles[i].name;
                if (!Contains(mobility, a)) report.Errors.Add($"{name} 不在移动区内");
                if (OverlapArea(work, a) > 0f) report.Errors.Add($"{name} 侵入作业区");
                if (obstacles[i].heightM <= 0f) report.Errors.Add($"{name} 高度必须大于 0");

                float toAircraft = RectDistance(a, aircraftBounds);
                report.AircraftToObstacleM = Mathf.Min(report.AircraftToObstacleM, toAircraft);
                if (toAircraft < layout.aisleClearanceM)
                    report.Errors.Add($"{name} 到模型机 {toAircraft:0.00} m，小于作业通道 {layout.aisleClearanceM:0.00} m");

                ConsiderPassage(report, InnerWallGap(hall, a), $"{name} 与墙之间");
                for (int j = i + 1; j < obstacles.Length; j++)
                {
                    float gap = RectDistance(a, obstacles[j].footprint.ToRect());
                    if (gap <= 0f) report.Errors.Add($"{name} 与 {obstacles[j].name} 重叠");
                    ConsiderPassage(report, gap, $"{name} 与 {obstacles[j].name} 之间");
                }
            }
            if (report.NarrowestPassageM < report.AgvWidthM)
                report.Errors.Add($"最窄通道 {report.NarrowestPassageM:0.00} m 小于 AGV 车宽 {report.AgvWidthM:0.00} m");
        }

        private static void CheckRoute(SceneLayoutReport report, SceneLayoutConfig layout, Rect mobility, Rect fuselage,
            Rect aircraftBounds, float agvHalfWidthM)
        {
            Vector2[] route = layout.mappingRouteM ?? Array.Empty<Vector2>();
            if (route.Length < 2)
            {
                report.Errors.Add("巡游路线少于 2 个途经点");
                return;
            }

            ObstacleSpec[] obstacles = layout.obstacles ?? Array.Empty<ObstacleSpec>();
            for (int i = 0; i < route.Length; i++)
            {
                if (!mobility.Contains(route[i])) report.Errors.Add($"巡游路线第 {i} 点不在移动区内");
                if (i == 0) continue;

                Vector2 a = route[i - 1];
                Vector2 b = route[i];
                report.RouteLengthM += Vector2.Distance(a, b);
                report.RouteToFuselageM = Mathf.Min(report.RouteToFuselageM, SegmentRectDistance(a, b, fuselage) - agvHalfWidthM);
                report.RouteToAircraftBoundsM = Mathf.Min(report.RouteToAircraftBoundsM, SegmentRectDistance(a, b, aircraftBounds) - agvHalfWidthM);
                foreach (ObstacleSpec obstacle in obstacles)
                {
                    float clearance = SegmentRectDistance(a, b, obstacle.footprint.ToRect()) - agvHalfWidthM;
                    report.RouteToObstacleM = Mathf.Min(report.RouteToObstacleM, clearance);
                    if (clearance <= 0f) report.Errors.Add($"巡游路线第 {i - 1}→{i} 段与 {obstacle.name} 碰撞");
                }
            }
            if (report.RouteToAircraftBoundsM <= 0f) report.Errors.Add("巡游路线与模型机碰撞");
        }

        private static void CheckPoint(SceneLayoutReport report, SceneLayoutConfig layout, Rect mobility, Vector2 point,
            float agvHalfWidthM, string label)
        {
            if (!mobility.Contains(point)) report.Errors.Add($"{label}不在移动区内");
            foreach (ObstacleSpec obstacle in layout.obstacles ?? Array.Empty<ObstacleSpec>())
            {
                if (PointRectDistance(point, obstacle.footprint.ToRect()) <= agvHalfWidthM)
                    report.Errors.Add($"{label}与 {obstacle.name} 冲突");
            }
        }

        private static void ConsiderPassage(SceneLayoutReport report, float gapM, string label)
        {
            if (gapM <= 0f || gapM >= report.NarrowestPassageM) return;
            report.NarrowestPassageM = gapM;
            report.NarrowestPassageLabel = label;
        }

        private static Rect CenteredRect(Vector2 center, Vector2 size) => new Rect(center - size * 0.5f, size);

        private static bool Contains(Rect outer, Rect inner) =>
            inner.xMin >= outer.xMin && inner.xMax <= outer.xMax && inner.yMin >= outer.yMin && inner.yMax <= outer.yMax;

        private static float OverlapArea(Rect a, Rect b)
        {
            float w = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin);
            float h = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin);
            return w > 0f && h > 0f ? w * h : 0f;
        }

        // 矩形到大厅内墙面的最小净距。
        private static float InnerWallGap(Rect hall, Rect r) =>
            Mathf.Min(Mathf.Min(r.xMin - hall.xMin, hall.xMax - r.xMax), Mathf.Min(r.yMin - hall.yMin, hall.yMax - r.yMax));

        /// <summary>两矩形最小距离（相交为 0）。</summary>
        internal static float RectDistance(Rect a, Rect b)
        {
            float dx = Mathf.Max(0f, Mathf.Max(a.xMin - b.xMax, b.xMin - a.xMax));
            float dy = Mathf.Max(0f, Mathf.Max(a.yMin - b.yMax, b.yMin - a.yMax));
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>点到矩形最小距离（在内部为 0）。</summary>
        internal static float PointRectDistance(Vector2 p, Rect r)
        {
            float dx = Mathf.Max(0f, Mathf.Max(r.xMin - p.x, p.x - r.xMax));
            float dy = Mathf.Max(0f, Mathf.Max(r.yMin - p.y, p.y - r.yMax));
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>线段到矩形最小距离（相交为 0）。</summary>
        internal static float SegmentRectDistance(Vector2 a, Vector2 b, Rect r)
        {
            if (SegmentIntersectsRect(a, b, r)) return 0f;
            float d = Mathf.Min(PointRectDistance(a, r), PointRectDistance(b, r));
            d = Mathf.Min(d, PointSegmentDistance(new Vector2(r.xMin, r.yMin), a, b));
            d = Mathf.Min(d, PointSegmentDistance(new Vector2(r.xMax, r.yMin), a, b));
            d = Mathf.Min(d, PointSegmentDistance(new Vector2(r.xMin, r.yMax), a, b));
            d = Mathf.Min(d, PointSegmentDistance(new Vector2(r.xMax, r.yMax), a, b));
            return d;
        }

        private static float PointSegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float lengthSq = ab.sqrMagnitude;
            float t = lengthSq > 0f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / lengthSq) : 0f;
            return Vector2.Distance(p, a + ab * t);
        }

        // Liang–Barsky 线段裁剪：线段与矩形有交集时返回 true。
        private static bool SegmentIntersectsRect(Vector2 a, Vector2 b, Rect r)
        {
            float t0 = 0f, t1 = 1f;
            Vector2 d = b - a;
            float[] p = { -d.x, d.x, -d.y, d.y };
            float[] q = { a.x - r.xMin, r.xMax - a.x, a.y - r.yMin, r.yMax - a.y };
            for (int i = 0; i < 4; i++)
            {
                if (Mathf.Approximately(p[i], 0f))
                {
                    if (q[i] < 0f) return false;
                    continue;
                }
                float t = q[i] / p[i];
                if (p[i] < 0f) t0 = Mathf.Max(t0, t);
                else t1 = Mathf.Min(t1, t);
                if (t0 > t1) return false;
            }
            return true;
        }
    }
}
