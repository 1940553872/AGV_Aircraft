namespace RivetSim.Simulation.Inspection
{
    /// <summary>
    /// 单钉判读结论（结果环颜色：绿 = 合格，黄 = 疑似，红 = 缺陷，灰 = 无效）。
    /// </summary>
    public enum InspectionVerdict
    {
        /// <summary>合格。</summary>
        Pass,
        /// <summary>疑似。</summary>
        Suspect,
        /// <summary>缺陷。</summary>
        Defect,
        /// <summary>无效（耦合不良或接触力不在允许范围）。</summary>
        Invalid,
    }
}
