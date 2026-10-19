using System.Diagnostics;
using UnityEngine.Profiling;

namespace RivetSim.Presentation.UI
{
    /// <summary>
    /// 界面性能探针（M6 验证用）：用 Profiler 的 GC.Alloc 标记（仅当前线程，与 Test Framework 的 AllocatingGCMemory 约束相同的方法）
    /// 统计演示界面逐帧刷新与事件处理中的托管分配次数，并用 Stopwatch 统计事件处理用时。
    /// 由 RivetSim/Verify/Run Demo UI 清零并读取，写入 Docs/Reports/M6.md。
    /// </summary>
    public static class UiPerfProbe
    {
        private const string GcAllocMarker = "GC.Alloc";
        private static Recorder _recorder;
        private static long _eventStartTicks;

        /// <summary>逐帧刷新累计分配次数。</summary>
        public static long FrameAllocCount { get; private set; }

        /// <summary>逐帧刷新调用次数。</summary>
        public static int FrameCount { get; private set; }

        /// <summary>有分配的逐帧刷新次数。</summary>
        public static int FramesWithAlloc { get; private set; }

        /// <summary>事件处理累计用时，单位 ms。</summary>
        public static double EventTotalMs { get; private set; }

        /// <summary>单次事件处理最长用时，单位 ms。</summary>
        public static double EventMaxMs { get; private set; }

        /// <summary>事件处理次数。</summary>
        public static int EventCount { get; private set; }

        /// <summary>事件处理累计分配次数。</summary>
        public static long EventAllocCount { get; private set; }

        /// <summary>清零。</summary>
        public static void Reset()
        {
            FrameAllocCount = 0;
            FrameCount = 0;
            FramesWithAlloc = 0;
            EventTotalMs = 0d;
            EventMaxMs = 0d;
            EventCount = 0;
            EventAllocCount = 0;
        }

        /// <summary>逐帧刷新开始。</summary>
        public static void BeginFrame() => StartRecorder();

        /// <summary>逐帧刷新结束。</summary>
        public static void EndFrame()
        {
            int count = StopRecorder();
            FrameAllocCount += count;
            FrameCount++;
            if (count > 0) FramesWithAlloc++;
        }

        /// <summary>事件处理开始。</summary>
        public static void BeginEvent()
        {
            StartRecorder();
            _eventStartTicks = Stopwatch.GetTimestamp();
        }

        /// <summary>事件处理结束。</summary>
        public static void EndEvent()
        {
            double ms = (Stopwatch.GetTimestamp() - _eventStartTicks) * 1000d / Stopwatch.Frequency;
            EventAllocCount += StopRecorder();
            EventTotalMs += ms;
            if (ms > EventMaxMs) EventMaxMs = ms;
            EventCount++;
        }

        private static void StartRecorder()
        {
            _recorder ??= Recorder.Get(GcAllocMarker);
            if (!_recorder.isValid) return;
            _recorder.enabled = false;
            _recorder.FilterToCurrentThread();
            _recorder.enabled = true;
        }

        private static int StopRecorder()
        {
            if (_recorder == null || !_recorder.isValid) return 0;
            _recorder.enabled = false;
            return _recorder.sampleBlockCount;
        }
    }
}
