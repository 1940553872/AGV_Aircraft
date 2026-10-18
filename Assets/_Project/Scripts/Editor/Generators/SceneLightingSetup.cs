using RivetSim.Editor.Tools;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RivetSim.Editor.Generators
{
    /// <summary>
    /// 场景灯光、环境光、后处理与相机的共用设置（Architecture §7 3D 观感：柔和阴影、ACES、轻微 Bloom 与 Vignette）。
    /// </summary>
    public static class SceneLightingSetup
    {
        // 灯光与后处理外观参数。
        private static readonly Vector3 SunEulerDeg = new Vector3(50f, -65f, 0f);
        private const float SunIntensity = 1.6f;
        private static readonly Color SunColor = new Color(1f, 0.97f, 0.92f);
        private static readonly Color AmbientSky = new Color(0.74f, 0.79f, 0.85f);
        private static readonly Color AmbientEquator = new Color(0.46f, 0.50f, 0.56f);
        private static readonly Color AmbientGround = new Color(0.22f, 0.24f, 0.27f);
        private static readonly Color CameraBackground = new Color(0.10f, 0.12f, 0.15f);
        private const float BloomIntensity = 0.25f;
        private const float BloomThreshold = 1.0f;
        private const float VignetteIntensity = 0.22f;
        private const float CameraFarClipM = 200f;

        /// <summary>创建主方向光（柔和阴影）。</summary>
        public static Light CreateSun(Transform parent)
        {
            Transform t = GeneratorUtility.CreateNode("Sun", parent, Vector3.zero, Quaternion.Euler(SunEulerDeg));
            var light = t.gameObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = SunIntensity;
            light.color = SunColor;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.85f;
            return light;
        }

        /// <summary>设置三色环境光，不使用天空盒。</summary>
        public static void ApplyAmbient()
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = AmbientSky;
            RenderSettings.ambientEquatorColor = AmbientEquator;
            RenderSettings.ambientGroundColor = AmbientGround;
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
            bloom.intensity.Override(BloomIntensity);
            bloom.threshold.Override(BloomThreshold);
            var vignette = AddComponent<Vignette>(profile);
            vignette.intensity.Override(VignetteIntensity);
            vignette.smoothness.Override(0.4f);

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
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
            camera.fieldOfView = fieldOfViewDeg;
            camera.farClipPlane = CameraFarClipM;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = CameraBackground;
            UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
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
