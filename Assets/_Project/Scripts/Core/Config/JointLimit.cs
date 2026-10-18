using System;
using UnityEngine;

namespace RivetSim.Core.Config
{
    /// <summary>
    /// 单个关节的位置与速度限制。
    /// </summary>
    [Serializable]
    public struct JointLimit
    {
        /// <summary>关节名，与运动学 yaml 一致。</summary>
        [Tooltip("关节名，与运动学 yaml 一致")]
        public string jointName;

        /// <summary>最小角度，单位 °。</summary>
        [Tooltip("最小角度，单位 °")]
        public float minDeg;

        /// <summary>最大角度，单位 °。</summary>
        [Tooltip("最大角度，单位 °")]
        public float maxDeg;

        /// <summary>最大角速度，单位 °/s。</summary>
        [Tooltip("最大角速度，单位 °/s")]
        public float maxSpeedDegPerS;

        /// <summary>创建关节限制。</summary>
        public JointLimit(string jointName, float minDeg, float maxDeg, float maxSpeedDegPerS)
        {
            this.jointName = jointName;
            this.minDeg = minDeg;
            this.maxDeg = maxDeg;
            this.maxSpeedDegPerS = maxSpeedDegPerS;
        }
    }
}
