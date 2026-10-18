using System.Collections.Generic;
using System.IO;
using RivetSim.Core.Config;
using RivetSim.Core.Logging;
using RivetSim.Editor.Tools;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace RivetSim.Editor.Generators
{
    /// <summary>
    /// 材质库生成器：缺少贴图时使用的纯色 URP/Lit 材质与程序生成的黄黑警示线贴图（Architecture §7 3D 部分）。
    /// 重复执行会就地更新同名材质，GUID 不变。
    /// </summary>
    public static class MaterialLibraryBuilder
    {
        /// <summary>环氧地面。</summary>
        public const string FloorEpoxy = "M_Floor_Epoxy";
        /// <summary>墙面。</summary>
        public const string Wall = "M_Wall";
        /// <summary>钢柱与桁架。</summary>
        public const string Steel = "M_Steel";
        /// <summary>卷帘门。</summary>
        public const string Door = "M_Door";
        /// <summary>黄色地面标线。</summary>
        public const string LineYellow = "M_Line_Yellow";
        /// <summary>白色地面标线。</summary>
        public const string LineWhite = "M_Line_White";
        /// <summary>黄黑警示线（沿世界 X 的边）。</summary>
        public const string HazardX = "M_Hazard_Stripe_X";
        /// <summary>黄黑警示线（沿世界 Z 的边）。</summary>
        public const string HazardZ = "M_Hazard_Stripe_Z";
        /// <summary>顶灯发光面。</summary>
        public const string LampEmissive = "M_Lamp_Emissive";
        /// <summary>机器人主体浅灰。</summary>
        public const string RobotBody = "M_Robot_Body";
        /// <summary>机器人橙色点缀。</summary>
        public const string RobotAccent = "M_Robot_Accent";
        /// <summary>机器人深灰件。</summary>
        public const string RobotDark = "M_Robot_Dark";
        /// <summary>橡胶（车轮、缓冲条）。</summary>
        public const string Rubber = "M_Rubber";
        /// <summary>亮金属。</summary>
        public const string MetalBright = "M_Metal_Bright";
        /// <summary>急停红。</summary>
        public const string EStopRed = "M_EStop_Red";
        /// <summary>警示黄（急停底座、轮挡）。</summary>
        public const string SafetyYellow = "M_Safety_Yellow";
        /// <summary>灯带青色发光。</summary>
        public const string LightStrip = "M_LightStrip_Cyan";
        /// <summary>设备指示灯（熄灭态）。</summary>
        public const string IndicatorOff = "M_Indicator_Off";
        /// <summary>雷达窗口。</summary>
        public const string LidarWindow = "M_Lidar_Window";
        /// <summary>模型机蒙皮。</summary>
        public const string AircraftSkin = "M_Aircraft_Skin";
        /// <summary>模型机尾翼涂装。</summary>
        public const string AircraftAccent = "M_Aircraft_Accent";
        /// <summary>演示台桌体。</summary>
        public const string Desk = "M_Desk";
        /// <summary>演示台桌面。</summary>
        public const string DeskTop = "M_DeskTop";
        /// <summary>显示器边框。</summary>
        public const string Bezel = "M_Bezel";
        /// <summary>屏幕（未通电），M6 改为 RenderTexture。</summary>
        public const string ScreenOff = "M_Screen_Off";
        /// <summary>货箱。</summary>
        public const string Crate = "M_Crate";
        /// <summary>工具车。</summary>
        public const string ToolCart = "M_ToolCart";
        /// <summary>锥桶橙。</summary>
        public const string Cone = "M_Cone";
        /// <summary>标记板白底。</summary>
        public const string MarkerBoard = "M_MarkerBoard";

        // 警示线条纹周期等于线宽，使条纹呈 45°。
        private const string HazardTextureName = "T_HazardStripe";
        private const int HazardTexturePx = 64;

        /// <summary>警示线宽度，单位 m（地面标线外观）。</summary>
        public const float HazardLineWidthM = 0.15f;

        private readonly struct Spec
        {
            public readonly string Name;
            public readonly Color Color;
            public readonly float Smoothness;
            public readonly float Metallic;
            public readonly Color Emission;

            public Spec(string name, Color color, float smoothness, float metallic, Color emission = default)
            {
                Name = name;
                Color = color;
                Smoothness = smoothness;
                Metallic = metallic;
                Emission = emission;
            }
        }

        private static readonly Spec[] Specs =
        {
            new Spec(FloorEpoxy, Hex(0x3A4048), 0.62f, 0f),
            new Spec(Wall, Hex(0x5C6672), 0.25f, 0.1f),
            new Spec(Steel, Hex(0x2F3A46), 0.45f, 0.6f),
            new Spec(Door, Hex(0x4B5866), 0.35f, 0.4f),
            new Spec(LineYellow, Hex(0xF2C200), 0.4f, 0f),
            new Spec(LineWhite, Hex(0xDCE2E8), 0.4f, 0f),
            new Spec(LampEmissive, Hex(0xF4F7FA), 0.5f, 0f, Hex(0xFFF6E8) * 3f),
            new Spec(RobotBody, Hex(0xD6DADF), 0.5f, 0.05f),
            new Spec(RobotAccent, Hex(0xFF7A1A), 0.5f, 0f),
            new Spec(RobotDark, Hex(0x2B3038), 0.4f, 0.2f),
            new Spec(Rubber, Hex(0x18191C), 0.2f, 0f),
            new Spec(MetalBright, Hex(0xB9BFC7), 0.75f, 0.85f),
            new Spec(EStopRed, Hex(0xD7262B), 0.6f, 0f, Hex(0x3A0405)),
            new Spec(SafetyYellow, Hex(0xF2C200), 0.45f, 0f),
            new Spec(LightStrip, Hex(0x2EC4E6), 0.6f, 0f, Hex(0x2EC4E6) * 2.5f),
            new Spec(IndicatorOff, Hex(0x3A434E), 0.7f, 0f),
            new Spec(LidarWindow, Hex(0x111418), 0.9f, 0.2f),
            new Spec(AircraftSkin, Hex(0xE9EDF0), 0.6f, 0.05f),
            new Spec(AircraftAccent, Hex(0x1F4E8C), 0.55f, 0.05f),
            new Spec(Desk, Hex(0x3E444C), 0.35f, 0.3f),
            new Spec(DeskTop, Hex(0x23272D), 0.5f, 0f),
            new Spec(Bezel, Hex(0x14171B), 0.5f, 0.1f),
            new Spec(ScreenOff, Hex(0x05080C), 0.92f, 0f, Hex(0x0E1621)),
            new Spec(Crate, Hex(0xA9824E), 0.25f, 0f),
            new Spec(ToolCart, Hex(0xC0392B), 0.5f, 0.3f),
            new Spec(Cone, Hex(0xFF6A00), 0.5f, 0f),
            new Spec(MarkerBoard, Hex(0xF5F5F5), 0.3f, 0f),
        };

        private static bool _building;

        /// <summary>生成或更新全部材质。</summary>
        [MenuItem("RivetSim/Generate/Material Library", priority = 10)]
        public static void Build()
        {
            _building = true;
            try
            {
                GeneratorUtility.EnsureFolder(ProjectPaths.MaterialsFolder);
                foreach (Spec spec in Specs)
                {
                    Material material = EnsureLitMaterial(spec.Name);
                    ApplyColor(material, spec.Color, spec.Smoothness, spec.Metallic, spec.Emission);
                }
                BuildHazardMaterials();
                AssetDatabase.SaveAssets();
                SimLog.Info(LogCategory.System, $"材质库已生成：{Specs.Length + 2} 个材质。");
            }
            finally
            {
                _building = false;
            }
        }

        /// <summary>按名称读取材质；材质库缺失时先生成。</summary>
        public static Material Get(string name)
        {
            string path = MaterialPath(name);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null && !_building)
            {
                Build();
                material = AssetDatabase.LoadAssetAtPath<Material>(path);
            }
            if (material == null) SimLog.Error(LogCategory.System, $"材质不存在：{path}");
            return material;
        }

        /// <summary>材质资产路径。</summary>
        public static string MaterialPath(string name) => $"{ProjectPaths.MaterialsFolder}/{name}.mat";

        /// <summary>读取或创建 URP/Lit 材质。</summary>
        public static Material EnsureLitMaterial(string name)
        {
            string path = MaterialPath(name);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;

            Shader shader = GraphicsSettings.currentRenderPipeline != null
                ? GraphicsSettings.currentRenderPipeline.defaultShader
                : Shader.Find("Universal Render Pipeline/Lit");
            material = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <summary>设置颜色、光滑度、金属度与自发光。</summary>
        public static void ApplyColor(Material material, Color color, float smoothness, float metallic, Color emission)
        {
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", metallic);
            bool emissive = emission.maxColorComponent > 0f;
            material.SetColor("_EmissionColor", emissive ? emission : Color.black);
            if (emissive)
            {
                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else
            {
                material.DisableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            }
            EditorUtility.SetDirty(material);
        }

        /// <summary>设置贴图与 Alpha 裁剪（用于地面文字等）。</summary>
        public static void ApplyAlphaClipTexture(Material material, Texture2D texture, Color tint, float cutoff)
        {
            ApplyColor(material, tint, 0.3f, 0f, default);
            material.SetTexture("_BaseMap", texture);
            material.SetFloat("_AlphaClip", 1f);
            material.SetFloat("_Cutoff", cutoff);
            material.EnableKeyword("_ALPHATEST_ON");
            material.SetOverrideTag("RenderType", "TransparentCutout");
            material.renderQueue = (int)RenderQueue.AlphaTest;
            EditorUtility.SetDirty(material);
        }

        private static void BuildHazardMaterials()
        {
            var layout = GeneratorUtility.LoadConfig<SceneLayoutConfig>(ProjectPaths.SceneLayoutAsset);
            Texture2D stripe = BuildHazardTexture();
            Vector2 workSize = layout != null ? layout.workZone.sizeM : Vector2.one;
            ApplyHazard(EnsureLitMaterial(HazardX), stripe, workSize.x / HazardLineWidthM);
            ApplyHazard(EnsureLitMaterial(HazardZ), stripe, workSize.y / HazardLineWidthM);
        }

        private static void ApplyHazard(Material material, Texture2D stripe, float repeats)
        {
            ApplyColor(material, Color.white, 0.4f, 0f, default);
            material.SetTexture("_BaseMap", stripe);
            material.SetTextureScale("_BaseMap", new Vector2(repeats, 1f));
            EditorUtility.SetDirty(material);
        }

        private static Texture2D BuildHazardTexture()
        {
            GeneratorUtility.EnsureFolder(ProjectPaths.TexturesFolder);
            string path = $"{ProjectPaths.TexturesFolder}/{HazardTextureName}.png";
            var pixels = new List<Color32>(HazardTexturePx * HazardTexturePx);
            Color32 yellow = Hex(0xF2C200);
            Color32 black = Hex(0x15171A);
            for (int y = 0; y < HazardTexturePx; y++)
            {
                for (int x = 0; x < HazardTexturePx; x++)
                    pixels.Add((x + y) % HazardTexturePx < HazardTexturePx / 2 ? yellow : black);
            }

            var texture = new Texture2D(HazardTexturePx, HazardTexturePx, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels.ToArray());
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Color Hex(int rgb)
        {
            return new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 0xFF);
        }
    }
}
