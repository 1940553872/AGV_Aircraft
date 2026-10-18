using System;
using RivetSim.Core.Logging;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace RivetSim.Editor.Generators
{
    /// <summary>
    /// 生成器共用工具：读取配置、建目录、创建基本体、保存预制体、查找场景根节点。
    /// </summary>
    public static class GeneratorUtility
    {
        /// <summary>读取配置资产，缺失时记录错误并返回 null。</summary>
        public static T LoadConfig<T>(string path) where T : ScriptableObject
        {
            var config = AssetDatabase.LoadAssetAtPath<T>(path);
            if (config == null)
                SimLog.Error(LogCategory.System, $"找不到配置 {path}，请先执行 RivetSim/Setup/Create Default Configs。");
            return config;
        }

        /// <summary>逐级创建资产目录。</summary>
        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int split = path.LastIndexOf("/", StringComparison.Ordinal);
            string parent = path.Substring(0, split);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(split + 1));
        }

        /// <summary>创建空节点。</summary>
        public static Transform CreateNode(string name, Transform parent, Vector3 localPosition, Quaternion localRotation)
        {
            var go = new GameObject(name);
            Transform t = go.transform;
            t.SetParent(parent, false);
            t.localPosition = localPosition;
            t.localRotation = localRotation;
            return t;
        }

        /// <summary>创建空节点（无旋转）。</summary>
        public static Transform CreateNode(string name, Transform parent, Vector3 localPosition)
        {
            return CreateNode(name, parent, localPosition, Quaternion.identity);
        }

        /// <summary>创建长方体，size 为 Unity 本地缩放（X, Y, Z）。</summary>
        public static GameObject CreateBox(string name, Transform parent, Vector3 localPosition, Vector3 size,
            Material material, bool keepCollider = false)
        {
            return CreatePrimitive(PrimitiveType.Cube, name, parent, localPosition, Quaternion.identity, size, material, keepCollider);
        }

        /// <summary>创建带旋转的长方体。</summary>
        public static GameObject CreateBox(string name, Transform parent, Vector3 localPosition, Quaternion localRotation,
            Vector3 size, Material material, bool keepCollider = false)
        {
            return CreatePrimitive(PrimitiveType.Cube, name, parent, localPosition, localRotation, size, material, keepCollider);
        }

        /// <summary>创建圆柱，axis 为圆柱轴线方向（本地），直径与长度单位 m。</summary>
        public static GameObject CreateCylinder(string name, Transform parent, Vector3 localPosition, Vector3 axis,
            float diameterM, float lengthM, Material material, bool keepCollider = false)
        {
            // Unity 圆柱沿 Y 轴，高 2 单位、直径 1 单位。
            Quaternion rotation = Quaternion.FromToRotation(Vector3.up, axis.normalized);
            return CreatePrimitive(PrimitiveType.Cylinder, name, parent, localPosition, rotation,
                new Vector3(diameterM, lengthM * 0.5f, diameterM), material, keepCollider);
        }

        /// <summary>创建球体。</summary>
        public static GameObject CreateSphere(string name, Transform parent, Vector3 localPosition, float diameterM,
            Material material)
        {
            return CreatePrimitive(PrimitiveType.Sphere, name, parent, localPosition, Quaternion.identity,
                Vector3.one * diameterM, material, false);
        }

        /// <summary>创建平放在地面上的四边形（法线朝 +Y，贴图上方朝 +Z），size 为（沿 X, 沿 Z）。</summary>
        public static GameObject CreateFloorQuad(string name, Transform parent, Vector3 localPosition, Vector2 size,
            float yawDeg, Material material)
        {
            // Quad 法线朝 −Z；绕 X 转 90° 后法线朝 +Y、贴图上方朝 +Z。
            Quaternion rotation = Quaternion.Euler(0f, yawDeg, 0f) * Quaternion.Euler(90f, 0f, 0f);
            return CreatePrimitive(PrimitiveType.Quad, name, parent, localPosition, rotation,
                new Vector3(size.x, size.y, 1f), material, false);
        }

        /// <summary>创建竖直四边形屏幕（可见面朝本地 +Z），size 为（宽, 高）。</summary>
        public static GameObject CreateScreenQuad(string name, Transform parent, Vector3 localPosition, Vector2 size,
            Material material)
        {
            return CreatePrimitive(PrimitiveType.Quad, name, parent, localPosition, Quaternion.Euler(0f, 180f, 0f),
                new Vector3(size.x, size.y, 1f), material, false);
        }

        /// <summary>创建基本体并设置材质；默认移除碰撞体。</summary>
        public static GameObject CreatePrimitive(PrimitiveType type, string name, Transform parent, Vector3 localPosition,
            Quaternion localRotation, Vector3 localScale, Material material, bool keepCollider)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
            Transform t = go.transform;
            t.SetParent(parent, false);
            t.localPosition = localPosition;
            t.localRotation = localRotation;
            t.localScale = localScale;
            if (material != null) go.GetComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        /// <summary>给节点添加盒碰撞体（本地中心与尺寸）。</summary>
        public static BoxCollider AddBoxCollider(GameObject go, Vector3 center, Vector3 size)
        {
            var collider = go.AddComponent<BoxCollider>();
            collider.center = center;
            collider.size = size;
            return collider;
        }

        /// <summary>递归设置 Layer。</summary>
        public static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform) SetLayerRecursive(child.gameObject, layer);
        }

        /// <summary>保存为预制体（覆盖同路径资产并保留 GUID），然后销毁临时根节点。</summary>
        public static GameObject SavePrefab(GameObject root, string path)
        {
            EnsureFolder(path.Substring(0, path.LastIndexOf("/", StringComparison.Ordinal)));
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
            Object.DestroyImmediate(root);
            if (!success) SimLog.Error(LogCategory.System, $"预制体保存失败：{path}");
            return prefab;
        }

        /// <summary>读取预制体，缺失时记录错误。</summary>
        public static GameObject LoadPrefab(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) SimLog.Error(LogCategory.System, $"找不到预制体 {path}，请先执行对应生成器。");
            return prefab;
        }

        /// <summary>在父节点下实例化预制体（保持预制体连接）。</summary>
        public static GameObject InstantiatePrefab(GameObject prefab, Transform parent, Vector3 localPosition,
            Quaternion localRotation, string name = null)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.transform.localPosition = localPosition;
            instance.transform.localRotation = localRotation;
            if (name != null) instance.name = name;
            return instance;
        }

        /// <summary>在场景中创建根节点（场景应为新建的空场景）。</summary>
        public static Transform CreateRoot(Scene scene, string name)
        {
            var go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, scene);
            return go.transform;
        }

        /// <summary>按名称查找场景根节点，不存在返回 null。</summary>
        public static Transform FindRoot(Scene scene, string name)
        {
            foreach (GameObject go in scene.GetRootGameObjects())
            {
                if (go.name == name) return go.transform;
            }
            return null;
        }
    }
}
