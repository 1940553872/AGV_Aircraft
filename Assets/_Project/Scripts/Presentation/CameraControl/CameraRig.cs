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
    /// M8 检测机位防遮挡：从 CFG_UiTheme.rivetViewFromRow（T3）起，且 AGV 在当前铆钉附近时，“2 跟随”“3 特写”以当前铆钉外法向为参照取景——
    /// 相机在机身外侧、与铆钉排同高，绕竖直轴向机身切向偏转（候选角 rivetViewYawCandidatesDeg），看向接触点；
    /// 每 occlusionCheckIntervalS（实际时间）从相机向接触点做 SphereCast（忽略 Visualization 层）并检查车体与机械臂渲染器的包围盒，
    /// 被挡时按候选顺序切换到第一个不被挡的角度，位置经指数平滑过渡。
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

        // M8 检测机位。
        private IRivetFocusSource _focus;
        private Renderer[] _occluders = new Renderer[0];
        private int _occlusionMask;
        private bool _inspectionPhase;
        private int _yawIndex, _elevationIndex;
        private float _nextOcclusionCheck;
        private Vector3 _lastOutward = Vector3.forward;

        /// <summary>当前是否按铆钉法向取景（验证用）。</summary>
        public bool IsRivetView { get; private set; }

        /// <summary>当前取景是否被遮挡（最近一次检查，所有候选都被挡时为 true；验证用）。</summary>
        public bool IsViewBlocked { get; private set; }

        /// <summary>因遮挡切换候选角度的次数（验证用）。</summary>
        public int OcclusionSwitchCount { get; private set; }

        /// <summary>遮挡检查次数（验证用）。</summary>
        public int OcclusionCheckCount { get; private set; }

        /// <summary>检查结果为“当前角度被挡”的次数（验证用）。</summary>
        public int OcclusionBlockedCount { get; private set; }

        /// <summary>当前候选偏航角，单位 °（验证用）。</summary>
        public float CurrentYawDeg => _theme != null && _theme.rivetViewYawCandidatesDeg.Length > 0 ? _theme.rivetViewYawCandidatesDeg[_yawIndex] : 0f;

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

        /// <summary>
        /// 绑定检测机位（M8）：focus 给出当前铆钉；occluders 为参与遮挡检查的车体与机械臂渲染器（探头末端除外）；occlusionMask 为 SphereCast 的层掩码。
        /// </summary>
        public void ConfigureRivetView(IRivetFocusSource focus, Renderer[] occluders, int occlusionMask)
        {
            _focus = focus;
            _occluders = occluders ?? new Renderer[0];
            _occlusionMask = occlusionMask;
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
            _inspectionPhase = row >= _theme.rivetViewFromRow;
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
            if (Time.unscaledTime >= _nextOcclusionCheck)
            {
                _nextOcclusionCheck = Time.unscaledTime + _theme.occlusionCheckIntervalS;
                CheckOcclusion();
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
            IsRivetView = false;
            if ((preset == 2 || preset == 3) && TryGetRivetFrame(out Vector3 point, out Vector3 outward))
            {
                IsRivetView = true;
                pos = RivetCameraPosition(point, outward, preset == 3 ? _theme.rivetCloseupDistanceM : _theme.rivetFollowDistanceM, _yawIndex, _elevationIndex);
                rot = Quaternion.LookRotation(point - pos, Vector3.up);
            }
            else if (preset == 2 && _agv != null)
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

        // 当前铆钉的接触点与水平外侧方向；未处于检测阶段、没有当前铆钉或 AGV 离铆钉较远（如返回充电位）时返回 false。
        private bool TryGetRivetFrame(out Vector3 point, out Vector3 outward)
        {
            outward = _lastOutward;
            point = default;
            if (!_inspectionPhase || _focus == null || _agv == null || !_focus.TryGetFocus(out point, out Vector3 normal)) return false;
            Vector3 toAgv = Vector3.ProjectOnPlane(_agv.position - point, Vector3.up);
            if (toAgv.magnitude > _theme.rivetViewMaxAgvDistanceM) return false;
            Vector3 h = Vector3.ProjectOnPlane(normal, Vector3.up);
            // 机腹铆钉的法向近乎竖直：改用 AGV 所在一侧作为外侧方向。
            if (h.magnitude >= _theme.rivetViewMinHorizontalNormal) outward = h.normalized;
            else if (toAgv.sqrMagnitude > 1e-4f) outward = toAgv.normalized;
            _lastOutward = outward;
            return true;
        }

        private Vector3 RivetCameraPosition(Vector3 point, Vector3 outward, float distanceM, int yawIndex, int elevationIndex)
        {
            float[] yaws = _theme.rivetViewYawCandidatesDeg;
            float[] elevations = _theme.rivetViewElevationCandidatesM;
            float yaw = yaws != null && yaws.Length > 0 ? yaws[Mathf.Clamp(yawIndex, 0, yaws.Length - 1)] : 0f;
            float elevation = elevations != null && elevations.Length > 0 ? elevations[Mathf.Clamp(elevationIndex, 0, elevations.Length - 1)] : _theme.rivetViewElevationM;
            Vector3 dir = Quaternion.AngleAxis(yaw, Vector3.up) * outward;
            return point + dir * distanceM + Vector3.up * elevation;
        }

        // 每个检查周期：当前候选被挡时，按“高度 → 偏航”顺序找第一个不被挡的候选。
        private void CheckOcclusion()
        {
            if ((Preset != 2 && Preset != 3) || !TryGetRivetFrame(out Vector3 point, out Vector3 outward))
            {
                IsViewBlocked = false;
                return;
            }
            float distance = Preset == 3 ? _theme.rivetCloseupDistanceM : _theme.rivetFollowDistanceM;
            OcclusionCheckCount++;
            if (!IsBlocked(RivetCameraPosition(point, outward, distance, _yawIndex, _elevationIndex), point))
            {
                IsViewBlocked = false;
                return;
            }
            OcclusionBlockedCount++;
            int yawCount = Mathf.Max(1, _theme.rivetViewYawCandidatesDeg?.Length ?? 0);
            int elevationCount = Mathf.Max(1, _theme.rivetViewElevationCandidatesM?.Length ?? 0);
            for (int e = 0; e < elevationCount; e++)
                for (int y = 0; y < yawCount; y++)
                {
                    if (y == _yawIndex && e == _elevationIndex) continue;
                    if (IsBlocked(RivetCameraPosition(point, outward, distance, y, e), point)) continue;
                    _yawIndex = y;
                    _elevationIndex = e;
                    OcclusionSwitchCount++;
                    IsViewBlocked = false;
                    return;
                }
            IsViewBlocked = true;
        }

        private bool IsBlocked(Vector3 from, Vector3 target)
        {
            Vector3 d = target - from;
            float length = d.magnitude - _theme.occlusionIgnoreNearTargetM;
            if (length <= 0f) return false;
            Vector3 dir = d / d.magnitude;
            float radius = _theme.occlusionProbeRadiusM;
            if (Physics.CheckSphere(from, radius, _occlusionMask, QueryTriggerInteraction.Ignore)) return true;
            if (Physics.SphereCast(from, radius, dir, out _, length, _occlusionMask, QueryTriggerInteraction.Ignore)) return true;
            Vector3 end = from + dir * length;
            foreach (Renderer r in _occluders)
            {
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                if (!r.bounds.IntersectRay(new Ray(from, dir), out float worldHit) || worldHit > length) continue;
                // 世界包围盒命中后再用本地包围盒（有向）精确判断。
                Matrix4x4 w2l = r.transform.worldToLocalMatrix;
                Vector3 lo = w2l.MultiplyPoint3x4(from);
                Vector3 le = w2l.MultiplyPoint3x4(end);
                Vector3 ld = le - lo;
                float localLength = ld.magnitude;
                if (localLength < 1e-6f) continue;
                if (r.localBounds.IntersectRay(new Ray(lo, ld / localLength), out float localHit) && localHit <= localLength) return true;
            }
            return false;
        }

        private void OnDestroy() => _subscription?.Dispose();
    }
}
