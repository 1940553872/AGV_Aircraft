namespace RivetSim.Presentation.UI
{
    /// <summary>
    /// 演示界面顶栏与 HUD 读取的运行状态（M6）：模式、倍速、仿真时间、暂停、区域 / 工位 / 铆钉总数。
    /// 主场景由 DemoModeController 提供，Sandbox_UI 由假数据提供。
    /// </summary>
    public interface IDemoStatusSource
    {
        /// <summary>模式文字（自动演示 / 分步演示 / 手动遥控），须为缓存字符串。</summary>
        string ModeText { get; }

        /// <summary>仿真时间，单位 s。</summary>
        double SimTimeS { get; }

        /// <summary>当前倍速。</summary>
        float SpeedMultiplier { get; }

        /// <summary>是否暂停。</summary>
        bool IsPaused { get; }

        /// <summary>区域总数。</summary>
        int RegionCount { get; }

        /// <summary>指定区域的工位总数。</summary>
        int StationCount(int regionIndex);

        /// <summary>当前工位的铆钉总数。</summary>
        int RivetCount { get; }
    }
}
