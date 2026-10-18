namespace RivetSim.Simulation.Robot
{
    /// <summary>
    /// 机械臂间隙检查结果：到机身外壳的连杆 / 探头末端最小间隙、到本车车体的最小间隙（达到搜索上限时即为上限）、最近的连杆与是否满足要求。
    /// </summary>
    public readonly struct ClearanceResult
    {
        /// <summary>UR5e 连杆最小间隙，单位 m。</summary>
        public readonly float MinLinkM;

        /// <summary>探头末端（排除前端）最小间隙，单位 m。</summary>
        public readonly float MinEndEffectorM;

        /// <summary>间隙最小的连杆名称。</summary>
        public readonly string NearestLink;

        /// <summary>到本车车体（底盘、升降柱、设备盒）的最小间隙，单位 m。</summary>
        public readonly float MinVehicleM;

        /// <summary>离车体最近的连杆名称。</summary>
        public readonly string NearestVehicleLink;

        /// <summary>是否满足要求。</summary>
        public readonly bool Acceptable;

        /// <summary>创建结果。</summary>
        public ClearanceResult(float minLinkM, float minEndEffectorM, string nearestLink, float minVehicleM, string nearestVehicleLink, bool acceptable)
        {
            MinLinkM = minLinkM;
            MinEndEffectorM = minEndEffectorM;
            NearestLink = nearestLink;
            MinVehicleM = minVehicleM;
            NearestVehicleLink = nearestVehicleLink;
            Acceptable = acceptable;
        }

        /// <summary>到机身外壳的连杆与末端中较小的间隙，单位 m。</summary>
        public float MinM => MinLinkM < MinEndEffectorM ? MinLinkM : MinEndEffectorM;

        /// <inheritdoc />
        public override string ToString() =>
            $"机身：连杆 {MinLinkM * 1000f:0} mm、末端 {MinEndEffectorM * 1000f:0} mm（最近 {NearestLink}）；车体 {MinVehicleM * 1000f:0} mm（最近 {NearestVehicleLink}）{(Acceptable ? "" : "，不足")}";
    }
}
