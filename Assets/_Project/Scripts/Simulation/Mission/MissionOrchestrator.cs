using System;
using System.Collections.Generic;
using System.Threading;
using RivetSim.Core.Events;
using RivetSim.Core.Logging;
using RivetSim.Core.Mission;
using RivetSim.Core.TaskMatrix;
using UnityEngine;

namespace RivetSim.Simulation.Mission
{
    /// <summary>
    /// SOP 任务矩阵编排（Docs/Architecture.md §5.1）：按 JSON 的阶段、单元类型、runWhen、trigger、onComplete 与回路执行，代码里不写死流程。
    /// 阶段内 active 单元并行启动（<see cref="PhaseDependencyTable"/> 中的依赖除外），全部结束后阶段结束；conditional 单元进入阶段时布防，
    /// trigger 发生后激活，阶段须等它完成；onComplete = retryPhase 时重新执行该阶段（每个条件单元最多 maxRetries 次）。
    /// 末阶段结束后按 JSON loops 的顺序判定 when：“下一颗”（T6→T4）、同区域“换站”（→T2）、其他区域“换站”（→T1）。
    /// 逐钉循环内的阶段失败且未恢复时，本颗铆钉记为无效（<see cref="RivetUnrecovered"/>）并直接进入回路起点阶段（T6 释放退出）；其他阶段失败时任务中止。
    /// </summary>
    public sealed class MissionOrchestrator
    {
        /// <summary>回路 when：本工位还有下一颗铆钉。</summary>
        public const string HasNextRivet = "station.hasNextRivet";

        /// <summary>回路 when：本区域还有下一工位。</summary>
        public const string HasNextStation = "region.hasNextStation";

        /// <summary>回路 when：还有下一区域。</summary>
        public const string HasNextRegion = "mission.hasNextRegion";

        /// <summary>runWhen：本工位最后一颗铆钉。</summary>
        public const string LastRivetInStation = "lastRivetInStation";

        private readonly TaskMatrixRuntime _matrix;
        private readonly ExecutorRegistry _executors;
        private readonly ConditionBus _conditions;
        private readonly IMissionPlan _plan;
        private readonly EventBus _bus;
        private readonly Func<double> _timeS;
        private PhaseRun _current;
        private int _phaseRuns;
        private int _unrecovered;

        /// <summary>当前执行位置。</summary>
        public MissionCursor Cursor { get; }

        /// <summary>任务矩阵运行状态。</summary>
        public TaskMatrixRuntime Matrix => _matrix;

        /// <summary>当前阶段 id（不在阶段内时为空）。</summary>
        public string CurrentPhaseId => _current != null ? _matrix.Data.Phases[_current.Row - 1].Id : "";

        /// <summary>逐钉循环内某阶段未恢复（参数：位置、阶段代号、原因）；执行器据此把本颗记为无效并清除残留故障。</summary>
        public Action<MissionCursor, string, string> RivetUnrecovered { get; set; }

        /// <summary>创建编排器；ConditionBus 的 trigger 交由本类处理。</summary>
        public MissionOrchestrator(TaskMatrixData data, ExecutorRegistry executors, ConditionBus conditions, IMissionPlan plan, EventBus bus, Func<double> timeS)
        {
            _executors = executors ?? throw new ArgumentNullException(nameof(executors));
            _conditions = conditions ?? throw new ArgumentNullException(nameof(conditions));
            _plan = plan ?? throw new ArgumentNullException(nameof(plan));
            _bus = bus;
            _timeS = timeS ?? (() => 0d);
            Cursor = new MissionCursor();
            _matrix = new TaskMatrixRuntime(data, Cursor, bus, _timeS);
            _conditions.TriggerHandler = HandleTrigger;
        }

        /// <summary>启动核对：缺失的执行器键名、无法识别的 when / runWhen / trigger；返回问题列表（为空表示通过）。</summary>
        public List<string> Validate()
        {
            var problems = new List<string>();
            foreach (string key in _executors.FindMissing(_matrix.Data.GetExecutorKeys())) problems.Add($"执行器未注册：{key}");
            foreach (LoopDef loop in _matrix.Data.Loops)
                if (!IsKnownLoopPredicate(loop.When)) problems.Add($"回路 {loop.Id} 的 when 无法识别：{loop.When}");
            foreach (CellDef cell in _matrix.Data.Cells)
            {
                if (!string.IsNullOrEmpty(cell.RunWhen) && cell.RunWhen != LastRivetInStation) problems.Add($"{cell.Id} 的 runWhen 无法识别：{cell.RunWhen}");
                if (cell.Kind == CellKind.Conditional && !ConditionBus.IsKnownTrigger(cell.Trigger)) problems.Add($"{cell.Id} 的 trigger 无法识别：{cell.Trigger}");
            }
            return problems;
        }

        /// <summary>从 T1 起运行到全部区域完成；取消时抛出 OperationCanceledException。</summary>
        public async Awaitable<MissionResult> RunAsync(CancellationToken ct)
        {
            double start = _timeS();
            TaskMatrixData data = _matrix.Data;
            Cursor.Reset(_plan.RegionCount > 0 && _plan.StationCount(0) > 0 ? _plan.RivetCount(0, 0) : 0);
            _phaseRuns = 0;
            _unrecovered = 0;
            if (_plan.RegionCount == 0 || Cursor.RivetCount == 0) return Finish(false, "没有可检测的区域 / 工位 / 铆钉", start);
            _matrix.ResetRows(1);
            LoopDef rivetLoop = data.Loops.Count > 0 ? FindLoop(HasNextRivet) : null;
            int rivetLoopFirst = rivetLoop != null ? data.RowOfPhase(rivetLoop.To) : 0;
            int rivetLoopLast = rivetLoop != null ? data.RowOfPhase(rivetLoop.From) : 0;

            int row = 1;
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                PhaseDef phase = data.Phases[row - 1];
                bool ok = await RunPhaseAsync(row, ct);
                ct.ThrowIfCancellationRequested();
                if (!ok)
                {
                    if (row >= rivetLoopFirst && row < rivetLoopLast)
                    {
                        string reason = $"{phase.Code} 未恢复";
                        _unrecovered++;
                        SimLog.Warning(LogCategory.Mission, $"[{phase.Code}] {Cursor}：{reason}，本颗记为无效，转 {data.Phases[rivetLoopLast - 1].Code}");
                        RivetUnrecovered?.Invoke(Cursor, phase.Code, reason);
                        row = rivetLoopLast;
                        continue;
                    }
                    return Finish(false, $"{phase.Code} {phase.Name} 失败（{Cursor}）", start);
                }

                LoopDef taken = null;
                foreach (LoopDef loop in data.Loops)
                {
                    if (loop.From != phase.Id || !EvaluateLoop(loop.When)) continue;
                    taken = loop;
                    break;
                }
                if (taken != null)
                {
                    AdvanceCursor(taken.When);
                    int to = data.RowOfPhase(taken.To);
                    SimLog.Info(LogCategory.Mission, $"[回路] {taken.Label}：{phase.Code} → {data.Phases[to - 1].Code}（{Cursor}）");
                    _bus?.Publish(new LoopTakenEvent(taken.Id, taken.Label, taken.From, taken.To));
                    _matrix.ResetRows(to);
                    row = to;
                    continue;
                }
                if (row >= data.RowCount) break;
                row++;
            }
            return Finish(true, "", start);
        }

        private MissionResult Finish(bool success, string reason, double start)
        {
            double duration = _timeS() - start;
            if (success) SimLog.Info(LogCategory.Mission, $"[任务] 全部区域完成：阶段执行 {_phaseRuns} 次，未恢复铆钉 {_unrecovered} 颗，用时 {duration:0.0} s（仿真）");
            else SimLog.Error(LogCategory.Mission, $"[任务] 中止：{reason}");
            return new MissionResult(success, reason, duration, _phaseRuns, _unrecovered);
        }

        // 运行一个阶段（含 retryPhase 重试），返回是否全部 active 单元完成或跳过。
        private async Awaitable<bool> RunPhaseAsync(int row, CancellationToken ct)
        {
            PhaseDef phase = _matrix.Data.Phases[row - 1];
            var retriesUsed = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int attempt = 1; ; attempt++)
            {
                _phaseRuns++;
                var run = new PhaseRun(this, row, attempt, retriesUsed, ct);
                _current = run;
                _matrix.EnterPhase(row);
                _bus?.Publish(new PhaseEnteredEvent(phase.Id, phase.Code, Cursor.RegionIndex, Cursor.StationIndex, Cursor.StationGlobalIndex, Cursor.RivetIndex, attempt));
                SimLog.Info(LogCategory.Mission, $"[{phase.Code}] 进入阶段 {phase.Name}（{Cursor}）{(attempt > 1 ? $"，第 {attempt} 次执行" : "")}");

                IReadOnlyList<CellDef> cells = _matrix.CellsInRow(row);
                var toStart = new List<CellDef>();
                foreach (CellDef cell in cells)
                {
                    if (cell.Kind != CellKind.Active) continue;
                    if (!string.IsNullOrEmpty(cell.RunWhen) && !EvaluateRunWhen(cell.RunWhen))
                    {
                        _matrix.SetState(cell, CellState.Skipped, $"runWhen {cell.RunWhen} 不满足");
                        SimLog.Info(LogCategory.Mission, $"{Tag(cell)} {Label(cell)} 跳过（{cell.RunWhen} 不满足）");
                        continue;
                    }
                    toStart.Add(cell);
                    run.Expect(cell.Id);
                }
                // 先登记全部 active 与计数触发的条件单元，再启动，保证依赖等待能看到同阶段内所有将要执行的单元。
                foreach (CellDef cell in cells)
                    if (cell.Kind == CellKind.Conditional && ConditionBus.IsCounterDue(cell.Trigger, Cursor))
                        TryTrigger(run, cell, $"工位内第 {Cursor.RivetIndex + 1} 颗完成");
                foreach (CellDef cell in toStart) run.Start(cell);
                run.ReleaseGuard();

                await run.AllDone;
                _current = null;
                ct.ThrowIfCancellationRequested();
                if (run.RetryRequestedBy != null)
                {
                    CellDef by = run.RetryRequestedBy;
                    SimLog.Info(LogCategory.Mission, $"[{phase.Code}] {by.Id} {Label(by)} 完成 → 重新执行 {phase.Code}（重试 {retriesUsed[by.Id]}/{by.MaxRetries}）");
                    continue;
                }
                return !run.AnyFailed;
            }
        }

        private bool HandleTrigger(string trigger, string reason)
        {
            PhaseRun run = _current;
            if (run == null)
            {
                SimLog.Info(LogCategory.Mission, $"[条件] {trigger}（{reason}）：当前不在阶段内，忽略");
                return false;
            }
            foreach (CellDef cell in _matrix.CellsInRow(run.Row))
                if (cell.Kind == CellKind.Conditional && cell.Trigger == trigger && _matrix.GetState(cell.Id) == CellState.Armed)
                    return TryTrigger(run, cell, reason);
            SimLog.Info(LogCategory.Mission, $"[条件] {trigger}（{reason}）：当前阶段没有布防的对应单元，忽略");
            return false;
        }

        private bool TryTrigger(PhaseRun run, CellDef cell, string reason)
        {
            if (cell.OnComplete == CellCompletionAction.RetryPhase)
            {
                run.RetriesUsed.TryGetValue(cell.Id, out int used);
                if (used >= cell.MaxRetries)
                {
                    SimLog.Warning(LogCategory.Mission, $"{Tag(cell)} {Label(cell)}：{reason}，但已重试 {used}/{cell.MaxRetries} 次，不再激活");
                    return false;
                }
                run.RetriesUsed[cell.Id] = used + 1;
            }
            _matrix.SetState(cell, CellState.Triggered, reason);
            // 计数器触发属于正常流程，记为 Info；故障与事件触发记为 Warning。
            SimLog.Write(LogCategory.Mission, ConditionBus.TryParseEveryNRivets(cell.Trigger, out _) ? SimLogLevel.Info : SimLogLevel.Warning,
                $"{Tag(cell)} {Label(cell)} 触发（{reason}）");
            run.Expect(cell.Id);
            run.Start(cell);
            return true;
        }

        private async Awaitable RunCellAsync(PhaseRun run, CellDef cell)
        {
            CellOutcome outcome = CellOutcome.Failed;
            try
            {
                foreach (string pred in PhaseDependencyTable.PredecessorsOf(cell.Id))
                {
                    CellOutcome p = await run.WaitFinishedAsync(pred);
                    if (p == CellOutcome.Failed || p == CellOutcome.Blocked)
                    {
                        outcome = CellOutcome.Blocked;
                        _matrix.SetState(cell, CellState.Skipped, $"前置单元 {pred} 未完成");
                        SimLog.Info(LogCategory.Mission, $"{Tag(cell)} {Label(cell)} 跳过（前置单元 {pred} 未完成）");
                        return;
                    }
                }
                run.Token.ThrowIfCancellationRequested();
                _matrix.SetState(cell, CellState.Running);
                SimLog.Info(LogCategory.Mission, $"{Tag(cell)} {Label(cell)} 开始");
                ExecutorResult result = _executors.TryGet(cell.Executor, out ITaskExecutor executor)
                    ? await executor.ExecuteAsync(cell, Cursor, run.Token)
                    : ExecutorResult.Fail($"执行器 {cell.Executor} 未注册");
                string suffix = string.IsNullOrEmpty(result.Message) ? "" : $"（{result.Message}）";
                switch (result.Outcome)
                {
                    case ExecutorOutcome.Done:
                        outcome = CellOutcome.Done;
                        _matrix.SetState(cell, CellState.Done, result.Message);
                        SimLog.Info(LogCategory.Mission, $"{Tag(cell)} {Label(cell)} 完成{suffix}");
                        if (cell.Kind == CellKind.Conditional && cell.OnComplete == CellCompletionAction.RetryPhase) run.RetryRequestedBy ??= cell;
                        break;
                    case ExecutorOutcome.Skipped:
                        outcome = CellOutcome.Skipped;
                        _matrix.SetState(cell, CellState.Skipped, result.Message);
                        SimLog.Info(LogCategory.Mission, $"{Tag(cell)} {Label(cell)} 跳过{suffix}");
                        break;
                    default:
                        outcome = CellOutcome.Failed;
                        _matrix.SetState(cell, CellState.Failed, result.Message);
                        SimLog.Warning(LogCategory.Mission, $"{Tag(cell)} {Label(cell)} 失败{suffix}");
                        break;
                }
            }
            catch (OperationCanceledException)
            {
                outcome = CellOutcome.Failed;
                _matrix.SetState(cell, CellState.Failed, "已中止");
            }
            catch (Exception e)
            {
                outcome = CellOutcome.Failed;
                _matrix.SetState(cell, CellState.Failed, e.Message);
                SimLog.Error(LogCategory.Mission, $"{Tag(cell)} {Label(cell)} 异常：{e}");
            }
            finally
            {
                run.Finish(cell, outcome);
            }
        }

        private bool EvaluateRunWhen(string key) => key == LastRivetInStation && Cursor.IsLastRivetInStation;

        private static bool IsKnownLoopPredicate(string when) => when == HasNextRivet || when == HasNextStation || when == HasNextRegion;

        private bool EvaluateLoop(string when) => when switch
        {
            HasNextRivet => Cursor.RivetIndex + 1 < Cursor.RivetCount,
            HasNextStation => Cursor.StationIndex + 1 < _plan.StationCount(Cursor.RegionIndex),
            HasNextRegion => NextRegionWithStations(Cursor.RegionIndex) >= 0,
            _ => false,
        };

        private void AdvanceCursor(string when)
        {
            switch (when)
            {
                case HasNextRivet:
                    Cursor.RivetIndex++;
                    break;
                case HasNextStation:
                    Cursor.StationIndex++;
                    Cursor.StationGlobalIndex++;
                    Cursor.RivetIndex = 0;
                    break;
                case HasNextRegion:
                    Cursor.RegionIndex = NextRegionWithStations(Cursor.RegionIndex);
                    Cursor.StationIndex = 0;
                    Cursor.StationGlobalIndex++;
                    Cursor.RivetIndex = 0;
                    break;
            }
            Cursor.RivetCount = _plan.RivetCount(Cursor.RegionIndex, Cursor.StationIndex);
        }

        private int NextRegionWithStations(int region)
        {
            for (int r = region + 1; r < _plan.RegionCount; r++)
                if (_plan.StationCount(r) > 0) return r;
            return -1;
        }

        private LoopDef FindLoop(string when)
        {
            foreach (LoopDef loop in _matrix.Data.Loops)
                if (loop.When == when) return loop;
            return null;
        }

        private string Tag(CellDef cell) => $"[{_matrix.PhaseCodeOf(cell)}][{_matrix.ActorCodeOf(cell)}] {cell.Id}";

        private static string Label(CellDef cell) => (cell.Label ?? "").Replace("\n", " ");

        private enum CellOutcome
        {
            Done,
            Skipped,
            Failed,
            Blocked
        }

        // 一次阶段执行：记录预期运行的单元、各单元结果与等待者；全部结束后完成 AllDone。
        private sealed class PhaseRun
        {
            private readonly MissionOrchestrator _owner;
            private readonly HashSet<string> _expected = new HashSet<string>(StringComparer.Ordinal);
            private readonly Dictionary<string, CellOutcome> _finished = new Dictionary<string, CellOutcome>(StringComparer.Ordinal);
            private readonly Dictionary<string, List<AwaitableCompletionSource<CellOutcome>>> _waiters =
                new Dictionary<string, List<AwaitableCompletionSource<CellOutcome>>>(StringComparer.Ordinal);
            private readonly AwaitableCompletionSource _allDone = new AwaitableCompletionSource();
            private int _pending = 1; // 守卫：全部单元启动后才允许完成。
            private bool _completed;

            public readonly int Row;
            public readonly int Attempt;
            public readonly Dictionary<string, int> RetriesUsed;
            public readonly CancellationToken Token;
            public CellDef RetryRequestedBy;
            public bool AnyFailed;

            public PhaseRun(MissionOrchestrator owner, int row, int attempt, Dictionary<string, int> retriesUsed, CancellationToken token)
            {
                _owner = owner;
                Row = row;
                Attempt = attempt;
                RetriesUsed = retriesUsed;
                Token = token;
            }

            public Awaitable AllDone => _allDone.Awaitable;

            public void Expect(string cellId) => _expected.Add(cellId);

            public void Start(CellDef cell)
            {
                _pending++;
                _ = _owner.RunCellAsync(this, cell);
            }

            public void ReleaseGuard() => Decrement();

            public Awaitable<CellOutcome> WaitFinishedAsync(string cellId)
            {
                var source = new AwaitableCompletionSource<CellOutcome>();
                if (_finished.TryGetValue(cellId, out CellOutcome outcome)) source.SetResult(outcome);
                else if (!_expected.Contains(cellId)) source.SetResult(CellOutcome.Skipped); // 不在本次执行（未触发的条件单元、runWhen 跳过）。
                else
                {
                    if (!_waiters.TryGetValue(cellId, out List<AwaitableCompletionSource<CellOutcome>> list))
                        _waiters[cellId] = list = new List<AwaitableCompletionSource<CellOutcome>>();
                    list.Add(source);
                }
                return source.Awaitable;
            }

            public void Finish(CellDef cell, CellOutcome outcome)
            {
                _finished[cell.Id] = outcome;
                if (cell.Kind == CellKind.Active && (outcome == CellOutcome.Failed || outcome == CellOutcome.Blocked)) AnyFailed = true;
                if (_waiters.TryGetValue(cell.Id, out List<AwaitableCompletionSource<CellOutcome>> list))
                {
                    _waiters.Remove(cell.Id);
                    foreach (AwaitableCompletionSource<CellOutcome> w in list) w.SetResult(outcome);
                }
                Decrement();
            }

            private void Decrement()
            {
                if (--_pending > 0 || _completed) return;
                _completed = true;
                _allDone.SetResult();
            }
        }
    }
}
