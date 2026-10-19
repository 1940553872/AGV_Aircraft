using System.Collections.Generic;
using RivetSim.Core.Config;
using RivetSim.Core.Logging;
using RivetSim.Editor.Tools;
using RivetSim.Presentation.Visualization;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace RivetSim.Editor.Generators
{
    /// <summary>
    /// 设备发光生成器（M5）：在 PF_AGV 与 PF_DemoStation 的 S1～S8 设备节点上挂 <see cref="DeviceIndicator"/>，
    /// 并为节点下每个网格渲染器复制一层略放大的纯视觉外壳（名称前缀 <see cref="SimLayers.VisualOnlyPrefix"/>，材质 M_DeviceGlow，默认隐藏、无碰撞体）。
    /// S3 同时包含机械臂与升降柱；S3 不含末端执行器（S4），S4 不含腕部相机（S2）。幂等：先删除上一次生成的外壳与组件。
    /// </summary>
    public static class DeviceIndicatorGenerator
    {
        private const string GlowName = SimLayers.VisualOnlyPrefix + "Glow";
        private const float GlowScale = 1.04f;

        // 主体 id、说明、设备节点名（可多个，第一个挂组件）、排除的子树名。
        private static readonly (string Actor, string Label, string[] Nodes, string[] Excluded)[] AgvDevices =
        {
            ("a1", "S1 导航：AGV 底盘", new[] { "Chassis" }, new string[0]),
            ("a2", "S2 视觉：腕部相机", new[] { EndEffectorGenerator.WristCameraName }, new string[0]),
            ("a3", "S3 机械臂：机械臂与升降柱", new[] { "Arm", AgvGenerator.LiftName }, new[] { "EndEffector", "Arm" }),
            ("a4", "S4 贴合采集：探头末端", new[] { "EndEffector" }, new[] { EndEffectorGenerator.WristCameraName }),
            ("a5", "S5 超声接口：车载超声仪", new[] { "Device_S5_UT" }, new string[0]),
            ("a6", "S6 安全：安全控制盒", new[] { "Device_S6_Safety" }, new string[0]),
            ("a7", "S7 调度：车载工控机", new[] { "Device_S7_IPC" }, new string[0]),
        };

        private static readonly (string Actor, string Label, string[] Nodes, string[] Excluded) DeskDevice =
            ("a8", "S8 数据：演示台电脑", new[] { "Device_S8_PC" }, new string[0]);

        /// <summary>为 AGV 与演示台生成设备发光。</summary>
        [MenuItem("RivetSim/Generate/Device Indicators", priority = 45)]
        public static void Build()
        {
            ApplyToAgv();
            ApplyToDemoStation();
        }

        /// <summary>为 PF_AGV 生成 S1～S7。</summary>
        public static void ApplyToAgv() => Apply(AgvGenerator.AgvPrefabPath, AgvDevices);

        /// <summary>为 PF_DemoStation 生成 S8。</summary>
        public static void ApplyToDemoStation() => Apply(DemoStationGenerator.DeskPrefabPath, new[] { DeskDevice });

        private static void Apply(string prefabPath, (string Actor, string Label, string[] Nodes, string[] Excluded)[] devices)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
            {
                SimLog.Error(LogCategory.System, $"找不到预制体 {prefabPath}，请先执行对应生成器。");
                return;
            }
            Material glow = MaterialLibraryBuilder.Get(MaterialLibraryBuilder.DeviceGlow);
            var theme = AssetDatabase.LoadAssetAtPath<UiThemeConfig>($"{ProjectPaths.ConfigFolder}/CFG_UiTheme.asset");
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                Clean(root.transform);
                int shells = 0;
                foreach ((string actor, string label, string[] nodes, string[] excluded) in devices)
                {
                    var renderers = new List<Renderer>();
                    Transform host = null;
                    foreach (string nodeName in nodes)
                    {
                        Transform node = FindDeep(root.transform, nodeName);
                        if (node == null)
                        {
                            SimLog.Error(LogCategory.System, $"{prefabPath}：找不到设备节点 {nodeName}（{label}）");
                            continue;
                        }
                        if (host == null) host = node;
                        CollectShells(node, node, excluded, glow, renderers);
                    }
                    if (host == null) continue;
                    host.gameObject.AddComponent<DeviceIndicator>().Configure(actor, label, renderers.ToArray(), theme);
                    shells += renderers.Count;
                }
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                SimLog.Info(LogCategory.System, $"设备发光已生成：{prefabPath}，{devices.Length} 个设备、{shells} 个外壳");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void CollectShells(Transform node, Transform deviceRoot, string[] excluded, Material glow, List<Renderer> output)
        {
            if (node != deviceRoot && System.Array.IndexOf(excluded, node.name) >= 0) return;
            var children = new List<Transform>();
            foreach (Transform child in node) children.Add(child);

            var meshRenderer = node.GetComponent<MeshRenderer>();
            var filter = node.GetComponent<MeshFilter>();
            if (meshRenderer != null && filter != null && filter.sharedMesh != null && !node.name.StartsWith(SimLayers.VisualOnlyPrefix, System.StringComparison.Ordinal))
            {
                var shell = new GameObject(GlowName);
                shell.layer = node.gameObject.layer;
                shell.transform.SetParent(node, false);
                shell.transform.localScale = Vector3.one * GlowScale;
                shell.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                var r = shell.AddComponent<MeshRenderer>();
                Material[] mats = new Material[Mathf.Max(1, filter.sharedMesh.subMeshCount)];
                for (int i = 0; i < mats.Length; i++) mats[i] = glow;
                r.sharedMaterials = mats;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.lightProbeUsage = LightProbeUsage.Off;
                r.reflectionProbeUsage = ReflectionProbeUsage.Off;
                r.enabled = false;
                output.Add(r);
            }
            foreach (Transform child in children) CollectShells(child, deviceRoot, excluded, glow, output);
        }

        private static void Clean(Transform root)
        {
            foreach (DeviceIndicator indicator in root.GetComponentsInChildren<DeviceIndicator>(true)) Object.DestroyImmediate(indicator);
            var stale = new List<GameObject>();
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == GlowName) stale.Add(t.gameObject);
            foreach (GameObject go in stale) Object.DestroyImmediate(go);
        }

        private static Transform FindDeep(Transform parent, string name)
        {
            if (parent.name == name && parent.parent != null) return parent;
            foreach (Transform child in parent)
            {
                Transform found = FindDeep(child, name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
