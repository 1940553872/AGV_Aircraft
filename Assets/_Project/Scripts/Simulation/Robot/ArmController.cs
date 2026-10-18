using System;
using System.Collections.Generic;
using System.Threading;
using RivetSim.Core.Config;
using RivetSim.Core.Events;
using RivetSim.Core.Logging;
using UnityEngine;

namespace RivetSim.Simulation.Robot
{
    /// <summary>
    /// 机械臂控制器（主体 S3，M4）：命名姿态（Stow / Observe）、关节插值运动、末端直线运动、沿法向逼近（接触力达到目标即停）、
    /// 回退与原地等待。运动在 <see cref="Tick"/> 中按仿真子步推进（不读 UnityEngine.Time，ADR-012），异步方法只等待动作结束；
    /// 同一时刻只执行一个动作。逆解见 <see cref="ArmIkSolver"/>，每个目标解与关节插值路径都经 <see cref="ArmClearance"/> 检查连杆到机身外壳的间隙
    /// （只检查不规划）。实现 <see cref="IArmPoseState"/>，供 SafetyMonitor 规则 2 判定；离开运输姿态须经规则 3 许可。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ArmController : MonoBehaviour, IArmPoseState
    {
        // 直线运动逐步跟踪时每个子步的逆解迭代上限。
        private const int TrackingIterations = 30;
        // 直线运动跟踪误差允许为收敛阈值的倍数。
        private const float TrackingToleranceFactor = 5f;
        // 姿态变化折算为路程的权重，单位 m/rad（只改姿态的直线运动也有合理用时）。
        private const float RotationPathWeightM = 0.1f;
        // 沿法向逼近时，预计接触点之后允许的额外行程（浮动行程的倍数）。
        private const float ApproachOvertravelFactor = 3f;

        [SerializeField, Tooltip("机械臂配置")]
        private ArmConfig config;

        [SerializeField, Tooltip("机械臂运动学骨架")]
        private ArmRig rig;

        private SafetyMonitor _safety;
        private EventBus _bus;
        private ContactModel _contact;
        private ArmKinematics _kinematics;
        private ArmIkSolver _ik;
        private ArmClearance _clearance;
        private float[] _q;
        private float[] _stow;
        private Motion _active;

        /// <summary>机械臂配置。</summary>
        public ArmConfig Config => config;

        /// <summary>运动学骨架。</summary>
        public ArmRig Rig => rig;

        /// <summary>运动学模型。</summary>
        public ArmKinematics Kinematics => _kinematics;

        /// <summary>逆解器。</summary>
        public ArmIkSolver Ik => _ik;

        /// <summary>连杆间隙检查（未设置碰撞层时为空）。</summary>
        public ArmClearance Clearance => _clearance;

        /// <summary>接触力模型（可为空）。</summary>
        public ContactModel Contact => _contact;

        /// <summary>逆解统计。</summary>
        public IkStatistics IkStats { get; } = new IkStatistics();

        /// <summary>是否有动作在执行。</summary>
        public bool IsMoving => _active != null;

        /// <summary>当前动作说明（空闲时为空字符串）。</summary>
        public string CurrentMotionLabel => _active?.Label ?? "";

        /// <summary>最近一次目标解的间隙检查结果。</summary>
        public ClearanceResult LastClearance { get; private set; }

        /// <summary>自上次清零以来，被接受的目标解与关节插值路径中的最小间隙（连杆 / 末端取较小者），单位 m。</summary>
        public float MinAcceptedClearanceM { get; private set; } = float.MaxValue;

        /// <summary>最小间隙对应的连杆。</summary>
        public string MinAcceptedClearanceLink { get; private set; } = "—";

        /// <summary>清零最小间隙统计。</summary>
        public void ResetClearanceStats()
        {
            MinAcceptedClearanceM = float.MaxValue;
            MinAcceptedClearanceLink = "—";
        }

        /// <inheritdoc />
        public bool IsInTransportPose => _active == null && IsAtJoints(_stow ?? StowPose());

        /// <summary>TCP 位姿（世界坐标）。</summary>
        public Pose TcpPoseWorld => new Pose(rig.Tcp.position, rig.Tcp.rotation);

        /// <summary>编辑器生成器写入引用。</summary>
        public void Configure(ArmConfig armConfig, ArmRig armRig)
        {
            config = armConfig;
            rig = armRig;
        }

        /// <summary>建立运动学模型、逆解器与间隙检查（编辑器工具也可单独调用）；clearanceLayerMask 为 0 时不检查间隙。</summary>
        public void InitializeKinematics(int seed, int clearanceLayerMask)
        {
            _kinematics = ArmKinematics.FromRig(rig, rig.Tcp);
            _ik = new ArmIkSolver(_kinematics, IkSettings.From(config), seed);
            AgvController vehicle = GetComponentInParent<AgvController>();
            _clearance = clearanceLayerMask != 0 ? new ArmClearance(rig, config, clearanceLayerMask, vehicle != null ? vehicle.transform : null) : null;
            _q = rig.GetJointAnglesDeg();
            _stow = StowPose();
        }

        /// <summary>运行时注入安全监视、事件总线与接触力模型，并建立运动学。</summary>
        public void Initialize(SafetyMonitor safety, EventBus bus, ContactModel contact, int seed, int clearanceLayerMask)
        {
            _safety = safety;
            _bus = bus;
            _contact = contact;
            InitializeKinematics(seed, clearanceLayerMask);
        }

        /// <summary>按仿真步长推进当前动作。</summary>
        public void Tick(float dtS)
        {
            if (_active == null || _active.State != MotionState.Running) return;
            _active.ElapsedS += dtS;
            _active.Step(dtS);
        }

        /// <summary>当前关节角（副本），单位 °。</summary>
        public float[] CurrentJointsDeg() => (float[])(_q ?? rig.GetJointAnglesDeg()).Clone();

        /// <summary>立即设置关节角（不经运动过程与联锁，仅供初始化、编辑器预览与截图）。</summary>
        public void SetJointsImmediate(float[] anglesDeg)
        {
            _q ??= new float[rig.JointCount];
            Apply(anglesDeg);
        }

        /// <summary>
        /// 取命名姿态关节角；mirrored 为 true 时基座角转半圈（Observe 定义为面向车体右侧，左侧工位用它的镜像）。
        /// 半圈取 −180°（基座角已小于 −180° 时取 +180°），使从直立过渡姿态转过去时手臂经车尾一侧，不扫向机身。找不到时返回 null。
        /// </summary>
        public float[] NamedPose(string poseName, bool mirrored = false)
        {
            if (!config.TryGetNamedPose(poseName, out float[] q)) return null;
            if (mirrored) q[0] = q[0] > -180f ? q[0] - 180f : q[0] + 180f;
            return q;
        }

        /// <summary>当前关节角是否在 target 的容差内（按角度差，-180° 与 180° 视为同一位置）。</summary>
        public bool IsAtJoints(float[] target)
        {
            if (target == null) return false;
            float[] q = _q ?? rig.GetJointAnglesDeg();
            for (int i = 0; i < target.Length && i < q.Length; i++)
                if (Mathf.Abs(Mathf.DeltaAngle(q[i], target[i])) > config.namedPoseToleranceDeg) return false;
            return true;
        }

        /// <summary>世界坐标位姿 → 机械臂基座坐标。</summary>
        public Pose ToBase(Pose world)
        {
            Transform b = rig.transform;
            Quaternion inv = Quaternion.Inverse(b.rotation);
            return new Pose(inv * (world.position - b.position), inv * world.rotation);
        }

        /// <summary>
        /// 求 TCP 到达 tcpWorld 的关节角（初值：当前关节角、Observe 及其镜像、Stow、额外初值），checkClearance 时只接受间隙满足要求的解。
        /// </summary>
        public IkResult SolveWorld(Pose tcpWorld, float[] solution, bool checkClearance, IReadOnlyList<float[]> extraSeeds = null)
        {
            var seeds = new List<float[]> { CurrentJointsDeg() };
            if (extraSeeds != null) seeds.AddRange(extraSeeds);
            float[] observe = NamedPose(ArmConfig.ObservePoseName);
            if (observe != null)
            {
                seeds.Add(observe);
                seeds.Add(NamedPose(ArmConfig.ObservePoseName, true));
            }
            seeds.Add(StowPose());
            Func<float[], bool> accept = null;
            if (checkClearance && _clearance != null)
            {
                accept = q =>
                {
                    LastClearance = _clearance.MeasureAt(q);
                    if (LastClearance.Acceptable) NoteClearance(LastClearance);
                    return LastClearance.Acceptable;
                };
            }
            IkResult result = _ik.Solve(ToBase(tcpWorld), seeds, accept, solution);
            IkStats.Record(result);
            return result;
        }

        /// <summary>关节插值移动到命名姿态。</summary>
        public Awaitable<ArmMotionResult> MoveToNamedPoseAsync(string poseName, bool mirrored, CancellationToken ct)
        {
            float[] q = NamedPose(poseName, mirrored);
            if (q == null) return Completed(ArmMotionResult.Failed($"CFG_Arm 中没有命名姿态 {poseName}"));
            string label = $"移动到 {poseName}{(mirrored ? "（镜像）" : "")}";
            SimLog.Info(LogCategory.Arm, $"机械臂{label}：({string.Join(", ", Array.ConvertAll(q, a => a.ToString("0.#")))})°");
            return MoveToJointsAsync(q, label, ct);
        }

        /// <summary>关节插值移动到 targetDeg；先沿关节空间直线采样检查连杆间隙。</summary>
        public Awaitable<ArmMotionResult> MoveToJointsAsync(float[] targetDeg, string label, CancellationToken ct)
        {
            if (!CanStart(label, out string refusal)) return Completed(ArmMotionResult.Failed(refusal));
            float[] from = CurrentJointsDeg();
            float clearance = float.NaN;
            if (_clearance != null)
            {
                ClearanceResult path = _clearance.MeasurePath(from, targetDeg, config.clearancePathSamples);
                clearance = path.MinM;
                if (!path.Acceptable) return Completed(Fail(label, $"关节插值路径上间隙不足：{path}"));
                NoteClearance(path);
            }
            float chord = Vector3.Distance(_kinematics.Forward(from).position, _kinematics.Forward(targetDeg).position);
            var trajectory = new JointTrajectory(from, targetDeg, JointSpeedLimitDegPerS(), config.jointMoveAccelerationDegPerS2,
                chord / config.endEffectorSpeedMps);
            return RunAsync(new JointMotion(this, label, trajectory, targetDeg), clearance, ct);
        }

        /// <summary>
        /// 移动 TCP 到 tcpWorld：先关节插值到沿工具轴后退 linearFinalM 的位置，再以近距离限速直线进入（linearFinalM 为 0 时只做关节插值）。
        /// </summary>
        public async Awaitable<ArmMotionResult> MoveToPoseAsync(Pose tcpWorld, float linearFinalM, string label, CancellationToken ct)
        {
            Pose pre = new Pose(tcpWorld.position - tcpWorld.rotation * Vector3.forward * linearFinalM, tcpWorld.rotation);
            var q = new float[rig.JointCount];
            IkResult ik = SolveWorld(pre, q, true);
            if (!ik.Converged) return Fail(label, $"逆解失败（{ik}；{LastClearance}）");
            ArmMotionResult joint = await MoveToJointsAsync(q, label, ct);
            if (!joint.Success || linearFinalM <= 0f) return joint;
            ArmMotionResult linear = await MoveLinearAsync(tcpWorld, config.nearFieldSpeedMps, $"{label}（末段直线）", ct);
            return linear.Success
                ? new ArmMotionResult(true, "", joint.DurationS + linear.DurationS, Mathf.Min(joint.MinClearanceM, linear.MinClearanceM))
                : linear;
        }

        /// <summary>TCP 直线移动到 tcpWorld（姿态球面插值），速度上限 speedMps；先检查终点解的连杆间隙。</summary>
        public Awaitable<ArmMotionResult> MoveLinearAsync(Pose tcpWorld, float speedMps, string label, CancellationToken ct)
        {
            if (!CanStart(label, out string refusal)) return Completed(ArmMotionResult.Failed(refusal));
            var q = new float[rig.JointCount];
            IkResult ik = SolveWorld(tcpWorld, q, true);
            if (!ik.Converged) return Completed(Fail(label, $"终点逆解失败（{ik}；{LastClearance}）"));
            float clearance = _clearance != null ? LastClearance.MinM : float.NaN;
            Pose start = TcpPoseWorld;
            float length = Mathf.Max(Vector3.Distance(start.position, tcpWorld.position),
                Quaternion.Angle(start.rotation, tcpWorld.rotation) * Mathf.Deg2Rad * RotationPathWeightM);
            var profile = new TrapezoidProfile(length, speedMps, config.endEffectorAccelerationMps2);
            return RunAsync(new LinearMotion(this, label, start, tcpWorld, profile), clearance, ct);
        }

        /// <summary>
        /// 沿工具轴逼近：距预计接触点 toolFloatTravelM 以前用近距离限速，之后用接触逼近速度；接触力达到目标即停，
        /// 超过超限阈值时发布 <see cref="ContactForceExceededEvent"/> 并失败，越过预计接触点过多仍未接触也失败。
        /// </summary>
        public Awaitable<ArmMotionResult> ApproachAlongNormalAsync(float expectedDistanceM, string label, CancellationToken ct)
        {
            if (_contact == null || !_contact.HasSurface) return Completed(ArmMotionResult.Failed($"{label}：未设置接触表面"));
            if (!CanStart(label, out string refusal)) return Completed(ArmMotionResult.Failed(refusal));
            float slowStart = Mathf.Max(0f, expectedDistanceM - config.toolFloatTravelM);
            float maxTravel = expectedDistanceM + config.toolFloatTravelM * ApproachOvertravelFactor;
            return RunAsync(new ApproachMotion(this, label, TcpPoseWorld, slowStart, maxTravel), float.NaN, ct);
        }

        /// <summary>沿工具轴反方向回退 distanceM（近距离限速）。</summary>
        public Awaitable<ArmMotionResult> RetreatAsync(float distanceM, string label, CancellationToken ct)
        {
            Pose now = TcpPoseWorld;
            Pose target = new Pose(now.position - now.rotation * Vector3.forward * distanceM, now.rotation);
            return MoveLinearAsync(target, config.nearFieldSpeedMps, label, ct);
        }

        /// <summary>原地等待 durationS（仿真时间），如视觉测量与超声采集。</summary>
        public Awaitable<ArmMotionResult> DwellAsync(float durationS, string label, CancellationToken ct)
        {
            if (_active != null) return Completed(ArmMotionResult.Failed($"{label}：已有动作在执行"));
            return RunAsync(new DwellMotion(this, label, durationS), float.NaN, ct);
        }

        private float[] StowPose() => NamedPose(ArmConfig.StowPoseName);

        private void NoteClearance(in ClearanceResult r)
        {
            if (r.MinM >= MinAcceptedClearanceM) return;
            MinAcceptedClearanceM = r.MinM;
            MinAcceptedClearanceLink = r.NearestLink;
        }

        private float JointSpeedLimitDegPerS()
        {
            float v = config.jointMoveSpeedDegPerS;
            for (int i = 0; i < rig.JointCount; i++) v = Mathf.Min(v, rig.GetJoint(i).MaxSpeedDegPerS);
            return v;
        }

        private bool CanStart(string label, out string refusal)
        {
            refusal = "";
            if (_kinematics == null) refusal = $"{label}：机械臂未初始化";
            else if (_active != null) refusal = $"{label}：已有动作在执行（{_active.Label}）";
            else if (_safety != null && IsInTransportPose && !_safety.TryAuthorizeArmLeaveTransport($"机械臂{label}"))
            {
                _safety.CheckArmLeaveTransport(out string reason);
                refusal = $"联锁拒绝：{reason}";
            }
            return refusal.Length == 0;
        }

        private void Apply(float[] anglesDeg)
        {
            rig.SetJointAnglesDeg(anglesDeg);
            rig.GetJointAnglesDeg(_q);
            if (_contact == null) return;
            _contact.Update(rig.Tcp.position);
            if (rig.Probe != null) rig.Probe.ApplyCompression(_contact.CompressionM);
        }

        private static ArmMotionResult Fail(string label, string reason)
        {
            SimLog.Warning(LogCategory.Arm, $"机械臂{label}未完成：{reason}");
            return ArmMotionResult.Failed(reason);
        }

        private static Awaitable<ArmMotionResult> Completed(ArmMotionResult result)
        {
            var source = new AwaitableCompletionSource<ArmMotionResult>();
            source.SetResult(result);
            return source.Awaitable;
        }

        private async Awaitable<ArmMotionResult> RunAsync(Motion motion, float clearanceM, CancellationToken ct)
        {
            _active = motion;
            try
            {
                while (motion.State == MotionState.Running) await Awaitable.NextFrameAsync(ct);
            }
            catch (OperationCanceledException)
            {
                SimLog.Warning(LogCategory.Arm, $"机械臂{motion.Label}被取消，停在当前位置");
                throw;
            }
            finally
            {
                _active = null;
            }
            return motion.State == MotionState.Succeeded
                ? new ArmMotionResult(true, "", motion.ElapsedS, clearanceM)
                : Fail(motion.Label, motion.Failure);
        }

        private enum MotionState
        {
            Running,
            Succeeded,
            Failed,
        }

        private abstract class Motion
        {
            protected readonly ArmController Owner;
            public readonly string Label;
            public MotionState State = MotionState.Running;
            public string Failure = "";
            public float ElapsedS;

            protected Motion(ArmController owner, string label)
            {
                Owner = owner;
                Label = label;
            }

            public abstract void Step(float dtS);

            protected void Finish() => State = MotionState.Succeeded;

            protected void Abort(string reason)
            {
                Failure = reason;
                State = MotionState.Failed;
            }
        }

        private sealed class JointMotion : Motion
        {
            private readonly JointTrajectory _trajectory;
            private readonly float[] _target;
            private readonly float[] _buffer;

            public JointMotion(ArmController owner, string label, JointTrajectory trajectory, float[] target) : base(owner, label)
            {
                _trajectory = trajectory;
                _target = (float[])target.Clone();
                _buffer = new float[target.Length];
            }

            public override void Step(float dtS)
            {
                if (ElapsedS >= _trajectory.DurationS)
                {
                    Owner.Apply(_target);
                    Finish();
                    return;
                }
                _trajectory.Sample(ElapsedS, _buffer);
                Owner.Apply(_buffer);
            }
        }

        // 直线运动：每个子步对插值位姿做少量逆解迭代（以当前关节角为初值），跟踪失败或关节速度超限（接近奇异位形）时停止。
        private class LinearMotion : Motion
        {
            private readonly Pose _start;
            private readonly Pose _end;
            private readonly TrapezoidProfile _profile;
            protected readonly float[] Buffer;

            public LinearMotion(ArmController owner, string label, Pose start, Pose end, TrapezoidProfile profile) : base(owner, label)
            {
                _start = start;
                _end = end;
                _profile = profile;
                Buffer = new float[owner.rig.JointCount];
            }

            public override void Step(float dtS)
            {
                float s = _profile.Distance > 0f ? _profile.PositionAt(ElapsedS) / _profile.Distance : 1f;
                var target = new Pose(Vector3.Lerp(_start.position, _end.position, s), Quaternion.Slerp(_start.rotation, _end.rotation, s));
                if (!Track(target, dtS)) return;
                if (ElapsedS >= _profile.DurationS) Finish();
            }

            protected bool Track(Pose targetWorld, float dtS)
            {
                Array.Copy(Owner._q, Buffer, Buffer.Length);
                ArmConfig c = Owner.config;
                Owner._ik.Refine(Owner.ToBase(targetWorld), Buffer, TrackingIterations, out float pos, out float ang, out _);
                if (pos > c.ikPositionToleranceM * TrackingToleranceFactor || ang > c.ikOrientationToleranceDeg * TrackingToleranceFactor)
                {
                    Abort($"直线运动逆解跟踪失败（位置误差 {pos * 1000f:0.00} mm、姿态误差 {ang:0.00}°）");
                    return false;
                }
                for (int i = 0; i < Buffer.Length; i++)
                {
                    float speed = Mathf.Abs(Buffer[i] - Owner._q[i]) / Mathf.Max(dtS, 1e-6f);
                    if (speed > Owner.rig.GetJoint(i).MaxSpeedDegPerS)
                    {
                        Abort($"关节 {i + 1} 角速度 {speed:0} °/s 超限（接近奇异位形）");
                        return false;
                    }
                }
                Owner.Apply(Buffer);
                return true;
            }
        }

        private sealed class ApproachMotion : LinearMotion
        {
            private readonly Pose _origin;
            private readonly Vector3 _axis;
            private readonly float _slowStartM;
            private readonly float _maxTravelM;
            private float _traveledM;

            public ApproachMotion(ArmController owner, string label, Pose origin, float slowStartM, float maxTravelM)
                : base(owner, label, origin, origin, new TrapezoidProfile(0f, 1f, 1f))
            {
                _origin = origin;
                _axis = origin.rotation * Vector3.forward;
                _slowStartM = slowStartM;
                _maxTravelM = maxTravelM;
            }

            public override void Step(float dtS)
            {
                ArmConfig c = Owner.config;
                float speed = _traveledM < _slowStartM ? c.nearFieldSpeedMps : c.contactApproachSpeedMps;
                _traveledM += speed * dtS;
                if (!Track(new Pose(_origin.position + _axis * _traveledM, _origin.rotation), dtS)) return;
                ContactModel contact = Owner._contact;
                if (contact.IsOverLimit)
                {
                    Owner._bus?.Publish(new ContactForceExceededEvent(contact.ForceN, contact.Config.contactForceLimitN, Label));
                    Abort($"接触力 {contact.ForceN:0.0} N 超过超限阈值 {contact.Config.contactForceLimitN:0} N");
                    return;
                }
                if (contact.ForceN >= contact.Config.targetContactForceN)
                {
                    Finish();
                    return;
                }
                if (_traveledM > _maxTravelM) Abort($"逼近 {_traveledM * 1000f:0.0} mm 仍未达到目标接触力（{contact.ForceN:0.0} N）");
            }
        }

        private sealed class DwellMotion : Motion
        {
            private readonly float _durationS;

            public DwellMotion(ArmController owner, string label, float durationS) : base(owner, label) => _durationS = durationS;

            public override void Step(float dtS)
            {
                if (ElapsedS >= _durationS) Finish();
            }
        }
    }
}
