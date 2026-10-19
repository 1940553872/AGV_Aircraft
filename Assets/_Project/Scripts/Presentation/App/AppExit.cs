using RivetSim.Core.Logging;
using UnityEngine;

namespace RivetSim.Presentation.App
{
    /// <summary>
    /// 退出程序（v1.1）：独立程序调用 Application.Quit；编辑器中退出 Play 模式。
    /// 由 HUD 退出确认框的“退出”按钮或 Enter 键触发。
    /// </summary>
    public static class AppExit
    {
        /// <summary>记录日志并退出。</summary>
        public static void Quit()
        {
            SimLog.Info(LogCategory.System, "用户确认退出程序");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
