using System.Collections.Generic;
using RivetSim.Core.Config;
using RivetSim.Core.Logging;
using RivetSim.Editor.Tools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace RivetSim.Editor.Generators
{
    /// <summary>
    /// A320 机身段生成器（M4 沙盒用，ADR-016）：从 PF_Aircraft_A320 的机身外壳网格（<see cref="AircraftA320Builder.FuselageNode"/>）中
    /// 截取主起落架后 SectionAftStartM～SectionAftEndM 的整圈三角形（三个顶点都在范围内才保留），顶点换算到 PF_Aircraft_A320 根节点坐标
    /// （单位 m，原点在主起落架中点地面投影，机头朝 +Z），保存为生成网格与 PF_FuselageSection（MeshCollider，Aircraft 层）。
    /// 机身段按与整机相同的根节点位姿摆放时几何完全一致，可直接复用 RivetZoneGenerator 的投影逻辑。重复执行覆盖上次结果（网格 GUID 不变）。
    /// </summary>
    public static class FuselageSectionBuilder
    {
        /// <summary>预制体路径。</summary>
        public const string PrefabPath = ProjectPaths.PrefabsAircraft + "/PF_FuselageSection.prefab";

        /// <summary>生成网格路径。</summary>
        public const string MeshPath = ProjectPaths.GeneratedModelsFolder + "/A320_FuselageSection.asset";

        // 截取范围（沙盒测试布置常量）：覆盖两个工位的铆钉排与基准标记（区域中心在主起落架后 4.85 m，工位间距 1.5 m）并各留约 1 m 余量。
        private const float SectionAftStartM = 2.6f;
        private const float SectionAftEndM = 7.1f;

        /// <summary>生成机身段网格与预制体。</summary>
        [MenuItem("RivetSim/Generate/Fuselage Section", priority = 13)]
        public static void Build()
        {
            GameObject a320 = GeneratorUtility.LoadPrefab(AircraftA320Builder.PrefabPath);
            if (a320 == null) return;
            var preview = EditorSceneManager.NewPreviewScene();
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(a320, preview);
            try
            {
                instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                Transform shell = FindDeep(instance.transform, AircraftA320Builder.FuselageNode);
                MeshFilter filter = shell != null ? shell.GetComponentInChildren<MeshFilter>() : null;
                if (filter == null || filter.sharedMesh == null)
                {
                    SimLog.Error(LogCategory.System, $"机身段生成失败：找不到机身外壳 {AircraftA320Builder.FuselageNode}");
                    return;
                }
                Mesh mesh = Extract(filter, instance.transform, out int[] keptSubmeshes, out int triangles);
                Mesh saved = SaveMesh(mesh);
                Material[] source = filter.GetComponent<MeshRenderer>().sharedMaterials;
                var materials = new Material[keptSubmeshes.Length];
                for (int i = 0; i < keptSubmeshes.Length; i++) materials[i] = source[Mathf.Min(keptSubmeshes[i], source.Length - 1)];
                BuildPrefab(saved, materials);
                SimLog.Info(LogCategory.System,
                    $"机身段已生成：{PrefabPath}（主起落架后 {SectionAftStartM:0.0}～{SectionAftEndM:0.0} m，{triangles:N0} 个三角形，{saved.vertexCount:N0} 个顶点）");
            }
            finally
            {
                Object.DestroyImmediate(instance);
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        private static Mesh Extract(MeshFilter filter, Transform root, out int[] keptSubmeshes, out int triangleCount)
        {
            Mesh src = filter.sharedMesh;
            Matrix4x4 m = root.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            var srcVertices = new List<Vector3>();
            var srcNormals = new List<Vector3>();
            var srcUv = new List<Vector2>();
            src.GetVertices(srcVertices);
            src.GetNormals(srcNormals);
            src.GetUVs(0, srcUv);
            var p = new Vector3[srcVertices.Count];
            for (int i = 0; i < p.Length; i++) p[i] = m.MultiplyPoint3x4(srcVertices[i]);

            var remap = new Dictionary<int, int>();
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uv = new List<Vector2>();
            var submeshes = new List<List<int>>();
            var kept = new List<int>();
            var indices = new List<int>();
            triangleCount = 0;
            for (int sub = 0; sub < src.subMeshCount; sub++)
            {
                src.GetTriangles(indices, sub);
                var outIndices = new List<int>();
                for (int t = 0; t + 2 < indices.Count; t += 3)
                {
                    if (!InRange(p[indices[t]]) || !InRange(p[indices[t + 1]]) || !InRange(p[indices[t + 2]])) continue;
                    for (int k = 0; k < 3; k++)
                    {
                        int s = indices[t + k];
                        if (!remap.TryGetValue(s, out int d))
                        {
                            d = vertices.Count;
                            remap[s] = d;
                            vertices.Add(p[s]);
                            normals.Add(s < srcNormals.Count ? m.MultiplyVector(srcNormals[s]).normalized : Vector3.up);
                            uv.Add(s < srcUv.Count ? srcUv[s] : Vector2.zero);
                        }
                        outIndices.Add(d);
                    }
                    triangleCount++;
                }
                if (outIndices.Count == 0) continue;
                submeshes.Add(outIndices);
                kept.Add(sub);
            }

            var mesh = new Mesh { name = "A320_FuselageSection", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uv);
            mesh.subMeshCount = submeshes.Count;
            for (int i = 0; i < submeshes.Count; i++) mesh.SetTriangles(submeshes[i], i);
            mesh.RecalculateBounds();
            keptSubmeshes = kept.ToArray();
            return mesh;
        }

        private static bool InRange(Vector3 p) => p.z <= -SectionAftStartM && p.z >= -SectionAftEndM;

        // 已有网格资产时就地覆盖内容，GUID 不变。
        private static Mesh SaveMesh(Mesh mesh)
        {
            GeneratorUtility.EnsureFolder(ProjectPaths.GeneratedModelsFolder);
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, MeshPath);
                return mesh;
            }
            EditorUtility.CopySerialized(mesh, existing);
            existing.name = mesh.name;
            Object.DestroyImmediate(mesh);
            EditorUtility.SetDirty(existing);
            AssetDatabase.SaveAssets();
            return existing;
        }

        private static void BuildPrefab(Mesh mesh, Material[] materials)
        {
            var root = new GameObject("PF_FuselageSection");
            Transform shell = GeneratorUtility.CreateNode("Shell", root.transform, Vector3.zero);
            shell.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            shell.gameObject.AddComponent<MeshRenderer>().sharedMaterials = materials;
            shell.gameObject.AddComponent<MeshCollider>().sharedMesh = mesh;
            GeneratorUtility.SetLayerRecursive(root, SimLayers.AircraftIndex);
            GeneratorUtility.SavePrefab(root, PrefabPath);
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                Transform found = FindDeep(child, name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
