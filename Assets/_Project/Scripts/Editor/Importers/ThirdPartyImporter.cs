using System.Collections.Generic;
using System.IO;
using RivetSim.Core.Logging;
using UnityEditor;
using UnityEngine;

namespace RivetSim.Editor.Importers
{
    /// <summary>
    /// 第三方资产导入（M2 第三部分 3A）：从工程根目录 _Downloads/ 复制所需文件到 Assets/ThirdParty/（原件不改），
    /// 并设置导入参数（AprilTag 点采样无压缩、法线贴图、ARM 线性可读等）。重复执行时覆盖文件，GUID 保持不变。
    /// </summary>
    public static class ThirdPartyImporter
    {
        /// <summary>第三方资产根目录。</summary>
        public const string ThirdPartyRoot = "Assets/ThirdParty";

        /// <summary>UR5e 根目录。</summary>
        public const string Ur5eRoot = ThirdPartyRoot + "/Robots/UR5e";

        /// <summary>UR5e 运动学与外观参数目录。</summary>
        public const string Ur5eConfigFolder = Ur5eRoot + "/config";

        /// <summary>UR5e 外观网格目录。</summary>
        public const string Ur5eMeshFolder = Ur5eRoot + "/meshes/visual";

        /// <summary>A320 模型文件。</summary>
        public const string A320Glb = ThirdPartyRoot + "/Aircraft/Airbus_A320-200/airbus_a320-200.glb";

        /// <summary>AprilTag 目录。</summary>
        public const string AprilTagFolder = ThirdPartyRoot + "/AprilTag/tag36h11";

        /// <summary>Poly Haven 贴图根目录。</summary>
        public const string PolyHavenRoot = ThirdPartyRoot + "/Textures/PolyHaven";

        /// <summary>Noto Sans SC 字体目录。</summary>
        public const string FontFolder = ThirdPartyRoot + "/Fonts/NotoSansSC";

        /// <summary>Poly Haven 材质名（与 _Downloads/Textures/PolyHaven_* 子文件夹对应）。</summary>
        public static readonly string[] PolyHavenMaterials = { "concrete_floor_painted", "metal_plate", "corrugated_iron_02", "oak_veneer_01" };

        private const int PolyHavenMaxSize = 2048;
        private const int AprilTagCount = 12;

        private static string DownloadsRoot => Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "_Downloads");

        /// <summary>复制并导入全部第三方资产。</summary>
        [MenuItem("RivetSim/Import/Third Party Assets", priority = 50)]
        public static void ImportAll()
        {
            var copied = new List<string>();
            CopyUr5e(copied);
            CopyFile("Aircraft/Airbus_A320-200/airbus_a320-200.glb", A320Glb, copied);
            CopyFile("Aircraft/Airbus_A320-200/SOURCE.md", ThirdPartyRoot + "/Aircraft/Airbus_A320-200/SOURCE.md", copied);
            CopyAprilTags(copied);
            CopyPolyHaven(copied);
            foreach (string weight in new[] { "Regular", "Medium", "Bold" })
                CopyFile($"Fonts/NotoSansSC/NotoSansSC-{weight}.ttf", $"{FontFolder}/NotoSansSC-{weight}.ttf", copied);
            CopyFile("Fonts/NotoSansSC/OFL.txt", FontFolder + "/OFL.txt", copied);
            CopyFile("Fonts/NotoSansSC/SOURCE.md", FontFolder + "/SOURCE.md", copied);

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ApplyImportSettings();
            SimLog.Info(LogCategory.System, $"第三方资产已导入：{copied.Count} 个文件 → {ThirdPartyRoot}/");
        }

        private static void CopyUr5e(List<string> copied)
        {
            CopyFile("Robot_UR5e/LICENSE", Ur5eRoot + "/LICENSE.txt", copied);
            CopyFile("Robot_UR5e/SOURCE.md", Ur5eRoot + "/SOURCE.md", copied);
            foreach (string name in new[] { "default_kinematics", "joint_limits", "physical_parameters", "visual_parameters" })
                CopyFile($"Robot_UR5e/config/ur5e/{name}.yaml", $"{Ur5eConfigFolder}/{name}.yaml", copied);
            foreach (string name in new[] { "base", "shoulder", "upperarm", "forearm", "wrist1", "wrist2", "wrist3" })
                CopyFile($"Robot_UR5e/meshes/ur5e/visual/{name}.dae", $"{Ur5eMeshFolder}/{name}.dae", copied);
        }

        private static void CopyAprilTags(List<string> copied)
        {
            CopyFile("AprilTag/tag36h11/LICENSE", AprilTagFolder + "/LICENSE.txt", copied);
            CopyFile("AprilTag/tag36h11/SOURCE.md", AprilTagFolder + "/SOURCE.md", copied);
            for (int id = 0; id < AprilTagCount; id++)
                CopyFile($"AprilTag/tag36h11/{AprilTagFileName(id)}", $"{AprilTagFolder}/{AprilTagFileName(id)}", copied);
        }

        private static void CopyPolyHaven(List<string> copied)
        {
            foreach (string name in PolyHavenMaterials)
            {
                string folder = $"{PolyHavenRoot}/{name}";
                CopyFile($"Textures/PolyHaven_{name}/SOURCE.md", folder + "/SOURCE.md", copied);
                foreach (string map in new[] { "diff", "nor_gl", "arm" })
                    CopyFile($"Textures/PolyHaven_{name}/{name}_{map}_2k.jpg", $"{folder}/{name}_{map}_2k.jpg", copied);
            }
        }

        /// <summary>AprilTag 文件名（ID 0～11）。</summary>
        public static string AprilTagFileName(int id) => $"tag36_11_{id:00000}.png";

        /// <summary>Poly Haven 贴图路径，map 为 diff / nor_gl / arm。</summary>
        public static string PolyHavenTexturePath(string name, string map) => $"{PolyHavenRoot}/{name}/{name}_{map}_2k.jpg";

        private static void CopyFile(string downloadsRelative, string assetPath, List<string> copied)
        {
            string source = Path.Combine(DownloadsRoot, downloadsRelative);
            if (!File.Exists(source))
            {
                SimLog.Error(LogCategory.System, $"缺少原始文件：_Downloads/{downloadsRelative}");
                return;
            }
            string destination = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", assetPath);
            // 目标已存在且内容相同则跳过（M8：重复执行不触发重新导入）。
            if (File.Exists(destination) && SameContent(source, destination)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(destination) ?? ".");
            File.Copy(source, destination, true);
            copied.Add(assetPath);
        }

        // 比较时忽略回车符（git 检出会改换行风格，文本内容相同即视为相同）。
        private static bool SameContent(string a, string b)
        {
            byte[] ba = File.ReadAllBytes(a);
            byte[] bb = File.ReadAllBytes(b);
            int i = 0, j = 0;
            while (true)
            {
                while (i < ba.Length && ba[i] == 13) i++;
                while (j < bb.Length && bb[j] == 13) j++;
                if (i >= ba.Length || j >= bb.Length) return i >= ba.Length && j >= bb.Length;
                if (ba[i++] != bb[j++]) return false;
            }
        }

        private static void ApplyImportSettings()
        {
            for (int id = 0; id < AprilTagCount; id++)
            {
                ConfigureTexture($"{AprilTagFolder}/{AprilTagFileName(id)}", t =>
                {
                    t.textureType = TextureImporterType.Default;
                    t.filterMode = FilterMode.Point;
                    t.mipmapEnabled = false;
                    t.wrapMode = TextureWrapMode.Clamp;
                    t.npotScale = TextureImporterNPOTScale.None;
                    t.textureCompression = TextureImporterCompression.Uncompressed;
                    t.sRGBTexture = true;
                });
            }
            foreach (string name in PolyHavenMaterials)
            {
                ConfigureTexture(PolyHavenTexturePath(name, "diff"), t =>
                {
                    t.textureType = TextureImporterType.Default;
                    t.sRGBTexture = true;
                    t.maxTextureSize = PolyHavenMaxSize;
                });
                ConfigureTexture(PolyHavenTexturePath(name, "nor_gl"), t =>
                {
                    t.textureType = TextureImporterType.NormalMap;
                    t.maxTextureSize = PolyHavenMaxSize;
                });
                // ARM 只作为生成金属度 / 光滑度与 AO 贴图的原料：线性、可读、无压缩。
                ConfigureTexture(PolyHavenTexturePath(name, "arm"), t =>
                {
                    t.textureType = TextureImporterType.Default;
                    t.sRGBTexture = false;
                    t.isReadable = true;
                    t.textureCompression = TextureImporterCompression.Uncompressed;
                    t.maxTextureSize = PolyHavenMaxSize;
                });
            }
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { Ur5eMeshFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not ModelImporter model) continue;
                model.importAnimation = false;
                model.animationType = ModelImporterAnimationType.None;
                model.importCameras = false;
                model.importLights = false;
                model.addCollider = false;
                model.SaveAndReimport();
            }
        }

        private static void ConfigureTexture(string path, System.Action<TextureImporter> configure)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
            {
                SimLog.Error(LogCategory.System, $"找不到贴图导入器：{path}");
                return;
            }
            configure(importer);
            importer.SaveAndReimport();
        }
    }
}
