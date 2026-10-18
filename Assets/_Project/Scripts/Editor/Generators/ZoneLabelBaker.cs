using System.Collections.Generic;
using System.IO;
using RivetSim.Core.Config;
using RivetSim.Core.Logging;
using RivetSim.Editor.Tools;
using UnityEditor;
using UnityEngine;

namespace RivetSim.Editor.Generators
{
    /// <summary>
    /// 地面文字烘焙：用 Noto Sans SC Bold 的动态字形把区域名称与尺寸画进贴图 T_ZoneLabel_*，
    /// 并生成 Alpha 裁剪材质 M_ZoneLabel_*，供 HallGenerator 贴在地面上。不依赖 TextMeshPro。
    /// </summary>
    public static class ZoneLabelBaker
    {
        /// <summary>作业区。</summary>
        public const string Work = "Work";
        /// <summary>移动区。</summary>
        public const string Mobility = "Mobility";
        /// <summary>演示区。</summary>
        public const string Demo = "Demo";
        /// <summary>充电位。</summary>
        public const string Charging = "Charging";
        /// <summary>起始位。</summary>
        public const string Start = "Start";

        // 字形栅格化像素尺寸与排版比例（贴图外观）。
        private const int TitleSizePx = 160;
        private const int SubtitleSizePx = 96;
        private const float LineGapRatio = 0.3f;
        private const int PaddingPx = 24;
        private const float AlphaCutoff = 0.5f;

        private readonly struct Line
        {
            public readonly string Text;
            public readonly int SizePx;

            public Line(string text, int sizePx)
            {
                Text = text;
                SizePx = sizePx;
            }
        }

        /// <summary>生成全部地面文字贴图与材质。</summary>
        [MenuItem("RivetSim/Generate/Zone Labels", priority = 11)]
        public static void Build()
        {
            var layout = GeneratorUtility.LoadConfig<SceneLayoutConfig>(ProjectPaths.SceneLayoutAsset);
            var font = AssetDatabase.LoadAssetAtPath<Font>(ProjectPaths.NotoSansScBold);
            if (layout == null) return;
            if (font == null)
            {
                SimLog.Error(LogCategory.System, $"找不到字体 {ProjectPaths.NotoSansScBold}");
                return;
            }

            var labels = new Dictionary<string, Line[]>
            {
                [Work] = ZoneLines("作业区", layout.workZone),
                [Mobility] = ZoneLines("移动区", layout.mobilityZone),
                [Demo] = ZoneLines("演示区", layout.demoZone),
                [Charging] = new[] { new Line("充电位", TitleSizePx) },
                [Start] = new[] { new Line("起始位", TitleSizePx) },
            };

            GeneratorUtility.EnsureFolder(ProjectPaths.TexturesFolder);
            GeneratorUtility.EnsureFolder(ProjectPaths.MaterialsFolder);
            foreach (KeyValuePair<string, Line[]> pair in labels)
            {
                if (!EnsureCharacters(font, pair.Value))
                {
                    SimLog.Error(LogCategory.System, $"字体无法提供标签 {pair.Key} 的全部字形。");
                    continue;
                }
                Texture2D texture = BakeTexture(font, pair.Value, TexturePath(pair.Key));
                Material material = MaterialLibraryBuilder.EnsureLitMaterial(MaterialName(pair.Key));
                MaterialLibraryBuilder.ApplyAlphaClipTexture(material, texture, new Color(0.90f, 0.93f, 0.97f), AlphaCutoff);
            }
            AssetDatabase.SaveAssets();
            SimLog.Info(LogCategory.System, $"地面文字已烘焙：{labels.Count} 张。");
        }

        /// <summary>读取地面文字材质。</summary>
        public static Material GetMaterial(string id)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialLibraryBuilder.MaterialPath(MaterialName(id)));
            if (material == null) SimLog.Error(LogCategory.System, $"地面文字材质缺失：{id}，请先执行 RivetSim/Generate/Zone Labels。");
            return material;
        }

        /// <summary>读取地面文字贴图宽高比（宽 / 高），缺失时返回 1。</summary>
        public static float GetAspect(string id)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath(id));
            return texture != null ? (float)texture.width / texture.height : 1f;
        }

        private static string MaterialName(string id) => $"M_ZoneLabel_{id}";

        private static string TexturePath(string id) => $"{ProjectPaths.TexturesFolder}/T_ZoneLabel_{id}.png";

        private static Line[] ZoneLines(string title, ZoneRect zone)
        {
            return new[]
            {
                new Line(title, TitleSizePx),
                new Line($"{zone.sizeM.x:0.#} × {zone.sizeM.y:0.#} m", SubtitleSizePx),
            };
        }

        // 动态字体请求其他字号时可能重建贴图、使已请求的字形失效，因此每张标签烘焙前单独请求并确认字形齐全。
        private const int MaxRequestAttempts = 4;

        private static bool EnsureCharacters(Font font, Line[] lines)
        {
            for (int attempt = 0; attempt < MaxRequestAttempts; attempt++)
            {
                foreach (Line line in lines) font.RequestCharactersInTexture(line.Text, line.SizePx, FontStyle.Normal);
                bool complete = true;
                foreach (Line line in lines)
                {
                    foreach (char c in line.Text)
                        complete &= font.GetCharacterInfo(c, out _, line.SizePx, FontStyle.Normal);
                }
                if (complete) return true;
            }
            return false;
        }

        private static Texture2D BakeTexture(Font font, Line[] lines, string path)
        {
            // 排版：逐行测量宽度与上下伸展，行间留白。
            var widths = new float[lines.Length];
            var ascents = new float[lines.Length];
            var descents = new float[lines.Length];
            float totalHeight = 0f, maxWidth = 0f;
            for (int i = 0; i < lines.Length; i++)
            {
                Measure(font, lines[i], out widths[i], out ascents[i], out descents[i]);
                maxWidth = Mathf.Max(maxWidth, widths[i]);
                totalHeight += ascents[i] - descents[i];
                if (i > 0) totalHeight += lines[i].SizePx * LineGapRatio;
            }
            int width = Mathf.CeilToInt(maxWidth) + PaddingPx * 2;
            int height = Mathf.CeilToInt(totalHeight) + PaddingPx * 2;

            var rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            GL.Clear(true, true, Color.clear);
            GL.PushMatrix();
            GL.LoadPixelMatrix(0, width, 0, height);
            font.material.SetPass(0);
            GL.Begin(GL.QUADS);
            GL.Color(Color.white);
            float top = height - PaddingPx;
            for (int i = 0; i < lines.Length; i++)
            {
                if (i > 0) top -= lines[i].SizePx * LineGapRatio;
                float baseline = top - ascents[i];
                DrawLine(font, lines[i], (width - widths[i]) * 0.5f, baseline);
                top = baseline + descents[i];
            }
            GL.End();
            GL.PopMatrix();

            var readback = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            readback.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            readback.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);

            // 字体着色器输出 rgb = 白 × 覆盖率，取 R 通道作为 Alpha，避免混合模式影响 Alpha。
            Color32[] pixels = readback.GetPixels32();
            for (int p = 0; p < pixels.Length; p++) pixels[p] = new Color32(255, 255, 255, pixels[p].r);
            readback.SetPixels32(pixels);
            File.WriteAllBytes(path, readback.EncodeToPNG());
            Object.DestroyImmediate(readback);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static void Measure(Font font, Line line, out float width, out float ascent, out float descent)
        {
            width = 0f;
            ascent = 0f;
            descent = 0f;
            foreach (char c in line.Text)
            {
                if (!font.GetCharacterInfo(c, out CharacterInfo info, line.SizePx, FontStyle.Normal)) continue;
                width += info.advance;
                ascent = Mathf.Max(ascent, info.maxY);
                descent = Mathf.Min(descent, info.minY);
            }
        }

        private static void DrawLine(Font font, Line line, float x, float baseline)
        {
            foreach (char c in line.Text)
            {
                if (!font.GetCharacterInfo(c, out CharacterInfo info, line.SizePx, FontStyle.Normal))
                {
                    SimLog.Warning(LogCategory.System, $"字体缺少字形：{c}");
                    continue;
                }
                float x0 = x + info.minX, x1 = x + info.maxX;
                float y0 = baseline + info.minY, y1 = baseline + info.maxY;
                GL.TexCoord(info.uvBottomLeft); GL.Vertex3(x0, y0, 0f);
                GL.TexCoord(info.uvTopLeft); GL.Vertex3(x0, y1, 0f);
                GL.TexCoord(info.uvTopRight); GL.Vertex3(x1, y1, 0f);
                GL.TexCoord(info.uvBottomRight); GL.Vertex3(x1, y0, 0f);
                x += info.advance;
            }
        }
    }
}
