using System.IO;
using RivetSim.Core.Logging;
using RivetSim.Editor.Importers;
using RivetSim.Editor.Tools;
using UnityEditor;
using UnityEngine;

namespace RivetSim.Editor.Generators
{
    /// <summary>
    /// 用 Poly Haven（CC0）贴图替换部分纯色材质（M2 第三部分 3A）：地面 → Concrete Floor Painted，墙面 → Corrugated Iron 02，
    /// 演示台台面与 AGV 上装平台 → Metal Plate。由 ARM 图（R = AO，G = 粗糙度，B = 金属度）生成 URP 用的
    /// 金属度 / 光滑度贴图（R = 金属度，A = 1 − 粗糙度）与 AO 贴图，存到 Art/Textures/Generated/。缺少贴图时保持纯色。
    /// </summary>
    public static class PolyHavenMaterialBuilder
    {
        /// <summary>AGV 上装平台材质名。</summary>
        public const string AgvDeck = "M_AGV_Deck";

        private const string GeneratedFolder = ProjectPaths.TexturesFolder + "/Generated";

        private readonly struct Binding
        {
            public readonly string Material;
            public readonly string Texture;
            public readonly Vector2 TilesPerMaterial;
            public readonly Color Tint;

            public Binding(string material, string texture, Vector2 tiles, Color tint)
            {
                Material = material;
                Texture = texture;
                TilesPerMaterial = tiles;
                Tint = tint;
            }
        }

        // 平铺次数按主要使用面估算：地面 64 × 44 m 约 3 m 一块；墙面按北墙 64 × 12 m；台面、平台各 1 块。色调保持 M1 的深灰配色。
        private static readonly Binding[] Bindings =
        {
            new Binding(MaterialLibraryBuilder.FloorEpoxy, "concrete_floor_painted", new Vector2(21f, 15f), new Color(0.42f, 0.46f, 0.52f)),
            new Binding(MaterialLibraryBuilder.Wall, "corrugated_iron_02", new Vector2(16f, 3f), new Color(0.55f, 0.60f, 0.66f)),
            new Binding(MaterialLibraryBuilder.DeskTop, "metal_plate", new Vector2(1f, 1f), new Color(0.30f, 0.33f, 0.37f)),
            new Binding(AgvDeck, "metal_plate", new Vector2(1f, 1f), new Color(0.30f, 0.33f, 0.37f)),
        };

        /// <summary>生成派生贴图并给材质赋图。</summary>
        [MenuItem("RivetSim/Generate/Poly Haven Materials", priority = 11)]
        public static void Build()
        {
            GeneratorUtility.EnsureFolder(GeneratedFolder);
            int applied = 0;
            foreach (Binding binding in Bindings)
            {
                Material material = MaterialLibraryBuilder.EnsureLitMaterial(binding.Material);
                var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(ThirdPartyImporter.PolyHavenTexturePath(binding.Texture, "diff"));
                var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(ThirdPartyImporter.PolyHavenTexturePath(binding.Texture, "nor_gl"));
                var arm = AssetDatabase.LoadAssetAtPath<Texture2D>(ThirdPartyImporter.PolyHavenTexturePath(binding.Texture, "arm"));
                if (albedo == null || normal == null || arm == null)
                {
                    SimLog.Warning(LogCategory.System, $"缺少 Poly Haven 贴图 {binding.Texture}，{binding.Material} 保持纯色。");
                    continue;
                }
                Texture2D metallic = DeriveFromArm(arm, binding.Texture, "MetallicSmoothness", c => new Color(c.b, 0f, 0f, 1f - c.g));
                Texture2D occlusion = DeriveFromArm(arm, binding.Texture, "Occlusion", c => new Color(c.r, c.r, c.r, 1f));

                material.SetColor("_BaseColor", binding.Tint);
                material.SetTexture("_BaseMap", albedo);
                material.SetTexture("_BumpMap", normal);
                material.EnableKeyword("_NORMALMAP");
                material.SetTexture("_MetallicGlossMap", metallic);
                material.EnableKeyword("_METALLICSPECGLOSSMAP");
                material.SetFloat("_Smoothness", 1f);
                material.SetFloat("_Metallic", 1f);
                material.SetTexture("_OcclusionMap", occlusion);
                material.EnableKeyword("_OCCLUSIONMAP");
                material.SetTextureScale("_BaseMap", binding.TilesPerMaterial);
                EditorUtility.SetDirty(material);
                applied++;
            }
            AssetDatabase.SaveAssets();
            SimLog.Info(LogCategory.System, $"Poly Haven 材质已应用：{applied} 个。");
        }

        // 派生贴图只在缺失时生成（ARM 原图不变则结果不变），保持 GUID 稳定。
        private static Texture2D DeriveFromArm(Texture2D arm, string name, string suffix, System.Func<Color, Color> map)
        {
            string path = $"{GeneratedFolder}/T_{name}_{suffix}.png";
            if (!File.Exists(path))
            {
                Color[] src = arm.GetPixels();
                var dst = new Color[src.Length];
                for (int i = 0; i < src.Length; i++) dst[i] = map(src[i]);
                var tex = new Texture2D(arm.width, arm.height, TextureFormat.RGBA32, false, true);
                tex.SetPixels(dst);
                tex.Apply();
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                if (AssetImporter.GetAtPath(path) is TextureImporter importer)
                {
                    importer.sRGBTexture = false;
                    importer.SaveAndReimport();
                }
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
