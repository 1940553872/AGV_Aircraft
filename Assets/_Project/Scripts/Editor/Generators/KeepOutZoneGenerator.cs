using System.Collections.Generic;
using RivetSim.Core.Config;
using RivetSim.Core.Logging;
using RivetSim.Editor.Tools;
using RivetSim.Simulation.Navigation;
using RivetSim.Simulation.Robot;
using RivetSim.Simulation.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RivetSim.Editor.Generators
{
    /// <summary>
    /// 虚拟禁行区生成器（M3，ADR-015）：把 Zone_Work/Aircraft 下全部可见网格的三角形投影到地面，按导航栅格分辨率保守栅格化
    /// （三角形与格子相交即标为投影内，不过滤高度，含机翼、平尾、发动机与起落架），并按各工位的停靠位姿生成接近通道，
    /// 写入 Zone_Work/VirtualKeepOut 的 <see cref="VirtualKeepOutZone"/>。掩膜格点与主场景导航栅格对齐。重复执行覆盖上次结果。
    /// </summary>
    public static class KeepOutZoneGenerator
    {
        /// <summary>禁行区节点名称。</summary>
        public const string NodeName = "VirtualKeepOut";

        /// <summary>在已打开的主场景中重新生成虚拟禁行区。</summary>
        [MenuItem("RivetSim/Generate/Virtual Keep-Out", priority = 36)]
        public static void BuildInMainScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ProjectPaths.MainScene)
            {
                SimLog.Error(LogCategory.System, $"请先打开 {ProjectPaths.MainScene}。");
                return;
            }
            Build(scene);
            EditorSceneManager.SaveScene(scene);
        }

        /// <summary>在主场景中生成虚拟禁行区（由 MainSceneBuilder 在检测区之后调用）。</summary>
        public static VirtualKeepOutZone Build(Scene scene)
        {
            var layout = GeneratorUtility.LoadConfig<SceneLayoutConfig>(ProjectPaths.SceneLayoutAsset);
            var agv = GeneratorUtility.LoadConfig<AgvConfig>(ProjectPaths.AgvAsset);
            var nav = GeneratorUtility.LoadConfig<NavigationConfig>(ProjectPaths.NavigationAsset);
            Transform work = GeneratorUtility.FindRoot(scene, "Zone_Work");
            Transform aircraft = work != null ? work.Find("Aircraft") : null;
            Transform regions = work != null ? work.Find(RivetZoneGenerator.RootName) : null;
            RivetRegistry registry = regions != null ? regions.GetComponent<RivetRegistry>() : null;
            if (layout == null || agv == null || nav == null || aircraft == null || registry == null)
            {
                SimLog.Error(LogCategory.System, "虚拟禁行区生成失败：缺少 CFG_Navigation 等配置，或主场景中没有 Zone_Work/Aircraft 与检测区。");
                return null;
            }

            Transform old = work.Find(NodeName);
            if (old != null) Object.DestroyImmediate(old.gameObject);

            GridGeometry navGrid = GridGeometry.Covering(Vector2.zero, new Vector2(layout.hallSizeM.x, layout.hallSizeM.y), nav.gridResolutionM);
            GridGeometry maskGrid = AlignedCovering(RendererBoundsXz(aircraft.gameObject), navGrid);
            bool[] mask = RasterizeMeshes(aircraft.gameObject, maskGrid, out int triangles);

            Vector2 aircraftCenter = layout.aircraftCenterM;
            var channels = new List<KeepOutChannel>();
            foreach (InspectionRegion region in registry.Regions)
            foreach (InspectionStation station in region.Stations)
                channels.Add(ComputeChannel(station.StationId, station.DockingPose, aircraftCenter, nav, agv));

            Transform node = GeneratorUtility.CreateNode(NodeName, work, Vector3.zero);
            var zone = node.gameObject.AddComponent<VirtualKeepOutZone>();
            zone.Configure(maskGrid, mask, channels.ToArray());
            EditorUtility.SetDirty(zone);

            KeepOutChannel c0 = channels.Count > 0 ? channels[0] : default;
            SimLog.Info(LogCategory.System,
                $"虚拟禁行区已生成：{triangles:N0} 个三角形投影到 {maskGrid.width} × {maskGrid.height} 格（{maskGrid.resolutionM:0.00} m），" +
                $"投影内 {zone.ProjectedCellCount:N0} 格（{zone.ProjectedCellCount * maskGrid.resolutionM * maskGrid.resolutionM:0.0} m²）；" +
                $"接近通道 {channels.Count} 条，开口 {c0.sizeM.x:0.00} × {c0.sizeM.y:0.00} m");
            return zone;
        }

        /// <summary>
        /// 工位接近通道（停靠坐标系）：纵向从停靠位姿车头前 channelFrontMargin 到预停靠位姿车尾后 channelRearMargin，
        /// 横向内侧（朝模型机中线）到车道中心线 channelInnerHalfWidth、外侧 channelOuterHalfWidth。
        /// </summary>
        public static KeepOutChannel ComputeChannel(string stationId, PlanarPose dock, Vector2 aircraftCenterM, NavigationConfig nav, AgvConfig agv)
        {
            Vector2 heading = DiffDriveKinematics.Heading(dock.yawDeg);
            Vector2 right = DiffDriveKinematics.Right(dock.yawDeg);
            float innerSign = Vector2.Dot(aircraftCenterM - dock.positionM, right) >= 0f ? 1f : -1f;
            float halfLength = 0.5f * agv.bodySizeM.x;
            float front = halfLength + nav.channelFrontMarginM;
            float rear = -(nav.dockApproachDistanceM + halfLength + nav.channelRearMarginM);
            float inner = nav.channelInnerHalfWidthM;
            float outer = -nav.channelOuterHalfWidthM;
            Vector2 center = dock.positionM + heading * (0.5f * (front + rear)) + right * (innerSign * 0.5f * (inner + outer));
            return new KeepOutChannel(stationId, center, dock.yawDeg, new Vector2(inner - outer, front - rear));
        }

        /// <summary>覆盖 boundsXz（x = 世界 X，y = 世界 Z）并外扩一格、格点与 reference 对齐的栅格。</summary>
        public static GridGeometry AlignedCovering(Rect boundsXz, GridGeometry reference)
        {
            float res = reference.resolutionM;
            Vector2 o = reference.originM;
            int x0 = Mathf.FloorToInt((boundsXz.xMin - o.x) / res) - 1;
            int y0 = Mathf.FloorToInt((boundsXz.yMin - o.y) / res) - 1;
            int x1 = Mathf.CeilToInt((boundsXz.xMax - o.x) / res) + 1;
            int y1 = Mathf.CeilToInt((boundsXz.yMax - o.y) / res) + 1;
            return new GridGeometry(o + new Vector2(x0, y0) * res, res, x1 - x0, y1 - y0);
        }

        /// <summary>轴对齐矩形（中心、尺寸，单位 m）的掩膜，供沙盒模拟机身使用。</summary>
        public static bool[] RasterizeRectangle(GridGeometry g, Vector2 centerM, Vector2 sizeM)
        {
            var mask = new bool[g.CellCount];
            Rect r = new Rect(centerM - 0.5f * sizeM, sizeM);
            for (int y = 0; y < g.height; y++)
            for (int x = 0; x < g.width; x++)
                mask[g.Index(x, y)] = r.Contains(g.CellCenter(x, y));
            return mask;
        }

        private static Rect RendererBoundsXz(GameObject root)
        {
            Bounds? total = null;
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled) continue;
                if (total.HasValue)
                {
                    Bounds b = total.Value;
                    b.Encapsulate(r.bounds);
                    total = b;
                }
                else total = r.bounds;
            }
            Bounds t = total ?? new Bounds();
            return Rect.MinMaxRect(t.min.x, t.min.z, t.max.x, t.max.z);
        }

        // 保守栅格化：每个三角形只检查其包围盒内的格，用分离轴判定格子与三角形是否相交。
        private static bool[] RasterizeMeshes(GameObject root, GridGeometry g, out int triangleCount)
        {
            var mask = new bool[g.CellCount];
            triangleCount = 0;
            var vertices = new List<Vector3>();
            var indices = new List<int>();
            foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>())
            {
                var renderer = mf.GetComponent<MeshRenderer>();
                Mesh mesh = mf.sharedMesh;
                if (renderer == null || !renderer.enabled || mesh == null) continue;
                Matrix4x4 m = mf.transform.localToWorldMatrix;
                mesh.GetVertices(vertices);
                var p = new Vector2[vertices.Count];
                for (int i = 0; i < p.Length; i++)
                {
                    Vector3 w = m.MultiplyPoint3x4(vertices[i]);
                    p[i] = new Vector2(w.x, w.z);
                }
                for (int sub = 0; sub < mesh.subMeshCount; sub++)
                {
                    mesh.GetTriangles(indices, sub);
                    for (int t = 0; t + 2 < indices.Count; t += 3)
                    {
                        RasterizeTriangle(g, mask, p[indices[t]], p[indices[t + 1]], p[indices[t + 2]]);
                        triangleCount++;
                    }
                }
            }
            return mask;
        }

        private static void RasterizeTriangle(GridGeometry g, bool[] mask, Vector2 a, Vector2 b, Vector2 c)
        {
            Vector2 min = Vector2.Min(a, Vector2.Min(b, c));
            Vector2 max = Vector2.Max(a, Vector2.Max(b, c));
            Vector2Int c0 = g.WorldToCell(min), c1 = g.WorldToCell(max);
            c0 = Vector2Int.Max(c0, Vector2Int.zero);
            c1 = Vector2Int.Min(c1, new Vector2Int(g.width - 1, g.height - 1));
            float res = g.resolutionM;
            for (int y = c0.y; y <= c1.y; y++)
            for (int x = c0.x; x <= c1.x; x++)
            {
                int i = g.Index(x, y);
                if (mask[i]) continue;
                Vector2 boxMin = g.originM + new Vector2(x, y) * res;
                if (c0 == c1 || Overlaps(a, b, c, boxMin, boxMin + Vector2.one * res)) mask[i] = true;
            }
        }

        // 三角形与轴对齐方格的分离轴判定（包围盒已重叠，只需检查三条边的法向；退化三角形的零长边跳过）。
        private static bool Overlaps(Vector2 a, Vector2 b, Vector2 c, Vector2 boxMin, Vector2 boxMax)
        {
            Vector2[] tri = { a, b, c };
            for (int e = 0; e < 3; e++)
            {
                Vector2 edge = tri[(e + 1) % 3] - tri[e];
                var axis = new Vector2(-edge.y, edge.x);
                if (axis.sqrMagnitude < 1e-12f) continue;
                float tMin = float.MaxValue, tMax = float.MinValue;
                foreach (Vector2 v in tri)
                {
                    float d = Vector2.Dot(v, axis);
                    tMin = Mathf.Min(tMin, d);
                    tMax = Mathf.Max(tMax, d);
                }
                float bMin = float.MaxValue, bMax = float.MinValue;
                for (int k = 0; k < 4; k++)
                {
                    var corner = new Vector2((k & 1) == 0 ? boxMin.x : boxMax.x, (k & 2) == 0 ? boxMin.y : boxMax.y);
                    float d = Vector2.Dot(corner, axis);
                    bMin = Mathf.Min(bMin, d);
                    bMax = Mathf.Max(bMax, d);
                }
                if (tMax < bMin || bMax < tMin) return false;
            }
            return true;
        }
    }
}
