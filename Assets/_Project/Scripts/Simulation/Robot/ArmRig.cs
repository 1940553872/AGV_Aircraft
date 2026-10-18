using System;
using System.Collections.Generic;
using UnityEngine;

namespace RivetSim.Simulation.Robot
{
    /// <summary>
    /// 机械臂运动学骨架（ADR-002）：6 个 <see cref="ArmJoint"/> 串联，末端为 tool0 法兰（探头末端挂在其下）。
    /// 由 UR5e 的 default_kinematics.yaml 在编辑器中构建；不使用 ArticulationBody。正式逆解在 M4（ArmIkSolver）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ArmRig : MonoBehaviour
    {
        [SerializeField, Tooltip("关节，自基座到腕 3")]
        private ArmJoint[] joints = Array.Empty<ArmJoint>();

        [SerializeField, Tooltip("tool0 法兰")]
        private Transform toolFlange;

        /// <summary>关节数。</summary>
        public int JointCount => joints.Length;

        /// <summary>tool0 法兰。</summary>
        public Transform ToolFlange => toolFlange;

        /// <summary>按序号取关节。</summary>
        public ArmJoint GetJoint(int index) => joints[index];

        /// <summary>编辑器生成器写入关节与法兰引用。</summary>
        public void Configure(ArmJoint[] armJoints, Transform flange)
        {
            joints = armJoints ?? Array.Empty<ArmJoint>();
            toolFlange = flange;
        }

        /// <summary>读取当前关节角，单位 °。</summary>
        public float[] GetJointAnglesDeg()
        {
            var angles = new float[joints.Length];
            for (int i = 0; i < joints.Length; i++) angles[i] = joints[i].AngleDeg;
            return angles;
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
