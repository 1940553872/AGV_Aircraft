using System;
using RivetSim.Core.Config;
using RivetSim.Core.Events;
using RivetSim.Core.Mission;
using RivetSim.Simulation.Inspection;
using RivetSim.Simulation.Navigation;

namespace RivetSim.Simulation.Mission
{
    /// <summary>
    /// 执行器之间共享的流程状态：分三级在位置变化时自动清零——工位级（换站时）、铆钉级（换钉时）、阶段级（每次进入阶段，含重试）。
    /// 条件 C1～C12 的布尔量（资源锁、配方、许可、记录已保存等）均取自这里。
    /// </summary>
    public sealed class MissionBlackboard : IDisposable
    {
        private readonly IDisposable _phaseSubscription;
        private int _stationKey = -1;
        private int _rivetKey = -1;

        // —— 工位级 ——
        /// <summary>检测配方已载入（t(3,5)，C7）。</summary>
        public bool RecipeLoaded { get; set; }

        /// <summary>机械臂运动许可有效（t(3,6)，C2）。</summary>
        public bool ArmPermit { get; set; }

        /// <summary>下一颗用关节插值对中（工位首颗、基准复测后）。</summary>
        public bool JointApproachNext { get; set; }

        /// <summary>最近一次对接结果。</summary>
        public DockingResult LastDock { get; set; }

        // —— 铆钉级 ——
        /// <summary>本颗的机械臂与超声通道资源锁已分配（t(4,7)，C1）。</summary>
        public bool ResourceLockHeld { get; set; }

        /// <summary>近距离限速有效（t(4,6)）。</summary>
        public bool NearFieldLimit { get; set; }

        /// <summary>本颗开始（首次进入 T4 的执行器取用）的仿真时间，单位 s。</summary>
        public double RivetStartS { get; private set; }

        /// <summary>本颗不计入单钉节拍的时长，单位 s（M6：工位最后一颗的收臂与降柱）。</summary>
        public double CycleExcludedS { get; set; }

        /// <summary>本颗单钉节拍，单位 s：从本颗开始到 nowS，扣除 <see cref="CycleExcludedS"/>。</summary>
        public float RivetCycleTimeS(double nowS) => (float)(nowS - RivetStartS - CycleExcludedS);

        // —— 阶段级（T5 重试时清零）——
        /// <summary>S5 判读得到、待 S8 保存的记录。</summary>
        public InspectionRecord PendingRecord { get; set; }

        /// <summary>本次采集无效且已激活重贴（S8 不保存，等待重试）。</summary>
        public bool InvalidRetryPending { get; set; }

        /// <summary>本颗记录已保存（C9）。</summary>
        public bool RecordSaved { get; set; }

        /// <summary>本颗已保存的记录。</summary>
        public InspectionRecord CommittedRecord { get; set; }

        /// <summary>检测结论已生成（t(6,5)，C10）。</summary>
        public bool ConclusionReady { get; set; }

        /// <summary>T6 回退距离（离开接触点），单位 m（C11）。</summary>
        public float RetreatM { get; set; }

        /// <summary>T6 回退后探头端面距表面，单位 m（C12）。</summary>
        public float SurfaceDistanceAfterRetreatM { get; set; }

        /// <summary>T6 释放条件满足项数。</summary>
        public int ReleaseConditionsPassed { get; set; }

        /// <summary>T6 释放条件判定项数。</summary>
        public int ReleaseConditionsChecked { get; set; }

        // —— 阶段级 ——
        /// <summary>T4：机械臂已到预计钉位（钉心复测可开始）。</summary>
        public bool ArmAtPredicted { get; set; }

        /// <summary>T4：机械臂未能到位。</summary>
        public bool ArmAlignFailed { get; set; }

        /// <summary>T4：钉心复测完成（视觉修正可开始）。</summary>
        public bool CenterMeasured { get; set; }

        /// <summary>T4：钉心复测失败。</summary>
        public bool CenterMeasureFailed { get; set; }

        /// <summary>T1：操作员初值已给出（t(1,8)）。</summary>
        public bool InitialPoseProvided { get; set; }

        /// <summary>T1：操作员给出的初值。</summary>
        public PlanarPose InitialPose { get; set; }

        /// <summary>T2：对接停车已结束（成功或失败）。</summary>
        public bool DockFinished { get; set; }

        /// <summary>创建并订阅阶段进入事件（阶段级状态清零）。</summary>
        public MissionBlackboard(EventBus bus)
        {
            if (bus != null) _phaseSubscription = bus.Subscribe<PhaseEnteredEvent>(_ => ResetPhase());
        }

        /// <summary>按当前位置清零已过期的工位级状态；includeRivet 为 true 时（执行器取铆钉）同时清零铆钉级状态并记录本颗开始时间。</summary>
        public void Sync(MissionCursor cursor, double nowS, bool includeRivet)
        {
            if (cursor.StationGlobalIndex != _stationKey)
            {
                _stationKey = cursor.StationGlobalIndex;
                RecipeLoaded = false;
                ArmPermit = false;
                JointApproachNext = true;
                _rivetKey = -1;
            }
            if (includeRivet && cursor.RivetIndex != _rivetKey)
            {
                _rivetKey = cursor.RivetIndex;
                ResourceLockHeld = false;
                NearFieldLimit = false;
                RivetStartS = nowS;
                CycleExcludedS = 0d;
                RecordSaved = false;
                CommittedRecord = null;
                ConclusionReady = false;
                RetreatM = 0f;
                SurfaceDistanceAfterRetreatM = 0f;
                ReleaseConditionsPassed = 0;
                ReleaseConditionsChecked = 0;
            }
        }

        /// <summary>取消订阅。</summary>
        public void Dispose() => _phaseSubscription?.Dispose();

        private void ResetPhase()
        {
            ArmAtPredicted = false;
            ArmAlignFailed = false;
            CenterMeasured = false;
            CenterMeasureFailed = false;
            InitialPoseProvided = false;
            DockFinished = false;
            PendingRecord = null;
            InvalidRetryPending = false;
        }
    }
}
