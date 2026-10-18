using RivetSim.Simulation.Navigation;
using UnityEngine;

namespace RivetSim.Presentation.Visualization
{
    /// <summary>
    /// 地面叠加层网格工具：生成覆盖栅格范围、贴图 u 沿世界 X、v 沿世界 Z 的水平四边形，以及点采样、无 Mip 的 RGBA 贴图。
    /// </summary>
    public static class GroundOverlayMesh
    {
        /// <summary>覆盖 geometry 范围、离地 heightM 的四边形（法向朝上）。</summary>
        public static Mesh CreateQuad(GridGeometry geometry, float heightM, string name)
        {
            Vector2 min = geometry.originM, max = geometry.MaxM;
            var mesh = new Mesh { name = name };
            mesh.SetVertices(new[]
            {
                new Vector3(min.x, heightM, min.y), new Vector3(max.x, heightM, min.y),
                new Vector3(max.x, heightM, max.y), new Vector3(min.x, heightM, max.y),
            });
            mesh.SetUVs(0, new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) });
            mesh.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>与栅格一一对应的贴图（像素 (x, y) = 格 (x, y)）。</summary>
        public static Texture2D CreateTexture(GridGeometry geometry, string name)
        {
            return new Texture2D(geometry.width, geometry.height, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
        }
    }
}
