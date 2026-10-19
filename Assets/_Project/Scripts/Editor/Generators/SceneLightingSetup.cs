using RivetSim.Core.Config;
using RivetSim.Editor.Tools;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RivetSim.Editor.Generators
{
    /// <summary>
    /// 场景灯光、环境光、后处理与相机的共用设置（Architecture §7 3D 观感：柔和阴影、ACES、轻微 Bloom 与 Vignette）。
    /// 参数来自 CFG_RenderLook（M7），缺失时用配置类默认值。
    /// </summary>
    public static class SceneLightingSetup
    {
        /// <summary>演示区补光节点名（幂等：随场景重建）。</summary>
        public const string DemoLightsName = "DemoLights";

        private static RenderLookConfig _defaults;

        // 每次调用重新读取，场景重建时配置可能已重新加载。
        private static RenderLookConfig Look
        {
            get
            {
                var look = AssetDatabase.LoadAssetAtPath<RenderLookConfig>(ProjectPaths.RenderLookAsset);
                if (look != null) return look;
                if (_defaults == null) _defaults = ScriptableObject.CreateInstance<RenderLookConfig>();
                return _defaults;
            }
        }

        /// <summary>创建主方向光（柔和阴影），并把阴影设置写入 PC_RPAsset。</summary>
        public static Light CreateSun(Transform parent)
        {
            RenderLookConfig look = Look;
            Transform t = GeneratorUtility.CreateNode("Sun", parent, Vector3.zero, Quaternion.Euler(look.sunEulerDeg));
            var light = t.gameObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = look.sunIntensity;
            light.color = look.sunColor;
            light.shadows = look.softShadows ? LightShadows.Soft : LightShadows.Hard;
            light.shadowStrength = look.sunShadowStrength;
            ApplyShadowSettings(look);
            return light;
        }

        /// <summary>设置三色环境光，不使用天空盒。</summary>
        public static void ApplyAmbient()
        {
            RenderLookConfig look = Look;
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = look.ambientSky;
            RenderSettings.ambientEquatorColor = look.ambientEquator;
            RenderSettings.ambientGroundColor = look.ambientGround;
            RenderSettings.fog = false;
        }

        /// <summary>创建全局后处理 Volume。</summary>
        public static Volume CreateGlobalVolume(Transform parent)
        {
            Transform t = GeneratorUtility.CreateNode("Global Volume", parent, Vector3.zero);
            var volume = t.gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = EnsureVolumeProfile();
            return volume;
        }

        /// <summary>生成或重建 VP_Main（ACES 色调映射、轻微 Bloom、Vignette）。</summary>
        public static VolumeProfile EnsureVolumeProfile()
        {
            RenderLookConfig look = Look;
            string path = ProjectPaths.MainVolumeProfile;
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }
            foreach (VolumeComponent component in profile.components) Object.DestroyImmediate(component, true);
            profile.components.Clear();

            var tonemapping = AddComponent<Tonemapping>(profile);
            tonemapping.mode.Override(TonemappingMode.ACES);
            var bloom = AddComponent<Bloom>(profile);
            bloom.intensity.Override(look.bloomIntensity);
            bloom.threshold.Override(look.bloomThreshold);
            bloom.scatter.Override(look.bloomScatter);
            var vignette = AddComponent<Vignette>(profile);
            vignette.intensity.Override(look.vignetteIntensity);
            vignette.smoothness.Override(look.vignetteSmoothness);
            // M8：整体提亮（ACES 会压暗中间调），只调曝光补偿。
            var colorAdjustments = AddComponent<ColorAdjustments>(profile);
            colorAdjustments.postExposure.Override(look.postExposureEv);

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        /// <summary>
        /// 演示区补光（M7）：演示台前上方一盏暖白聚光灯照亮桌面与显示器，后方一盏冷色点光照亮大屏与背景；均不投射阴影。
        /// </summary>
        public static void CreateDemoLights(Transform parent, PlanarPose deskPose)
        {
            RenderLookConfig look = Look;
            Transform root = GeneratorUtility.CreateNode(DemoLightsName, parent, Vector3.zero);
            Vector3 origin = deskPose.WorldPosition;
            Quaternion rotation = deskPose.WorldRotation;

            Vector3 keyPos = origin + rotation * look.demoKeyLightOffsetM;
            Vector3 keyTarget = origin + rotation * look.demoKeyLightTargetM;
            Transform keyNode = GeneratorUtility.CreateNode("DemoKeyLight", root, keyPos, Quaternion.LookRotation(keyTarget - keyPos, Vector3.up));
            var key = keyNode.gameObject.AddComponent<Light>();
            key.type = LightType.Spot;
            key.intensity = look.demoKeyLightIntensity;
            key.range = look.demoKeyLightRangeM;
            key.spotAngle = look.demoKeyLightSpotAngleDeg;
            key.innerSpotAngle = look.demoKeyLightSpotAngleDeg * 0.5f;
            key.color = look.demoKeyLightColor;
            key.shadows = LightShadows.None;

            Transform fillNode = GeneratorUtility.CreateNode("DemoFillLight", root, origin + rotation * look.demoFillLightOffsetM);
            var fill = fillNode.gameObject.AddComponent<Light>();
            fill.type = LightType.Point;
            fill.intensity = look.demoFillLightIntensity;
            fill.range = look.demoFillLightRangeM;
            fill.color = look.demoFillLightColor;
            fill.shadows = LightShadows.None;
        }

        /// <summary>创建相机并朝向目标点；mainCamera 为 true 时打 MainCamera 标签。</summary>
        public static Camera CreateCamera(Transform parent, string name, Vector3 position, Vector3 lookAt, float fieldOfViewDeg,
            bool mainCamera)
        {
            Transform t = GeneratorUtility.CreateNode(name, parent, position, Quaternion.LookRotation(lookAt - position, Vector3.up));
            var camera = t.gameObject.AddComponent<Camera>();
            ConfigureCamera(camera, fieldOfViewDeg);
            if (mainCamera)
            {
                t.gameObject.tag = "MainCamera";
                t.gameObject.AddComponent<AudioListener>();
            }
            return camera;
        }

        /// <summary>设置相机背景、裁剪面、后处理与抗锯齿。</summary>
        public static void ConfigureCamera(Camera camera, float fieldOfViewDeg)
        {
            RenderLookConfig look = Look;
            camera.fieldOfView = fieldOfViewDeg;
            camera.farClipPlane = look.cameraFarClipM;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = look.cameraBackground;
            UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
        }

        // 阴影距离、级联与柔和阴影写入 PC_RPAsset（经 SerializedObject，只在值变化时写盘）。
        private static void ApplyShadowSettings(RenderLookConfig look)
        {
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(ProjectPaths.PcRenderPipelineAsset);
            if (asset == null) return;
            var so = new SerializedObject(asset);
            bool changed = SetFloat(so, "m_ShadowDistance", look.shadowDistanceM);
            changed |= SetInt(so, "m_ShadowCascadeCount", look.shadowCascadeCount);
            changed |= SetInt(so, "m_SoftShadowsSupported", look.softShadows ? 1 : 0);
            changed |= SetInt(so, "m_SoftShadowQuality", look.softShadowQuality);
            if (!changed) return;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
        }

        private static bool SetFloat(SerializedObject so, string name, float value)
        {
            SerializedProperty p = so.FindProperty(name);
            if (p == null || Mathf.Approximately(p.floatValue, value)) return false;
            p.floatValue = value;
            return true;
        }

        private static bool SetInt(SerializedObject so, string name, int value)
        {
            SerializedProperty p = so.FindProperty(name);
            if (p == null) return false;
            if (p.propertyType == SerializedPropertyType.Boolean)
            {
                if (p.boolValue == (value != 0)) return false;
                p.boolValue = value != 0;
                return true;
            }
            if (p.intValue == value) return false;
            p.intValue = value;
            return true;
        }

        private static T AddComponent<T>(VolumeProfile profile) where T : VolumeComponent
        {
            var component = profile.Add<T>(true);
            component.name = typeof(T).Name;
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }
    }
}
