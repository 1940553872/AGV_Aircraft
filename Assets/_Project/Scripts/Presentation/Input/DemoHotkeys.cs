using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RivetSim.Presentation.Input
{
    /// <summary>
    /// 演示快捷键（M6，新输入系统）：1～4 机位、0 自动机位、Tab 矩阵全屏、P 暂停 / 继续、N 下一步、B 大屏 / 全屏视图切换（M8）、Esc 退出确认 / 取消、Enter 确认退出（v1.1）。
    /// V 由 <see cref="VisualizationToggleInput"/> 处理。M8 起不再提供故障注入热键（故障由随机故障调度器自动注入）。
    /// </summary>
    public sealed class DemoHotkeys : MonoBehaviour
    {
        /// <summary>机位键（0 自动，1～4 预设）。</summary>
        public event Action<int> CameraPressed;

        /// <summary>Tab。</summary>
        public event Action FullscreenPressed;

        /// <summary>P。</summary>
        public event Action PausePressed;

        /// <summary>N。</summary>
        public event Action NextPressed;

        /// <summary>B（M8：大屏视图切换；全屏时切换全屏视图）。</summary>
        public event Action ViewPressed;

        /// <summary>Esc（v1.1：打开 / 取消退出确认框）。</summary>
        public event Action EscapePressed;

        /// <summary>Enter / 小键盘 Enter（v1.1：退出确认框打开时确认退出）。</summary>
        public event Action ConfirmPressed;

        private void Update()
        {
            Keyboard k = Keyboard.current;
            if (k == null) return;
            if (k.digit1Key.wasPressedThisFrame) CameraPressed?.Invoke(1);
            if (k.digit2Key.wasPressedThisFrame) CameraPressed?.Invoke(2);
            if (k.digit3Key.wasPressedThisFrame) CameraPressed?.Invoke(3);
            if (k.digit4Key.wasPressedThisFrame) CameraPressed?.Invoke(4);
            if (k.digit0Key.wasPressedThisFrame) CameraPressed?.Invoke(0);
            if (k.tabKey.wasPressedThisFrame) FullscreenPressed?.Invoke();
            if (k.pKey.wasPressedThisFrame) PausePressed?.Invoke();
            if (k.nKey.wasPressedThisFrame) NextPressed?.Invoke();
            if (k.bKey.wasPressedThisFrame) ViewPressed?.Invoke();
            if (k.escapeKey.wasPressedThisFrame) EscapePressed?.Invoke();
            if (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame) ConfirmPressed?.Invoke();
        }
    }
}
