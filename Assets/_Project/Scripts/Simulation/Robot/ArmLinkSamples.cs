using System;
using UnityEngine;

namespace RivetSim.Simulation.Robot
{
    /// <summary>
    /// 机械臂单个连杆（或探头末端）的表面采样点（连杆本地坐标），由编辑器生成器从外观网格顶点按网格抽稀得到，用于连杆与机身外壳的间隙检查。
    /// </summary>
    [Serializable]
    public sealed class ArmLinkSamples
    {
        [Tooltip("采样点所在的连杆节点")]
        public Transform link;

        [Tooltip("连杆名称（日志用）")]
        public string linkName;

        [Tooltip("是否为探头末端（使用末端间隙要求，并排除探头前端）")]
        public bool isEndEffector;

        [Tooltip("采样点（连杆本地坐标），单位 m")]
        public Vector3[] pointsLocal = Array.Empty<Vector3>();

        [Tooltip("包围球中心（连杆本地坐标），单位 m")]
        public Vector3 boundsCenterLocal;

        [Tooltip("包围球半径，单位 m")]
        public float boundsRadiusM;
    }
}
