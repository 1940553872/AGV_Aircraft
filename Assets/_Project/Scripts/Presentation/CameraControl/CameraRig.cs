using System;
using RivetSim.Core.Config;
using RivetSim.Core.Events;
using RivetSim.Core.Mission;
using RivetSim.Core.TaskMatrix;
using UnityEngine;

namespace RivetSim.Presentation.CameraControl
{
    /// <summary>
    /// 相机机位（M6）：使用场景中的 CamAnchor_1～4（1 总览、2 跟随 AGV、3 末端特写、4 演示台），平滑过渡。
    /// 跟随 AGV 保持锚点 2 相对起始位姿的偏移；末端特写位于探头 TCP 的 AGV 一侧（机身外侧）斜上方并对准 TCP，
    /// 没有 AGV / 探头时（沙盒）用锚点 3。
    /// 自动机位：阶段切换时先显示演示台（CFG_UiTheme.demoStationHoldS），再切到该阶段的机位（phaseCameraPresets）；
    /// demoStationOnPresetChangeOnly 时只在机位变化的阶段切换（如 T2→T3、换站）显示演示台。
    /// 按 1～4 选机位后关闭自动机位，按 0 恢复。
    /// </summary>
    public sealed class CameraRig : MonoBehaviour
    {
        /// <summary>机位数。</summary>
        public const int PresetCount = 4;

        [SerializeField, Tooltip("CamAnchor_1～4，按编号顺序")] private Transform[] anchors = new Transform[PresetCount];

        private UiThemeConfig _theme;
        private TaskMatrixData _data;
        private Transform _agv;
        private Transform _tool;
        private Vector3 _followLocalPos;
        private IDisposable _subscription;
        private int _phasePreset = 1;
        private float _holdUntil;
        private bool _initialized;

        /// <summary>当前机位（1～4）。</summary>
        public int Preset { get; private set; } = 1;

        /// <summary>是否自动机位。</summary>
        public bool IsAuto { get; private set; } = true;

        /// <summary>机位或自动状态变化（机位、是否自动）。</summary>
        public event Action<int, bool> Changed;

        /// <summary>编辑器生成器写入锚点。</summary>
        public void Configure(Transform[] presetAnchors) => anchors = presetAnchors;

        /// <summary>
        /// 绑定跟随目标。followReference 为锚点 2 生成时参照的 AGV 位姿（CFG_SceneLayout.startPose）；agv / tool 可为空（沙盒）。
        /// </summary>
        public void Initialize(UiThemeConfig theme, EventBus bus, TaskMatrixData data, Transform agv, Transform tool, PlanarPose followReference)
        {
            _theme = theme;
            _data = data;
            _agv = agv;
            _tool = tool;
            if (anchors.Length > 1 && anchors[1] != null)
                _followLocalPos = Quaternion.Inverse(followReference.WorldRotation) * (anchors[1].position - followReference.WorldPosition);
            _subscription?.Dispose();
            if (bus != null) _subscription = bus.Subscribe<PhaseEnteredEvent>(OnPhaseEntered);
            _initialized = true;
            SnapTo(Preset);
        }

        /// <summary>选择机位（1～4），关闭自动机位。</summary>
        public void Select(int preset)
        {
            if (preset < 1 || preset > PresetCount) return;
            IsAuto = false;
            Preset = preset;
            Changed?.Invoke(Preset, IsAuto);
        }

        /// <summary>开启 / 关闭自动机位。</summary>
        public void SetAuto(bool auto)
        {
            IsAuto = auto;
            if (auto) Preset = _phasePreset;
            Changed?.Invoke(Preset, IsAuto);
        }

        private void OnPhaseEntered(PhaseEnteredEvent e)
        {
            int row = _data != null ? _data.RowOfPhase(e.PhaseId) : 0;
            int[] map = _theme.phaseCameraPresets;
            int previous = _phasePreset;
            _phasePreset = row >= 1 && map != null && row <= map.Length ? Mathf.Clamp(map[row - 1], 1, PresetCount) : 1;
            if (!IsAuto) return;
            if (_theme.demoStationOnPresetChangeOnly && _phasePreset == previous)
            {
                if (_holdUntil <= 0f && Preset != _phasePreset)
                {
                    Preset = _phasePreset;
                    Changed?.Invoke(Preset, IsAuto);
                }
                return;
            }
            _holdUntil = Time.unscaledTime + _theme.demoStationHoldS;
            Preset = PresetCount;
            Changed?.Invoke(Preset, IsAuto);
        }

        private void LateUpdate()
        {
            if (!_initialized) return;
            if (IsAuto && Preset == PresetCount && _holdUntil > 0f && Time.unscaledTime >= _holdUntil)
            {
                _holdUntil = 0f;
                Preset = _phasePreset;
                Changed?.Invoke(Preset, IsAuto);
            }
            if (!TryGetTarget(Preset, out Vector3 pos, out Quaternion rot)) return;
            float blend = Mathf.Max(0.01f, _theme.cameraBlendS);
            // 指数平滑：约 blend 秒内收敛到目标机位（与仿真倍速无关）。
            float k = 1f - Mathf.Exp(-4f * Time.unscaledDeltaTime / blend);
            transform.SetPositionAndRotation(Vector3.Lerp(transform.position, pos, k), Quaternion.Slerp(transform.rotation, rot, k));
        }

        private void SnapTo(int preset)
        {
            if (TryGetTarget(preset, out Vector3 pos, out Quaternion rot)) transform.SetPositionAndRotation(pos, rot);
        }

        private bool TryGetTarget(int preset, out Vector3 pos, out Quaternion rot)
        {
            pos = default;
            rot = default;
            Transform anchor = preset >= 1 && preset <= anchors.Length ? anchors[preset - 1] : null;
            if (anchor == null) return false;
            pos = anchor.position;
            rot = anchor.rotation;
            if (preset == 2 && _agv != null)
            {
                pos = _agv.position + _agv.rotation * _followLocalPos;
                Vector3 look = _agv.position + Vector3.up * _theme.followLookHeightM;
                rot = Quaternion.LookRotation(look - pos, Vector3.up);
            }
            else if (preset == 3 && _tool != null && _agv != null)
            {
                // 从探头指向 AGV 的水平方向（相机始终在机身外侧），偏转后拉开并抬高。
                Vector3 toAgv = Vector3.ProjectOnPlane(_agv.position - _tool.position, Vector3.up);
                if (toAgv.sqrMagnitude < 1e-4f) toAgv = -anchor.forward;
                Vector3 dir = Quaternion.Euler(0f, _theme.closeupYawOffsetDeg, 0f) * toAgv.normalized;
                pos = _tool.position + dir * _theme.closeupDistanceM + Vector3.up * _theme.closeupHeightM;
                rot = Quaternion.LookRotation(_tool.position - pos, Vector3.up);
            }
            return true;
        }

        private void OnDestroy() => _subscription?.Dispose();
    }
}
