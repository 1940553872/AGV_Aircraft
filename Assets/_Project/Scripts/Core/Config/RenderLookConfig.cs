using UnityEngine;

namespace RivetSim.Core.Config
{
    /// <summary>
    /// 画面观感配置（CFG_RenderLook，M7）：主光与柔和阴影、环境光、后处理（ACES、轻微 Bloom、Vignette）、演示区补光。
    /// 由场景生成器读取后写入场景与 VP_Main / PC_RPAsset，运行时不读取。
    /// </summary>
    [CreateAssetMenu(fileName = "CFG_RenderLook", menuName = "RivetSim/Config/Render Look")]
    public sealed class RenderLookConfig : ScriptableObject
    {
        [TextArea(2, 6)]
        [Tooltip("参数来源与修改说明")]
        public string notes = "M7 观感打磨：数值为目测调校结果，修改后重新执行 RivetSim/Generate/Main Teaching Hall Scene。";

        [Header("主光（方向光）")]
        [Tooltip("主光欧拉角，单位 °")]
        public Vector3 sunEulerDeg = new Vector3(50f, -65f, 0f);

        [Tooltip("主光强度")]
        [Min(0f)]
        public float sunIntensity = 1.6f;

        [Tooltip("主光颜色")]
        public Color sunColor = new Color(1f, 0.97f, 0.92f);

        [Tooltip("主光阴影强度（0～1）")]
        [Range(0f, 1f)]
        public float sunShadowStrength = 0.8f;

        [Header("阴影（写入 PC_RPAsset）")]
        [Tooltip("阴影距离，单位 m")]
        [Min(1f)]
        public float shadowDistanceM = 50f;

        [Tooltip("阴影级联数（1～4）")]
        [Range(1, 4)]
        public int shadowCascadeCount = 4;

        [Tooltip("启用柔和阴影")]
        public bool softShadows = true;

        [Tooltip("柔和阴影质量：1 低、2 中、3 高")]
        [Range(1, 3)]
        public int softShadowQuality = 2;

        [Header("环境光（三色）与相机背景")]
        public Color ambientSky = new Color(0.74f, 0.79f, 0.85f);
        public Color ambientEquator = new Color(0.46f, 0.50f, 0.56f);
        public Color ambientGround = new Color(0.22f, 0.24f, 0.27f);
        public Color cameraBackground = new Color(0.10f, 0.12f, 0.15f);

        [Tooltip("相机远裁剪面，单位 m")]
        [Min(1f)]
        public float cameraFarClipM = 200f;

        [Header("后处理（VP_Main）")]
        [Tooltip("Bloom 强度（轻微）")]
        [Min(0f)]
        public float bloomIntensity = 0.2f;

        [Tooltip("Bloom 阈值（线性亮度）")]
        [Min(0f)]
        public float bloomThreshold = 1.1f;

        [Tooltip("Bloom 扩散（0～1）")]
        [Range(0f, 1f)]
        public float bloomScatter = 0.6f;

        [Tooltip("Vignette 强度（0～1）")]
        [Range(0f, 1f)]
        public float vignetteIntensity = 0.2f;

        [Tooltip("Vignette 柔和度（0～1）")]
        [Range(0.01f, 1f)]
        public float vignetteSmoothness = 0.4f;

        [Header("演示区补光（相对演示台位姿：x 右、y 上、z 朝观众）")]
        [Tooltip("主补光（聚光灯）位置，单位 m")]
        public Vector3 demoKeyLightOffsetM = new Vector3(0.6f, 3.0f, 2.2f);

        [Tooltip("主补光照射点，单位 m")]
        public Vector3 demoKeyLightTargetM = new Vector3(0f, 0.8f, 0f);

        [Tooltip("主补光强度（URP 物理单位前的 Unity 强度）")]
        [Min(0f)]
        public float demoKeyLightIntensity = 28f;

        [Tooltip("主补光作用距离，单位 m")]
        [Min(0.1f)]
        public float demoKeyLightRangeM = 8f;

        [Tooltip("主补光外锥角，单位 °")]
        [Range(1f, 179f)]
        public float demoKeyLightSpotAngleDeg = 80f;

        [Tooltip("主补光颜色（偏暖白）")]
        public Color demoKeyLightColor = new Color(1f, 0.95f, 0.88f);

        [Tooltip("背景补光（点光，照亮大屏与背景墙）位置，单位 m")]
        public Vector3 demoFillLightOffsetM = new Vector3(0f, 2.6f, -1.6f);

        [Tooltip("背景补光强度")]
        [Min(0f)]
        public float demoFillLightIntensity = 4f;

        [Tooltip("背景补光作用距离，单位 m")]
        [Min(0.1f)]
        public float demoFillLightRangeM = 6f;

        [Tooltip("背景补光颜色（偏冷，与界面主色呼应）")]
        public Color demoFillLightColor = new Color(0.80f, 0.90f, 1f);

        [Header("曝光与场景明度（M8）")]
        [Tooltip("后处理曝光补偿（Color Adjustments.postExposure），单位 EV")]
        public float postExposureEv = 0.3f;

        [Tooltip("墙面目标平均颜色（波纹钢板贴图按此重新定标，保留纹理明暗）")]
        public Color wallTargetColor = new Color32(0xE0, 0xE3, 0xE8, 0xFF);

        [Tooltip("墙面金属度（喷涂钢板，取低值避免反射发暗）")]
        [Range(0f, 1f)]
        public float wallMetallic = 0.1f;

        [Tooltip("墙面光滑度")]
        [Range(0f, 1f)]
        public float wallSmoothness = 0.3f;

        [Tooltip("屋顶内侧（桁架与檩条）颜色")]
        public Color roofInnerColor = new Color32(0xD9, 0xDD, 0xE2, 0xFF);

        [Tooltip("演示台木纹色调（乘以橡木贴图）")]
        public Color deskWoodTint = new Color(1f, 1f, 1f);

        [Tooltip("演示台台面木纹平铺次数（台面长 × 深）")]
        public Vector2 deskWoodTiles = new Vector2(1f, 0.5f);
    }
}
