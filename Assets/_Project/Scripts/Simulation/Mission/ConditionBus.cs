using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using RivetSim.Core.Events;
using RivetSim.Core.Logging;
using RivetSim.Simulation.Robot;

namespace RivetSim.Simulation.Mission
{
    /// <summary>
    /// 条件触发源（Docs/Architecture.md §3）：把仿真事件、计数器与故障注入转换为 JSON 中 conditional 单元的 trigger 键名，
    /// 交给 <see cref="MissionOrchestrator"/> 激活对应的条件单元。
    /// 故障注入（<see cref="Inject"/>）只布设一次性的物理故障，由执行器在下一次相应动作时取用，真实触发仍来自仿真结果：
    /// 识别失败由 T1 执行器上报；力超限来自 <see cref="ContactForceExceededEvent"/>；采集无效由 S5 有效性判定上报。
    /// </summary>
    public sealed class ConditionBus : IDisposable
    {
        /// <summary>整机识别失败（t(1,8)）。</summary>
        public const string RecognitionFailedTrigger = "fault.aircraftRecognitionFailed";

        /// <summary>接触力超限（t(5,6)）。</summary>
        public const string ContactForceExceededTrigger = "fault.contactForceExceeded";

        /// <summary>采集无效（t(5,7)）。</summary>
        public const string AcquisitionInvalidTrigger = "event.acquisitionInvalid";

        private static readonly Regex EveryNRivets = new Regex(@"^counter\.every(\d+)Rivets$", RegexOptions.CultureInvariant);

        private readonly HashSet<FaultKind> _pending = new HashSet<FaultKind>();
        private readonly IDisposable _forceSubscription;

        /// <summary>trigger 处理（编排器设置）：参数为 trigger 键名与原因，返回是否激活了条件单元。</summary>
        public Func<string, string, bool> TriggerHandler { get; set; }

        /// <summary>故障注入后（参数为故障类别）。</summary>
        public event Action<FaultKind> FaultInjected;

        /// <summary>故障类条件单元被激活（系统检测到异常并开始自动处理；参数为故障类别与原因）。</summary>
        public event Action<FaultKind, string> AnomalyRaised;

        /// <summary>创建并订阅力超限事件。</summary>
        public ConditionBus(EventBus bus)
        {
            if (bus != null)
                _forceSubscription = bus.Subscribe<ContactForceExceededEvent>(e =>
                    Raise(ContactForceExceededTrigger, $"接触力 {e.ForceN:0.0} N 超过 {e.LimitN:0} N（{e.Label}）"));
        }

        /// <summary>上报 trigger；返回是否激活了当前阶段布防的条件单元。</summary>
        public bool Raise(string trigger, string reason)
        {
            Func<string, string, bool> handler = TriggerHandler;
            bool activated = handler != null && handler(trigger, reason);
            if (activated && TryGetFault(trigger, out FaultKind kind))
            {
                SimLog.Warning(LogCategory.Mission, $"系统检测到异常：{Describe(kind)}（{reason}），正在自动处理");
                AnomalyRaised?.Invoke(kind, reason);
            }
            return activated;
        }

        /// <summary>布设一次性故障（内部接口：M8 起由随机故障调度器与 Verify 菜单调用，界面不再提供手动注入）。</summary>
        public void Inject(FaultKind kind)
        {
            _pending.Add(kind);
            SimLog.Warning(LogCategory.Mission, $"[故障注入] {Describe(kind)}：已布设，将在下一次{Where(kind)}生效");
            FaultInjected?.Invoke(kind);
        }

        /// <summary>是否有待生效的故障。</summary>
        public bool IsPending(FaultKind kind) => _pending.Contains(kind);

        /// <summary>取用待生效的故障（取用后清除）。</summary>
        public bool Consume(FaultKind kind) => _pending.Remove(kind);

        /// <summary>trigger 是否为故障类键名；是时给出故障类别。</summary>
        public static bool TryGetFault(string trigger, out FaultKind kind)
        {
            kind = trigger == RecognitionFailedTrigger ? FaultKind.RecognitionFailed
                : trigger == ContactForceExceededTrigger ? FaultKind.ContactForceExceeded
                : FaultKind.AcquisitionInvalid;
            return trigger == RecognitionFailedTrigger || trigger == ContactForceExceededTrigger || trigger == AcquisitionInvalidTrigger;
        }

        /// <summary>trigger 是否为本类认识的键名（启动核对用）。</summary>
        public static bool IsKnownTrigger(string trigger) =>
            trigger == RecognitionFailedTrigger || trigger == ContactForceExceededTrigger || trigger == AcquisitionInvalidTrigger ||
            TryParseEveryNRivets(trigger, out _);

        /// <summary>解析“counter.everyNRivets”的 N。</summary>
        public static bool TryParseEveryNRivets(string trigger, out int n)
        {
            n = 0;
            Match m = EveryNRivets.Match(trigger ?? "");
            return m.Success && int.TryParse(m.Groups[1].Value, out n) && n > 0;
        }

        /// <summary>计数器 trigger 是否在进入本阶段时成立：工位内单独计数，第 N、2N… 颗铆钉完成时触发。</summary>
        public static bool IsCounterDue(string trigger, MissionCursor cursor) =>
            TryParseEveryNRivets(trigger, out int n) && (cursor.RivetIndex + 1) % n == 0;

        /// <summary>故障中文名称。</summary>
        public static string Describe(FaultKind kind) => kind switch
        {
            FaultKind.RecognitionFailed => "识别失败",
            FaultKind.ContactForceExceeded => "力超限",
            _ => "采集无效",
        };

        /// <summary>取消订阅。</summary>
        public void Dispose() => _forceSubscription?.Dispose();

        private static string Where(FaultKind kind) => kind switch
        {
            FaultKind.RecognitionFailed => " T1 整机识别",
            FaultKind.ContactForceExceeded => " T5 力控接触",
            _ => " T5 接触采集",
        };
    }
}
