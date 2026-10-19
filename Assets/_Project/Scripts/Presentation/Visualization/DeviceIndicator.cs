using System;
using System.Collections.Generic;
using RivetSim.Core.Config;
using RivetSim.Core.Events;
using RivetSim.Core.Mission;
using UnityEngine;

namespace RivetSim.Presentation.Visualization
{
    /// <summary>
    /// 设备发光联动（M5，Docs/Architecture.md §5.3）：订阅 <see cref="CellStateChangedEvent"/>，所属主体（a1～a8）有单元 Running 时
    /// 设备外壳发橙光、条件单元被激活时发琥珀光，无执行单元时熄灭。外壳为编辑器生成器创建的纯视觉渲染器（材质 M_DeviceGlow），
    /// 颜色用 MaterialPropertyBlock 切换，不新建材质实例。由 DeviceIndicatorGenerator 挂到 S1～S8 的设备节点。
    /// </summary>
    public sealed class DeviceIndicator : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField, Tooltip("所属主体 id（a1～a8）")] private string actorId;
        [SerializeField, Tooltip("主体代号与设备说明（仅显示）")] private string label;
        [SerializeField, Tooltip("发光外壳渲染器")] private Renderer[] glowRenderers = Array.Empty<Renderer>();
        [SerializeField, Tooltip("状态配色（CFG_UiTheme）")] private UiThemeConfig theme;
        [SerializeField, Tooltip("外壳不透明度"), Range(0f, 1f)] private float glowAlpha = 0.45f;

        private readonly HashSet<string> _running = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _triggered = new HashSet<string>(StringComparer.Ordinal);
        private MaterialPropertyBlock _block;
        private IDisposable _subscription;

        /// <summary>所属主体 id。</summary>
        public string ActorId => actorId;

        /// <summary>当前是否发光。</summary>
        public bool IsLit { get; private set; }

        /// <summary>编辑器生成器写入。</summary>
        public void Configure(string actor, string text, Renderer[] renderers, UiThemeConfig uiTheme)
        {
            actorId = actor;
            label = text;
            glowRenderers = renderers ?? Array.Empty<Renderer>();
            theme = uiTheme;
        }

        /// <summary>订阅事件总线并熄灭。</summary>
        public void Initialize(EventBus bus)
        {
            _subscription?.Dispose();
            _running.Clear();
            _triggered.Clear();
            _subscription = bus.Subscribe<CellStateChangedEvent>(OnCellChanged);
            Apply();
        }

        private void Awake() => Apply();

        private void OnDestroy()
        {
            _subscription?.Dispose();
            _subscription = null;
        }

        private void OnCellChanged(CellStateChangedEvent e)
        {
            if (e.ActorId != actorId) return;
            _running.Remove(e.CellId);
            _triggered.Remove(e.CellId);
            if (e.State == CellState.Running) _running.Add(e.CellId);
            else if (e.State == CellState.Triggered) _triggered.Add(e.CellId);
            Apply();
        }

        private void Apply()
        {
            bool lit = _running.Count > 0 || _triggered.Count > 0;
            IsLit = lit;
            Color color = theme == null ? Color.white : _triggered.Count > 0 ? theme.conditionTriggered : theme.running;
            color.a = glowAlpha;
            _block ??= new MaterialPropertyBlock();
            foreach (Renderer r in glowRenderers)
            {
                if (r == null) continue;
                r.enabled = lit;
                if (!lit) continue;
                r.GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, color);
                r.SetPropertyBlock(_block);
            }
        }
    }
}
