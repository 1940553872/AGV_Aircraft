using RivetSim.Core.Config;
using RivetSim.Simulation.Navigation;
using RivetSim.Simulation.World;
using UnityEngine;

namespace RivetSim.Simulation.Inspection
{
    /// <summary>
    /// 腕部相机简化视觉（主体 S2，教学简化模型，不运行图像算法）：目标在视场（60° × 45°）、工作距离（0.15～0.40 m）内，
    /// 正面朝向相机且入射角不超过上限即视为识别成功（复用 <see cref="MarkerVisibility"/>，不检查遮挡）；
    /// 识别结果 = 真值 + 高斯噪声（钉心复测在蒙皮切平面内加噪声，基准注册加位置噪声与绕法向的转角噪声）。参数取自 CFG_Arm。
    /// </summary>
    public sealed class WristVision
    {
        private readonly ArmConfig _config;
        private readonly System.Random _random;

        /// <summary>创建视觉模型；random 为噪声随机源（固定种子）。</summary>
        public WristVision(ArmConfig config, System.Random random)
        {
            _config = config;
            _random = random;
        }

        /// <summary>判定相机能否看到 targetM 处、法向 normal 的目标。</summary>
        public bool CanSee(Transform camera, Vector3 targetM, Vector3 normal)
        {
            Vector2 range = _config.wristCameraWorkingDistanceM;
            if (!MarkerVisibility.IsVisible(camera.position, camera.rotation, _config.wristCameraFovDeg, range.x, range.y, targetM, normal)) return false;
            return Vector3.Angle(camera.position - targetM, normal) <= _config.visionMaxIncidenceDeg;
        }

        /// <summary>钉心复测：可见时输出含噪声的钉心位置（世界坐标）。</summary>
        public bool TryMeasureRivet(Transform camera, Rivet rivet, out Vector3 measuredM)
        {
            measuredM = rivet.PositionM;
            if (!CanSee(camera, rivet.PositionM, rivet.Normal)) return false;
            Vector3 n = rivet.Normal;
            Vector3 u = Vector3.Cross(n, Vector3.up);
            if (u.sqrMagnitude < 1e-6f) u = Vector3.Cross(n, Vector3.forward);
            u.Normalize();
            Vector3 v = Vector3.Cross(n, u);
            float sigma = _config.rivetCenterNoiseSigmaM;
            measuredM += u * GaussianNoise.Next(_random, sigma) + v * GaussianNoise.Next(_random, sigma);
            return true;
        }

        /// <summary>基准注册：可见时输出含噪声的标记位姿（位置为标记中心，旋转为标记节点朝向，世界坐标）。</summary>
        public bool TryRegisterDatum(Transform camera, RegionMarker datum, out Pose estimated)
        {
            Vector3 center = datum.TagCenterWorld;
            Quaternion rotation = datum.transform.rotation;
            estimated = new Pose(center, rotation);
            if (!CanSee(camera, center, datum.NormalWorld)) return false;
            float sigma = _config.datumPositionNoiseSigmaM;
            Vector3 noise = new Vector3(GaussianNoise.Next(_random, sigma), GaussianNoise.Next(_random, sigma), GaussianNoise.Next(_random, sigma));
            Quaternion spin = Quaternion.AngleAxis(GaussianNoise.Next(_random, _config.datumRotationNoiseSigmaDeg), datum.NormalWorld);
            estimated = new Pose(center + noise, spin * rotation);
            return true;
        }
    }
}
