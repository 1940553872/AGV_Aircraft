using UnityEngine;

namespace RivetSim.Simulation.Navigation
{
    /// <summary>
    /// 标记可见性判定（教学简化模型，不运行 AprilTag 识别）：标记中心在相机视场与识别距离内、且标记正面朝向相机即视为识别成功；不检查遮挡。
    /// </summary>
    public static class MarkerVisibility
    {
        /// <summary>判定相机能否识别标记。</summary>
        /// <param name="cameraPosition">相机位置（世界坐标）。</param>
        /// <param name="cameraRotation">相机朝向（本地 +Z 为光轴）。</param>
        /// <param name="fovDeg">视场（水平 × 垂直），单位 °。</param>
        /// <param name="minRangeM">识别距离下限，单位 m。</param>
        /// <param name="maxRangeM">识别距离上限，单位 m。</param>
        /// <param name="markerCenter">标记中心（世界坐标）。</param>
        /// <param name="markerNormal">标记正面法向（世界坐标）。</param>
        public static bool IsVisible(Vector3 cameraPosition, Quaternion cameraRotation, Vector2 fovDeg, float minRangeM, float maxRangeM,
            Vector3 markerCenter, Vector3 markerNormal)
        {
            Vector3 toMarker = markerCenter - cameraPosition;
            float distance = toMarker.magnitude;
            if (distance < minRangeM || distance > maxRangeM) return false;
            if (Vector3.Dot(markerNormal, -toMarker) <= 0f) return false;

            Vector3 local = Quaternion.Inverse(cameraRotation) * toMarker;
            if (local.z <= 0f) return false;
            float horizontalDeg = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            float verticalDeg = Mathf.Atan2(local.y, local.z) * Mathf.Rad2Deg;
            return Mathf.Abs(horizontalDeg) <= 0.5f * fovDeg.x && Mathf.Abs(verticalDeg) <= 0.5f * fovDeg.y;
        }
    }
}
