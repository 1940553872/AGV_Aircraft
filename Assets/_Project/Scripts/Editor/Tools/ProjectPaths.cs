namespace RivetSim.Editor.Tools
{
    /// <summary>
    /// 编辑器工具共用的工程路径（Architecture §2）。
    /// </summary>
    public static class ProjectPaths
    {
        /// <summary>项目内容根目录。</summary>
        public const string ProjectRoot = "Assets/_Project";

        /// <summary>配置资产目录。</summary>
        public const string ConfigFolder = ProjectRoot + "/Config";

        /// <summary>SOP 任务矩阵 JSON。</summary>
        public const string TaskMatrixJson = ConfigFolder + "/TaskMatrix/SOP_TaskMatrix_v1.json";

        /// <summary>仿真全局设置资产。</summary>
        public const string SimSettingsAsset = ConfigFolder + "/CFG_SimSettings.asset";

        /// <summary>
        /// Architecture §2 规定的 _Project 子目录（相对 Assets/_Project）。
        /// </summary>
        public static readonly string[] ProjectFolders =
        {
            "Config/TaskMatrix",
            "Art/Materials",
            "Art/Textures",
            "Art/Models/Generated",
            "Prefabs/Environment",
            "Prefabs/Robot",
            "Prefabs/Aircraft",
            "Prefabs/DemoStation",
            "Prefabs/Vis",
            "Scenes/Sandbox",
            "Scripts/Core",
            "Scripts/Simulation/World",
            "Scripts/Simulation/Robot",
            "Scripts/Simulation/Navigation",
            "Scripts/Simulation/Inspection",
            "Scripts/Simulation/Mission",
            "Scripts/Presentation/App",
            "Scripts/Presentation/UI",
            "Scripts/Presentation/Visualization",
            "Scripts/Presentation/CameraControl",
            "Scripts/Presentation/Input",
            "Scripts/Editor/Generators",
            "Scripts/Editor/Tools",
            "Scripts/Tests/EditMode",
            "UI",
        };
    }
}
