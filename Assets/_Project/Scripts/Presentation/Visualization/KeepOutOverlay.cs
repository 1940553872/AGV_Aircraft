using RivetSim.Simulation.Navigation;
using Unity.Collections;
using UnityEngine;

namespace RivetSim.Presentation.Visualization
{
    /// <summary>
    /// 虚拟禁行区叠加层：模型机投影显示为半透明红色，工位接近通道显示为淡绿色（通道内不叠加禁行区）。
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class KeepOutOverlay : MonoBehaviour
    {
        // 叠加层离地高度与配色（外观常量，ADR-009）；略高于地图叠加层。
        private const float HeightM = 0.016f;
        private static readonly Color32 KeepOutColor = new Color32(0xFF, 0x5D, 0x5D, 0x60);
        private static readonly Color32 ChannelColor = new Color32(0x3D, 0xD6, 0x8C, 0x48);
        private static readonly Color32 ClearColor = new Color32(0, 0, 0, 0);

        private Texture2D _texture;

        /// <summary>按禁行区掩膜生成叠加层，覆盖 displayGrid 范围（取导航地图范围，使接近通道完整显示）。</summary>
        public void Initialize(KeepOutMask mask, GridGeometry displayGrid)
        {
            GridGeometry g = displayGrid;
            GetComponent<MeshFilter>().sharedMesh = GroundOverlayMesh.CreateQuad(g, HeightM, "KeepOutOverlay");
            _texture = GroundOverlayMesh.CreateTexture(g, "T_KeepOutOverlay");
            NativeArray<Color32> pixels = _texture.GetPixelData<Color32>(0);
            for (int y = 0; y < g.height; y++)
            for (int x = 0; x < g.width; x++)
            {
                Vector2 p = g.CellCenter(x, y);
                pixels[g.Index(x, y)] = mask.InChannel(p) ? ChannelColor : mask.IsProjected(p) ? KeepOutColor : ClearColor;
            }
            _texture.Apply(false);
            GetComponent<MeshRenderer>().material.SetTexture("_BaseMap", _texture);
        }

        private void OnDestroy()
        {
            if (_texture != null) Destroy(_texture);
        }
    }
}
