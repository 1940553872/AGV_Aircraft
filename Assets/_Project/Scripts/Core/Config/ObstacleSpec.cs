using System;
using UnityEngine;

namespace RivetSim.Core.Config
{
    /// <summary>
    /// 障碍物规格：类型、地面占地矩形与高度。
    /// </summary>
    [Serializable]
    public struct ObstacleSpec
    {
        /// <summary>显示名称（如 O1 货箱堆）。</summary>
        [Tooltip("显示名称（如 O1 货箱堆）")]
        public string name;

        /// <summary>障碍物类型。</summary>
        [Tooltip("障碍物类型")]
        public ObstacleKind kind;

        /// <summary>占地矩形（世界 X / Z 平面）。</summary>
        [Tooltip("占地矩形（x = 世界 X，y = 世界 Z），单位 m")]
        public ZoneRect footprint;

        /// <summary>高度，单位 m。</summary>
        [Tooltip("高度，单位 m")]
        public float heightM;

        /// <summary>创建障碍物规格。</summary>
        public ObstacleSpec(string name, ObstacleKind kind, ZoneRect footprint, float heightM)
        {
            this.name = name;
            this.kind = kind;
            this.footprint = footprint;
            this.heightM = heightM;
        }
    }
}
