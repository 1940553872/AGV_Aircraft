using RivetSim.Simulation.Navigation;
using UnityEngine;

namespace RivetSim.Presentation.Visualization
{
    /// <summary>
    /// 激光点云显示：每条命中射线在命中点画一个小方片（雷达高度），未命中的射线不显示。网格与顶点数组复用，不产生每帧分配。
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class LidarPointCloudView : MonoBehaviour
    {
        // 点的边长（外观常量，ADR-009）。
        private const float PointSizeM = 0.07f;

        private Lidar2D _lidar;
        private Mesh _mesh;
        private Vector3[] _vertices;
        private int _sequence = -1;

        /// <summary>绑定激光雷达。</summary>
        public void Initialize(Lidar2D lidar)
        {
            _lidar = lidar;
            int beams = lidar.LatestScan.EndpointsM.Length;
            _vertices = new Vector3[beams * 4];
            var triangles = new int[beams * 6];
            for (int i = 0; i < beams; i++)
            {
                int v = i * 4, t = i * 6;
                triangles[t] = v; triangles[t + 1] = v + 2; triangles[t + 2] = v + 1;
                triangles[t + 3] = v; triangles[t + 4] = v + 3; triangles[t + 5] = v + 2;
            }
            _mesh = new Mesh { name = "LidarPoints" };
            _mesh.MarkDynamic();
            _mesh.vertices = _vertices;
            _mesh.triangles = triangles;
            _mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1000f);
            GetComponent<MeshFilter>().sharedMesh = _mesh;
        }

        private void LateUpdate()
        {
            LidarScan scan = _lidar != null ? _lidar.LatestScan : null;
            if (scan == null || scan.Sequence == _sequence) return;
            _sequence = scan.Sequence;
            float h = scan.OriginM.y;
            float s = 0.5f * PointSizeM;
            for (int i = 0; i < scan.Hits.Length; i++)
            {
                int v = i * 4;
                if (!scan.Hits[i])
                {
                    _vertices[v] = _vertices[v + 1] = _vertices[v + 2] = _vertices[v + 3] = scan.OriginM;
                    continue;
                }
                Vector2 p = scan.EndpointsM[i];
                _vertices[v] = new Vector3(p.x - s, h, p.y - s);
                _vertices[v + 1] = new Vector3(p.x + s, h, p.y - s);
                _vertices[v + 2] = new Vector3(p.x + s, h, p.y + s);
                _vertices[v + 3] = new Vector3(p.x - s, h, p.y + s);
            }
            _mesh.vertices = _vertices;
        }

        private void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
        }
    }
}
