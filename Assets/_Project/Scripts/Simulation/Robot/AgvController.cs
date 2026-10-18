using RivetSim.Core.Config;
using RivetSim.Core.Logging;
using UnityEngine;

namespace RivetSim.Simulation.Robot
{
    /// <summary>
    /// AGV 底盘控制器（M2 第二部分最小子集）：差速运动学、运动学刚体（MovePosition）、线速度与线加速度限幅、零速锁定。
    /// 行驶与解除锁定须经 <see cref="SafetyMonitor"/> 规则 2 许可（升降柱最低位、机械臂运输姿态）。
    /// 角速度暂不做加速度限幅；车轮动画、导航接口在 M3 补充。由启动入口每帧用仿真步长调用 <see cref="Tick"/>。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class AgvController : MonoBehaviour, IChassisState
    {
        [SerializeField, Tooltip("AGV 配置（速度、加速度、角速度上限）")]
        private AgvConfig config;

        private Rigidbody _body;
        private SafetyMonitor _safety;
        private float _commandLinearMps;
        private float _commandAngularDegPerS;
        private Vector3 _positionM;
        private float _yawDeg;

        /// <summary>当前线速度（前进为正），单位 m/s。</summary>
        public float LinearSpeedMps { get; private set; }

        /// <summary>当前角速度（顺时针为正），单位 °/s。</summary>
        public float AngularSpeedDegPerS { get; private set; }

        /// <summary>累计行驶里程，单位 m。</summary>
        public float OdometerM { get; private set; }

        /// <inheritdoc />
        public bool IsZeroSpeedLocked { get; private set; }

        /// <inheritdoc />
        public bool IsStationary => LinearSpeedMps == 0f && AngularSpeedDegPerS == 0f;

        /// <summary>AGV 配置。</summary>
        public AgvConfig Config => config;

        /// <summary>编辑器生成器写入配置。</summary>
        public void Configure(AgvConfig agvConfig) => config = agvConfig;

        /// <summary>注入安全监视，并以当前变换作为初始位姿。</summary>
        public void Initialize(SafetyMonitor safety)
        {
            _safety = safety;
            _body = GetComponent<Rigidbody>();
            _positionM = transform.position;
            _yawDeg = transform.eulerAngles.y;
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
                if (!Authorize("底盘行驶")) return false;
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

        /// <summary>按仿真步长推进运动学。</summary>
        public void Tick(float dtS)
        {
            if (dtS <= 0f || _body == null) return;
            float linearTarget = IsZeroSpeedLocked ? 0f : _commandLinearMps;
            LinearSpeedMps = Mathf.MoveTowards(LinearSpeedMps, linearTarget, config.maxAccelerationMps2 * dtS);
            AngularSpeedDegPerS = IsZeroSpeedLocked ? 0f : _commandAngularDegPerS;
            if (IsStationary) return;

            _yawDeg += AngularSpeedDegPerS * dtS;
            Quaternion rotation = Quaternion.Euler(0f, _yawDeg, 0f);
            _positionM += rotation * Vector3.forward * (LinearSpeedMps * dtS);
            OdometerM += Mathf.Abs(LinearSpeedMps) * dtS;
            _body.MovePosition(_positionM);
            _body.MoveRotation(rotation);
        }

        private bool Authorize(string command)
        {
            if (_safety != null) return _safety.TryAuthorizeChassisMotion(command);
            SimLog.Error(LogCategory.Nav, $"底盘未初始化安全监视，拒绝：{command}");
            return false;
        }
    }
}
