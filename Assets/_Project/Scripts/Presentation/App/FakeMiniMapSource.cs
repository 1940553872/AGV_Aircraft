using System.Collections.Generic;
using RivetSim.Core.Config;
using RivetSim.Presentation.UI;
using UnityEngine;

namespace RivetSim.Presentation.App
{
    /// <summary>
    /// Sandbox_UI 小地图假数据（M6）：程序生成的示意栅格（厂房外墙、模型机轮廓、障碍物），路径与 AGV 位姿由 <see cref="FakeMissionFeed"/> 写入。
    /// </summary>
    public sealed class FakeMiniMapSource : IMiniMapSource
    {
        // 示意尺寸（假数据，不参与仿真）：厂房 40 m × 24 m，0.2 m 一格。
        private const int WidthPx = 200;
        private const int HeightPx = 120;
        private static readonly Vector2 HallSizeM = new Vector2(40f, 24f);
        private static readonly Color32 FreeColor = new Color32(0x26, 0x3A, 0x52, 0xFF);
        private static readonly Color32 OccupiedColor = new Color32(0xE6, 0xEE, 0xF7, 0xFF);

        private readonly List<Vector2> _path = new List<Vector2>();

        /// <inheritdoc />
        public Texture2D GridTexture { get; }

        /// <inheritdoc />
        public Rect WorldRectM { get; } = new Rect(-HallSizeM * 0.5f, HallSizeM);

        /// <inheritdoc />
        public PlanarPose AgvPose { get; set; } = new PlanarPose(new Vector2(-16f, -8f), 90f);

        /// <inheritdoc />
        public IReadOnlyList<Vector2> PathPoints => _path;

        /// <inheritdoc />
        public int PathRevision { get; private set; }

        /// <summary>生成示意栅格。</summary>
        public FakeMiniMapSource()
        {
            GridTexture = new Texture2D(WidthPx, HeightPx, TextureFormat.RGBA32, false) { name = "T_MiniMapFake", filterMode = FilterMode.Point };
            var pixels = new Color32[WidthPx * HeightPx];
            for (int y = 0; y < HeightPx; y++)
            for (int x = 0; x < WidthPx; x++)
            {
                bool wall = x == 0 || y == 0 || x == WidthPx - 1 || y == HeightPx - 1;
                bool fuselage = x > 70 && x < 180 && y > 52 && y < 68;
                bool wing = x > 110 && x < 130 && y > 20 && y < 100 && !fuselage;
                bool crate = x > 30 && x < 40 && y > 85 && y < 95;
                pixels[y * WidthPx + x] = wall || ((fuselage || wing || crate) && (Edge(x, y) || crate)) ? OccupiedColor : FreeColor;
            }
            GridTexture.SetPixels32(pixels);
            GridTexture.Apply(false);
        }

        private static bool Edge(int x, int y) =>
            x == 71 || x == 179 || y == 53 || y == 67 || x == 111 || x == 129 || y == 21 || y == 99;

        /// <summary>写入规划路径。</summary>
        public void SetPath(params Vector2[] points)
        {
            _path.Clear();
            _path.AddRange(points);
            PathRevision++;
        }

        /// <inheritdoc />
        public void Refresh(float realDeltaS)
        {
        }
    }
}
