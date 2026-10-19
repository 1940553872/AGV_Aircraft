using RivetSim.Core.Config;
using UnityEngine;

namespace RivetSim.Simulation.Robot
{
    /// <summary>
    /// 电量与通讯延迟（M8，教学简化模型，仅用于设备状态显示，不参与任务流程）：
    /// 电量按恒定功率线性放电——静止功耗按仿真时间累计，行驶附加功耗按累计行驶里程折算（以 AGV 最大速度换算行驶时间）；
    /// 通讯延迟取 CFG_Agv 固定值加平滑噪声抖动。v1 不做电源与通讯链路建模（Architecture §9）。
    /// </summary>
    public sealed class TeachingPowerCommModel
    {
        private const float SecondsPerHour = 3600f;
        private const float PercentScale = 100f;
        // 延迟抖动的噪声采样频率，单位 Hz。
        private const float JitterFrequencyHz = 0.7f;

        private readonly AgvConfig _config;
        private readonly float _noiseRow;

        /// <summary>创建；seed 决定抖动噪声的相位。</summary>
        public TeachingPowerCommModel(AgvConfig config, int seed)
        {
            _config = config;
            _noiseRow = (seed % 997) * 0.37f;
        }

        /// <summary>电量，单位 %。simTimeS 为仿真时间，odometerM 为累计行驶里程。</summary>
        public float BatterySocPct(double simTimeS, float odometerM)
        {
            float capacityWh = Mathf.Max(1f, _config.batteryVoltageV * _config.batteryCapacityAh);
            float driveTimeS = _config.maxSpeedMps > 0f ? odometerM / _config.maxSpeedMps : 0f;
            float usedWh = (_config.idlePowerW * (float)simTimeS + _config.drivePowerW * driveTimeS) / SecondsPerHour;
            return Mathf.Clamp(_config.batteryInitialSocPct - usedWh / capacityWh * PercentScale, 0f, PercentScale);
        }

        /// <summary>通讯延迟，单位 s。</summary>
        public float CommLatencyS(double simTimeS)
        {
            float n = Mathf.PerlinNoise((float)(simTimeS * JitterFrequencyHz), _noiseRow) * 2f - 1f;
            return Mathf.Max(0f, _config.commLatencyS + n * _config.commLatencyJitterS);
        }
    }
}
