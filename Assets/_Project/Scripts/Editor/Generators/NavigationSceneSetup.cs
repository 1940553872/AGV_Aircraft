using RivetSim.Core.Config;
using RivetSim.Editor.Tools;
using RivetSim.Presentation.Input;
using RivetSim.Presentation.UI;
using RivetSim.Presentation.Visualization;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace RivetSim.Editor.Generators
{
    /// <summary>
    /// 导航可视化与输入的场景装配（主场景与 Sandbox_Robot 共用，M3）：地图叠加层、虚拟禁行区叠加层、激光点云、路径与目标位姿
    /// （Visualization 层，按 V 开关），导航叠加信息 UIDocument，以及挂在入口物体上的键盘遥控与可视化开关；
    /// M4 起另有检测可视化（铆钉结果环、腕部相机视场锥、探头接触指示）与检测叠加信息（<see cref="BuildInspectionViews"/>）。
    /// </summary>
    public static class NavigationSceneSetup
    {
        // 线宽（外观常量，ADR-009）。
        private const float PathWidthM = 0.07f;
        private const float ArrowWidthM = 0.09f;

        /// <summary>生成的可视化与输入组件。</summary>
        public readonly struct Views
        {
            /// <summary>地图叠加层。</summary>
            public readonly OccupancyGridOverlay Map;
            /// <summary>禁行区叠加层。</summary>
            public readonly KeepOutOverlay KeepOut;
            /// <summary>激光点云。</summary>
            public readonly LidarPointCloudView Lidar;
            /// <summary>路径显示。</summary>
            public readonly PathView Path;
            /// <summary>叠加信息。</summary>
            public readonly NavOverlayView Overlay;
            /// <summary>键盘遥控。</summary>
            public readonly AgvTeleopInput Teleop;

            /// <summary>创建。</summary>
            public Views(OccupancyGridOverlay map, KeepOutOverlay keepOut, LidarPointCloudView lidar, PathView path, NavOverlayView overlay, AgvTeleopInput teleop)
            {
                Map = map;
                KeepOut = keepOut;
                Lidar = lidar;
                Path = path;
                Overlay = overlay;
                Teleop = teleop;
            }
        }

        // 视场锥线宽（外观常量）。
        private const float FrustumWidthM = 0.002f;

        /// <summary>M4 检测可视化组件。</summary>
        public readonly struct InspectionViews
        {
            /// <summary>检测叠加信息。</summary>
            public readonly InspectionOverlayView Overlay;
            /// <summary>铆钉结果环。</summary>
            public readonly RivetResultRingView Rings;
            /// <summary>腕部相机视场锥。</summary>
            public readonly WristCameraFrustumView Frustum;
            /// <summary>探头接触指示。</summary>
            public readonly ProbeContactView Contact;

            /// <summary>创建。</summary>
            public InspectionViews(InspectionOverlayView overlay, RivetResultRingView rings, WristCameraFrustumView frustum, ProbeContactView contact)
            {
                Overlay = overlay;
                Rings = rings;
                Frustum = frustum;
                Contact = contact;
            }
        }

        /// <summary>在 visualizationRoot / uiRoot 下生成 M4 检测可视化与检测叠加信息（Visualization 层，V 开关）。</summary>
        public static InspectionViews BuildInspectionViews(Transform visualizationRoot, Transform uiRoot)
        {
            Transform ringsNode = GeneratorUtility.CreateNode("RivetResultRings", visualizationRoot, Vector3.zero);
            var rings = ringsNode.gameObject.AddComponent<RivetResultRingView>();
            rings.Configure(MaterialLibraryBuilder.Get(MaterialLibraryBuilder.VisResultPass), MaterialLibraryBuilder.Get(MaterialLibraryBuilder.VisResultSuspect),
                MaterialLibraryBuilder.Get(MaterialLibraryBuilder.VisResultDefect), MaterialLibraryBuilder.Get(MaterialLibraryBuilder.VisResultInvalid));
            LineRenderer frustumLine = CreateLine("WristCameraFrustum", visualizationRoot, MaterialLibraryBuilder.VisCameraFrustum, FrustumWidthM);
            frustumLine.alignment = LineAlignment.View;
            frustumLine.transform.localRotation = Quaternion.identity;
            var frustum = frustumLine.gameObject.AddComponent<WristCameraFrustumView>();
            LineRenderer contactLine = CreateLine("ProbeContact", visualizationRoot, MaterialLibraryBuilder.VisResultInvalid, FrustumWidthM);
            var contact = contactLine.gameObject.AddComponent<ProbeContactView>();
            GeneratorUtility.SetLayerRecursive(visualizationRoot.gameObject, SimLayers.VisualizationIndex);

            Transform overlayNode = GeneratorUtility.CreateNode("InspectionOverlay", uiRoot, Vector3.zero);
            var document = overlayNode.gameObject.AddComponent<UIDocument>();
            document.panelSettings = SandboxSceneBuilder.EnsurePanelSettings();
            document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(ProjectPaths.InspectionOverlayUxml);
            var overlay = overlayNode.gameObject.AddComponent<InspectionOverlayView>();
            return new InspectionViews(overlay, rings, frustum, contact);
        }

        /// <summary>在 visualizationRoot / uiRoot 下生成可视化与叠加信息，遥控与 V 开关挂在 entry 上。</summary>
        public static Views Build(Transform visualizationRoot, Transform uiRoot, GameObject entry, Camera camera)
        {
            OccupancyGridOverlay map = CreateOverlay<OccupancyGridOverlay>("MapOverlay", visualizationRoot, MaterialLibraryBuilder.VisMapOverlay);
            KeepOutOverlay keepOut = CreateOverlay<KeepOutOverlay>("KeepOutOverlay", visualizationRoot, MaterialLibraryBuilder.VisKeepOut);
            LidarPointCloudView lidar = CreateOverlay<LidarPointCloudView>("LidarPoints", visualizationRoot, MaterialLibraryBuilder.VisLidarPoint);

            Transform pathNode = GeneratorUtility.CreateNode("PlannedPath", visualizationRoot, Vector3.zero);
            var path = pathNode.gameObject.AddComponent<PathView>();
            path.Configure(CreateLine("PathLine", pathNode, MaterialLibraryBuilder.VisPath, PathWidthM),
                CreateLine("GoalArrow", pathNode, MaterialLibraryBuilder.VisGoal, ArrowWidthM));
            GeneratorUtility.SetLayerRecursive(visualizationRoot.gameObject, SimLayers.VisualizationIndex);

            Transform overlayNode = GeneratorUtility.CreateNode("NavOverlay", uiRoot, Vector3.zero);
            var document = overlayNode.gameObject.AddComponent<UIDocument>();
            document.panelSettings = SandboxSceneBuilder.EnsurePanelSettings();
            document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(ProjectPaths.NavOverlayUxml);
            var overlay = overlayNode.gameObject.AddComponent<NavOverlayView>();
            overlay.Configure(camera);

            var teleop = entry.AddComponent<AgvTeleopInput>();
            entry.AddComponent<VisualizationToggleInput>().Configure(visualizationRoot.gameObject);
            return new Views(map, keepOut, lidar, path, overlay, teleop);
        }

        private static T CreateOverlay<T>(string name, Transform parent, string materialName) where T : Component
        {
            Transform node = GeneratorUtility.CreateNode(name, parent, Vector3.zero);
            node.gameObject.AddComponent<MeshFilter>();
            var renderer = node.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = MaterialLibraryBuilder.Get(materialName);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return node.gameObject.AddComponent<T>();
        }

        private static LineRenderer CreateLine(string name, Transform parent, string materialName, float widthM)
        {
            Transform node = GeneratorUtility.CreateNode(name, parent, Vector3.zero);
            var line = node.gameObject.AddComponent<LineRenderer>();
            line.sharedMaterial = MaterialLibraryBuilder.Get(materialName);
            line.useWorldSpace = true;
            line.widthMultiplier = widthM;
            line.alignment = LineAlignment.TransformZ;
            node.localRotation = Quaternion.Euler(90f, 0f, 0f);
            line.numCornerVertices = 2;
            line.numCapVertices = 2;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.positionCount = 0;
            return line;
        }
    }
}
