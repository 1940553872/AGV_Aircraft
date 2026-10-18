using System.Collections.Generic;
using System.IO;
using System.Text;
using RivetSim.Core.Config;
using RivetSim.Core.Logging;
using RivetSim.Editor.Importers;
using RivetSim.Editor.Tools;
using UnityEditor;
using UnityEngine;

namespace RivetSim.Editor.Generators
{
    /// <summary>
    /// A320 模型机预制体（M2 第三部分 3A）：
    /// 1. PF_Aircraft_A320_Model —— glb 的 Prefab Variant：隐藏机身内部件（驾驶舱座椅等），层设为 Aircraft，只给机身外壳（铆钉投影）与起落架轮胎（2D 激光扫描，M3）加 MeshCollider；原件不改。
    /// 2. PF_Aircraft_A320 —— 外层预制体：根节点在主起落架中点的地面投影，内嵌上述 Variant，并统一缩放、转到机头朝 +Z（ADR-008）。
    /// 缩放系数 = 机长（CFG_SceneLayout）÷ 原始机长；同时输出层级清单 Docs/Reference/A320_Hierarchy.md 与实测外形尺寸。
    /// </summary>
    public static class AircraftA320Builder
    {
        /// <summary>Variant 路径。</summary>
        public const string ModelVariantPath = ProjectPaths.PrefabsAircraft + "/PF_Aircraft_A320_Model.prefab";

        /// <summary>外层预制体路径。</summary>
        public const string PrefabPath = ProjectPaths.PrefabsAircraft + "/PF_Aircraft_A320.prefab";

        /// <summary>机身外壳节点（铆钉投影所用的 MeshCollider）。</summary>
        public const string FuselageNode = "Cylinder.027_68";

        /// <summary>主起落架外侧 / 内侧机轮节点（每个网格横跨左右两侧）。</summary>
        public static readonly string[] MainGearWheelNodes = { "Cylinder.082_137", "Cylinder.091_146" };

        /// <summary>前起落架左 / 右轮胎节点。</summary>
        public static readonly string[] NoseGearWheelNodes = { "Cylinder.042_69", "Cylinder.046_75" };

        private const string HierarchyReportPath = "Docs/Reference/A320_Hierarchy.md";

        // 机身内部件（驾驶舱座椅等，完全包在机身外壳内）：2026-10-18 按包围盒包含关系筛出，约 4.3 千面。
        private static readonly string[] InteriorNodes =
        {
            "Cylinder.005_13", "Cylinder.018_27", "Cube.030_228", "Cylinder.021_227",
            "Cube.009_226", "Cylinder.007_225", "Cylinder.017_26", "Cylinder.006_14",
        };

        // glTFast 导入后机头朝 +X、Y 向上（根节点自带绕 X 轴 270°）；再绕 Y 轴 −90° 使机头朝 +Z。
        private static readonly Quaternion ModelRotation = Quaternion.Euler(0f, -90f, 0f) * Quaternion.Euler(270f, 0f, 0f);

        /// <summary>生成 Variant 与外层预制体，并输出层级清单。</summary>
        [MenuItem("RivetSim/Generate/Aircraft A320", priority = 13)]
        public static void Build()
        {
            var layout = GeneratorUtility.LoadConfig<SceneLayoutConfig>(ProjectPaths.SceneLayoutAsset);
            var glb = AssetDatabase.LoadAssetAtPath<GameObject>(ThirdPartyImporter.A320Glb);
            if (layout == null) return;
            if (glb == null)
            {
                SimLog.Error(LogCategory.System, $"找不到 {ThirdPartyImporter.A320Glb}，请先执行 RivetSim/Import/Third Party Assets。");
                return;
            }

            GameObject variant = BuildVariant(glb);
            if (variant == null) return;

            var root = new GameObject("PF_Aircraft_A320");
            GameObject model = GeneratorUtility.InstantiatePrefab(variant, root.transform, Vector3.zero, ModelRotation, "Model");
            Bounds raw = VertexBounds(model);
            float scale = layout.aircraftLengthM / raw.size.z;
            model.transform.localScale = Vector3.one * scale;

            // 原点：主起落架中点的地面投影（地面 = 主机轮最低点）。
            Vector3 gear = MainGearCenter(model);
            float groundY = VertexBounds(FindNode(model, MainGearWheelNodes[0]).gameObject).min.y;
            model.transform.localPosition = -new Vector3(gear.x, groundY, gear.z);

            GeneratorUtility.SetLayerRecursive(root, SimLayers.AircraftIndex);
            WriteHierarchyReport(model, scale);
            AircraftMeasurement m = Measure(root);
            GeneratorUtility.SavePrefab(root, PrefabPath);
            SimLog.Info(LogCategory.System, $"A320 模型机已生成：{PrefabPath}（缩放 {scale:0.000000}）\n{m}");
        }

        private static GameObject BuildVariant(GameObject glb)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(glb);
            var hidden = new HashSet<string>(InteriorNodes);
            // 机身外壳供铆钉投影；主 / 前起落架轮胎供离地 0.30 m 的 2D 激光扫描（M3，ADR-015）。
            var collidable = new HashSet<string>(MainGearWheelNodes) { FuselageNode };
            collidable.UnionWith(NoseGearWheelNodes);
            foreach (Transform t in instance.GetComponentsInChildren<Transform>(true))
            {
                if (hidden.Contains(t.name)) t.gameObject.SetActive(false);
                if (!collidable.Contains(t.name)) continue;
                foreach (MeshFilter mf in t.GetComponentsInChildren<MeshFilter>(true))
                {
                    MeshCollider collider = mf.GetComponent<MeshCollider>();
                    if (collider == null) collider = mf.gameObject.AddComponent<MeshCollider>();
                    collider.sharedMesh = mf.sharedMesh;
                }
            }
            GeneratorUtility.SetLayerRecursive(instance, SimLayers.AircraftIndex);
            GeneratorUtility.EnsureFolder(ProjectPaths.PrefabsAircraft);
            GameObject variant = PrefabUtility.SaveAsPrefabAsset(instance, ModelVariantPath, out bool ok);
            Object.DestroyImmediate(instance);
            if (!ok) SimLog.Error(LogCategory.System, $"Variant 保存失败：{ModelVariantPath}");
            return ok ? variant : null;
        }

        /// <summary>可见（激活）网格顶点的世界包围盒（比渲染器包围盒精确，斜放的薄片不会被放大）。</summary>
        public static Bounds VertexBounds(GameObject go)
        {
            bool first = true;
            var b = new Bounds();
            foreach (MeshFilter mf in go.GetComponentsInChildren<MeshFilter>(false))
            {
                if (mf.sharedMesh == null) continue;
                Matrix4x4 m = mf.transform.localToWorldMatrix;
                foreach (Vector3 v in mf.sharedMesh.vertices)
                {
                    Vector3 w = m.MultiplyPoint3x4(v);
                    if (first) { b = new Bounds(w, Vector3.zero); first = false; }
                    else b.Encapsulate(w);
                }
            }
            return b;
        }

        /// <summary>可见（激活）渲染器的世界包围盒。</summary>
        public static Bounds VisibleBounds(GameObject go)
        {
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>(false);
            Bounds b = renderers[0].bounds;
            foreach (Renderer r in renderers) b.Encapsulate(r.bounds);
            return b;
        }

        /// <summary>按名称查找子节点（含未激活）。</summary>
        public static Transform FindNode(GameObject root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name) return t;
            }
            return null;
        }

        /// <summary>主起落架中点（两组主机轮包围盒的中心），世界坐标。</summary>
        public static Vector3 MainGearCenter(GameObject root)
        {
            Bounds b = VertexBounds(FindNode(root, MainGearWheelNodes[0]).gameObject);
            b.Encapsulate(VertexBounds(FindNode(root, MainGearWheelNodes[1]).gameObject));
            return b.center;
        }

        /// <summary>测量外层预制体实例（根节点在原点、无旋转）的外形尺寸。</summary>
        public static AircraftMeasurement Measure(GameObject root)
        {
            Bounds all = VertexBounds(root);
            Bounds fuselage = VertexBounds(FindNode(root, FuselageNode).gameObject);
            Bounds outer = VertexBounds(FindNode(root, MainGearWheelNodes[0]).gameObject);
            Bounds inner = VertexBounds(FindNode(root, MainGearWheelNodes[1]).gameObject);
            long triangles = 0;
            foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>(false))
            {
                if (mf.sharedMesh != null) triangles += mf.sharedMesh.triangles.Length / 3;
            }
            return new AircraftMeasurement
            {
                LengthM = all.size.z,
                WingspanM = all.size.x,
                HeightM = all.max.y,
                LowestPointM = all.min.y,
                FuselageDiameterM = fuselage.size.x,
                FuselageCenterlineHeightM = fuselage.center.y,
                FuselageBottomHeightM = fuselage.min.y,
                BoundsCenterOffsetM = new Vector2(all.center.x, all.center.z),
                MainGearSideOffsetM = (outer.extents.x + inner.extents.x) * 0.5f,
                MainWheelRadiusM = outer.extents.y,
                Triangles = triangles,
            };
        }

        private static void WriteHierarchyReport(GameObject model, float scale)
        {
            var sb = new StringBuilder();
            sb.Append("# A320 模型层级清单（自动生成，RivetSim/Generate/Aircraft A320）\n\n");
            sb.Append($"来源：{ThirdPartyImporter.A320Glb}；缩放 {scale:0.000000}；坐标为外层预制体本地坐标（m，机头朝 +Z，Y 向上，原点在主起落架中点地面投影）。\n\n");
            sb.Append("| 节点 | 三角面 | 中心 (x, y, z) | 尺寸 (x, y, z) | 状态 |\n|---|---|---|---|---|\n");
            Transform sceneRoot = FindNode(model, "GLTF_SceneRootNode");
            long visible = 0, hiddenTris = 0;
            foreach (Transform node in sceneRoot)
            {
                long tris = 0;
                foreach (MeshFilter mf in node.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (mf.sharedMesh != null) tris += mf.sharedMesh.triangles.Length / 3;
                }
                Renderer[] rs = node.GetComponentsInChildren<Renderer>(true);
                if (rs.Length == 0) continue;
                Bounds b = rs[0].bounds;
                foreach (Renderer r in rs) b.Encapsulate(r.bounds);
                bool active = node.gameObject.activeSelf;
                string state = !active ? "隐藏（机身内部件）"
                    : node.name == FuselageNode ? "机身外壳（MeshCollider）"
                    : System.Array.IndexOf(MainGearWheelNodes, node.name) >= 0 || System.Array.IndexOf(NoseGearWheelNodes, node.name) >= 0 ? "起落架轮胎（MeshCollider，供激光扫描）" : "";
                if (active) visible += tris; else hiddenTris += tris;
                sb.Append($"| {node.name} | {tris} | ({b.center.x:0.00}, {b.center.y:0.00}, {b.center.z:0.00}) | ({b.size.x:0.00}, {b.size.y:0.00}, {b.size.z:0.00}) | {state} |\n");
            }
            sb.Insert(sb.ToString().IndexOf("| 节点", System.StringComparison.Ordinal), $"可见三角面 {visible}，隐藏 {hiddenTris}。\n\n");
            Directory.CreateDirectory(Path.GetDirectoryName(HierarchyReportPath) ?? ".");
            File.WriteAllText(HierarchyReportPath, sb.ToString(), new UTF8Encoding(false));
        }
    }
}
