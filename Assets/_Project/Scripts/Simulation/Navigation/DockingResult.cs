using RivetSim.Core.Config;

namespace RivetSim.Simulation.Navigation
{
    /// <summary>
    /// 对接停靠结果：工位、是否成功、失败原因、停靠误差（相对真值停靠位姿）、是否在合格容差内、对接段用时与里程。
    /// </summary>
    public readonly struct DockingResult
    {
        /// <summary>工位 ID。</summary>
        public readonly string StationId;

        /// <summary>是否完成对接（停在目标处）。</summary>
        public readonly bool Success;

        /// <summary>失败原因（成功时为空）。</summary>
        public readonly string FailureReason;

        /// <summary>目标停靠位姿（真值）。</summary>
        public readonly PlanarPose TargetPose;

        /// <summary>停靠误差。</summary>
        public readonly DockingError Error;

        /// <summary>误差是否在合格容差内。</summary>
        public readonly bool WithinTolerance;

        /// <summary>对接段（预停靠位姿 → 停靠位姿）仿真用时，单位 s。</summary>
        public readonly float DurationS;

        /// <summary>对接段里程，单位 m。</summary>
        public readonly float TraveledM;

        /// <summary>创建结果。</summary>
        public DockingResult(string stationId, bool success, string failureReason, PlanarPose targetPose, DockingError error,
            bool withinTolerance, float durationS, float traveledM)
        {
            StationId = stationId;
            Success = success;
            FailureReason = failureReason ?? "";
            TargetPose = targetPose;
            Error = error;
            WithinTolerance = withinTolerance;
            DurationS = durationS;
            TraveledM = traveledM;
        }

        /// <summary>失败结果。</summary>
        public static DockingResult Failed(string stationId, PlanarPose target, string reason) =>
            new DockingResult(stationId, false, reason, target, default, false, 0f, 0f);

        /// <inheritdoc />
        public override string ToString() => Success
            ? $"{StationId} 停靠误差：{Error}（{(WithinTolerance ? "合格" : "超出容差")}），对接段 {TraveledM:0.000} m、{DurationS:0.0} s"
            : $"{StationId} 对接失败：{FailureReason}";
    }
}
