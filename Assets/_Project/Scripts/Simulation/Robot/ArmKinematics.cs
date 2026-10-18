using System;
using System.Collections.Generic;
using UnityEngine;

namespace RivetSim.Simulation.Robot
{
    /// <summary>
    /// 机械臂运动学模型（纯计算，不依赖 Transform）：每个关节相对上一关节坐标系的固定偏移（含零位旋转）与转轴，
    /// 末端（TCP）相对最后一个关节的固定偏移。关节角按 ROS 右手定则记，Unity 中绕本地轴转 −角度（与 <see cref="ArmJoint"/> 一致）。
    /// 坐标均为机械臂基座（<see cref="ArmRig"/> 根节点）坐标系。
    /// </summary>
    public sealed class ArmKinematics
    {
        private readonly Vector3[] _offsetPositions;
        private readonly Quaternion[] _offsetRotations;
        private readonly Vector3[] _axes;
        private readonly float[] _minDeg;
        private readonly float[] _maxDeg;
        private readonly Vector3 _tipPosition;
        private readonly Quaternion _tipRotation;

        /// <summary>关节数。</summary>
        public int JointCount => _axes.Length;

        /// <summary>创建运动学模型。</summary>
        /// <param name="offsetPositions">各关节坐标系原点在上一关节坐标系中的位置（第 0 个相对基座），单位 m。</param>
        /// <param name="offsetRotations">各关节坐标系零位相对上一关节坐标系的旋转。</param>
        /// <param name="axesLocal">各关节转轴（关节本地坐标）。</param>
        /// <param name="minDeg">关节下限，单位 °。</param>
        /// <param name="maxDeg">关节上限，单位 °。</param>
        /// <param name="tipPosition">TCP 在最后一个关节坐标系中的位置，单位 m。</param>
        /// <param name="tipRotation">TCP 相对最后一个关节坐标系的旋转。</param>
        public ArmKinematics(Vector3[] offsetPositions, Quaternion[] offsetRotations, Vector3[] axesLocal, float[] minDeg, float[] maxDeg,
            Vector3 tipPosition, Quaternion tipRotation)
        {
            int n = axesLocal.Length;
            if (offsetPositions.Length != n || offsetRotations.Length != n || minDeg.Length != n || maxDeg.Length != n)
                throw new ArgumentException("运动学参数数组长度不一致。");
            _offsetPositions = (Vector3[])offsetPositions.Clone();
            _offsetRotations = (Quaternion[])offsetRotations.Clone();
            _axes = new Vector3[n];
            for (int i = 0; i < n; i++) _axes[i] = axesLocal[i].normalized;
            _minDeg = (float[])minDeg.Clone();
            _maxDeg = (float[])maxDeg.Clone();
            _tipPosition = tipPosition;
            _tipRotation = tipRotation;
        }

        /// <summary>从机械臂骨架与 TCP 节点提取运动学模型（与当前关节角无关）。</summary>
        public static ArmKinematics FromRig(ArmRig rig, Transform tcp)
        {
            int n = rig.JointCount;
            var positions = new Vector3[n];
            var rotations = new Quaternion[n];
            var axes = new Vector3[n];
            var min = new float[n];
            var max = new float[n];
            for (int i = 0; i < n; i++)
            {
                ArmJoint joint = rig.GetJoint(i);
                Transform previous = i == 0 ? rig.transform : rig.GetJoint(i - 1).transform;
                Matrix4x4 m = previous.worldToLocalMatrix * joint.transform.parent.localToWorldMatrix *
                              Matrix4x4.TRS(joint.transform.localPosition, joint.ZeroLocalRotation, Vector3.one);
                positions[i] = m.GetColumn(3);
                rotations[i] = m.rotation;
                axes[i] = joint.AxisLocal;
                min[i] = joint.MinDeg;
                max[i] = joint.MaxDeg;
            }
            Matrix4x4 tip = rig.GetJoint(n - 1).transform.worldToLocalMatrix * tcp.localToWorldMatrix;
            return new ArmKinematics(positions, rotations, axes, min, max, tip.GetColumn(3), tip.rotation);
        }

        /// <summary>关节下限，单位 °。</summary>
        public float MinDeg(int joint) => _minDeg[joint];

        /// <summary>关节上限，单位 °。</summary>
        public float MaxDeg(int joint) => _maxDeg[joint];

        /// <summary>把关节角限制在限位内。</summary>
        public float Clamp(int joint, float angleDeg) => Mathf.Clamp(angleDeg, _minDeg[joint], _maxDeg[joint]);

        /// <summary>正运动学：TCP 在基座坐标系中的位姿。</summary>
        public Pose Forward(IReadOnlyList<float> anglesDeg) => Forward(anglesDeg, null, null);

        /// <summary>正运动学，同时输出各关节原点与转轴（基座坐标系，转轴已含“绕 −角度旋转”的符号，即关节角增大时的角速度方向）。</summary>
        public Pose Forward(IReadOnlyList<float> anglesDeg, Vector3[] jointOrigins, Vector3[] jointAxes)
        {
            Vector3 p = Vector3.zero;
            Quaternion r = Quaternion.identity;
            for (int i = 0; i < _axes.Length; i++)
            {
                p += r * _offsetPositions[i];
                r *= _offsetRotations[i];
                if (jointOrigins != null) jointOrigins[i] = p;
                if (jointAxes != null) jointAxes[i] = -(r * _axes[i]);
                r *= Quaternion.AngleAxis(-anglesDeg[i], _axes[i]);
            }
            return new Pose(p + r * _tipPosition, r * _tipRotation);
        }
    }
}
