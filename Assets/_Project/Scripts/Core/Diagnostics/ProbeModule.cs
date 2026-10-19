namespace RivetSim.Core.Diagnostics
{
    /// <summary>GC 探针统计的模块（M7 性能验证）。</summary>
    public enum ProbeModule
    {
        /// <summary>激光扫描（含栅格写入，Simulation）。</summary>
        LidarScan = 0,

        /// <summary>激光点云网格更新（Presentation）。</summary>
        LidarView = 1,

        /// <summary>栅格地面贴图更新（Presentation）。</summary>
        GridOverlay = 2,

        /// <summary>小地图栅格贴图刷新（Presentation）。</summary>
        MiniMapSource = 3,

        /// <summary>小地图叠加层绘制（AGV 标记、路径，Presentation）。</summary>
        MiniMapDraw = 4,
    }
}
