using System;
using UnityEngine;

namespace RivetSim.Core.Config
{
    /// <summary>
    /// 地面上的轴对齐矩形区域（世界 X / Z 平面）。
    /// </summary>
    [Serializable]
    public struct ZoneRect
    {
        /// <summary>中心（x = 世界 X，y = 世界 Z），单位 m。</summary>
        [Tooltip("中心（x = 世界 X，y = 世界 Z），单位 m")]
        public Vector2 centerM;

        /// <summary>尺寸（x = 沿世界 X，y = 沿世界 Z），单位 m。</summary>
        [Tooltip("尺寸（x = 沿世界 X，y = 沿世界 Z），单位 m")]
        public Vector2 sizeM;
    }
}
