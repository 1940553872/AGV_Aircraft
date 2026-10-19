using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using RivetSim.Core.Logging;
using RivetSim.Core.Mission;
using UnityEngine;

namespace RivetSim.Presentation.App
{
    /// <summary>
    /// v1.1 独立程序帧率探针：仅在命令行带 -perfRun 时生效，正常启动不创建。
    /// 工序开始后 ×2 运行 x2Seconds（实际时间），再切 ×8 直到工序结束或总时长超过 maxSeconds；
    /// 每帧实际帧时写入 PerfLogs/frames.csv，结束时写 summary.txt 并退出。不改流程与界面。
    /// </summary>
    public sealed class BuildPerfProbe : MonoBehaviour
    {
        private const string Flag = "-perfRun";
        private const float WarmupS = 2f;
        private const float X2Seconds = 180f;
        private const float MaxSeconds = 600f;
        private const float SpikeAbsS = 0.05f;
        private const float SpikeMedianRatio = 4f;

        private struct Frame
        {
            public float RealT, Dt, Speed, SimT;
            public int Substeps;
            public string Phase, Cell;
        }

        private readonly List<Frame> _frames = new List<Frame>(200000);
        private readonly List<string> _guardEvents = new List<string>();
        private readonly List<string> _notes = new List<string>();
        private readonly List<IDisposable> _subscriptions = new List<IDisposable>();
        private string _outDir;
        private float _startRealT = -1f;
        private bool _switchedToX8;
        private bool _shotX2, _shotX8;
        private float _x8StartRealT;
        private bool _finished;
        private string _phase = "-", _cell = "-";
        private int _errors, _exceptions, _focusLost;
        private string _endReason = "未结束";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), Flag) < 0) return;
            var go = new GameObject("BuildPerfProbe");
            DontDestroyOnLoad(go);
            go.AddComponent<BuildPerfProbe>();
        }

        private void Awake()
        {
            Application.runInBackground = true;
            _outDir = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "PerfLogs");
            Directory.CreateDirectory(_outDir);
            Application.logMessageReceived += OnUnityLog;
            MissionLaunchOptions.Started = OnMissionStarted;
            MissionLaunchOptions.Completed = _ => Finish("工序完成");
        }

        private void OnMissionStarted(RobotRuntime runtime, MissionRoutine mission)
        {
            _subscriptions.Add(runtime.Bus.Subscribe<PhaseEnteredEvent>(e => _phase = e.PhaseCode));
            _subscriptions.Add(runtime.Bus.Subscribe<CellStateChangedEvent>(e => _cell = e.CellId));
            SimLog.EntryLogged += OnSimLog;
            runtime.Clock.TrySetSpeed(2f);
            _startRealT = Time.realtimeSinceStartup;
        }

        private void OnSimLog(SimLogEntry entry)
        {
            if (entry.Level == SimLogLevel.Error) _errors++;
        }

        private void OnUnityLog(string condition, string stack, LogType type)
        {
            if (type == LogType.Exception) _exceptions++;
        }

        private void OnApplicationFocus(bool focus)
        {
            if (!focus && _startRealT >= 0f) _focusLost++;
        }

        private void Update()
        {
            if (_finished || _startRealT < 0f) return;
            AppBootstrap app = AppBootstrap.Active;
            if (app == null || app.Runtime == null) return;
            float t = Time.realtimeSinceStartup - _startRealT;
            var clock = app.Runtime.Clock;

            if (t >= WarmupS)
            {
                _frames.Add(new Frame
                {
                    RealT = t, Dt = Time.unscaledDeltaTime, Speed = clock.SpeedMultiplier, SimT = (float)clock.TimeS,
                    Substeps = app.Runtime.LastFrameSubsteps, Phase = _phase, Cell = _cell,
                });
            }

            if (app.Guard != null && app.Guard.TriggerCount > _guardEvents.Count)
                _guardEvents.Add($"实际 {t:0.0} s / 仿真 {clock.TimeS:0.0} s：窗口 {app.Guard.LastWindowFps:0} FPS，降到 ×{clock.SpeedMultiplier:0.##}");

            if (!_shotX2 && t >= X2Seconds - 30f) { _shotX2 = true; Capture("Build_v1.1_x2.png"); }
            if (!_switchedToX8 && t >= X2Seconds)
            {
                _switchedToX8 = true;
                _x8StartRealT = t;
                if (!clock.TrySetSpeed(8f)) _notes.Add("切换 ×8 失败");
            }
            if (_switchedToX8 && !_shotX8 && t >= _x8StartRealT + 60f) { _shotX8 = true; Capture("Build_v1.1_x8.png"); }
            if (t >= MaxSeconds) Finish($"超过 {MaxSeconds:0} s 上限");
        }

        private void Capture(string fileName)
        {
            _ = CaptureAsync(Path.Combine(_outDir, fileName));
        }

        private async Awaitable CaptureAsync(string path)
        {
            await Awaitable.EndOfFrameAsync();
            Texture2D full = ScreenCapture.CaptureScreenshotAsTexture();
            const int w = 1280, h = 720;
            RenderTexture rt = RenderTexture.GetTemporary(w, h, 0);
            Graphics.Blit(full, rt);
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            var small = new Texture2D(w, h, TextureFormat.RGB24, false);
            small.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            small.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            File.WriteAllBytes(path, small.EncodeToPNG());
            Destroy(full);
            Destroy(small);
        }

        private void Finish(string reason)
        {
            if (_finished) return;
            _finished = true;
            _endReason = reason;
            try
            {
                WriteFrames();
                WriteSummary();
            }
            catch (Exception ex)
            {
                File.WriteAllText(Path.Combine(_outDir, "summary.txt"), "写入失败：" + ex);
            }
            Application.Quit();
        }

        private void WriteFrames()
        {
            var sb = new StringBuilder("real_s,dt_ms,speed,sim_s,substeps,phase,cell\n");
            foreach (Frame f in _frames)
                sb.Append(string.Format(CultureInfo.InvariantCulture, "{0:0.000},{1:0.00},{2:0.##},{3:0.00},{4},{5},\"{6}\"\n",
                    f.RealT, f.Dt * 1000f, f.Speed, f.SimT, f.Substeps, f.Phase, f.Cell));
            File.WriteAllText(Path.Combine(_outDir, "frames.csv"), sb.ToString());
        }

        private void WriteSummary()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"结束原因：{_endReason}");
            sb.AppendLine($"版本：{Application.version}  Unity {Application.unityVersion}");
            sb.AppendLine($"CPU：{SystemInfo.processorType}（{SystemInfo.processorCount} 线程）");
            sb.AppendLine($"GPU：{SystemInfo.graphicsDeviceName}（{SystemInfo.graphicsMemorySize} MB，{SystemInfo.graphicsDeviceType}）");
            sb.AppendLine($"内存：{SystemInfo.systemMemorySize} MB  系统：{SystemInfo.operatingSystem}");
            sb.AppendLine($"分辨率：{Screen.width}×{Screen.height}  {Screen.fullScreenMode}  刷新 {Screen.currentResolution.refreshRateRatio.value:0} Hz  vSync {QualitySettings.vSyncCount}  targetFrameRate {Application.targetFrameRate}");
            sb.AppendLine($"仿真时间：{(_frames.Count > 0 ? _frames[_frames.Count - 1].SimT : 0f):0.0} s  记录帧数：{_frames.Count}（启动后 {WarmupS:0} s 预热不计）");
            sb.AppendLine($"SimLog 错误：{_errors}  未捕获异常：{_exceptions}  失焦次数：{_focusLost}");
            sb.AppendLine();
            sb.AppendLine("倍速 | 帧数 | 实际时长 s | 平均 FPS | 最低 FPS（单帧） | 1% Low FPS | 中位帧时 ms | 尖峰数");

            var speeds = new SortedSet<float>();
            foreach (Frame f in _frames) speeds.Add(f.Speed);
            var spikes = new List<string>();
            foreach (float speed in speeds)
            {
                var dts = new List<float>();
                foreach (Frame f in _frames) if (Mathf.Approximately(f.Speed, speed)) dts.Add(f.Dt);
                if (dts.Count == 0) continue;
                var sorted = new List<float>(dts);
                sorted.Sort();
                float total = 0f;
                foreach (float d in dts) total += d;
                float median = sorted[sorted.Count / 2];
                float max = sorted[sorted.Count - 1];
                int worst = Mathf.Max(1, sorted.Count / 100);
                float worstSum = 0f;
                for (int i = sorted.Count - worst; i < sorted.Count; i++) worstSum += sorted[i];
                float threshold = Mathf.Min(SpikeAbsS, median * SpikeMedianRatio);
                int spikeCount = 0;
                foreach (Frame f in _frames)
                {
                    if (!Mathf.Approximately(f.Speed, speed) || f.Dt <= threshold) continue;
                    spikeCount++;
                    if (spikes.Count < 40)
                        spikes.Add($"×{speed:0.##} 实际 {f.RealT:0.00} s / 仿真 {f.SimT:0.0} s：{f.Dt * 1000f:0.0} ms（子步 {f.Substeps}，{f.Phase}，{f.Cell}）");
                }
                sb.AppendLine($"×{speed:0.##} | {dts.Count} | {total:0.0} | {dts.Count / total:0} | {1f / max:0} | {worst / worstSum:0} | {median * 1000f:0.00} | {spikeCount}");
            }
            sb.AppendLine();
            sb.AppendLine($"尖峰判据：帧时 > min({SpikeAbsS * 1000f:0} ms, 中位帧时 × {SpikeMedianRatio:0})");
            sb.AppendLine($"倍速保护触发：{_guardEvents.Count} 次");
            foreach (string g in _guardEvents) sb.AppendLine("- " + g);
            sb.AppendLine("尖峰明细（最多 40 条）：");
            foreach (string s in spikes) sb.AppendLine("- " + s);
            foreach (string n in _notes) sb.AppendLine("备注：" + n);
            File.WriteAllText(Path.Combine(_outDir, "summary.txt"), sb.ToString());
        }

        private void OnDestroy()
        {
            Application.logMessageReceived -= OnUnityLog;
            SimLog.EntryLogged -= OnSimLog;
            foreach (IDisposable s in _subscriptions) s.Dispose();
        }
    }
}
