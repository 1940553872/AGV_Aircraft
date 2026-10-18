using System;
using UnityEngine;

namespace RivetSim.Core.Config
{
    /// <summary>
    /// 地面平面位姿：位置取世界 X / Z（米），朝向为绕 Y 轴偏航角（度，0 指向 +Z）。
    /// </summary>
    [Serializable]
    public struct PlanarPose
    {
        /// <summary>平面位置（x = 世界 X，y = 世界 Z），单位 m。</summary>
        [Tooltip("平面位置（x = 世界 X，y = 世界 Z），单位 m")]
        public Vector2 positionM;

        /// <summary>偏航角，单位 °，0 指向 +Z，顺时针为正。</summary>
        [Tooltip("偏航角，单位 °，0 指向 +Z，顺时针为正")]
        public float yawDeg;

        /// <summary>创建平面位姿。</summary>
        public PlanarPose(Vector2 positionM, float yawDeg)
        {
            this.positionM = positionM;
            this.yawDeg = yawDeg;
        }

        /// <summary>世界坐标位置（Y = 0）。</summary>
        public Vector3 WorldPosition => new Vector3(positionM.x, 0f, positionM.y);

        /// <summary>世界旋转（绕 Y 轴）。</summary>
        public Quaternion WorldRotation => Quaternion.Euler(0f, yawDeg, 0f);
    }
}
