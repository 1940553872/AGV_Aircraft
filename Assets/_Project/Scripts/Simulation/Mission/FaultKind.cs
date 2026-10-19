namespace RivetSim.Simulation.Mission
{
    /// <summary>
    /// 可注入的故障（M5：键盘 F5 / F6 / F7 与 Verify 菜单；M6 的 HUD 按钮调用同一接口）。
    /// </summary>
    public enum FaultKind
    {
        /// <summary>整机识别失败（下一次 T1 触发 t(1,8)）。</summary>
        RecognitionFailed,
        /// <summary>接触力超限（下一次 T5 力控接触触发 t(5,6)）。</summary>
        ContactForceExceeded,
        /// <summary>采集无效（下一次 T5 采集耦合不良，触发 t(5,7)）。</summary>
        AcquisitionInvalid
    }
}
