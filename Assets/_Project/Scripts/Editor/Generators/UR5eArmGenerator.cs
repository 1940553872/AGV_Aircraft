using System.Collections.Generic;
using RivetSim.Core.Config;
using RivetSim.Core.Logging;
using RivetSim.Editor.Importers;
using RivetSim.Editor.Tools;
using RivetSim.Simulation.Robot;
using UnityEditor;
using UnityEngine;

namespace RivetSim.Editor.Generators
{
    /// <summary>
    /// UR5e 机械臂生成器：读取 default_kinematics / joint_limits / visual_parameters 三个 yaml，按 URDF（ur_macro.xacro）的
    /// 连杆链 base_link → base_link_inertia → 6 个转动关节 → flange → tool0 生成 PF_Arm_UR5e，挂 <see cref="ArmRig"/>（不使用 ArticulationBody）。
    /// ROS → Unity 换算见 <see cref="RosFrame"/>；外观网格按 visual_parameters 的 mesh_offset 摆放，原材质（Standard）换成项目的 URP 材质；
    /// 探头末端挂在 tool0 下（工具轴为 tool0 的 ROS z，即 Unity 本地 +Y）。M4：根节点挂 <see cref="ArmController"/>，
    /// 并按外观网格顶点抽稀生成各连杆的表面采样点（连杆间隙检查用，<see cref="ArmLinkSamples"/>）。
    /// </summary>
    public static class UR5eArmGenerator
    {
        /// <summary>预制体路径。</summary>
        public const string PrefabPath = ProjectPaths.PrefabsRobot + "/PF_Arm_UR5e.prefab";

        /// <summary>tool0 节点名称。</summary>
        public const string ToolFlangeName = "tool0";

        // Unity 导入 Z 向上的 DAE 时，网格坐标 u = (−x, z, −y)（相对 DAE 场景坐标 p）；
        // 再绕 Y 轴转 +90° 即得到 RosFrame 换算 (−y, z, x)。2026-10-18 按 7 个网格的包围盒实测确定。
        private static readonly Quaternion DaeImportCorrection = Quaternion.Euler(0f, 90f, 0f);

        // 探头末端沿本地 +Z；tool0 的工具轴为本地 +Y。
        private static readonly Quaternion EndEffectorMount = Quaternion.Euler(-90f, 0f, 0f);

        private sealed class LinkSpec
        {
            public string VisualKey;   // visual_parameters.yaml 中 mesh_files 下的键
            public string MeshFile;    // meshes/visual 下的文件名
            public string KinematicKey; // default_kinematics.yaml 中 kinematics 下的键（基座为空）
            public string JointName;   // joint_limits.yaml 中的关节名（基座为空）
        }

        private static readonly LinkSpec[] Links =
        {
            new LinkSpec { VisualKey = "shoulder", MeshFile = "shoulder", KinematicKey = "shoulder", JointName = "shoulder_pan_joint" },
            new LinkSpec { VisualKey = "upper_arm", MeshFile = "upperarm", KinematicKey = "upper_arm", JointName = "shoulder_lift_joint" },
            new LinkSpec { VisualKey = "forearm", MeshFile = "forearm", KinematicKey = "forearm", JointName = "elbow_joint" },
            new LinkSpec { VisualKey = "wrist_1", MeshFile = "wrist1", KinematicKey = "wrist_1", JointName = "wrist_1_joint" },
            new LinkSpec { VisualKey = "wrist_2", MeshFile = "wrist2", KinematicKey = "wrist_2", JointName = "wrist_2_joint" },
            new LinkSpec { VisualKey = "wrist_3", MeshFile = "wrist3", KinematicKey = "wrist_3", JointName = "wrist_3_joint" },
        };

        // UR 原材质名 → 项目材质名（颜色取原材质的 _Color）。
        private static readonly Dictionary<string, string> MaterialMap = new Dictionary<string, string>
        {
            ["Black"] = "M_UR5e_Black",
            ["JointGrey"] = "M_UR5e_JointGrey",
            ["LinkGrey"] = "M_UR5e_LinkGrey",
            ["URBlue"] = "M_UR5e_Blue",
        };

        // UR 全部转动关节的 URDF 轴为本地 z（axis xyz="0 0 1"）。
        private static readonly Vector3 UrdfJointAxis = new Vector3(0f, 0f, 1f);

        private const float MaterialSmoothness = 0.45f;
        // 表面采样点抽稀网格边长，单位 m（UR5e 连杆 / 探头末端）。
        private const float LinkSampleCellM = 0.02f;
        private const float EndEffectorSampleCellM = 0.01f;
        private const float UrSemicircleRad = Mathf.PI;
        private const float UrQuarterRad = Mathf.PI * 0.5f;

        /// <summary>生成 PF_Arm_UR5e。</summary>
        [MenuItem("RivetSim/Generate/Arm UR5e", priority = 15)]
        public static void Build()
        {
            GameObject endEffector = GeneratorUtility.LoadPrefab(EndEffectorGenerator.PrefabPath);
            if (endEffector == null) return;
            UrYamlReader kinematics = UrYamlReader.Load(ThirdPartyImporter.Ur5eConfigFolder + "/default_kinematics.yaml");
            UrYamlReader limits = UrYamlReader.Load(ThirdPartyImporter.Ur5eConfigFolder + "/joint_limits.yaml");
            UrYamlReader visuals = UrYamlReader.Load(ThirdPartyImporter.Ur5eConfigFolder + "/visual_parameters.yaml");
            var materials = new Dictionary<string, Material>();

            var root = new GameObject("PF_Arm_UR5e");
            Transform baseLink = root.transform;
            // base_link（REP-103，x 前）→ base_link_inertia（绕 z 转 π，UR 内部坐标 x 向后）。
            Transform inertia = GeneratorUtility.CreateNode("base_link_inertia", baseLink, Vector3.zero,
                RosFrame.ToUnityRotation(0f, 0f, UrSemicircleRad));
            AddVisual(inertia, visuals, "base", "base", materials);

            Transform parent = inertia;
            var joints = new ArmJoint[Links.Length];
            for (int i = 0; i < Links.Length; i++)
            {
                LinkSpec link = Links[i];
                string k = $"kinematics.{link.KinematicKey}";
                Vector3 position = RosFrame.ToUnityPosition(new Vector3(
                    kinematics.GetNumber($"{k}.x"), kinematics.GetNumber($"{k}.y"), kinematics.GetNumber($"{k}.z")));
                Quaternion rotation = RosFrame.ToUnityRotation(
                    kinematics.GetRadians($"{k}.roll"), kinematics.GetRadians($"{k}.pitch"), kinematics.GetRadians($"{k}.yaw"));
                Transform jointNode = GeneratorUtility.CreateNode($"J{i + 1}_{link.JointName}", parent, position, rotation);

                string l = $"joint_limits.{link.JointName}";
                var joint = jointNode.gameObject.AddComponent<ArmJoint>();
                joint.Configure(link.JointName, RosFrame.ToUnityDirection(UrdfJointAxis),
                    limits.GetDegrees($"{l}.min_position"), limits.GetDegrees($"{l}.max_position"), limits.GetDegrees($"{l}.max_velocity"));
                joints[i] = joint;

                AddVisual(jointNode, visuals, link.VisualKey, link.MeshFile, materials);
                parent = jointNode;
            }

            // wrist_3_link → flange（rpy 0, −π/2, −π/2）→ tool0（rpy π/2, 0, π/2），取自 ur_macro.xacro。
            Transform flange = GeneratorUtility.CreateNode("flange", parent, Vector3.zero,
                RosFrame.ToUnityRotation(0f, -UrQuarterRad, -UrQuarterRad));
            Transform tool0 = GeneratorUtility.CreateNode(ToolFlangeName, flange, Vector3.zero,
                RosFrame.ToUnityRotation(UrQuarterRad, 0f, UrQuarterRad));
            Transform mount = GeneratorUtility.CreateNode("EndEffectorMount", tool0, Vector3.zero, EndEffectorMount);
            GameObject ee = GeneratorUtility.InstantiatePrefab(endEffector, mount, Vector3.zero, Quaternion.identity, "EndEffector");

            ArmRig rig = root.AddComponent<ArmRig>();
            rig.Configure(joints, tool0);
            var probe = ee.GetComponent<ProbeEndEffector>();
            var samples = new List<ArmLinkSamples> { Sample(inertia, "base", false, LinkSampleCellM) };
            for (int i = 0; i < joints.Length; i++) samples.Add(Sample(joints[i].transform, Links[i].VisualKey, false, LinkSampleCellM));
            samples.Add(Sample(ee.transform, "end_effector", true, EndEffectorSampleCellM));
            rig.ConfigureEndEffector(probe != null ? probe.Tcp : null, probe != null ? probe.WristCamera : null, probe, samples.ToArray());
            var armConfig = GeneratorUtility.LoadConfig<ArmConfig>(ProjectPaths.ArmAsset);
            root.AddComponent<ArmController>().Configure(armConfig, rig);
            GeneratorUtility.SetLayerRecursive(root, SimLayers.RobotIndex);
            GeneratorUtility.SavePrefab(root, PrefabPath);
            LinkKinematicsYaml();
            SimLog.Info(LogCategory.System, $"UR5e 机械臂已生成：{PrefabPath}\n{DescribeJoints(kinematics, limits)}");
        }

        // 连杆表面采样：只取该连杆自身外观（Visual_* 子节点；探头末端取全部网格）的顶点，换到连杆本地坐标后按网格抽稀（每格保留一个顶点）。
        private static ArmLinkSamples Sample(Transform link, string name, bool endEffector, float cellM)
        {
            var cells = new Dictionary<Vector3Int, Vector3>();
            var vertices = new List<Vector3>();
            Matrix4x4 toLink = link.worldToLocalMatrix;
            foreach (MeshFilter mf in link.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null || !BelongsTo(mf.transform, link, endEffector)) continue;
                Matrix4x4 m = toLink * mf.transform.localToWorldMatrix;
                mf.sharedMesh.GetVertices(vertices);
                foreach (Vector3 v in vertices)
                {
                    Vector3 p = m.MultiplyPoint3x4(v);
                    var key = Vector3Int.FloorToInt(p / cellM);
                    if (!cells.ContainsKey(key)) cells[key] = p;
                }
            }
            var points = new Vector3[cells.Count];
            cells.Values.CopyTo(points, 0);
            Vector3 min = Vector3.one * float.MaxValue, max = Vector3.one * float.MinValue;
            foreach (Vector3 p in points)
            {
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
            }
            Vector3 center = points.Length > 0 ? 0.5f * (min + max) : Vector3.zero;
            float radius = 0f;
            foreach (Vector3 p in points) radius = Mathf.Max(radius, (p - center).magnitude);
            return new ArmLinkSamples
            {
                link = link, linkName = name, isEndEffector = endEffector, pointsLocal = points, boundsCenterLocal = center, boundsRadiusM = radius,
            };
        }

        // 网格是否属于该连杆自身：探头末端取全部；UR 连杆只取路径上第一个子节点名以 Visual_ 开头的网格（不含下游关节）。
        private static bool BelongsTo(Transform t, Transform link, bool endEffector)
        {
            if (endEffector) return true;
            Transform child = t;
            while (child != null && child.parent != link) child = child.parent;
            return child != null && child.name.StartsWith("Visual_");
        }

        /// <summary>逐关节列出原点（ROS 坐标）与限位，供与 yaml 核对。</summary>
        public static string DescribeJoints(UrYamlReader kinematics, UrYamlReader limits)
        {
            var sb = new System.Text.StringBuilder();
            foreach (LinkSpec link in Links)
            {
                string k = $"kinematics.{link.KinematicKey}";
                string l = $"joint_limits.{link.JointName}";
                sb.Append($"{link.JointName}: xyz=({kinematics.GetNumber($"{k}.x"):0.####}, {kinematics.GetNumber($"{k}.y"):0.####}, {kinematics.GetNumber($"{k}.z"):0.####}) ")
                  .Append($"rpy=({kinematics.GetRadians($"{k}.roll"):0.####}, {kinematics.GetRadians($"{k}.pitch"):0.####}, {kinematics.GetRadians($"{k}.yaw"):0.####}) rad ")
                  .Append($"限位 {limits.GetDegrees($"{l}.min_position"):0}～{limits.GetDegrees($"{l}.max_position"):0}° 速度 {limits.GetDegrees($"{l}.max_velocity"):0}°/s\n");
            }
            return sb.ToString();
        }

        private static void AddVisual(Transform link, UrYamlReader visuals, string visualKey, string meshFile, Dictionary<string, Material> materials)
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>($"{ThirdPartyImporter.Ur5eMeshFolder}/{meshFile}.dae");
            if (model == null)
            {
                SimLog.Error(LogCategory.System, $"找不到 UR5e 网格：{meshFile}.dae（请先执行 RivetSim/Import/Third Party Assets）");
                return;
            }
            string o = $"mesh_files.{visualKey}.mesh_offset";
            Vector3 position = RosFrame.ToUnityPosition(new Vector3(visuals.GetNumber($"{o}.x"), visuals.GetNumber($"{o}.y"), visuals.GetNumber($"{o}.z")));
            Quaternion rotation = RosFrame.ToUnityRotation(visuals.GetRadians($"{o}.roll"), visuals.GetRadians($"{o}.pitch"), visuals.GetRadians($"{o}.yaw"));
            Transform visual = GeneratorUtility.CreateNode($"Visual_{visualKey}", link, position, rotation);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, visual);
            instance.name = meshFile;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = DaeImportCorrection * model.transform.localRotation;

            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                Material[] shared = renderer.sharedMaterials;
                for (int i = 0; i < shared.Length; i++) shared[i] = ProjectMaterial(shared[i], materials);
                renderer.sharedMaterials = shared;
            }
        }

        private static Material ProjectMaterial(Material source, Dictionary<string, Material> cache)
        {
            string key = source != null && MaterialMap.ContainsKey(source.name) ? source.name : "LinkGrey";
            if (cache.TryGetValue(key, out Material cached)) return cached;
            Material material = MaterialLibraryBuilder.EnsureLitMaterial(MaterialMap[key]);
            Color color = source != null && source.HasProperty("_Color") ? source.color : Color.gray;
            MaterialLibraryBuilder.ApplyColor(material, color, MaterialSmoothness, 0f, Color.black);
            EditorUtility.SetDirty(material);
            cache[key] = material;
            return material;
        }

        // CFG_Arm.kinematicsYaml 指向导入后的 default_kinematics.yaml（ArmConfig 注释“M2 导入后指定”）。
        private static void LinkKinematicsYaml()
        {
            var arm = GeneratorUtility.LoadConfig<ArmConfig>(ProjectPaths.ArmAsset);
            var yaml = AssetDatabase.LoadAssetAtPath<TextAsset>(ThirdPartyImporter.Ur5eConfigFolder + "/default_kinematics.yaml");
            if (arm == null || yaml == null || arm.kinematicsYaml == yaml) return;
            var so = new SerializedObject(arm);
            so.FindProperty("kinematicsYaml").objectReferenceValue = yaml;
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
        }
    }
}
