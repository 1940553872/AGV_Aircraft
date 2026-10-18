using System;
using System.Collections.Generic;
using UnityEngine;

namespace RivetSim.Simulation.Robot
{
    /// <summary>
    /// 机械臂运动学骨架（ADR-002）：6 个 <see cref="ArmJoint"/> 串联，末端为 tool0 法兰（探头末端挂在其下）。
    /// 由 UR5e 的 default_kinematics.yaml 在编辑器中构建；不使用 ArticulationBody。逆解见 <see cref="ArmIkSolver"/>（M4）。
    /// 另记录探头末端的 TCP、腕部相机与各连杆的表面采样点（连杆间隙检查用，M4）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ArmRig : MonoBehaviour
    {
        [SerializeField, Tooltip("关节，自基座到腕 3")]
        private ArmJoint[] joints = Array.Empty<ArmJoint>();

        [SerializeField, Tooltip("tool0 法兰")]
        private Transform toolFlange;

        [SerializeField, Tooltip("探头端面中心（TCP，本地 +Z 为工具轴）")]
        private Transform tcp;

        [SerializeField, Tooltip("腕部相机（本地 +Z 为光轴）")]
        private Transform wristCamera;

        [SerializeField, Tooltip("探头末端")]
        private ProbeEndEffector probe;

        [SerializeField, Tooltip("各连杆的表面采样点（连杆间隙检查）")]
        private ArmLinkSamples[] linkSamples = Array.Empty<ArmLinkSamples>();

        /// <summary>关节数。</summary>
        public int JointCount => joints.Length;

        /// <summary>tool0 法兰。</summary>
        public Transform ToolFlange => toolFlange;

        /// <summary>TCP（探头端面中心）。</summary>
        public Transform Tcp => tcp;

        /// <summary>腕部相机。</summary>
        public Transform WristCamera => wristCamera;

        /// <summary>探头末端。</summary>
        public ProbeEndEffector Probe => probe;

        /// <summary>连杆表面采样点。</summary>
        public IReadOnlyList<ArmLinkSamples> LinkSamples => linkSamples;

        /// <summary>按序号取关节。</summary>
        public ArmJoint GetJoint(int index) => joints[index];

        /// <summary>编辑器生成器写入关节与法兰引用。</summary>
        public void Configure(ArmJoint[] armJoints, Transform flange)
        {
            joints = armJoints ?? Array.Empty<ArmJoint>();
            toolFlange = flange;
        }

        /// <summary>编辑器生成器写入探头末端引用与连杆采样点。</summary>
        public void ConfigureEndEffector(Transform tcpNode, Transform cameraNode, ProbeEndEffector endEffector, ArmLinkSamples[] samples)
        {
            tcp = tcpNode;
            wristCamera = cameraNode;
            probe = endEffector;
            linkSamples = samples ?? Array.Empty<ArmLinkSamples>();
        }

        /// <summary>读取当前关节角，单位 °。</summary>
        public float[] GetJointAnglesDeg()
        {
            var angles = new float[joints.Length];
            GetJointAnglesDeg(angles);
            return angles;
        }

        /// <summary>读取当前关节角到 angles（不分配），单位 °。</summary>
        public void GetJointAnglesDeg(float[] angles)
        {
            for (int i = 0; i < joints.Length; i++) angles[i] = joints[i].AngleDeg;
        }

        /// <summary>设置全部关节角（各自限制在限位内），单位 °。</summary>
        public void SetJointAnglesDeg(IReadOnlyList<float> anglesDeg)
        {
            if (anglesDeg == null || anglesDeg.Count != joints.Length)
                throw new ArgumentException($"关节角数量应为 {joints.Length}。", nameof(anglesDeg));
            for (int i = 0; i < joints.Length; i++) joints[i].SetAngleDeg(anglesDeg[i]);
        }
    }
}
