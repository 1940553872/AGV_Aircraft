using RivetSim.Core.Logging;
using RivetSim.Editor.Importers;
using UnityEditor;
using UnityEngine;

namespace RivetSim.Editor.Generators
{
    /// <summary>
    /// AprilTag 标记材质：每个 ID 一个 M_AprilTag_xx（URP/Lit，贴 tag36h11 原图，点采样导入设置见 ThirdPartyImporter）。
    /// </summary>
    public static class AprilTagMaterialBuilder
    {
        private const float TagSmoothness = 0.1f;

        /// <summary>材质名。</summary>
        public static string MaterialName(int tagId) => $"M_AprilTag_{tagId:00}";

        /// <summary>读取或创建指定 ID 的标记材质；缺少贴图时返回 null。</summary>
        public static Material Get(int tagId)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ThirdPartyImporter.AprilTagFolder}/{ThirdPartyImporter.AprilTagFileName(tagId)}");
            if (texture == null)
            {
                SimLog.Error(LogCategory.System, $"缺少 AprilTag 贴图 ID {tagId}，请先执行 RivetSim/Import/Third Party Assets。");
                return null;
            }
            Material material = MaterialLibraryBuilder.EnsureLitMaterial(MaterialName(tagId));
            MaterialLibraryBuilder.ApplyColor(material, Color.white, TagSmoothness, 0f, Color.black);
            material.SetTexture("_BaseMap", texture);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
