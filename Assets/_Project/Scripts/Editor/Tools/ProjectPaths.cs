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

        /// <summary>场景布局配置资产。</summary>
        public const string SceneLayoutAsset = ConfigFolder + "/CFG_SceneLayout.asset";

        /// <summary>AGV 配置资产。</summary>
        public const string AgvAsset = ConfigFolder + "/CFG_Agv.asset";

        /// <summary>机械臂配置资产。</summary>
        public const string ArmAsset = ConfigFolder + "/CFG_Arm.asset";

        /// <summary>检测配置资产。</summary>
        public const string InspectionAsset = ConfigFolder + "/CFG_Inspection.asset";

        /// <summary>导航配置资产（M3）。</summary>
        public const string NavigationAsset = ConfigFolder + "/CFG_Navigation.asset";

        /// <summary>M3 导航叠加信息 UXML。</summary>
        public const string NavOverlayUxml = ProjectRoot + "/UI/UI_NavOverlay.uxml";

        /// <summary>M4 检测叠加信息 UXML。</summary>
        public const string InspectionOverlayUxml = ProjectRoot + "/UI/UI_InspectionOverlay.uxml";

        /// <summary>生成网格目录。</summary>
        public const string GeneratedModelsFolder = ProjectRoot + "/Art/Models/Generated";

        /// <summary>生成材质目录。</summary>
        public const string MaterialsFolder = ProjectRoot + "/Art/Materials";

        /// <summary>生成贴图目录。</summary>
        public const string TexturesFolder = ProjectRoot + "/Art/Textures";

        /// <summary>后处理 Volume Profile。</summary>
        public const string MainVolumeProfile = ProjectRoot + "/Art/VP_Main.asset";

        /// <summary>环境道具预制体目录。</summary>
        public const string PrefabsEnvironment = ProjectRoot + "/Prefabs/Environment";

        /// <summary>机器人预制体目录。</summary>
        public const string PrefabsRobot = ProjectRoot + "/Prefabs/Robot";

        /// <summary>模型机预制体目录。</summary>
        public const string PrefabsAircraft = ProjectRoot + "/Prefabs/Aircraft";

        /// <summary>演示台预制体目录。</summary>
        public const string PrefabsDemoStation = ProjectRoot + "/Prefabs/DemoStation";

        /// <summary>主场景。</summary>
        public const string MainScene = ProjectRoot + "/Scenes/Main_TeachingHall.unity";

        /// <summary>机器人沙盒场景。</summary>
        public const string SandboxRobotScene = ProjectRoot + "/Scenes/Sandbox/Sandbox_Robot.unity";

        /// <summary>UI 沙盒场景。</summary>
        public const string SandboxUiScene = ProjectRoot + "/Scenes/Sandbox/Sandbox_UI.unity";

        /// <summary>UI 资源目录。</summary>
        public const string UiFolder = ProjectRoot + "/UI";

        /// <summary>中文粗体字体（地面区域文字烘焙用）。</summary>
        public const string NotoSansScBold = "Assets/ThirdParty/Fonts/NotoSansSC/NotoSansSC-Bold.ttf";

        /// <summary>验收截图目录（工程根目录下，相对路径）。</summary>
        public const string ScreenshotsFolder = "Docs/Screenshots";

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
