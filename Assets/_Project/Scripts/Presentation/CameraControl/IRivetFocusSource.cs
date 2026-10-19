using UnityEngine;

namespace RivetSim.Presentation.CameraControl
{
    /// <summary>
    /// 检测机位取景目标（M8）：当前铆钉的接触点与蒙皮外法向。
    /// </summary>
    public interface IRivetFocusSource
    {
        /// <summary>取当前铆钉；无当前铆钉时返回 false。pointM 为接触点（世界坐标，单位 m），normal 为蒙皮外法向（单位向量）。</summary>
        bool TryGetFocus(out Vector3 pointM, out Vector3 normal);
    }
}
