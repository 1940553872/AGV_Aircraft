using RivetSim.Core.Config;
using RivetSim.Core.Logging;
using RivetSim.Editor.Tools;
using RivetSim.Simulation.Robot;
using UnityEditor;
using UnityEngine;

namespace RivetSim.Editor.Generators
{
    /// <summary>
    /// 探头末端生成器：快换法兰、浮动夹持、超声探头（含延迟块）、耦合剂管、腕部相机。
    /// 根节点在法兰端面中心，工具轴为本地 +Z；法兰到探头端面总长取 CFG_Arm.toolLengthM，端面处放 TCP 节点。
    /// 根节点挂 <see cref="ProbeEndEffector"/>（M4）：探头与延迟块随接触压缩沿工具轴后退（外观）。
    /// </summary>
    public static class EndEffectorGenerator
    {
        /// <summary>预制体路径。</summary>
        public const string PrefabPath = ProjectPaths.PrefabsRobot + "/PF_EndEffector_Probe.prefab";

        /// <summary>TCP 节点名称。</summary>
        public const string TcpName = "TCP";

        /// <summary>腕部相机节点名称。</summary>
        public const string WristCameraName = "WristCamera";

        // 各段占工具总长的比例与外观直径（单位 m）。
        private const float QuickChangeRatio = 0.15f;
        private const float HolderRatio = 0.35f;
        private const float DelayLineRatio = 0.12f;
        private const float FlangeDiameterM = 0.063f;
        private const float HolderSizeM = 0.045f;
        private const float ProbeDiameterM = 0.016f;

        /// <summary>生成探头末端预制体。</summary>
        [MenuItem("RivetSim/Generate/End Effector", priority = 14)]
        public static void Build()
        {
            var arm = GeneratorUtility.LoadConfig<ArmConfig>(ProjectPaths.ArmAsset);
            if (arm == null) return;

            Material body = MaterialLibraryBuilder.Get(MaterialLibraryBuilder.RobotBody);
            Material accent = MaterialLibraryBuilder.Get(MaterialLibraryBuilder.RobotAccent);
            Material dark = MaterialLibraryBuilder.Get(MaterialLibraryBuilder.RobotDark);
            Material metal = MaterialLibraryBuilder.Get(MaterialLibraryBuilder.MetalBright);
            Material lens = MaterialLibraryBuilder.Get(MaterialLibraryBuilder.LidarWindow);
            Material tube = MaterialLibraryBuilder.Get(MaterialLibraryBuilder.LightStrip);

            var root = new GameObject("PF_EndEffector_Probe");
            Transform t = root.transform;
            float length = arm.toolLengthM;
            float qc = length * QuickChangeRatio;
            float holder = length * HolderRatio;
            float delay = length * DelayLineRatio;
            float probe = length - qc - holder - delay;

            GeneratorUtility.CreateCylinder("QuickChangeFlange", t, new Vector3(0f, 0f, qc * 0.5f), Vector3.forward, FlangeDiameterM, qc, metal);
            Transform floating = GeneratorUtility.CreateNode("FloatingHolder", t, new Vector3(0f, 0f, qc));
            GeneratorUtility.CreateBox("Housing", floating, new Vector3(0f, 0f, holder * 0.5f), new Vector3(HolderSizeM, HolderSizeM, holder), body);
            GeneratorUtility.CreateBox("TravelBand", floating, new Vector3(0f, 0f, holder * 0.75f),
                new Vector3(HolderSizeM + 0.002f, HolderSizeM + 0.002f, Mathf.Max(arm.toolFloatTravelM * 2f, 0.004f)), accent);
            GameObject probeBody = GeneratorUtility.CreateCylinder("Probe", t, new Vector3(0f, 0f, qc + holder + probe * 0.5f), Vector3.forward, ProbeDiameterM, probe, dark);
            GameObject delayLine = GeneratorUtility.CreateCylinder("DelayLine", t, new Vector3(0f, 0f, length - delay * 0.5f), Vector3.forward, ProbeDiameterM * 0.9f, delay, metal);
            GeneratorUtility.CreateCylinder("CouplantTube", t, new Vector3(-HolderSizeM * 0.6f, 0f, qc + (holder + probe) * 0.5f),
                Vector3.forward, 0.004f, holder + probe, tube);
            Transform tcp = GeneratorUtility.CreateNode(TcpName, t, new Vector3(0f, 0f, length));

            // 腕部相机装在法兰侧面，光轴与工具轴平行。
            Transform camera = GeneratorUtility.CreateNode(WristCameraName, t, new Vector3(0f, FlangeDiameterM * 0.5f + 0.02f, qc * 0.5f));
            GeneratorUtility.CreateBox("Body", camera, new Vector3(0f, 0f, 0.01f), new Vector3(0.04f, 0.03f, 0.04f), dark);
            GeneratorUtility.CreateCylinder("Lens", camera, new Vector3(0f, 0f, 0.035f), Vector3.forward, 0.018f, 0.012f, lens);

            root.AddComponent<ProbeEndEffector>().Configure(tcp, camera, new[] { probeBody.transform, delayLine.transform }, arm.toolFloatTravelM);
            GeneratorUtility.SetLayerRecursive(root, SimLayers.RobotIndex);
            GeneratorUtility.SavePrefab(root, PrefabPath);
            SimLog.Info(LogCategory.System, $"探头末端已生成：{PrefabPath}（法兰到端面 {length:0.000} m）");
        }
    }
}
