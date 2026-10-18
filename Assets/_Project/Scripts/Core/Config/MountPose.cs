using System;
using UnityEngine;

namespace RivetSim.Core.Config
{
    /// <summary>
    /// 安装位姿：相对父坐标系（如 AGV 底盘地面投影中心）的位置与欧拉角。
    /// </summary>
    [Serializable]
    public struct MountPose
    {
        /// <summary>位置（x 右，y 上，z 前），单位 m。</summary>
        [Tooltip("位置（x 右，y 上，z 前），单位 m")]
        public Vector3 positionM;

        /// <summary>欧拉角，单位 °。</summary>
        [Tooltip("欧拉角，单位 °")]
        public Vector3 eulerDeg;

        /// <summary>创建安装位姿。</summary>
        public MountPose(Vector3 positionM, Vector3 eulerDeg)
        {
            this.positionM = positionM;
            this.eulerDeg = eulerDeg;
        }
    }
}
