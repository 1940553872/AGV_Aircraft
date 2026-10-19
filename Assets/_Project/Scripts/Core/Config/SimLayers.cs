namespace RivetSim.Core.Config
{
    /// <summary>
    /// 项目自定义 Layer 的名称与编号（Architecture §4）。激光射线只检测 Environment、Aircraft、Obstacle 三层。
    /// </summary>
    public static class SimLayers
    {
        /// <summary>厂房、地面、墙体。</summary>
        public const string Environment = "Environment";
        /// <summary>模型机。</summary>
        public const string Aircraft = "Aircraft";
        /// <summary>障碍物。</summary>
        public const string Obstacle = "Obstacle";
        /// <summary>AGV 与机械臂。</summary>
        public const string Robot = "Robot";
        /// <summary>可视化叠加（按 V 开关）。</summary>
        public const string Visualization = "Visualization";

        /// <summary>Environment 层编号。</summary>
        public const int EnvironmentIndex = 8;
        /// <summary>Aircraft 层编号。</summary>
        public const int AircraftIndex = 9;
        /// <summary>Obstacle 层编号。</summary>
        public const int ObstacleIndex = 10;
        /// <summary>Robot 层编号。</summary>
        public const int RobotIndex = 11;
        /// <summary>Visualization 层编号。</summary>
        public const int VisualizationIndex = 12;

        /// <summary>纯视觉物体（设备发光外壳等）的名称前缀：外廓量测与连杆间隙检查跳过这些渲染器。</summary>
        public const string VisualOnlyPrefix = "VisualOnly_";

        /// <summary>激光雷达检测的层掩码（Environment | Aircraft | Obstacle）。</summary>
        public const int LidarMask = (1 << EnvironmentIndex) | (1 << AircraftIndex) | (1 << ObstacleIndex);
    }
}
