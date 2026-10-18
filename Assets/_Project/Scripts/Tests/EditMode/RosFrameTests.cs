using System.Collections.Generic;
using NUnit.Framework;
using RivetSim.Simulation.Robot;
using UnityEngine;

namespace RivetSim.Tests.EditMode
{
    /// <summary>
    /// RosFrame 与 ArmJoint 测试：ROS → Unity 位置 / 旋转换算，以及按 UR5e default_kinematics.yaml 搭建的关节链零位与竖直姿态的正运动学。
    /// </summary>
    public sealed class RosFrameTests
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

        private readonly List<GameObject> _created = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _created) Object.DestroyImmediate(go);
            _created.Clear();
        }

        [Test]
        public void Position_MapsRosToUnity()
        {
            Assert.That(RosFrame.ToUnityPosition(new Vector3(1f, 2f, 3f)), Is.EqualTo(new Vector3(-2f, 3f, 1f)));
        }

        [Test]
        public void YawQuarterTurn_MapsRosXToRosY()
        {
            Quaternion q = RosFrame.ToUnityRotation(0f, 0f, Mathf.PI * 0.5f);
            Vector3 rotatedRosX = q * RosFrame.ToUnityDirection(Vector3.right);
            AssertClose(rotatedRosX, RosFrame.ToUnityDirection(Vector3.up));
        }

        [Test]
        public void Ur5eChain_ZeroAndUprightPosesMatchKinematics()
        {
            Transform tool0 = BuildChain(out ArmJoint[] joints);
            // 零位：tool0 在 base_link 中位于 ROS (0.8172, 0.2329, 0.0628)。
            AssertClose(tool0.position, RosFrame.ToUnityPosition(new Vector3(0.425f + 0.3922f, 0.1333f + 0.0996f, 0.1625f - 0.0997f)));

            // 竖直姿态 (0, −90°, 0, −90°, 0, 0)：tool0 高度 = 0.1625 + 0.425 + 0.3922 + 0.0997。
            joints[1].SetAngleDeg(-90f);
            joints[3].SetAngleDeg(-90f);
            Assert.That(tool0.position.y, Is.EqualTo(0.1625f + 0.425f + 0.3922f + 0.0997f).Within(1e-4f));
        }

        [Test]
        public void Joint_ClampsToLimits()
        {
            var go = new GameObject("joint");
            _created.Add(go);
            ArmJoint joint = go.AddComponent<ArmJoint>();
            joint.Configure("elbow_joint", Vector3.up, -180f, 180f, 180f);
            Assert.That(joint.SetAngleDeg(270f), Is.EqualTo(180f));
            Assert.That(joint.SetAngleDeg(-500f), Is.EqualTo(-180f));
        }

        // base_link → base_link_inertia（绕 z 转 π）→ 6 个关节（轴均为本地 z）→ tool0（不含 flange 旋转，位置相同）。
        private Transform BuildChain(out ArmJoint[] joints)
        {
            var root = new GameObject("base_link");
            _created.Add(root);
            var inertia = new GameObject("base_link_inertia").transform;
            inertia.SetParent(root.transform, false);
            inertia.localRotation = RosFrame.ToUnityRotation(0f, 0f, Mathf.PI);

            Transform parent = inertia;
            joints = new ArmJoint[Ur5eOrigins.Length];
            for (int i = 0; i < Ur5eOrigins.Length; i++)
            {
                var node = new GameObject($"J{i + 1}").transform;
                node.SetParent(parent, false);
                node.localPosition = RosFrame.ToUnityPosition(Ur5eOrigins[i].Xyz);
                node.localRotation = RosFrame.ToUnityRotation(Ur5eOrigins[i].Rpy.x, Ur5eOrigins[i].Rpy.y, Ur5eOrigins[i].Rpy.z);
                joints[i] = node.gameObject.AddComponent<ArmJoint>();
                joints[i].Configure($"j{i + 1}", RosFrame.ToUnityDirection(Vector3.forward), -360f, 360f, 180f);
                parent = node;
            }
            return parent;
        }

        private static void AssertClose(Vector3 actual, Vector3 expected)
        {
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(1e-4f), $"实际 {actual:F5}，期望 {expected:F5}");
        }
    }
}
