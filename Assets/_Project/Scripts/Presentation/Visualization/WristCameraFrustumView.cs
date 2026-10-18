using RivetSim.Core.Config;
using RivetSim.Simulation.Inspection;
using RivetSim.Simulation.Robot;
using UnityEngine;

namespace RivetSim.Presentation.Visualization
{
    /// <summary>
    /// 腕部相机视场锥线框（Visualization 层，V 开关）：相机测量（基准注册、钉心复测）期间显示，四棱锥顶点在相机，底面在最远工作距离处，
    /// 视场取 CFG_Arm（60° × 45°，教学简化模型的可见性判定范围）。
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public sealed class WristCameraFrustumView : MonoBehaviour
    {
        // 折线经过 8 条棱：c1 c2 c3 c4 c1 顶点 c3 c2 顶点 c4。
        private static readonly int[] PathIndices = { 1, 2, 3, 4, 1, 0, 3, 2, 0, 4 };

        private ArmRig _rig;
        private RivetInspectionService _inspection;
        private ArmConfig _config;
        private LineRenderer _line;
        private readonly Vector3[] _corners = new Vector3[5];

        /// <summary>绑定机械臂与检测服务。</summary>
        public void Initialize(ArmRig rig, RivetInspectionService inspection, ArmConfig config)
        {
            _rig = rig;
            _inspection = inspection;
            _config = config;
            _line = GetComponent<LineRenderer>();
            _line.useWorldSpace = true;
            _line.positionCount = PathIndices.Length;
            _line.enabled = false;
        }

        private void LateUpdate()
        {
            if (_line == null) return;
            bool show = _inspection.CameraActive && _rig.WristCamera != null;
            _line.enabled = show;
            if (!show) return;
            Transform cam = _rig.WristCamera;
            float d = _config.wristCameraWorkingDistanceM.y;
            float hx = d * Mathf.Tan(0.5f * _config.wristCameraFovDeg.x * Mathf.Deg2Rad);
            float hy = d * Mathf.Tan(0.5f * _config.wristCameraFovDeg.y * Mathf.Deg2Rad);
            _corners[0] = cam.position;
            _corners[1] = cam.TransformPoint(new Vector3(-hx, hy, d));
            _corners[2] = cam.TransformPoint(new Vector3(hx, hy, d));
            _corners[3] = cam.TransformPoint(new Vector3(hx, -hy, d));
            _corners[4] = cam.TransformPoint(new Vector3(-hx, -hy, d));
            for (int i = 0; i < PathIndices.Length; i++) _line.SetPosition(i, _corners[PathIndices[i]]);
        }
    }
}
