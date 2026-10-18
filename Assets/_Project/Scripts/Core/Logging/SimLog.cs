using System;
using UnityEngine;

namespace RivetSim.Core.Logging
{
    /// <summary>
    /// 统一日志入口：同时输出到 Unity Console 与 <see cref="EntryLogged"/> 订阅者（演示屏日志）。
    /// 项目代码不直接调用 Debug.Log。
    /// </summary>
    public static class SimLog
    {
        /// <summary>每写入一条日志时触发。</summary>
        public static event Action<SimLogEntry> EntryLogged;

        /// <summary>仿真时间来源，由启动入口接入 SimClock；为空时记为 0。</summary>
        public static Func<double> SimTimeProvider { get; set; }

        /// <summary>是否同时输出到 Unity Console，默认 true。</summary>
        public static bool ForwardToUnityConsole { get; set; } = true;

        /// <summary>写入普通信息。</summary>
        public static void Info(LogCategory category, string message) => Write(category, SimLogLevel.Info, message);

        /// <summary>写入警告。</summary>
        public static void Warning(LogCategory category, string message) => Write(category, SimLogLevel.Warning, message);

        /// <summary>写入错误。</summary>
        public static void Error(LogCategory category, string message) => Write(category, SimLogLevel.Error, message);

        /// <summary>按指定级别写入。</summary>
        public static void Write(LogCategory category, SimLogLevel level, string message)
        {
            double timeS = SimTimeProvider?.Invoke() ?? 0d;
            var entry = new SimLogEntry(timeS, category, level, message);

            if (ForwardToUnityConsole)
            {
                string text = $"[{category}] {message}";
                switch (level)
                {
                    case SimLogLevel.Warning: Debug.LogWarning(text); break;
                    case SimLogLevel.Error: Debug.LogError(text); break;
                    default: Debug.Log(text); break;
                }
            }

            EntryLogged?.Invoke(entry);
        }

        // 关闭“进入 Play 模式时重载域”时，静态状态不会自动清空，这里手动复位。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            EntryLogged = null;
            SimTimeProvider = null;
            ForwardToUnityConsole = true;
        }
    }
}
