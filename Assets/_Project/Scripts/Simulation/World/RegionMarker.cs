using UnityEngine;

namespace RivetSim.Simulation.World
{
    /// <summary>
    /// AprilTag 标记（tag36h11）：ID、边长与用途。标记平面的法向为本地 +Z（朝向观察方）。
    /// 仿真中不运行识别算法，按视场判定是否可见（M3 / M4）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RegionMarker : MonoBehaviour
    {
        [SerializeField, Tooltip("AprilTag ID")]
        private int tagId;

        [SerializeField, Tooltip("标记边长（含 1 格白边），单位 m")]
        private float sizeM;

        [SerializeField, Tooltip("用途")]
        private MarkerRole role;

        /// <summary>AprilTag ID。</summary>
        public int TagId => tagId;

        /// <summary>标记边长，单位 m。</summary>
        public float SizeM => sizeM;

        /// <summary>用途。</summary>
        public MarkerRole Role => role;

        /// <summary>编辑器生成器写入参数。</summary>
        public void Configure(int id, float size, MarkerRole markerRole)
        {
            tagId = id;
            sizeM = size;
            role = markerRole;
        }
    }
}
