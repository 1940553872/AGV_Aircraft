using System;
using System.Collections.Generic;
using RivetSim.Core.Config;
using RivetSim.Presentation.App;
using RivetSim.Simulation.Inspection;
using RivetSim.Simulation.Robot;
using UnityEngine;
using UnityEngine.UIElements;

namespace RivetSim.Presentation.UI
{
    /// <summary>
    /// M4 检测叠加信息（UI Toolkit，屏幕右侧）：当前阶段与步骤、工位 / 铆钉进度、接触力条（允许范围与超限线）、A 扫波形与闸门、
    /// 最近一颗的判读结论、本工位统计与逆解统计；标注“教学简化模型”。M6 的演示界面就绪后由 HUD / 演示屏取代。
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class InspectionOverlayView : MonoBehaviour
    {
        // 接触力条满量程，单位 N（外观常量，取超限阈值的 1.25 倍）。
        private const float ForceBarRangeFactor = 1.25f;
        private const string VerdictClassPrefix = "insp-verdict--";

        private RobotRuntime _runtime;
        private InspectionConfig _inspection;
        private readonly List<IDisposable> _subscriptions = new List<IDisposable>();
        private Label _phase, _progress, _step, _force, _verdict, _stats, _ik;
        private VisualElement _forceFill, _forceBand, _forceLimit;
        private AScanGraphElement _ascan;
        private string _stationId = "";
        private bool _bandPlaced;
        private int _statsCount;
        private float _statsLastCycle;

        /// <summary>绑定运行时并订阅检测事件。</summary>
        public void Initialize(RobotRuntime runtime, InspectionConfig inspection)
        {
            _runtime = runtime;
            _inspection = inspection;
            _subscriptions.Add(runtime.Bus.Subscribe<RivetInspectedEvent>(OnInspected));
        }

        /// <summary>设置阶段文字。</summary>
        public void SetPhase(string text)
        {
            if (Bind()) _phase.text = text;
        }

        /// <summary>设置工位与铆钉进度。</summary>
        public void SetProgress(string stationId, int rivetIndex, int rivetCount)
        {
            _stationId = stationId;
            if (Bind()) _progress.text = $"{stationId} · 铆钉 {rivetIndex}/{rivetCount}";
        }

        // UIDocument 在自身 OnEnable 时才建立元素树，首次使用时再查询元素。
        private bool Bind()
        {
            if (_phase != null) return true;
            VisualElement root = GetComponent<UIDocument>().rootVisualElement;
            if (root == null || root.Q<Label>("insp-phase") == null) return false;
            _phase = root.Q<Label>("insp-phase");
            _progress = root.Q<Label>("insp-progress");
            _step = root.Q<Label>("insp-step");
            _force = root.Q<Label>("insp-force");
            _verdict = root.Q<Label>("insp-verdict");
            _stats = root.Q<Label>("insp-stats");
            _ik = root.Q<Label>("insp-ik");
            _forceFill = root.Q<VisualElement>("insp-force-fill");
            _forceBand = root.Q<VisualElement>("insp-force-band");
            _forceLimit = root.Q<VisualElement>("insp-force-limit");
            VisualElement host = root.Q<VisualElement>("insp-ascan");
            _ascan = new AScanGraphElement();
            _ascan.AddToClassList("insp-ascan-graph");
            host.Add(_ascan);
            return true;
        }

        private void OnInspected(RivetInspectedEvent e)
        {
            if (!Bind()) return;
            InspectionRecord r = e.Record;
            _ascan.SetData(e.Waveform, r.Gates, _inspection.gateAThresholdRatio, _inspection.gateBThresholdRatio, _inspection.backwallDefectRatio);
            foreach (InspectionVerdict v in Enum.GetValues(typeof(InspectionVerdict)))
                _verdict.EnableInClassList(VerdictClassPrefix + v.ToString().ToLowerInvariant(), v == r.Verdict);
            _verdict.text = $"{r.RivetId}：{RivetInspectionService.Label(r.Verdict)}——{r.Reason}";
        }

        private void LateUpdate()
        {
            if (_runtime == null || !Bind()) return;
            _step.text = string.IsNullOrEmpty(_runtime.Inspection.CurrentStep) ? "—" : _runtime.Inspection.CurrentStep;
            // 单钉节拍在 T6 回退结束后才写入记录，统计每帧刷新。
            var records = _runtime.Inspection.Store.Records;
            float lastCycle = records.Count > 0 ? records[records.Count - 1].CycleTimeS : 0f;
            if (records.Count != _statsCount || lastCycle != _statsLastCycle)
            {
                _statsCount = records.Count;
                _statsLastCycle = lastCycle;
                _stats.text = records.Count > 0 ? _runtime.Inspection.Store.Summarize(_stationId) : "";
            }
            ContactModel contact = _runtime.Contact;
            float range = _inspection.contactForceLimitN * ForceBarRangeFactor;
            if (!_bandPlaced)
            {
                _forceBand.style.left = Length.Percent(100f * _inspection.contactForceMinN / range);
                _forceBand.style.width = Length.Percent(100f * (_inspection.contactForceMaxN - _inspection.contactForceMinN) / range);
                _forceLimit.style.left = Length.Percent(100f * _inspection.contactForceLimitN / range);
                _bandPlaced = true;
            }
            _forceFill.style.width = Length.Percent(100f * Mathf.Clamp01(contact.ForceN / range));
            _force.text = $"接触力 {contact.ForceN:0.00} N（目标 {_inspection.targetContactForceN:0} N，允许 {_inspection.contactForceMinN:0}～{_inspection.contactForceMaxN:0} N，" +
                          $"超限 {_inspection.contactForceLimitN:0} N）· 压缩 {contact.CompressionM * 1000f:0.00} mm";
            ArmController arm = _runtime.Arm;
            _ik.text = $"{arm.IkStats}\n最小间隙 {(arm.MinAcceptedClearanceM < float.MaxValue ? $"{arm.MinAcceptedClearanceM * 1000f:0} mm（{arm.MinAcceptedClearanceLink}）" : "—")}";
        }

        private void OnDestroy()
        {
            foreach (IDisposable s in _subscriptions) s.Dispose();
            _subscriptions.Clear();
        }
    }
}
