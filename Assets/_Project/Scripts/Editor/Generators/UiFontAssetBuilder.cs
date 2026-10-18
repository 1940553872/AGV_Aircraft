using System.IO;
using RivetSim.Core.Logging;
using RivetSim.Editor.Importers;
using RivetSim.Editor.Tools;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.Text;

namespace RivetSim.Editor.Generators
{
    /// <summary>
    /// UI Toolkit 字体资源（M2 第三部分 3A）：用 Noto Sans SC Regular / Medium / Bold 创建动态 FontAsset（按需生成字形），
    /// 并在 UI_Theme_Runtime.tss 中把 Regular 设为默认字体。已存在的字体资源不重建（保持 GUID 与引用稳定）。
    /// </summary>
    public static class UiFontAssetBuilder
    {
        /// <summary>字体资源目录。</summary>
        public const string FontFolder = ProjectPaths.UiFolder + "/Fonts";

        /// <summary>字重列表。</summary>
        public static readonly string[] Weights = { "Regular", "Medium", "Bold" };

        /// <summary>字体资源路径。</summary>
        public static string FontAssetPath(string weight) => $"{FontFolder}/UI_Font_NotoSansSC_{weight}.asset";

        /// <summary>创建字体资源并更新主题样式表。</summary>
        [MenuItem("RivetSim/Generate/UI Font Assets", priority = 32)]
        public static void Build()
        {
            GeneratorUtility.EnsureFolder(FontFolder);
            foreach (string weight in Weights)
            {
                string path = FontAssetPath(weight);
                if (AssetDatabase.LoadAssetAtPath<FontAsset>(path) != null) continue;
                var font = AssetDatabase.LoadAssetAtPath<Font>($"{ThirdPartyImporter.FontFolder}/NotoSansSC-{weight}.ttf");
                if (font == null)
                {
                    SimLog.Error(LogCategory.System, $"缺少字体 NotoSansSC-{weight}.ttf，请先执行 RivetSim/Import/Third Party Assets。");
                    continue;
                }
                FontAsset asset = FontAsset.CreateFontAsset(font);
                asset.name = Path.GetFileNameWithoutExtension(path);
                AssetDatabase.CreateAsset(asset, path);
                foreach (Texture2D atlas in asset.atlasTextures)
                {
                    if (atlas == null) continue;
                    atlas.name = asset.name + " Atlas";
                    AssetDatabase.AddObjectToAsset(atlas, asset);
                }
                if (asset.material != null)
                {
                    asset.material.name = asset.name + " Material";
                    AssetDatabase.AddObjectToAsset(asset.material, asset);
                }
            }
            AssetDatabase.SaveAssets();
            WriteTheme();
            SimLog.Info(LogCategory.System, $"UI 字体资源已就绪：{FontFolder}/（默认 Regular）");
        }

        private static void WriteTheme()
        {
            string theme = SandboxSceneBuilder.ThemePath;
            string contents =
                "@import url(\"unity-theme://default\");\n\n" +
                "/* 中文字体：Noto Sans SC（SIL OFL 1.1），由 RivetSim/Generate/UI Font Assets 生成 */\n" +
                ":root {\n" +
                $"    -unity-font-definition: url(\"project://database/{FontAssetPath("Regular")}\");\n" +
                "}\n";
            if (File.Exists(theme) && File.ReadAllText(theme) == contents) return;
            File.WriteAllText(theme, contents, new System.Text.UTF8Encoding(false));
            AssetDatabase.ImportAsset(theme);
        }
    }
}
