namespace RivetSim.Core.Config
{
    /// <summary>
    /// 障碍物类型，决定 PropsGenerator 生成的外观。
    /// </summary>
    public enum ObstacleKind
    {
        /// <summary>货箱堆（一排货箱）。</summary>
        CrateStack,
        /// <summary>工具车。</summary>
        ToolCart,
        /// <summary>锥桶。</summary>
        Cone
    }
}
