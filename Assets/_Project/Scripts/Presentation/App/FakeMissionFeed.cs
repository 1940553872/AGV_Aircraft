using System;
using System.Collections.Generic;
using RivetSim.Core.Clock;
using RivetSim.Core.Config;
using RivetSim.Core.Events;
using RivetSim.Core.Logging;
using RivetSim.Core.Mission;
using RivetSim.Core.TaskMatrix;
using RivetSim.Simulation.Inspection;
using RivetSim.Simulation.Mission;
using RivetSim.Simulation.World;
using UnityEngine;

namespace RivetSim.Presentation.App
{
    /// <summary>
    /// Sandbox_UI 假数据（M6）：按脚本发布 M5 的事件（CellStateChanged / ConditionChecked / PhaseEntered / LoopTaken / RivetInspected），
    /// 每次 <see cref="Step"/> 推进一条，逐步驱动全部 8 种单元状态、两种回路箭头与 C2～C8 / C9～C12 的 ✓ / ✗，
    /// 并在 t(1,8) 打开操作员初值通道（演示小地图点选）。单元、阶段、条件与回路均取自任务矩阵 JSON。
    /// </summary>
    public sealed class FakeMissionFeed
    {
        // 假数据规模（示意）：2 个区域 × 2 个工位，每工位 12 颗铆钉。
        /// <summary>区域数（假数据）。</summary>
        public const int RegionCount = 2;
        /// <summary>每区域工位数（假数据）。</summary>
        public const int StationsPerRegion = 2;
        /// <summary>每工位铆钉数（假数据）。</summary>
        public const int RivetsPerStation = 12;
        private const float StepSimS = 6f;
        private const string RecognitionCell = "t(1,8)";
        private const string FailDemoCell = "t(3,2)";
        private const string TriggeredDemoCell = "t(5,7)";
        private const string FailedConditionId = "C5";

        private readonly TaskMatrixData _data;
        private readonly EventBus _bus;
        private readonly SimClock _clock;
        private readonly InspectionConfig _inspection;
        private readonly FakeMiniMapSource _map;
        private readonly System.Random _random = new System.Random(42);
        private readonly Dictionary<string, CellState> _states = new Dictionary<string, CellState>();
        private readonly List<(string title, Action action)> _steps = new List<(string, Action)>();
        private int _region, _station, _rivet;
        private bool _lastRivet;

        /// <summary>t(1,8) 操作员初值通道（演示小地图点选）。</summary>
        public OperatorPoseChannel OperatorPose { get; } = new OperatorPoseChannel { AutoProvide = false };

        /// <summary>已发布过的单元状态（验证用）。</summary>
        public HashSet<CellState> PublishedStates { get; } = new HashSet<CellState>();

        /// <summary>已发布过的回路 id（验证用）。</summary>
        public HashSet<string> PublishedLoops { get; } = new HashSet<string>();

        /// <summary>已执行的步数。</summary>
        public int StepIndex { get; private set; }

        /// <summary>总步数。</summary>
        public int StepCount => _steps.Count;

        /// <summary>是否已走完。</summary>
        public bool IsFinished => StepIndex >= _steps.Count;

        /// <summary>当前铆钉序号（工位内，从 0 起）。</summary>
        public int RivetIndex => _rivet;

        /// <summary>刚执行完的步骤标题。</summary>
        public string LastTitle { get; private set; } = "";

        /// <summary>按任务矩阵生成脚本。</summary>
        public FakeMissionFeed(TaskMatrixData data, EventBus bus, SimClock clock, InspectionConfig inspection, FakeMiniMapSource map)
        {
            _data = data;
            _bus = bus;
            _clock = clock;
            _inspection = inspection;
            _map = map;
            foreach (CellDef c in data.Cells) _states[c.Id] = CellState.Idle;
            BuildScript();
        }

        /// <summary>单元当前状态（初始状态回调）。</summary>
        public CellState GetState(string cellId) => _states.TryGetValue(cellId, out CellState s) ? s : CellState.Idle;

        /// <summary>推进一步；已走完返回 false。</summary>
        public bool Step()
        {
            if (IsFinished) return false;
            (string title, Action action) = _steps[StepIndex];
            StepIndex++;
            LastTitle = title;
            _clock.AdvanceSim(StepSimS);
            SimLog.Info(LogCategory.Mission, $"[假数据 {StepIndex}/{_steps.Count}] {title}");
            action();
            return true;
        }

        /// <summary>推进到标题包含 marker 的步骤为止（含该步）；找不到则走完。</summary>
        public void StepUntil(string marker)
        {
            while (Step())
                if (LastTitle.Contains(marker)) return;
        }

        // —— 脚本 ——

        private void BuildScript()
        {
            Add("初始化：单元按 JSON 类型显示", () => { foreach (CellDef c in _data.Cells) Set(c, c.Kind == CellKind.Standby ? CellState.Standby : CellState.Idle); });

            // T1：整机识别失败 → t(1,8) 条件触发 → 操作员小地图点选初值。
            Add("T1 全局导航：进入阶段", () =>
            {
                _map.SetPath(new Vector2(-16f, -8f), new Vector2(-16f, 4f), new Vector2(-6f, 4f), new Vector2(-2f, 0f));
                EnterPhase(1, 1);
            });
            Add("T1 执行单元开始", () => RunRow(1, CellState.Running));
            Add("T1 整机识别失败（假数据）：t(1,8) 条件触发", () => Set(RecognitionCell, CellState.Triggered, "整机识别失败"));
            Add("t(1,8) 等待操作员在小地图点选初值", () =>
            {
                Set(RecognitionCell, CellState.Running);
                OperatorPose.Open(_clock.TimeS, 0f);
            });
            Add("T1 完成", () =>
            {
                OperatorPose.Close();
                _map.AgvPose = new PlanarPose(new Vector2(-6f, 4f), 120f);
                RunRow(1, CellState.Done);
                Set(RecognitionCell, CellState.Done, "初值已给出");
            });

            Add("T2 精确停靠：进入阶段", () => EnterPhase(2, 1));
            Add("T2 执行单元开始", () => RunRow(2, CellState.Running));
            Add("T2 完成", () =>
            {
                _map.AgvPose = new PlanarPose(new Vector2(-2f, 0f), 90f);
                RunRow(2, CellState.Done);
            });

            // T3：演示 Failed → 阶段重试。
            Add("T3 注册：进入阶段", () => EnterPhase(3, 1));
            Add("T3 执行单元开始", () => RunRow(3, CellState.Running));
            Add("T3 基准注册超差（假数据）：单元失败", () => Set(FailDemoCell, CellState.Failed, "基准复测超差"));
            Add("T3 重试：进入阶段（第 2 次）", () => EnterPhase(3, 2));
            Add("T3 重试完成", () =>
            {
                RunRow(3, CellState.Running);
                RunRow(3, CellState.Done);
            });

            AddRivetCycle(first: true);
            AddRivetCycle(first: false);
        }

        private void AddRivetCycle(bool first)
        {
            Add(first ? "T4 对中：进入阶段" : "T4 对中：进入阶段（假数据：本工位最后一颗）", () =>
            {
                _lastRivet = !first;
                EnterPhase(4, 1);
            });
            Add("T4 执行单元开始", () => RunRow(4, CellState.Running));
            Add("T4 条件判定（t(4,7)）", () => CheckAll(4, null));
            Add("T4 完成", () => RunRow(4, CellState.Done));

            Add("T5 贴合采集：进入阶段", () => EnterPhase(5, 1));
            Add("T5 执行单元开始", () => RunRow(5, CellState.Running));
            Add(first ? "T5 条件判定 C2～C8（含 1 项 ✗）与采集无效触发" : "T5 条件判定 C2～C8（全部 ✓）", () =>
            {
                CheckAll(5, first ? FailedConditionId : null);
                Inspect(first ? DefectType.EdgeCrack : DefectType.None, first ? 0.55f : 1f);
                if (first) Set(TriggeredDemoCell, CellState.Triggered, "采集无效：耦合不足");
            });
            if (first) Add("t(5,7) 重贴后重采", () => Set(TriggeredDemoCell, CellState.Running));
            Add("T5 完成（未触发的条件单元跳过）", () =>
            {
                RunRow(5, CellState.Done);
                if (first) Set(TriggeredDemoCell, CellState.Done);
                SkipArmed(5);
            });

            Add("T6 释放退出：进入阶段", () => EnterPhase(6, 1));
            Add("T6 执行单元开始", () => RunRow(6, CellState.Running));
            Add("T6 条件判定 C9～C12", () => CheckAll(6, null));
            if (first)
            {
                Add("T6 完成 → 回路“下一颗”", () =>
                {
                    RunRow(6, CellState.Done);
                    SkipArmed(6);
                    TakeLoop("nextRivet");
                    ResetRows(4);
                    _rivet++;
                });
            }
            else
            {
                Add("T6 完成（工位最后一颗）→ 回路“换站”", () =>
                {
                    RunRow(6, CellState.Done);
                    SkipArmed(6);
                    TakeLoop("nextStationSameRegion");
                    ResetRows(2);
                    _station++;
                    _rivet = 0;
                });
            }
        }

        private void Add(string title, Action action) => _steps.Add((title, action));

        // —— 发布 ——

        private void EnterPhase(int row, int attempt)
        {
            PhaseDef phase = _data.Phases[row - 1];
            _bus.Publish(new PhaseEnteredEvent(phase.Id, phase.Code, _region, _station, _region * StationsPerRegion + _station, _rivet, attempt));
            foreach (CellDef c in _data.Cells)
                if (c.Row == row)
                    Set(c, c.Kind == CellKind.Conditional ? CellState.Armed : c.Kind == CellKind.Standby ? CellState.Standby : CellState.Idle);
        }

        private void RunRow(int row, CellState state)
        {
            foreach (CellDef c in _data.Cells)
            {
                if (c.Row != row || c.Kind != CellKind.Active) continue;
                CellState current = GetState(c.Id);
                // runWhen（如 t(6,1) 仅工位最后一颗）不满足时跳过。
                if (!string.IsNullOrEmpty(c.RunWhen) && !_lastRivet)
                {
                    if (state == CellState.Running) Set(c, CellState.Skipped, $"runWhen {c.RunWhen} 不满足");
                    continue;
                }
                if (state == CellState.Done && current != CellState.Running) continue;
                Set(c, state);
            }
        }

        private void SkipArmed(int row)
        {
            foreach (CellDef c in _data.Cells)
                if (c.Row == row && GetState(c.Id) == CellState.Armed) Set(c, CellState.Skipped, "条件未触发");
        }

        private void ResetRows(int fromRow)
        {
            foreach (CellDef c in _data.Cells)
                if (c.Row >= fromRow) Set(c, c.Kind == CellKind.Standby ? CellState.Standby : CellState.Idle);
        }

        private void CheckAll(int row, string failId)
        {
            foreach (CellDef c in _data.Cells)
            {
                if (c.Row != row || c.ConditionRefs.Count == 0) continue;
                foreach (string id in c.ConditionRefs)
                {
                    bool pass = id != failId;
                    float value = pass ? 1f : 0f;
                    _bus.Publish(new ConditionCheckedEvent(c.Id, id, pass, value, pass ? "满足（假数据）" : "未满足（假数据）"));
                }
            }
        }

        private void TakeLoop(string loopId)
        {
            foreach (LoopDef loop in _data.Loops)
            {
                if (loop.Id != loopId) continue;
                _bus.Publish(new LoopTakenEvent(loop.Id, loop.Label, loop.From, loop.To));
                PublishedLoops.Add(loop.Id);
            }
        }

        private void Inspect(DefectType defect, float coupling)
        {
            if (_inspection == null) return;
            AScanWaveform wave = new AScanSynthesizer(_inspection, _random).Synthesize(defect, coupling);
            GateResult gates = new GateJudge(_inspection).Judge(wave);
            InspectionVerdict verdict = defect == DefectType.None ? InspectionVerdict.Pass : InspectionVerdict.Defect;
            string id = $"FAKE-R{_rivet + 1:00}";
            var record = new InspectionRecord(id, "FAKE-S1", defect, verdict, defect == DefectType.None ? "闸门 B 无回波（假数据）" : "闸门 B 超阈值（假数据）",
                _inspection.targetContactForceN, coupling, 0f, gates);
            _bus.Publish(new RivetInspectedEvent(null, record, wave));
        }

        private void Set(string cellId, CellState state, string note = "")
        {
            CellDef c = _data.FindCell(cellId);
            if (c != null) Set(c, state, note);
        }

        private void Set(CellDef c, CellState state, string note = "")
        {
            CellState previous = GetState(c.Id);
            if (previous == state) return;
            _states[c.Id] = state;
            PublishedStates.Add(state);
            string actor = c.Col >= 1 && c.Col <= _data.ColCount ? _data.Actors[c.Col - 1].Id : "";
            string phase = c.Row >= 1 && c.Row <= _data.RowCount ? _data.Phases[c.Row - 1].Id : "";
            _bus.Publish(new CellStateChangedEvent(c.Id, actor, phase, state, previous, _region, _station, _rivet, _clock.TimeS, note));
        }
    }
}
