using System;
using UnityEngine;

namespace RivetSim.Simulation.Navigation
{
    /// <summary>
    /// 工位接近通道：地面上的有向矩形（中心、长边方向偏航、横向宽度 × 纵向长度），通道内不叠加虚拟禁行区（ADR-015）。
    /// </summary>
    [Serializable]
    public struct KeepOutChannel
    {
        /// <summary>所属工位 ID。</summary>
        public string stationId;

        /// <summary>矩形中心（x = 世界 X，y = 世界 Z），单位 m。</summary>
        public Vector2 centerM;

        /// <summary>纵向（车头方向）偏航角，单位 °。</summary>
        public float yawDeg;

        /// <summary>尺寸（x = 横向宽度，y = 纵向长度），单位 m。</summary>
        public Vector2 sizeM;

        /// <summary>创建通道。</summary>
        public KeepOutChannel(string stationId, Vector2 centerM, float yawDeg, Vector2 sizeM)
        {
            this.stationId = stationId;
            this.centerM = centerM;
            this.yawDeg = yawDeg;
            this.sizeM = sizeM;
        }

        /// <summary>平面点是否在通道内（含边界）。</summary>
        public bool Contains(Vector2 p)
        {
            float yaw = yawDeg * Mathf.Deg2Rad;
            var heading = new Vector2(Mathf.Sin(yaw), Mathf.Cos(yaw));
            var right = new Vector2(heading.y, -heading.x);
            Vector2 d = p - centerM;
            return Mathf.Abs(Vector2.Dot(d, right)) <= 0.5f * sizeM.x && Mathf.Abs(Vector2.Dot(d, heading)) <= 0.5f * sizeM.y;
        }

        /// <summary>四个角点（顺序绕行），单位 m。</summary>
        public Vector2[] Corners()
        {
            float yaw = yawDeg * Mathf.Deg2Rad;
            var heading = new Vector2(Mathf.Sin(yaw), Mathf.Cos(yaw)) * (0.5f * sizeM.y);
            var right = new Vector2(Mathf.Cos(yaw), -Mathf.Sin(yaw)) * (0.5f * sizeM.x);
            return new[] { centerM - right - heading, centerM + right - heading, centerM + right + heading, centerM - right + heading };
        }
    }
}
