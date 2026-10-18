using System;
using System.Threading;
using RivetSim.Core.Config;
using RivetSim.Core.Events;
using RivetSim.Core.Logging;
using RivetSim.Simulation.Robot;
using RivetSim.Simulation.World;
using UnityEngine;

namespace RivetSim.Simulation.Inspection
{
    /// <summary>
    /// 工位 / 单钉检测动作（M4）：T3 基准注册、T4 对中、T5 贴合采集（接触 → 采集 → 判读）、T6 回退，以及收臂。
    /// M5 的执行器直接调用这些方法，流程顺序由 SOP 任务矩阵决定（本类不写死流程）。
    /// 视觉、接触力、A 扫与判读均为教学简化模型（<see cref="WristVision"/>、<see cref="ContactModel"/>、<see cref="AScanSynthesizer"/>、<see cref="GateJudge"/>）。
    /// 工具姿态：工具轴沿 −法向，腕部相机优先朝上（逆解失败时依次试绕工具轴转 90°、−90°、180°）。
    /// </summary>
    public sealed class RivetInspectionService
    {
        private static readonly float[] RollCandidatesDeg = { 0f, 90f, -90f, 180f };

        private readonly ArmController _arm;
        private readonly ContactModel _contact;
        private readonly WristVision _vision;
        private readonly AScanSynthesizer _synthesizer;
        private readonly GateJudge _judge;
        private readonly InspectionRecordStore _store;
        private readonly ArmConfig _armConfig;
        private readonly InspectionConfig _inspection;
        private readonly EventBus _bus;
        private readonly Func<double> _timeS;
        private float _rollDeg;
        private bool _mirrored;
        private Vector3 _measuredCenter;

        /// <summary>当前步骤说明。</summary>
        public string CurrentStep { get; private set; } = "";

        /// <summary>腕部相机是否正在测量（可视化用）。</summary>
        public bool CameraActive { get; private set; }

        /// <summary>当前工位的注册结果（未注册为空）。</summary>
        public StationRegistration Registration { get; private set; }

        /// <summary>检测记录。</summary>
        public InspectionRecordStore Store => _store;

        /// <summary>A 扫合成器。</summary>
        public AScanSynthesizer Synthesizer => _synthesizer;

        /// <summary>创建服务。</summary>
        public RivetInspectionService(ArmController arm, ContactModel contact, WristVision vision, AScanSynthesizer synthesizer, GateJudge judge,
            InspectionRecordStore store, ArmConfig armConfig, InspectionConfig inspection, EventBus bus, Func<double> timeS)
        {
            _arm = arm;
            _contact = contact;
            _vision = vision;
            _synthesizer = synthesizer;
            _judge = judge;
            _store = store;
            _armConfig = armConfig;
            _inspection = inspection;
            _bus = bus;
            _timeS = timeS;
        }

        /// <summary>工位的机身是否在机械臂基座左侧（是则使用 Observe 的镜像）。</summary>
        public static bool IsLeftSide(ArmController arm, InspectionStation station)
        {
            Vector3 normal = station.Rivets.Count > 0 ? station.Rivets[0].Normal : station.DatumMarker.NormalWorld;
            return Vector3.Dot(normal, arm.Rig.transform.right) > 0f;
        }

        /// <summary>工具轴沿 −normal、端面距 pointM 为 standoffM、相机朝上并绕工具轴转 rollDeg 的 TCP 位姿。</summary>
        public static Pose ToolPoseFacing(Vector3 pointM, Vector3 normal, float standoffM, float rollDeg)
        {
            Vector3 forward = -normal.normalized;
            Vector3 up = Vector3.ProjectOnPlane(Vector3.up, forward);
            if (up.sqrMagnitude < 1e-6f) up = Vector3.ProjectOnPlane(Vector3.forward, forward);
            Quaternion rotation = Quaternion.AngleAxis(rollDeg, forward) * Quaternion.LookRotation(forward, up.normalized);
            return new Pose(pointM + normal.normalized * standoffM, rotation);
        }

        /// <summary>使腕部相机位于 cameraPose 时的 TCP 位姿。</summary>
        public static Pose TcpForCamera(ArmRig rig, Pose cameraPose)
        {
            Quaternion inv = Quaternion.Inverse(rig.Tcp.rotation);
            Vector3 camInTcpPos = inv * (rig.WristCamera.position - rig.Tcp.position);
            Quaternion camInTcpRot = inv * rig.WristCamera.rotation;
            Quaternion tcpRot = cameraPose.rotation * Quaternion.Inverse(camInTcpRot);
            return new Pose(cameraPose.position - tcpRot * camInTcpPos, tcpRot);
        }

        /// <summary>基准注册时相机的期望位姿：光轴沿 −标记法向，距标记中心 observeStandoffM，相机朝上。</summary>
        public static Pose DatumViewCameraPose(RegionMarker datum, float standoffM, float rollDeg) =>
            ToolPoseFacing(datum.TagCenterWorld, datum.NormalWorld, standoffM, rollDeg);

        /// <summary>T3 基准注册：移动到 Observe（左侧工位用镜像）→ 相机对准基准标记 → 识别得到标记位姿估计。</summary>
        public async Awaitable<ArmMotionResult> RegisterStationAsync(InspectionStation station, CancellationToken ct)
        {
            Registration = null;
            if (station.DatumMarker == null) return ArmMotionResult.Failed($"{station.StationId} 没有基准标记");
            double start = _timeS();
            _mirrored = IsLeftSide(_arm, station);
            Step($"T3 注册：经直立过渡姿态移动到观察姿态{(_mirrored ? "（左侧镜像）" : "")}");
            ArmMotionResult ready = await _arm.MoveToNamedPoseAsync(ArmConfig.ReadyPoseName, false, ct);
            if (!ready.Success) return ready;
            ArmMotionResult observe = await _arm.MoveToNamedPoseAsync(ArmConfig.ObservePoseName, _mirrored, ct);
            if (!observe.Success) return observe;

            Step($"T3 注册：腕部相机对准基准标记 ID {station.DatumMarker.TagId}");
            var q = new float[_arm.Rig.JointCount];
            bool solved = false;
            foreach (float roll in RollCandidatesDeg)
            {
                Pose tcp = TcpForCamera(_arm.Rig, DatumViewCameraPose(station.DatumMarker, _armConfig.observeStandoffM, roll));
                if (!_arm.SolveWorld(tcp, q, true).Converged) continue;
                solved = true;
                break;
            }
            if (!solved) return ArmMotionResult.Failed($"基准观察位姿逆解失败（{_arm.LastClearance}）");
            ArmMotionResult view = await _arm.MoveToJointsAsync(q, "对准基准标记", ct);
            if (!view.Success) return view;

            CameraActive = true;
            ArmMotionResult measure = await _arm.DwellAsync(_armConfig.visionMeasureTimeS, "基准标记识别", ct);
            CameraActive = false;
            if (!measure.Success) return measure;
            if (!_vision.TryRegisterDatum(_arm.Rig.WristCamera, station.DatumMarker, out Pose estimate))
                return ArmMotionResult.Failed($"腕部相机未识别到基准标记 ID {station.DatumMarker.TagId}");
            Registration = new StationRegistration(station, estimate);
            SimLog.Info(LogCategory.Arm, $"{station.StationId} 基准注册完成：标记 ID {station.DatumMarker.TagId}，位置估计误差 {Registration.DatumErrorM * 1000f:0.00} mm（教学简化模型）");
            return new ArmMotionResult(true, "", (float)(_timeS() - start), view.MinClearanceM);
        }

        /// <summary>
        /// T4 对中：移动到预计钉位法向外 preContactStandoffM（first 为 true 时先关节插值到近距离限速区外再直线进入，否则直线横移），
        /// 腕部相机复测钉心后修正到实测钉位。
        /// </summary>
        public async Awaitable<ArmMotionResult> CenterOnRivetAsync(Rivet rivet, bool first, CancellationToken ct)
        {
            if (Registration == null) return ArmMotionResult.Failed("工位尚未注册");
            Registration.TryGetPredicted(rivet, out Vector3 predicted, out Vector3 normal);
            float standoff = _armConfig.preContactStandoffM;
            Step($"T4 对中：{rivet.RivetId}");
            ArmMotionResult move;
            if (first)
            {
                var q = new float[_arm.Rig.JointCount];
                bool solved = false;
                foreach (float roll in RollCandidatesDeg)
                {
                    if (!_arm.SolveWorld(ToolPoseFacing(predicted, normal, standoff, roll), q, true).Converged) continue;
                    _rollDeg = roll;
                    solved = true;
                    break;
                }
                if (!solved) return ArmMotionResult.Failed($"{rivet.RivetId} 对中位姿逆解失败（{_arm.LastClearance}）");
                float linear = Mathf.Max(0f, _armConfig.nearFieldDistanceM - standoff);
                move = await _arm.MoveToPoseAsync(ToolPoseFacing(predicted, normal, standoff, _rollDeg), linear, $"对中 {rivet.RivetId}", ct);
            }
            else
            {
                move = await _arm.MoveLinearAsync(ToolPoseFacing(predicted, normal, standoff, _rollDeg), _armConfig.nearFieldSpeedMps, $"移到 {rivet.RivetId}", ct);
            }
            if (!move.Success) return move;

            CameraActive = true;
            ArmMotionResult measure = await _arm.DwellAsync(_armConfig.visionMeasureTimeS, "钉心复测", ct);
            CameraActive = false;
            if (!measure.Success) return measure;
            if (!_vision.TryMeasureRivet(_arm.Rig.WristCamera, rivet, out _measuredCenter))
                return ArmMotionResult.Failed($"腕部相机未看到 {rivet.RivetId}（视场 / 工作距离外）");
            return await _arm.MoveLinearAsync(ToolPoseFacing(_measuredCenter, normal, standoff, _rollDeg), _armConfig.nearFieldSpeedMps,
                $"修正对中 {rivet.RivetId}", ct);
        }

        /// <summary>T5 贴合采集：沿法向逼近到目标接触力 → 采集 → 合成 A 扫并判读 → 写入记录与铆钉结果、发布事件。</summary>
        public async Awaitable<ArmMotionResult> ContactAndAcquireAsync(Rivet rivet, CancellationToken ct)
        {
            Step($"T5 贴合：{rivet.RivetId} 沿法向逼近（目标 {_inspection.targetContactForceN:0} N）");
            _contact.SetSurface(rivet.PositionM, rivet.Normal);
            ArmMotionResult approach = await _arm.ApproachAlongNormalAsync(_armConfig.preContactStandoffM, $"贴合 {rivet.RivetId}", ct);
            if (!approach.Success) return approach;

            Step($"T5 采集：{rivet.RivetId}（{_inspection.acquisitionDurationS:0.0} s）");
            ArmMotionResult acquire = await _arm.DwellAsync(_inspection.acquisitionDurationS, "超声采集", ct);
            if (!acquire.Success) return acquire;

            float force = _contact.ForceN;
            float coupling = _contact.Coupling;
            AScanWaveform waveform = _synthesizer.Synthesize(rivet.Defect, coupling);
            GateResult gates = _judge.Judge(waveform);
            InspectionVerdict verdict = gates.Verdict;
            string reason = gates.Reason;
            if (!_contact.IsWithinAllowedRange)
            {
                verdict = InspectionVerdict.Invalid;
                reason = $"接触力 {force:0.0} N 不在 {_inspection.contactForceMinN:0}～{_inspection.contactForceMaxN:0} N";
            }
            else if (coupling < _inspection.couplingInvalidThreshold)
            {
                verdict = InspectionVerdict.Invalid;
                reason = $"耦合质量 {coupling:0.00} 低于 {_inspection.couplingInvalidThreshold:0.00}";
            }
            Vector3 offset = Vector3.ProjectOnPlane(_measuredCenter - rivet.PositionM, rivet.Normal);
            var record = new InspectionRecord(rivet.RivetId, StationIdOf(rivet), rivet.Defect, verdict, reason, force, coupling, offset.magnitude, gates)
            {
                TimeS = _timeS(),
            };
            rivet.SetResult(verdict);
            _store.Add(record);
            _bus?.Publish(new RivetInspectedEvent(rivet, record, waveform));
            return new ArmMotionResult(true, "", approach.DurationS + acquire.DurationS, float.NaN);
        }

        /// <summary>T6 释放：沿工具轴回退到对中距离，接触力归零。</summary>
        public async Awaitable<ArmMotionResult> ReleaseAsync(Rivet rivet, CancellationToken ct)
        {
            Step($"T6 释放：{rivet.RivetId} 回退");
            ArmMotionResult retreat = await _arm.RetreatAsync(_armConfig.preContactStandoffM + _contact.CompressionM, $"回退 {rivet.RivetId}", ct);
            _contact.ClearSurface();
            return retreat;
        }

        /// <summary>一颗铆钉的 T4 → T5 → T6，并把节拍写入记录。</summary>
        public async Awaitable<ArmMotionResult> InspectRivetAsync(Rivet rivet, bool first, CancellationToken ct)
        {
            double start = _timeS();
            ArmMotionResult center = await CenterOnRivetAsync(rivet, first, ct);
            if (!center.Success) return center;
            ArmMotionResult acquire = await ContactAndAcquireAsync(rivet, ct);
            if (!acquire.Success)
            {
                _contact.ClearSurface();
                return acquire;
            }
            ArmMotionResult release = await ReleaseAsync(rivet, ct);
            if (!release.Success) return release;
            float cycle = (float)(_timeS() - start);
            InspectionRecord record = _store.Records[_store.Records.Count - 1];
            record.CycleTimeS = cycle;
            SimLog.Info(LogCategory.UT, $"{rivet.RivetId}：{Label(record.Verdict)}（{record.Reason}）；接触力 {record.ContactForceN:0.00} N、" +
                                        $"对中误差 {record.CenteringErrorM * 1000f:0.00} mm、节拍 {cycle:0.00} s（真值 {rivet.Defect}）");
            return new ArmMotionResult(true, "", cycle, center.MinClearanceM);
        }

        /// <summary>收臂到运输姿态：已注册工位时先回到该侧观察姿态，再经直立过渡姿态收到 Stow（与展开路径相反）。</summary>
        public async Awaitable<ArmMotionResult> StowAsync(CancellationToken ct)
        {
            Step("收臂：运输姿态");
            if (Registration != null)
            {
                ArmMotionResult observe = await _arm.MoveToNamedPoseAsync(ArmConfig.ObservePoseName, _mirrored, ct);
                if (!observe.Success) return observe;
            }
            Registration = null;
            ArmMotionResult ready = await _arm.MoveToNamedPoseAsync(ArmConfig.ReadyPoseName, false, ct);
            if (!ready.Success) return ready;
            return await _arm.MoveToNamedPoseAsync(ArmConfig.StowPoseName, false, ct);
        }

        /// <summary>结论的中文名称。</summary>
        public static string Label(InspectionVerdict verdict) => verdict switch
        {
            InspectionVerdict.Pass => "合格",
            InspectionVerdict.Suspect => "疑似",
            InspectionVerdict.Defect => "缺陷",
            _ => "无效",
        };

        private string StationIdOf(Rivet rivet) => Registration != null ? Registration.Station.StationId : "";

        private void Step(string text) => CurrentStep = text;
    }
}
