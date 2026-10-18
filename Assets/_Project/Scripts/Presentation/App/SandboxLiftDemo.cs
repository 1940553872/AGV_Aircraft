using System;
using System.Threading;
using RivetSim.Core.Clock;
using RivetSim.Core.Config;
using RivetSim.Core.Events;
using RivetSim.Core.Logging;
using RivetSim.Simulation.Robot;
using UnityEngine;

namespace RivetSim.Presentation.App
{
    /// <summary>
    /// Sandbox_Robot 的升降柱联锁演示入口（M2 第二部分）：持有仿真时钟并每帧推进底盘与升降柱，进入 Play 模式后依次执行
    /// 未锁定升降（应被拒绝）→ 零速锁定 → 升到安装面 2.0 m → 升起时解锁（应被拒绝）→ 降到最低 → 解锁 → 行驶 1 m 停车。
    /// 引用由 SandboxSceneBuilder 写入，不在运行时查找对象。
    /// </summary>
    public sealed class SandboxLiftDemo : MonoBehaviour
    {
        [SerializeField, Tooltip("仿真设置（倍速列表）")]
        private SimSettings simSettings;

        [SerializeField, Tooltip("AGV 底盘控制器")]
        private AgvController chassis;

        [SerializeField, Tooltip("升降柱控制器")]
        private LiftColumnController lift;

        [SerializeField, Tooltip("演示倍速（须在 CFG_SimSettings 的倍速列表内）")]
        private float demoSpeedMultiplier = 4f;

        [SerializeField, Tooltip("演示目标：机械臂安装面离地高度，单位 m")]
        private float demoMountHeightM = 2.0f;

        [SerializeField, Tooltip("演示行驶距离，单位 m")]
        private float demoDriveDistanceM = 1.0f;

        [SerializeField, Tooltip("步骤之间的停顿（仿真时间），单位 s")]
        private float stepPauseS = 1.0f;

        private SimClock _clock;
        private CancellationTokenSource _cancellation;

        /// <summary>编辑器场景生成器写入引用。</summary>
        public void Configure(SimSettings settings, AgvController agvController, LiftColumnController liftController)
        {
            simSettings = settings;
            chassis = agvController;
            lift = liftController;
        }

        private void Awake()
        {
            _clock = new SimClock(simSettings.speedOptions, simSettings.defaultSpeedMultiplier);
            if (!_clock.TrySetSpeed(demoSpeedMultiplier))
                SimLog.Warning(LogCategory.System, $"演示倍速 {demoSpeedMultiplier} 不在倍速列表内，使用默认倍速 {_clock.SpeedMultiplier}");
            SimLog.SimTimeProvider = () => _clock.TimeS;

            var eventBus = new EventBus();
            // M4 起机械臂姿态由 ArmController 提供（运输姿态 Stow），取代占位实现。
            var safety = new SafetyMonitor(chassis, lift, chassis.GetComponentInChildren<ArmController>());
            chassis.Initialize(safety);
            lift.Initialize(safety, eventBus);
            _cancellation = new CancellationTokenSource();
        }

        private void Update()
        {
            float dtS = _clock.Advance(Time.deltaTime);
            chassis.Tick(dtS);
            lift.Tick(dtS);
        }

        private void OnDestroy()
        {
            _cancellation?.Cancel();
            _cancellation?.Dispose();
            _cancellation = null;
            SimLog.SimTimeProvider = null;
        }

        private async Awaitable Start()
        {
            try
            {
                await RunAsync(_cancellation.Token);
            }
            catch (OperationCanceledException)
            {
                SimLog.Info(LogCategory.System, "升降柱演示已中止");
            }
        }

        private async Awaitable RunAsync(CancellationToken ct)
        {
            AgvConfig agv = chassis.Config;
            SimLog.Info(LogCategory.System, $"升降柱联锁演示开始（{_clock.SpeedMultiplier:0.#}× 倍速）");

            Step(1, "未锁定时下发升降指令（应被拒绝）");
            Expect(!await lift.MoveToHeightAsync(demoMountHeightM, ct), "升降指令被拒绝");
            await PauseAsync(ct);

            Step(2, "零速锁定");
            Expect(chassis.ZeroSpeedLock(), "底盘已零速锁定");
            await PauseAsync(ct);

            Step(3, $"升到安装面离地 {demoMountHeightM:0.00} m");
            double start = _clock.TimeS;
            Expect(await lift.MoveToHeightAsync(demoMountHeightM, ct), $"已到位，用时 {_clock.TimeS - start:0.0} s（仿真时间）");
            await PauseAsync(ct);

            Step(4, "升降柱升起时解除零速锁定（应被拒绝）");
            Expect(!chassis.ReleaseZeroSpeedLock(), "解锁被拒绝");
            await PauseAsync(ct);

            Step(5, "降到最低位");
            start = _clock.TimeS;
            Expect(await lift.LowerToBottomAsync(ct), $"已降到最低，用时 {_clock.TimeS - start:0.0} s（仿真时间）");
            await PauseAsync(ct);

            Step(6, "解除零速锁定");
            Expect(chassis.ReleaseZeroSpeedLock(), "底盘已解锁");
            await PauseAsync(ct);

            Step(7, $"以 {agv.cruiseSpeedMps:0.00} m/s 行驶 {demoDriveDistanceM:0.00} m 后停车");
            float startOdometer = chassis.OdometerM;
            float brakingM = agv.cruiseSpeedMps * agv.cruiseSpeedMps / (2f * agv.maxAccelerationMps2);
            Expect(chassis.SetVelocity(agv.cruiseSpeedMps, 0f), "开始行驶");
            while (chassis.OdometerM - startOdometer < demoDriveDistanceM - brakingM) await Awaitable.NextFrameAsync(ct);
            chassis.Stop();
            while (!chassis.IsStationary) await Awaitable.NextFrameAsync(ct);
            Expect(true, $"已停车，行驶 {chassis.OdometerM - startOdometer:0.00} m");

            SimLog.Info(LogCategory.System, $"升降柱联锁演示结束，总用时 {_clock.TimeS:0.0} s（仿真时间）");
        }

        private static void Step(int index, string text) => SimLog.Info(LogCategory.System, $"演示步骤 {index}：{text}");

        private static void Expect(bool ok, string text)
        {
            if (ok) SimLog.Info(LogCategory.System, $"  ✓ {text}");
            else SimLog.Error(LogCategory.System, $"  × 与预期不符：{text}");
        }

        private async Awaitable PauseAsync(CancellationToken ct)
        {
            double until = _clock.TimeS + stepPauseS;
            while (_clock.TimeS < until) await Awaitable.NextFrameAsync(ct);
        }
    }
}
