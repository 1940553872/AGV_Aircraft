using System;
using UnityEngine;

namespace RivetSim.Core.Config
{
    /// <summary>
    /// 机械臂命名姿态（如 Stow 运输姿态、Observe 观察姿态），以 6 个关节角表示。
    /// </summary>
    [Serializable]
    public struct NamedJointPose
    {
        /// <summary>姿态名。</summary>
        [Tooltip("姿态名，如 Stow / Observe")]
        public string poseName;

        /// <summary>各关节角，单位 °，顺序与关节限制列表一致。</summary>
        [Tooltip("各关节角，单位 °，顺序与关节限制列表一致")]
        public float[] jointAnglesDeg;

        /// <summary>创建命名姿态。</summary>
        public NamedJointPose(string name, float[] anglesDeg)
        {
            poseName = name;
            jointAnglesDeg = anglesDeg;
        }
    }
}
