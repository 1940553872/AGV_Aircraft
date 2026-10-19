# HANDOFF（会话交接，≤ 80 行，每个里程碑结束时重写）

更新：2026-10-19，M7 联调与观感打磨完成（v1 交付，待验收）。下一个：等待验收；独立程序构建需用户单独指令。

## 现有接口（直接调用，不要重写）
- RobotRuntime（Presentation/App）：SimClock、EventBus、SafetyMonitor、地图、NavigationService、MappingRoutine、ArmController（`Rig.Tcp`）、ContactModel、RivetInspectionService；`Chassis`、`Lift`；`FastForwardSimSecondsPerFrame`。
- MissionRoutine：建图 → `MissionOrchestrator.RunAsync` → 返回充电位 → CSV。属性 `Conditions`、`Orchestrator`（`Matrix` / `Cursor`）、`OperatorPose`、`Trace`、`Evaluator`、`Registry`。
- 事件（Core/Mission）：`CellStateChangedEvent`、`ConditionCheckedEvent`、`PhaseEnteredEvent`、`LoopTakenEvent`；Simulation/Inspection：`RivetInspectedEvent`。演示界面只订阅这些事件。
- 故障注入：`ConditionBus.Inject(FaultKind)`（HUD 三个按钮与 F5 / F6 / F7 共用），`FaultInjected` 事件。
- t(1,8)：`OperatorPoseChannel`（`MissionServices.OperatorPose`）：执行器 `Open` → 界面 `Submit(PlanarPose)` 或 `AutoProvide` 超时由模拟操作员给出 → 写 `MissionBlackboard.InitialPose`。
- 单钉节拍：`MissionBlackboard.RivetCycleTimeS(now)` = 本颗用时 − `CycleExcludedS`（工位末颗收臂与降柱）。

## M6 演示界面（Presentation）
- `DemoConsoleView`（UI_DemoConsole.uxml/.uss）：顶栏、6×8 矩阵（`TaskCellElement`，状态 USS 类 `cell--<state>`）、`LoopArrowElement`、图例、阶段卡、执行情况说明 + 条件行、`MiniMapElement`、A 扫、底栏日志。
  平时用 `UI_PanelSettings_Console`（targetTexture = T_DemoConsoleRT，屏幕输入被屏蔽）；`SetFullscreen(true)` 换到 `UI_PanelSettings_ConsoleFullscreen`，可点单元弹说明卡（`ShowDetail`）。
  事件处理只改模型与脏标记，LateUpdate 刷新；空闲帧几乎不分配。
- `HudView`（UI_Hud.uxml/.uss）：模式、倍速（`SimClock.SpeedOptions`）、暂停、下一步、故障按钮、机位、可视化、全屏、t(1,8) 点选提示（`ShowPosePrompt` / `PosePicked`）、致谢角标（全屏时隐藏）。只发事件。
- `DemoUiHub`（App）：把 HUD / `DemoHotkeys` 连到 `DemoModeController`、`ConditionBus`、`CameraRig`、`VisualizationToggleInput`、演示屏全屏与初值通道。`NextHandler` 可替换（沙盒 = 推进假数据）。
- `DemoModeController`：Auto / Step（PhaseEntered 时暂停时钟，Next 继续）/ Manual（仅工序未运行时）；实现 `IDemoStatusSource`；`BindOperatorPose` 设置 `AutoProvide = (Mode == Auto)`。
- `CameraRig`（主相机）：CamAnchor_1～4；自动机位按 `CFG_UiTheme.phaseCameraPresets`，机位变化时先显示演示台 `demoStationHoldS`（实际时间）。
- 小地图源：`RuntimeMiniMapSource`（栅格贴图按 0.5 s 刷新、PathPlannedEvent、底盘位姿）；沙盒 `FakeMiniMapSource`。
- 入口：`AppBootstrap.SetUpDemoUi()`（工序开始时）；`AppBootstrap.Active` / `Rig` / `Hub` 供验证工具读取。Sandbox_UI：`SandboxUiBootstrap` + `FakeMissionFeed`（39 步，覆盖 8 种状态与两种回路）。

## M7 观感与验证
- 画面参数：`CFG_RenderLook`（`RenderLookConfig`）→ `SceneLightingSetup`（Sun、环境光、VP_Main、PC_RPAsset 阴影、`DemoLights` 补光）；改完重跑 Generate Main。
- CamAnchor_4 位姿、小地图 AGV 标记：`CFG_UiTheme.demoCameraOffsetM / demoCameraLookOffsetM / miniMapAgvMarkerSizePx`。大屏 5.70 × 3.21 m（CFG_SceneLayout）。
- 默认倍速只来自 `CFG_SimSettings.defaultSpeedMultiplier`（2 倍）；主场景 AppBootstrap 不再有倍速字段。
- GC 探针：`Core.Diagnostics.GcProbe`（`Enabled` 默认 false；`Begin/End(ProbeModule)` 不可嵌套）。已打点：Lidar2D.Scan、LidarPointCloudView、OccupancyGridOverlay、RuntimeMiniMapSource.Refresh、MiniMapElement 绘制。
- 验证：`RivetSim/Verify/Run M7 Demo`（`M7DemoVerifyRunner`）：自动演示（布局检查 → 4 个真实倍速观察窗口 + 截图，其余快进）→ 分步演示（快进，自动按“下一步”）→ Docs/Reports/M7.md（保留标记 `<!-- 以下为人工整理…-->` 之后的内容）。约 2.5 min。
- 布局检查共用 `UiLayoutCheck`（Begin / Check / End）。

## 编辑器
- 生成：`RivetSim/Generate/Demo UI Assets`（RT、M_DemoScreen、3 份 PanelSettings）；DemoStation 屏幕用 M_DemoScreen；MainSceneBuilder 末尾 `DemoUiGenerator.BuildMain`；SandboxSceneBuilder 的 Sandbox_UI 调 `BuildSandbox`。
- 验证：`RivetSim/Verify/Run Demo UI`（`DemoUiVerifyRunner`）→ Docs/Reports/M6.md。阶段写 SessionState `RivetSim.M6Verify.Stage`：shot_matrix → to_main → main → shot_monitor / shot_hud / shot_pose；截图后 eval `DemoUiVerifyRunner.Resume()`。
  编辑器失去焦点时 to_main 不会自动继续：eval `DemoUiVerifyRunner.LaunchMain()`。
- M5 验证 `RivetSim/Verify/Run Mission (Fast)` 仍可用。

## 关键约定
- 轴向与布局：ADR-008；模型机 1:1.5、厂房、升降柱与联锁：ADR-010；其余见 ADR-012、ADR-015、ADR-016。
- 参考时长（仿真，种子 42）：建图 316.5 s；矩阵流程约 1067～1076 s；返回 85.0 s；合计约 1470 s。
- 日志格式 `[T5][S4] t(5,4) 接触采集 C2～C8 开始 / 完成`；计数触发记 Info，故障 / 事件触发记 Warning。
- SimLog 订阅在进入 Play 时被清空，编辑器工具须在流程开始后订阅；沙盒 `SimLog.SimTimeProvider` 由 SandboxUiBootstrap 设置。
- 主场景物体数 986（Generate All 后，M7）。
- USS 颜色按 Architecture §7 写死在 UI_*.uss；Painter2D（虚线、回路、小地图）用 CFG_UiTheme 颜色。
- 子元素绘制在父元素内容之上：叠加 Painter2D 内容要放在独立子元素（见 MiniMapElement）。
- 快速流程下编辑器帧率很高，相机或动画等待要按实际时间，不按帧数。
- 任务矩阵 label 不改；t(6,1) 仅工位最后一颗执行；C1～C12 只上报不阻断。

## 遗留问题
- 操作员初值只写黑板与日志，导航仍用真值位姿；点选只给位置（朝向沿用当前估计）。
- C4 / C8 各有个别 ✗ 未逐颗定位原因。
- 第一次 Sandbox_UI 运行出现 2 条字体资产导入警告（动态图集新增字形），是否复现未确认。
- Editor 中各观察窗口都有单帧约 19 FPS 的偶发尖峰（平均 366 FPS），未在独立程序中复测。

## 下一步
- 等待 v1 验收。之后按需：Windows 独立程序构建（模板 M7 第 5 项，需单独指令）、v2 建议见 PROGRESS.md。
