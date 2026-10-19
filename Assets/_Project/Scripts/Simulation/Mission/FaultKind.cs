namespace RivetSim.Simulation.Mission
{
    /// <summary>
    /// 可注入的故障（M5 起 Verify 菜单；M8 起由随机故障调度器每轮自动注入，界面不再提供手动注入）。
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
