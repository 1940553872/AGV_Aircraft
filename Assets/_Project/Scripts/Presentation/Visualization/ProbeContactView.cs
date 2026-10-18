using RivetSim.Simulation.Inspection;
using RivetSim.Simulation.Robot;
using UnityEngine;

namespace RivetSim.Presentation.Visualization
{
    /// <summary>
    /// 探头接触指示（Visualization 层，V 开关）：设置了接触表面时在探头端面外圈画圆环——灰 = 未接触，黄 = 接触但不在允许范围，
    /// 绿 = 在允许范围（8～12 N），红 = 超限。颜色材质复用结果环。
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public sealed class ProbeContactView : MonoBehaviour
    {
        // 外观常量（ADR-009），单位 m。
        private const float RingRadiusM = 0.014f;
        private const float RingWidthM = 0.003f;
        private const int RingSegments = 32;

        private ArmRig _rig;
        private ContactModel _contact;
        private RivetResultRingView _palette;
        private LineRenderer _line;

        /// <summary>绑定机械臂、接触力模型与配色来源。</summary>
        public void Initialize(ArmRig rig, ContactModel contact, RivetResultRingView palette)
        {
            _rig = rig;
            _contact = contact;
            _palette = palette;
            _line = GetComponent<LineRenderer>();
            _line.useWorldSpace = false;
            _line.loop = true;
            _line.alignment = LineAlignment.TransformZ;
            _line.widthMultiplier = RingWidthM;
            _line.positionCount = RingSegments;
            for (int i = 0; i < RingSegments; i++)
            {
                float a = 2f * Mathf.PI * i / RingSegments;
                _line.SetPosition(i, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * RingRadiusM);
            }
            _line.enabled = false;
        }

        private void LateUpdate()
        {
            if (_line == null) return;
            bool show = _contact.HasSurface;
            _line.enabled = show;
            if (!show) return;
            transform.SetPositionAndRotation(_rig.Tcp.position, _rig.Tcp.rotation);
            InspectionVerdict look = _contact.IsOverLimit ? InspectionVerdict.Defect
                : _contact.IsWithinAllowedRange ? InspectionVerdict.Pass
                : _contact.InContact ? InspectionVerdict.Suspect
                : InspectionVerdict.Invalid;
            _line.sharedMaterial = _palette.MaterialFor(look);
        }
    }
}
