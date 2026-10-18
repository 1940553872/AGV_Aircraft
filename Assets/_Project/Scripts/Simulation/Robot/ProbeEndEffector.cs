using System;
using UnityEngine;

namespace RivetSim.Simulation.Robot
{
    /// <summary>
    /// 探头末端（主体 S4）：TCP（探头端面中心，本地 +Z 为工具轴）、腕部相机，以及随接触压缩后退的浮动段外观
    /// （探头与延迟块，压缩量取自 <see cref="ContactModel"/>，限制在浮动行程内）。由 EndEffectorGenerator 写入引用。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProbeEndEffector : MonoBehaviour
    {
        [SerializeField, Tooltip("TCP（探头端面中心）")]
        private Transform tcp;

        [SerializeField, Tooltip("腕部相机")]
        private Transform wristCamera;

        [SerializeField, Tooltip("随浮动夹持后退的外观节点（探头、延迟块）")]
        private Transform[] floatingParts = Array.Empty<Transform>();

        [SerializeField, Tooltip("浮动段外观节点的初始本地位置")]
        private Vector3[] floatingBaseLocal = Array.Empty<Vector3>();

        [SerializeField, Tooltip("浮动行程（±），单位 m")]
        private float floatTravelM = 0.003f;

        /// <summary>TCP。</summary>
        public Transform Tcp => tcp;

        /// <summary>腕部相机。</summary>
        public Transform WristCamera => wristCamera;

        /// <summary>编辑器生成器写入引用。</summary>
        public void Configure(Transform tcpNode, Transform cameraNode, Transform[] parts, float travelM)
        {
            tcp = tcpNode;
            wristCamera = cameraNode;
            floatingParts = parts ?? Array.Empty<Transform>();
            floatingBaseLocal = new Vector3[floatingParts.Length];
            for (int i = 0; i < floatingParts.Length; i++) floatingBaseLocal[i] = floatingParts[i].localPosition;
            floatTravelM = travelM;
        }

        /// <summary>按压缩量（单位 m）让浮动段沿工具轴后退（外观）。</summary>
        public void ApplyCompression(float compressionM)
        {
            float d = Mathf.Clamp(compressionM, 0f, floatTravelM);
            int count = Mathf.Min(floatingParts.Length, floatingBaseLocal.Length);
            for (int i = 0; i < count; i++)
            {
                if (floatingParts[i] != null) floatingParts[i].localPosition = floatingBaseLocal[i] - Vector3.forward * d;
            }
        }
    }
}
