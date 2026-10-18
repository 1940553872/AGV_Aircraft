using RivetSim.Core.Config;
using RivetSim.Core.Logging;
using UnityEngine;

namespace RivetSim.Simulation.Robot
{
    /// <summary>
    /// AGV 底盘控制器：差速运动学（<see cref="DiffDriveKinematics"/>）、运动学刚体（MovePosition）、线速度 / 角速度及其加速度限幅、
    /// 零速锁定、急停与驱动轮转动动画。行驶与解除锁定须经 <see cref="SafetyMonitor"/> 规则 2 许可（升降柱最低位、机械臂运输姿态）。
    /// 由启动入口按仿真步长调用 <see cref="Tick"/>，本类不读取 UnityEngine.Time（ADR-012）。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class AgvController : MonoBehaviour, IChassisState
    {
        [SerializeField, Tooltip("AGV 配置（速度、加速度、角速度上限、轮距、轮径）")]
        private AgvConfig config;

        [SerializeField, Tooltip("左驱动轮转动节点（绕本地 X 轴转动）")]
        private Transform leftWheel;

        [SerializeField, Tooltip("右驱动轮转动节点（绕本地 X 轴转动）")]
        private Transform rightWheel;

        private Rigidbody _body;
        private SafetyMonitor _safety;
        private float _commandLinearMps;
        private float _commandAngularDegPerS;
        private PlanarPose _pose;
        private float _leftWheelAngleDeg;
        private float _rightWheelAngleDeg;

        /// <summary>当前线速度（前进为正），单位 m/s。</summary>
        public float LinearSpeedMps { get; private set; }

        /// <summary>当前角速度（顺时针为正），单位 °/s。</summary>
        public float AngularSpeedDegPerS { get; private set; }

        /// <summary>累计行驶里程，单位 m。</summary>
        public float OdometerM { get; private set; }

        /// <summary>当前真值位姿（底盘地面投影中心 = 驱动轮轴中点）。</summary>
        public PlanarPose Pose => _pose;

        /// <inheritdoc />
        public bool IsZeroSpeedLocked { get; private set; }

        /// <inheritdoc />
        public bool IsStationary => LinearSpeedMps == 0f && AngularSpeedDegPerS == 0f;

        /// <summary>AGV 配置。</summary>
        public AgvConfig Config => config;

        /// <summary>编辑器生成器写入配置。</summary>
        public void Configure(AgvConfig agvConfig) => config = agvConfig;

        /// <summary>编辑器生成器写入配置与驱动轮转动节点。</summary>
        public void Configure(AgvConfig agvConfig, Transform leftWheelPivot, Transform rightWheelPivot)
        {
            config = agvConfig;
            leftWheel = leftWheelPivot;
            rightWheel = rightWheelPivot;
        }

        /// <summary>注入安全监视，并以当前变换作为初始位姿。</summary>
        public void Initialize(SafetyMonitor safety)
        {
            _safety = safety;
            _body = GetComponent<Rigidbody>();
            Vector3 p = transform.position;
            _pose = new PlanarPose(new Vector2(p.x, p.z), Mathf.Repeat(transform.eulerAngles.y, 360f));
        }

        /// <summary>零速锁定（须已停稳且无速度指令）。</summary>
        public bool ZeroSpeedLock()
        {
            if (!IsStationary || _commandLinearMps != 0f || _commandAngularDegPerS != 0f)
            {
                SimLog.Warning(LogCategory.Nav, "底盘未停稳，不能零速锁定");
                return false;
            }
            if (IsZeroSpeedLocked) return true;
            IsZeroSpeedLocked = true;
            SimLog.Info(LogCategory.Nav, "底盘零速锁定");
            return true;
        }

        /// <summary>解除零速锁定；被联锁拒绝时返回 false。</summary>
        public bool ReleaseZeroSpeedLock()
        {
            if (!IsZeroSpeedLocked) return true;
            if (!Authorize("解除零速锁定")) return false;
            IsZeroSpeedLocked = false;
            SimLog.Info(LogCategory.Nav, "底盘解除零速锁定");
            return true;
        }

        /// <summary>下发速度指令（按上限截断）；非零指令在零速锁定中或被联锁拒绝时返回 false。</summary>
        public bool SetVelocity(float linearMps, float angularDegPerS)
        {
            bool moving = linearMps != 0f || angularDegPerS != 0f;
            if (moving)
            {
                if (IsZeroSpeedLocked)
                {
                    SimLog.Warning(LogCategory.Safety, "联锁拒绝：底盘行驶（零速锁定中）");
                    return false;
                }
                // 已在行驶中时联锁状态不可能改变（升降柱运动须先零速锁定），只在由静止起步时判定一次，避免每个子步重复记录。
                bool alreadyCommanded = _commandLinearMps != 0f || _commandAngularDegPerS != 0f || !IsStationary;
                if (!alreadyCommanded && !Authorize("底盘行驶")) return false;
            }
            _commandLinearMps = Mathf.Clamp(linearMps, -config.maxSpeedMps, config.maxSpeedMps);
            _commandAngularDegPerS = Mathf.Clamp(angularDegPerS, -config.maxAngularSpeedDegPerS, config.maxAngularSpeedDegPerS);
            return true;
        }

        /// <summary>停车指令（按加速度限幅减速到 0，不需联锁许可）。</summary>
        public void Stop()
        {
            _commandLinearMps = 0f;
            _commandAngularDegPerS = 0f;
        }

        /// <summary>急停：清除指令并立即停止（教学简化模型，不计制动距离）。</summary>
        public void EmergencyStop()
        {
            Stop();
            LinearSpeedMps = 0f;
            AngularSpeedDegPerS = 0f;
            SimLog.Warning(LogCategory.Safety, "底盘急停");
        }

        /// <summary>按仿真步长推进运动学与车轮动画。</summary>
        public void Tick(float dtS)
        {
            if (dtS <= 0f || _body == null) return;
            float linearTarget = IsZeroSpeedLocked ? 0f : _commandLinearMps;
            float angularTarget = IsZeroSpeedLocked ? 0f : _commandAngularDegPerS;
            LinearSpeedMps = Mathf.MoveTowards(LinearSpeedMps, linearTarget, config.maxAccelerationMps2 * dtS);
            AngularSpeedDegPerS = Mathf.MoveTowards(AngularSpeedDegPerS, angularTarget, config.maxAngularAccelerationDegPerS2 * dtS);
            if (IsStationary) return;

            _pose = DiffDriveKinematics.Integrate(_pose, LinearSpeedMps, AngularSpeedDegPerS, dtS);
            OdometerM += Mathf.Abs(LinearSpeedMps) * dtS;
            _body.MovePosition(_pose.WorldPosition);
            _body.MoveRotation(_pose.WorldRotation);
            AnimateWheels(dtS);
        }

        private void AnimateWheels(float dtS)
        {
            Vector2 wheel = DiffDriveKinematics.ToWheelSpeeds(LinearSpeedMps, AngularSpeedDegPerS, config.wheelTrackM);
            float degPerM = Mathf.Rad2Deg / (0.5f * config.driveWheelDiameterM);
            _leftWheelAngleDeg = Mathf.Repeat(_leftWheelAngleDeg + wheel.x * dtS * degPerM, 360f);
            _rightWheelAngleDeg = Mathf.Repeat(_rightWheelAngleDeg + wheel.y * dtS * degPerM, 360f);
            if (leftWheel != null) leftWheel.localRotation = Quaternion.Euler(_leftWheelAngleDeg, 0f, 0f);
            if (rightWheel != null) rightWheel.localRotation = Quaternion.Euler(_rightWheelAngleDeg, 0f, 0f);
        }

        private bool Authorize(string command)
        {
            if (_safety != null) return _safety.TryAuthorizeChassisMotion(command);
            SimLog.Error(LogCategory.Nav, $"底盘未初始化安全监视，拒绝：{command}");
            return false;
        }
    }
}
