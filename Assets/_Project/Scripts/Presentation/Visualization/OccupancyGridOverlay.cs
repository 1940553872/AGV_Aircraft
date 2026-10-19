using RivetSim.Core.Diagnostics;
using RivetSim.Simulation.Navigation;
using Unity.Collections;
using UnityEngine;

namespace RivetSim.Presentation.Visualization
{
    /// <summary>
    /// 占据栅格地图叠加层：半透明贴在地面上，随建图逐步“长出来”（空闲 = 淡青色，占据 = 亮白，未知 = 透明）。
    /// 按地图脏矩形增量写像素，地图版本不变时不上传贴图。
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class OccupancyGridOverlay : MonoBehaviour
    {
        // 叠加层离地高度与配色（外观常量，ADR-009）。
        private const float HeightM = 0.012f;
        private static readonly Color32 FreeColor = new Color32(0x2E, 0xC4, 0xE6, 0x38);
        private static readonly Color32 OccupiedColor = new Color32(0xE6, 0xEE, 0xF7, 0xF0);
        private static readonly Color32 UnknownColor = new Color32(0, 0, 0, 0);

        private OccupancyGridMap _map;
        private Texture2D _texture;
        private int _revision = -1;

        /// <summary>绑定地图并生成网格与贴图。</summary>
        public void Initialize(OccupancyGridMap map)
        {
            _map = map;
            GetComponent<MeshFilter>().sharedMesh = GroundOverlayMesh.CreateQuad(map.Geometry, HeightM, "OccupancyOverlay");
            _texture = GroundOverlayMesh.CreateTexture(map.Geometry, "T_OccupancyOverlay");
            NativeArray<Color32> pixels = _texture.GetPixelData<Color32>(0);
            for (int i = 0; i < pixels.Length; i++) pixels[i] = UnknownColor;
            _texture.Apply(false);
            GetComponent<MeshRenderer>().material.SetTexture("_BaseMap", _texture);
        }

        private void LateUpdate()
        {
            if (_map == null || _map.Revision == _revision) return;
            _revision = _map.Revision;
            if (!_map.ConsumeDirtyRect(out RectInt rect)) return;
            GcProbe.Begin(ProbeModule.GridOverlay);
            GridGeometry g = _map.Geometry;
            NativeArray<Color32> pixels = _texture.GetPixelData<Color32>(0);
            for (int y = rect.yMin; y < rect.yMax; y++)
            for (int x = rect.xMin; x < rect.xMax; x++)
            {
                int i = g.Index(x, y);
                OccupancyState s = _map.GetState(i);
                pixels[i] = s == OccupancyState.Occupied ? OccupiedColor : s == OccupancyState.Free ? FreeColor : UnknownColor;
            }
            _texture.Apply(false);
            GcProbe.End(ProbeModule.GridOverlay);
        }

        private void OnDestroy()
        {
            if (_texture != null) Destroy(_texture);
        }
    }
}
