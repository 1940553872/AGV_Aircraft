using RivetSim.Core.Config;
using RivetSim.Presentation.UI;
using RivetSim.Presentation.Visualization;

namespace RivetSim.Presentation.App
{
    /// <summary>
    /// M4 检测可视化的运行时绑定（AppBootstrap 与 SandboxInspectionDemo 共用）：检测叠加信息、铆钉结果环、腕部相机视场锥与探头接触指示。
    /// 引用未赋值时跳过（序列化引用的“假空”对象须用 != null 判断）。
    /// </summary>
    public static class InspectionSceneViews
    {
        /// <summary>绑定全部检测可视化。</summary>
        public static void Initialize(RobotRuntime runtime, ArmConfig arm, InspectionConfig inspection, InspectionOverlayView overlay,
            RivetResultRingView rings, WristCameraFrustumView frustum, ProbeContactView contact)
        {
            if (overlay != null) overlay.Initialize(runtime, inspection);
            if (rings != null) rings.Initialize(runtime.Bus);
            if (frustum != null) frustum.Initialize(runtime.Arm.Rig, runtime.Inspection, arm);
            if (contact != null && rings != null) contact.Initialize(runtime.Arm.Rig, runtime.Contact, rings);
        }
    }
}
