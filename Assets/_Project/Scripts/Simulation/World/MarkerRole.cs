namespace RivetSim.Simulation.World
{
    /// <summary>
    /// AprilTag 标记的用途。
    /// </summary>
    public enum MarkerRole
    {
        /// <summary>工位停靠标记（地面立杆标记板）。</summary>
        Docking,
        /// <summary>检测区基准标记（贴在蒙皮上，基准注册 / 每 10 颗复测）。</summary>
        Datum,
        /// <summary>充电位对接标记。</summary>
        Charger,
    }
}
