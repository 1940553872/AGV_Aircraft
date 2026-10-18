using System.Collections.Generic;
using NUnit.Framework;
using RivetSim.Simulation.Robot;
using UnityEngine;

namespace RivetSim.Tests.EditMode
{
    /// <summary>
    /// 机械臂运动学与逆解测试（Architecture §8）：ArmKinematics.FromRig 与 Transform 链一致；随机可达位姿逆解收敛率 ≥ 95%、位置误差 &lt; 1 mm；
    /// 逆解结果在关节限位内。运动学参数取 UR5e default_kinematics.yaml，探头末端长 0.15 m。
    /// </summary>
    public sealed class ArmIkSolverTests
    {
        // UR5e default_kinematics.yaml（ur_description 4.3.1）：关节原点 xyz（m）与 rpy（rad）。
        private static readonly (Vector3 Xyz, Vector3 Rpy)[] Ur5eOrigins =
        {
            (new Vector3(0f, 0f, 0.1625f), Vector3.zero),
            (Vector3.zero, new Vector3(1.570796327f, 0f, 0f)),
            (new Vector3(-0.425f, 0f, 0f), Vector3.zero),
            (new Vector3(-0.3922f, 0f, 0.1333f), Vector3.zero),
            (new Vector3(0f, -0.0997f, 0f), new Vector3(1.570796327f, 0f, 0f)),
            (new Vector3(0f, 0.0996f, 0f), new Vector3(1.570796327f, Mathf.PI, Mathf.PI)),
        };

        private const float ToolLengthM = 0.15f;
        private const int RandomPoseCount = 200;
        private const int Seed = 42;
        private static readonly float[] ObserveSeed = { -61f, -82f, 146f, -82f, -243f, -98f };

        private readonly List<GameObject> _created = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _created) Object.DestroyImmediate(go);
            _created.Clear();
        }

        [Test]
        public void FromRig_MatchesTransformChain()
        {
            ArmRig rig = BuildRig(out Transform tcp);
            ArmKinematics kinematics = ArmKinematics.FromRig(rig, tcp);
            var rng = new System.Random(Seed);
            var q = new float[6];
            for (int k = 0; k < 20; k++)
            {
                for (int i = 0; i < 6; i++) q[i] = (float)(rng.NextDouble() * 360.0 - 180.0);
                rig.SetJointAnglesDeg(q);
                Pose fk = kinematics.Forward(q);
                Vector3 expected = rig.transform.InverseTransformPoint(tcp.position);
                Assert.That(Vector3.Distance(fk.position, expected), Is.LessThan(1e-4f));
                Assert.That(Quaternion.Angle(fk.rotation, Quaternion.Inverse(rig.transform.rotation) * tcp.rotation), Is.LessThan(0.01f));
            }
        }

        [Test]
        public void RandomReachablePoses_ConvergeWithinOneMillimetre()
        {
            ArmKinematics kinematics = Kinematics();
            var solver = new ArmIkSolver(kinematics, Settings(), Seed);
            var rng = new System.Random(Seed);
            var q = new float[6];
            var solution = new float[6];
            var stats = new IkStatistics();
            var seeds = new List<float[]> { ObserveSeed };
            for (int k = 0; k < RandomPoseCount; k++)
            {
                for (int i = 0; i < 6; i++) q[i] = (float)((rng.NextDouble() * 2.0 - 1.0) * (i == 2 ? 160.0 : 180.0));
                IkResult r = solver.Solve(kinematics.Forward(q), seeds, null, solution);
                stats.Record(r);
                if (!r.Converged) continue;
                Pose check = kinematics.Forward(solution);
                Assert.That(Vector3.Distance(check.position, kinematics.Forward(q).position), Is.LessThan(0.001f));
            }
            TestContext.WriteLine(stats.ToString());
            Assert.That(stats.ConvergenceRate, Is.GreaterThanOrEqualTo(0.95f));
            Assert.That(stats.MaxPositionErrorM, Is.LessThan(0.001f));
            Assert.That(stats.MaxAngleErrorDeg, Is.LessThan(1f));
        }

        [Test]
        public void Solution_RespectsJointLimits()
        {
            ArmKinematics kinematics = Kinematics();
            var solver = new ArmIkSolver(kinematics, Settings(), Seed);
            var solution = new float[6];
            Pose target = kinematics.Forward(new[] { 30f, -100f, 120f, -110f, -90f, 15f });
            IkResult r = solver.Solve(target, new List<float[]> { new[] { 0f, -90f, 90f, -90f, -90f, 0f } }, null, solution);
            Assert.That(r.Converged, Is.True);
            for (int i = 0; i < 6; i++)
            {
                Assert.That(solution[i], Is.InRange(kinematics.MinDeg(i), kinematics.MaxDeg(i)));
            }
        }

        [Test]
        public void Solve_RejectedSolutions_TryOtherSeeds()
        {
            ArmKinematics kinematics = Kinematics();
            var solver = new ArmIkSolver(kinematics, Settings(), Seed);
            var solution = new float[6];
            Pose target = kinematics.Forward(new[] { 30f, -100f, 120f, -110f, -90f, 15f });
            int calls = 0;
            IkResult r = solver.Solve(target, null, q => ++calls > 1, solution);
            Assert.That(r.Converged, Is.True);
            Assert.That(r.Rejected, Is.EqualTo(1));
        }

        private static IkSettings Settings() => new IkSettings(150, 0.0002f, 0.5f, 0.05f, 8f, 8, 0.2f);

        // 与 UR5eArmGenerator 一致：base_link_inertia 绕 z 转 π；tool0 之后接探头（EndEffectorMount 绕 X −90°，TCP 在 +Z 0.15 m）。
        private static ArmKinematics Kinematics()
        {
            Quaternion inertia = RosFrame.ToUnityRotation(0f, 0f, Mathf.PI);
            var positions = new Vector3[6];
            var rotations = new Quaternion[6];
            var axes = new Vector3[6];
            var min = new float[6];
            var max = new float[6];
            for (int i = 0; i < 6; i++)
            {
                Vector3 p = RosFrame.ToUnityPosition(Ur5eOrigins[i].Xyz);
                Quaternion r = RosFrame.ToUnityRotation(Ur5eOrigins[i].Rpy.x, Ur5eOrigins[i].Rpy.y, Ur5eOrigins[i].Rpy.z);
                positions[i] = i == 0 ? inertia * p : p;
                rotations[i] = i == 0 ? inertia * r : r;
                axes[i] = RosFrame.ToUnityDirection(Vector3.forward);
                min[i] = i == 2 ? -180f : -360f;
                max[i] = i == 2 ? 180f : 360f;
            }
            Quaternion tip = ToolRotation();
            return new ArmKinematics(positions, rotations, axes, min, max, tip * new Vector3(0f, 0f, ToolLengthM), tip);
        }

        private static Quaternion ToolRotation() =>
            RosFrame.ToUnityRotation(0f, -0.5f * Mathf.PI, -0.5f * Mathf.PI) * RosFrame.ToUnityRotation(0.5f * Mathf.PI, 0f, 0.5f * Mathf.PI) *
            Quaternion.Euler(-90f, 0f, 0f);

        private ArmRig BuildRig(out Transform tcp)
        {
            var root = new GameObject("arm");
            _created.Add(root);
            var inertia = new GameObject("base_link_inertia").transform;
            inertia.SetParent(root.transform, false);
            inertia.localRotation = RosFrame.ToUnityRotation(0f, 0f, Mathf.PI);
            Transform parent = inertia;
            var joints = new ArmJoint[6];
            for (int i = 0; i < 6; i++)
            {
                var node = new GameObject($"J{i + 1}").transform;
                node.SetParent(parent, false);
                node.localPosition = RosFrame.ToUnityPosition(Ur5eOrigins[i].Xyz);
                node.localRotation = RosFrame.ToUnityRotation(Ur5eOrigins[i].Rpy.x, Ur5eOrigins[i].Rpy.y, Ur5eOrigins[i].Rpy.z);
                joints[i] = node.gameObject.AddComponent<ArmJoint>();
                joints[i].Configure($"j{i + 1}", RosFrame.ToUnityDirection(Vector3.forward), i == 2 ? -180f : -360f, i == 2 ? 180f : 360f, 180f);
                parent = node;
            }
            tcp = new GameObject("TCP").transform;
            tcp.SetParent(parent, false);
            tcp.localRotation = ToolRotation();
            tcp.localPosition = tcp.localRotation * new Vector3(0f, 0f, ToolLengthM);
            ArmRig rig = root.AddComponent<ArmRig>();
            rig.Configure(joints, parent);
            return rig;
        }
    }
}
