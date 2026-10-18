using System;
using System.Threading;
using RivetSim.Core.Config;
using RivetSim.Core.Events;
using RivetSim.Core.Logging;
using UnityEngine;

namespace RivetSim.Simulation.Robot
{
    /// <summary>
    /// 升降柱控制器（属于主体 S3 的执行范围）：按 <see cref="LiftMotionProfile"/> 驱动各节伸缩段，机械臂装在最上一节的法兰上。
    /// 运动前须经 <see cref="SafetyMonitor"/> 规则 1 许可（底盘零速锁定）；到位后发布 <see cref="LiftArrivedEvent"/>。
    /// 由启动入口调用 <see cref="Initialize"/> 注入依赖，并每帧用仿真步长调用 <see cref="Tick"/>。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LiftColumnController : MonoBehaviour, ILiftState
    {
        [SerializeField, Tooltip("AGV 配置（升降柱行程、速度、加速度、到位精度）")]
        private AgvConfig config;

        [SerializeField, Tooltip("伸缩段，自下而上；第 i 节（从 0 起）上升量 = 伸出量 × (i + 1) / 节数")]
        private Transform[] stages = Array.Empty<Transform>();

        [SerializeField, Tooltip("各伸缩段在最低位时的本地位置")]
        private Vector3[] stageBaseLocalPositions = Array.Empty<Vector3>();

        private LiftMotionProfile _profile;
        private SafetyMonitor _safety;
        private EventBus _eventBus;

        /// <inheritdoc />
        public float ExtensionM => Profile.PositionM;

        /// <inheritdoc />
        public bool IsMoving => Profile.IsMoving;

        /// <inheritdoc />
        public bool IsAtBottom => !Profile.IsMoving && Profile.PositionM <= Profile.ToleranceM;

        /// <summary>当前机械臂安装面离地高度，单位 m。</summary>
        public float MountHeightM => config.ArmMountHeightM(ExtensionM);

        /// <summary>AGV 配置。</summary>
        public AgvConfig Config => config;

        private LiftMotionProfile Profile => _profile ??= new LiftMotionProfile(config.liftStrokeM, config.liftSpeedMps,
            config.liftAccelerationMps2, config.liftPositionToleranceM);

        /// <summary>编辑器生成器写入配置与伸缩段引用。</summary>
        public void Configure(AgvConfig agvConfig, Transform[] stageTransforms)
        {
            config = agvConfig;
            stages = stageTransforms ?? Array.Empty<Transform>();
            stageBaseLocalPositions = new Vector3[stages.Length];
            for (int i = 0; i < stages.Length; i++) stageBaseLocalPositions[i] = stages[i].localPosition;
            _profile = null;
        }

        /// <summary>注入安全监视与事件总线。</summary>
        public void Initialize(SafetyMonitor safety, EventBus eventBus)
        {
            _safety = safety;
            _eventBus = eventBus;
        }

        /// <summary>按仿真步长推进运动。</summary>
        public void Tick(float dtS)
        {
            if (!Profile.IsMoving) return;
            bool arrived = Profile.Step(dtS);
            ApplyStages();
            if (!arrived) return;
            SimLog.Info(LogCategory.Arm, $"升降柱到位：伸出 {ExtensionM:0.000} m，安装面离地 {MountHeightM:0.000} m");
            _eventBus?.Publish(new LiftArrivedEvent(ExtensionM, MountHeightM));
        }

        /// <summary>把机械臂安装面移动到离地 mountHeightM（超出行程时截断）；被联锁拒绝时返回 false。</summary>
        public Awaitable<bool> MoveToHeightAsync(float mountHeightM, CancellationToken cancellationToken) =>
            MoveToExtensionAsync(config.ExtensionForMountHeightM(mountHeightM), cancellationToken);

        /// <summary>降到最低位；被联锁拒绝时返回 false。</summary>
        public Awaitable<bool> LowerToBottomAsync(CancellationToken cancellationToken) => MoveToExtensionAsync(0f, cancellationToken);

        /// <summary>移动到伸出量 extensionM（超出行程时截断）；被联锁拒绝时返回 false，取消时停在当前位置。</summary>
        public async Awaitable<bool> MoveToExtensionAsync(float extensionM, CancellationToken cancellationToken)
        {
            if (_safety == null)
            {
                SimLog.Error(LogCategory.Arm, "升降柱未初始化安全监视，拒绝运动。");
                return false;
            }
            if (!_safety.TryAuthorizeLiftMotion($"升降柱移动到伸出 {extensionM:0.000} m")) return false;

            float target = Profile.ClampToStroke(extensionM);
            if (Mathf.Abs(target - extensionM) > config.liftPositionToleranceM)
                SimLog.Warning(LogCategory.Arm, $"升降柱目标伸出 {extensionM:0.000} m 超出行程 0～{config.liftStrokeM:0.00} m，已限制为 {target:0.000} m");

            float from = ExtensionM;
            Profile.SetTarget(target);
            if (!Profile.IsMoving) return true;
            SimLog.Info(LogCategory.Arm,
                $"升降柱开始运动：伸出 {from:0.000} → {target:0.000} m（安装面 {config.ArmMountHeightM(target):0.000} m）");
            try
            {
                while (Profile.IsMoving) await Awaitable.NextFrameAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                Profile.Halt();
                SimLog.Warning(LogCategory.Arm, $"升降柱运动被取消，停在伸出 {ExtensionM:0.000} m");
                throw;
            }
            return true;
        }

        /// <summary>立即设置伸出量（不经运动过程与联锁，仅供编辑器预览与截图）。</summary>
        public void SetExtensionImmediate(float extensionM)
        {
            Profile.SetPositionImmediate(extensionM);
            ApplyStages();
        }

        private void ApplyStages()
        {
            int count = Mathf.Min(stages.Length, stageBaseLocalPositions.Length);
            for (int i = 0; i < count; i++)
            {
                if (stages[i] == null) continue;
                stages[i].localPosition = stageBaseLocalPositions[i] + Vector3.up * (ExtensionM * (i + 1) / count);
            }
        }
    }
}
