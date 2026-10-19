using RivetSim.Core.Config;
using RivetSim.Core.Diagnostics;
using RivetSim.Simulation.Robot;
using UnityEngine;

namespace RivetSim.Simulation.Navigation
{
    /// <summary>
    /// 2D 激光雷达：按 CFG_Agv 的线数、频率、量程与测距噪声，以底盘真值位姿从安装点水平发射射线（只检测 Environment / Aircraft / Obstacle 层），
    /// 每圈结果写入占据栅格（教学简化模型）。由启动入口按仿真步长调用 <see cref="Tick"/>，不读取 UnityEngine.Time。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Lidar2D : MonoBehaviour
    {
        [SerializeField, Tooltip("AGV 配置（雷达安装位姿、线数、频率、量程、噪声）")]
        private AgvConfig config;

        private AgvController _chassis;
        private OccupancyGridMap _map;
        private System.Random _random;
        private float _sinceScanS;

        /// <summary>最近一圈扫描（未扫描前为空）。</summary>
        public LidarScan LatestScan { get; private set; }

        /// <summary>是否写入地图（关闭时只扫描用于显示）。</summary>
        public bool MappingEnabled { get; set; } = true;

        /// <summary>编辑器生成器写入配置。</summary>
        public void Configure(AgvConfig agvConfig) => config = agvConfig;

        /// <summary>注入底盘（取真值位姿）、地图与随机种子。</summary>
        public void Initialize(AgvController chassis, OccupancyGridMap map, int seed)
        {
            _chassis = chassis;
            _map = map;
            _random = new System.Random(seed);
            LatestScan = new LidarScan(config.lidarBeamCount);
            _sinceScanS = float.MaxValue;
        }

        /// <summary>按仿真步长推进；到达扫描周期时扫描一圈。</summary>
        public void Tick(float dtS)
        {
            if (_chassis == null) return;
            _sinceScanS += dtS;
            if (_sinceScanS < 1f / config.lidarRateHz) return;
            _sinceScanS = 0f;
            Scan();
        }

        /// <summary>立即扫描一圈并写入地图。</summary>
        public void Scan()
        {
            GcProbe.Begin(ProbeModule.LidarScan);
            ScanCore();
            GcProbe.End(ProbeModule.LidarScan);
        }

        private void ScanCore()
        {
            PlanarPose pose = _chassis.Pose;
            Vector3 origin = pose.WorldPosition + pose.WorldRotation * config.lidarMount.positionM;
            var origin2 = new Vector2(origin.x, origin.z);
            LidarScan scan = LatestScan;
            int beams = scan.EndpointsM.Length;
            float stepDeg = 360f / beams;
            int hits = 0;
            for (int i = 0; i < beams; i++)
            {
                float yaw = (pose.yawDeg + config.lidarMount.eulerDeg.y + i * stepDeg) * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
                bool hit = Physics.Raycast(origin, dir, out RaycastHit info, config.lidarRangeM, SimLayers.LidarMask, QueryTriggerInteraction.Ignore);
                float range = hit ? info.distance + (float)(_random.NextDouble() * 2.0 - 1.0) * config.lidarRangeNoiseM : config.lidarRangeM;
                Vector2 end = origin2 + new Vector2(dir.x, dir.z) * Mathf.Max(0f, range);
                scan.EndpointsM[i] = end;
                scan.Hits[i] = hit;
                if (hit) hits++;
                if (MappingEnabled) _map?.IntegrateRay(origin2, end, hit);
            }
            scan.OriginM = origin;
            scan.HitCount = hits;
            scan.Sequence++;
            if (MappingEnabled) _map?.CommitScan();
        }
    }
}
