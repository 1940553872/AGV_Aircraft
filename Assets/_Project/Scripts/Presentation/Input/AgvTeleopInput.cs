using RivetSim.Simulation.Navigation;
using RivetSim.Simulation.Robot;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RivetSim.Presentation.Input
{
    /// <summary>
    /// 键盘遥控：W / S 以巡航速度前进 / 后退，A / D 以最大角速度左转 / 右转，空格急停（同时中止正在执行的导航任务）。
    /// 只在允许遥控且没有导航任务时下发速度；按键状态不变时不重复下发，避免联锁拒绝日志刷屏。
    /// </summary>
    public sealed class AgvTeleopInput : MonoBehaviour
    {
        private AgvController _chassis;
        private NavigationService _navigation;
        private Vector2 _lastCommand;

        /// <summary>是否允许遥控（自动演示期间为 false；急停始终有效）。</summary>
        public bool TeleopEnabled { get; set; }

        /// <summary>注入底盘与导航服务。</summary>
        public void Initialize(AgvController chassis, NavigationService navigation)
        {
            _chassis = chassis;
            _navigation = navigation;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || _chassis == null) return;
            if (keyboard.spaceKey.wasPressedThisFrame)
            {
                _navigation?.Abort("急停");
                _chassis.EmergencyStop();
                _lastCommand = Vector2.zero;
                return;
            }
            if (!TeleopEnabled || (_navigation != null && _navigation.IsBusy)) return;

            float forward = (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f);
            float turn = (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f);
            var command = new Vector2(forward * _chassis.Config.cruiseSpeedMps, turn * _chassis.Config.maxAngularSpeedDegPerS);
            if (command == _lastCommand) return;
            _lastCommand = command;
            if (command == Vector2.zero) _chassis.Stop();
            else _chassis.SetVelocity(command.x, command.y);
        }
    }
}
