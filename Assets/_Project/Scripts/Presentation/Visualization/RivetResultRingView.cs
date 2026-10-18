using System;
using System.Collections.Generic;
using RivetSim.Core.Events;
using RivetSim.Simulation.Inspection;
using RivetSim.Simulation.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace RivetSim.Presentation.Visualization
{
    /// <summary>
    /// 铆钉结果环（Visualization 层，V 开关）：每颗铆钉检测完成后在钉头外圈画一个圆环——绿 = 合格，黄 = 疑似，红 = 缺陷，灰 = 无效。
    /// 圆环贴在蒙皮外侧（沿法向略微抬起），重复检测时更新颜色。材质由场景生成器写入。
    /// </summary>
    public sealed class RivetResultRingView : MonoBehaviour
    {
        // 外观常量（ADR-009），单位 m：钉头直径 8 mm，圆环取半径 9 mm、线宽 3 mm，抬离蒙皮 1.5 mm。
        private const float RingRadiusM = 0.009f;
        private const float RingWidthM = 0.003f;
        private const float RingLiftM = 0.0015f;
        private const int RingSegments = 32;

        [SerializeField, Tooltip("合格")] private Material passMaterial;
        [SerializeField, Tooltip("疑似")] private Material suspectMaterial;
        [SerializeField, Tooltip("缺陷")] private Material defectMaterial;
        [SerializeField, Tooltip("无效")] private Material invalidMaterial;

        private readonly Dictionary<Rivet, LineRenderer> _rings = new Dictionary<Rivet, LineRenderer>();
        private IDisposable _subscription;

        /// <summary>编辑器生成器写入材质。</summary>
        public void Configure(Material pass, Material suspect, Material defect, Material invalid)
        {
            passMaterial = pass;
            suspectMaterial = suspect;
            defectMaterial = defect;
            invalidMaterial = invalid;
        }

        /// <summary>订阅检测完成事件。</summary>
        public void Initialize(EventBus bus) => _subscription = bus.Subscribe<RivetInspectedEvent>(OnInspected);

        /// <summary>结论对应的材质。</summary>
        public Material MaterialFor(InspectionVerdict verdict) => verdict switch
        {
            InspectionVerdict.Pass => passMaterial,
            InspectionVerdict.Suspect => suspectMaterial,
            InspectionVerdict.Defect => defectMaterial,
            _ => invalidMaterial,
        };

        private void OnInspected(RivetInspectedEvent e)
        {
            if (!_rings.TryGetValue(e.Rivet, out LineRenderer ring))
            {
                ring = CreateRing(e.Rivet);
                _rings[e.Rivet] = ring;
            }
            ring.sharedMaterial = MaterialFor(e.Record.Verdict);
        }

        private LineRenderer CreateRing(Rivet rivet)
        {
            var node = new GameObject($"Ring_{rivet.RivetId}");
            node.layer = gameObject.layer;
            node.transform.SetParent(transform, false);
            node.transform.SetPositionAndRotation(rivet.PositionM + rivet.Normal * RingLiftM, Quaternion.LookRotation(rivet.Normal, Vector3.up));
            var line = node.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.alignment = LineAlignment.TransformZ;
            line.widthMultiplier = RingWidthM;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.positionCount = RingSegments;
            for (int i = 0; i < RingSegments; i++)
            {
                float a = 2f * Mathf.PI * i / RingSegments;
                line.SetPosition(i, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * RingRadiusM);
            }
            return line;
        }

        private void OnDestroy() => _subscription?.Dispose();
    }
}
