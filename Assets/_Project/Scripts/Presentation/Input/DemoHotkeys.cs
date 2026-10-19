using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RivetSim.Presentation.Input
{
    /// <summary>
    /// 演示快捷键（M6，新输入系统）：1～4 机位、0 自动机位、Tab 矩阵全屏、P 暂停 / 继续、N 下一步。
    /// 故障注入 F5 / F6 / F7 仍由 <see cref="FaultHotkeys"/> 处理，V 由 <see cref="VisualizationToggleInput"/> 处理。
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
        }
    }
}
