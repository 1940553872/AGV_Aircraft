using System.Diagnostics;
using UnityEngine.Profiling;

namespace RivetSim.Core.Diagnostics
{
    /// <summary>
    /// 模块级 GC 探针（M7）：用 Profiler 的 GC.Alloc 标记统计各模块更新入口的托管分配次数与耗时（仅当前线程，
    /// 与 Test Framework 的 AllocatingGCMemory 约束、M6 UiPerfProbe 方法相同）。默认关闭，关闭时 Begin / End 只做一次判断。
    /// 不可嵌套调用（GC.Alloc 记录器全局唯一）。
    /// </summary>
    public static class GcProbe
    {
        private const string GcAllocMarker = "GC.Alloc";
        private const int ModuleCount = 5;
        private static Recorder _recorder;
        private static long _startTicks;
        private static readonly long[] Calls = new long[ModuleCount];
        private static readonly long[] AllocCounts = new long[ModuleCount];
        private static readonly long[] CallsWithAlloc = new long[ModuleCount];
        private static readonly double[] TotalMs = new double[ModuleCount];
        private static readonly double[] MaxMs = new double[ModuleCount];

        /// <summary>是否统计（验证工具打开）。</summary>
        public static bool Enabled { get; set; }

        /// <summary>清零全部模块。</summary>
        public static void Reset()
        {
            for (int i = 0; i < ModuleCount; i++)
            {
                Calls[i] = AllocCounts[i] = CallsWithAlloc[i] = 0;
                TotalMs[i] = MaxMs[i] = 0d;
            }
        }

        /// <summary>模块更新开始。</summary>
        public static void Begin(ProbeModule module)
        {
            if (!Enabled) return;
            _recorder ??= Recorder.Get(GcAllocMarker);
            if (_recorder.isValid)
            {
                _recorder.enabled = false;
                _recorder.FilterToCurrentThread();
                _recorder.enabled = true;
            }
            _startTicks = Stopwatch.GetTimestamp();
        }

        /// <summary>模块更新结束。</summary>
        public static void End(ProbeModule module)
        {
            if (!Enabled) return;
            double ms = (Stopwatch.GetTimestamp() - _startTicks) * 1000d / Stopwatch.Frequency;
            int allocs = 0;
            if (_recorder != null && _recorder.isValid)
            {
                _recorder.enabled = false;
                allocs = _recorder.sampleBlockCount;
            }
            int i = (int)module;
            Calls[i]++;
            AllocCounts[i] += allocs;
            if (allocs > 0) CallsWithAlloc[i]++;
            TotalMs[i] += ms;
            if (ms > MaxMs[i]) MaxMs[i] = ms;
        }

        /// <summary>调用次数。</summary>
        public static long CallCount(ProbeModule module) => Calls[(int)module];

        /// <summary>累计 GC.Alloc 次数。</summary>
        public static long AllocCount(ProbeModule module) => AllocCounts[(int)module];

        /// <summary>发生分配的调用次数。</summary>
        public static long CallsWithAllocCount(ProbeModule module) => CallsWithAlloc[(int)module];

        /// <summary>平均耗时，单位 ms。</summary>
        public static double AverageMs(ProbeModule module) => Calls[(int)module] > 0 ? TotalMs[(int)module] / Calls[(int)module] : 0d;

        /// <summary>最大耗时，单位 ms。</summary>
        public static double MaxDurationMs(ProbeModule module) => MaxMs[(int)module];
    }
}
