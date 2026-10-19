using RivetSim.Core.Clock;
using RivetSim.Core.Config;
using RivetSim.Core.Events;
using RivetSim.Core.Logging;
using RivetSim.Core.TaskMatrix;
using RivetSim.Presentation.Input;
using RivetSim.Presentation.UI;
using RivetSim.Simulation.Mission;
using UnityEngine;

namespace RivetSim.Presentation.App
{
    /// <summary>
    /// Sandbox_UI 入口（M6）：加载任务矩阵，用 <see cref="FakeMissionFeed"/> 假数据驱动演示屏（全屏显示）与 HUD；
    /// HUD 的“下一步”（或 N 键）逐条推进假数据。
    /// </summary>
    public sealed class SandboxUiBootstrap : MonoBehaviour
    {
        [SerializeField] private SimSettings simSettings;
        [SerializeField] private InspectionConfig inspectionConfig;
        [SerializeField] private UiThemeConfig uiTheme;
        [SerializeField] private DemoConsoleView console;
        [SerializeField] private HudView hud;

        private SimClock _clock;
        private EventBus _bus;
        private ConditionBus _conditions;
        private DemoModeController _mode;
        private DemoUiHub _hub;
        private FakeMiniMapSource _map;

        /// <summary>当前沙盒入口（验证工具读取）。</summary>
        public static SandboxUiBootstrap Active { get; private set; }

        /// <summary>假数据。</summary>
        public FakeMissionFeed Feed { get; private set; }

        /// <summary>演示屏。</summary>
        public DemoConsoleView Console => console;

        /// <summary>HUD。</summary>
        public HudView Hud => hud;

        /// <summary>界面连线。</summary>
        public DemoUiHub Hub => _hub;

        /// <summary>编辑器生成器写入引用。</summary>
        public void Configure(SimSettings settings, InspectionConfig inspection, UiThemeConfig theme, DemoConsoleView consoleView, HudView hudView)
        {
            simSettings = settings;
            inspectionConfig = inspection;
            uiTheme = theme;
            console = consoleView;
            hud = hudView;
        }

        private void Awake()
        {
            Active = this;
            TaskMatrixLoadResult load = TaskMatrixLoader.LoadFromJson(simSettings.taskMatrixJson.text);
            if (!load.Success)
            {
                SimLog.Error(LogCategory.System, $"Sandbox_UI：任务矩阵加载失败：{string.Join("；", load.Errors)}");
                return;
            }
            _clock = new SimClock(simSettings.speedOptions, simSettings.defaultSpeedMultiplier);
            _bus = new EventBus();
            SimLog.SimTimeProvider = () => _clock.TimeS;
            _conditions = new ConditionBus(_bus);
            _map = new FakeMiniMapSource();
            Feed = new FakeMissionFeed(load.Data, _bus, _clock, inspectionConfig, _map);
            _mode = new DemoModeController(_clock, _bus, DemoMode.Auto)
            {
                RegionCountSource = () => FakeMissionFeed.RegionCount,
                StationCountSource = _ => FakeMissionFeed.StationsPerRegion,
                RivetCountSource = () => FakeMissionFeed.RivetsPerStation,
            };
            console.Initialize(_bus, load.Data, Feed.GetState, _mode, _map, inspectionConfig, uiTheme);
            hud.Initialize(_mode, _clock.SpeedOptions, _map, uiTheme);
            var hotkeys = gameObject.AddComponent<DemoHotkeys>();
            gameObject.AddComponent<FaultHotkeys>().Initialize(_conditions);
            _hub = new DemoUiHub(_mode, _bus, load.Data, hud, console, null, _conditions, Feed.OperatorPose, null, hotkeys, _map)
            {
                NextHandler = () => Feed.Step(),
            };
            _hub.SetFullscreen(true);
            SimLog.Info(LogCategory.System, $"Sandbox_UI：假数据 {Feed.StepCount} 步，点“下一步”或按 N 推进");
        }

        private void Update()
        {
            _clock?.Advance(Time.deltaTime);
            _map?.Refresh(Time.deltaTime);
        }

        private void OnDestroy()
        {
            if (Active == this) Active = null;
            _hub?.Dispose();
            _mode?.Dispose();
            _conditions?.Dispose();
        }
    }
}
