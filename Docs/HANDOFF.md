# HANDOFF（会话交接，≤ 80 行，每个里程碑结束时重写）

更新：2026-10-19，v1.1 独立程序构建与复测完成，等待用户验收（Docs/Reports/Build_v1.1.md）。下一个：修复复测发现的问题（另开会话）或 v2。

## 现有接口（直接调用，不要重写）
- RobotRuntime（Presentation/App）：SimClock、EventBus、SafetyMonitor、地图、NavigationService、MappingRoutine、ArmController（`Rig.Tcp`）、ContactModel、RivetInspectionService；`Chassis`、`Lift`；`FastForwardSimSecondsPerFrame`。
- MissionRoutine：建图 → `MissionOrchestrator.RunAsync` → 返回充电位 → CSV。属性 `Conditions`、`Orchestrator`（`Matrix` / `Cursor`）、`OperatorPose`、`Trace`、`Evaluator`、`Registry`。
- 事件（Core/Mission）：`CellStateChangedEvent`、`ConditionCheckedEvent`、`PhaseEnteredEvent`、`LoopTakenEvent`；Simulation/Inspection：`RivetInspectedEvent`。演示界面只订阅这些事件与只读接口。
- 故障：`ConditionBus.Inject(FaultKind)` 只作内部接口（验证工具用，HUD 按钮与 F5～F7 已删）；`ConditionBus.AnomalyRaised(FaultKind, reason)` → HUD 3 s 提示“系统检测到异常：…，正在自动处理”。
- `RandomFaultScheduler.CreatePlan(IMissionPlan, seed, probability)`（Simulation/Mission）：每类故障每轮一次，种子 = `randomSeed + randomFaultSeedOffset`（CFG_SimSettings：`randomFaultsEnabled` / `randomFaultProbability` / `randomFaultSeedOffset`）；AppBootstrap `ScheduleRandomFaults()` 调用。
- t(1,8)：`OperatorPoseChannel`：`Open` → `Submit(PlanarPose)` 或 `AutoProvide` 超时由模拟操作员给出（Auto 模式自动给出）→ `MissionBlackboard.InitialPose`。
- 单钉节拍：`MissionBlackboard.RivetCycleTimeS(now)` = 本颗用时 − `CycleExcludedS`。
- 倍速：`SimClock.SpeedOptions` 来自 `CFG_SimSettings.speedOptions`（0.5 / 1 / 2 / 4 / 8），HUD 下拉框；`SpeedGuard(clock, settings).Tick(realDt, substeps, fastForward)` 按窗口统计帧率，×8 下低于 `speedGuardMinFps` 降到 ×4，`Triggered` 事件给 HUD 提示。
- 设备状态：`IDeviceStatusSource`（Presentation/UI：位姿、速度、升降柱、机械臂状态、联锁、电量、通讯延迟）← `RuntimeDeviceStatusSource`；电量 / 延迟来自 `TeachingPowerCommModel`（Simulation/Robot，教学简化模型，参数在 CFG_Agv）。
- 机位防遮挡：`IRivetFocusSource.TryGetFocus(out point, out normal)` ← `MissionRivetFocusSource`；CameraRig 从 T3 起按铆钉外法向取景（同高、±35° 等候选角），每 0.2 s（实际时间）SphereCast + 车体 / 机械臂有向包围盒检查，被挡切换并平滑过渡。
- 间隙：`VehicleClearanceMeter(vehicleRoot, excludeRoot, layerMask, searchMaxM).Measure(out nearest)`（车体包围盒表面采样到机身）；机械臂仍用 `ArmClearance`。

## 演示界面（Presentation）
- `DemoConsoleView`（UI_DemoConsole.uxml/.uss）：顶栏、6×8 矩阵（`TaskCellElement`，USS 类 `cell--<state>`）、`LoopArrowElement`、阶段卡、执行说明 + 条件行、`MiniMapElement`、A 扫、日志；第二页 dc-status-page = `InspectionStatusView`（不合格铆钉列表与统计 + AGV 状态），`ToggleView()` 切换。
- RT：左屏 / 大屏 T_DemoConsoleRT（M_DemoScreen，UI_PanelSettings_Console）；右屏 T_DemoStatusRT（M_DemoScreenStatus，UI_PanelSettings_ConsoleStatus）；全屏 UI_PanelSettings_ConsoleFullscreen（可点单元弹说明卡）。
- `DemoUiHub`：HUD / `DemoHotkeys` → `DemoModeController`、`CameraRig`、可视化、全屏、初值通道；B 键或 HUD 按钮：全屏时切页，否则换大屏材质（`IsBigScreenStatus`、`BigScreenViewHandler`）。
- `HudView`：模式、倍速下拉、暂停、下一步、机位、可视化、全屏、视图切换、t(1,8) 点选提示、异常提示、致谢角标。
- `DemoModeController`：Auto / Step / Manual；`CameraRig`：CamAnchor_1～4，自动机位按 `CFG_UiTheme.phaseCameraPresets`。
- 小地图源：`RuntimeMiniMapSource`（栅格 0.5 s 刷新、路径、底盘位姿、禁行区、工位、充电位、铆钉结果点）；沙盒 `FakeMiniMapSource`。颜色在 CFG_UiTheme（未知 #0B121B、空闲 #2C4663）。
- 入口：`AppBootstrap.SetUpDemoUi()`；`AppBootstrap.Active` / `Rig` / `Hub` 供验证工具读取。Sandbox_UI：`SandboxUiBootstrap` + `FakeMissionFeed`。

## 观感参数
- `CFG_RenderLook` → `SceneLightingSetup`（Sun 1.9、环境光、曝光 +0.3 EV、Bloom 阈值 1.3、演示台补光 22）；墙面 M_Wall 贴图定标到 `wallTargetColor` #E0E3E8，屋顶内侧 M_Roof_Inner #D9DDE2；演示屏材质基础色 ≈ 0.81 抵消曝光。改完重跑 Generate Main。
- 演示台：`CFG_SceneLayout.monitorScale` 1.4、桌面 2.00 × 0.80 × 0.80 m、屏幕中心 1.22 m；桌子 Poly Haven Oak Veneer 01（CC0）。
- `RivetSim/Config/Apply M8 Values`（`M8ConfigMigration`）把 M8 参数写入现有 CFG 资产（Create Default Configs 只补缺不覆盖）。
- GC 探针 `Core.Diagnostics.GcProbe`（默认关闭）。

## 编辑器与验证
- 生成：`RivetSim/Generate/Demo UI Assets`（RT、屏幕材质、4 份 PanelSettings）；MainSceneBuilder 末尾 `DemoUiGenerator.BuildMain`。
- 验证：`RivetSim/Verify/Run M8`（`M8VerifyRunner`，快进完整流程 + 随机故障 + ×8 窗口 + 强制降速检查 + 间隙 + 布局 + 遮挡 + 截图）→ Docs/Reports/M8.md，约 2 min。M7 `Run M7 Demo`、M6 `Run Demo UI`、M5 `Run Mission (Fast)` 仍可用。
- 构建：`RivetSim/Build/Windows v1.1`（`BuildV11`）→ Builds/v1.1/（不入库）。帧率复测：`AGV_Aircraft.exe -perfRun -screen-fullscreen 0 -screen-width 1920 -screen-height 1080 -popupwindow`，`BuildPerfProbe` 写 PerfLogs/summary.txt 后自动退出（约 5.5 min）。
- 布局检查共用 `UiLayoutCheck`（Begin / Check / End）。ThirdPartyImporter 跳过内容相同的文件（忽略换行差异）。

## 关键约定
- 轴向与布局 ADR-008；模型机 1:1.5、厂房、升降柱与联锁 ADR-010；其余 ADR-012、015、016、017。
- 参考时长（仿真，种子 42）：建图 316.3 s；矩阵流程约 1074 s（含 3 次随机故障）；返回 85.0 s；合计约 1476 s。
- 日志格式 `[T5][S4] t(5,4) … 开始 / 完成`；故障 / 事件与条件 ✗ 记 Warning（Console 警告属正常）。
- SimLog 订阅在进入 Play 时被清空，编辑器工具须在流程开始后订阅。
- 主场景物体数 987（Generate All 后，M8）；EditMode 测试 125 个。
- 矩阵显示文字为 M8 优化版（`label` / `name`），参考图原文在 `labelOriginal` / `nameOriginal`（Docs/Reference/SOP_TaskMatrix_v1.md）；label 再改须用户同意。
- USS 颜色写死在 UI_*.uss；Painter2D 用 CFG_UiTheme 颜色；叠加绘制放独立子元素。
- 快速流程下帧率很高，相机或动画等待按实际时间，不按帧数。

## 遗留问题
- 操作员初值只写黑板与日志，导航仍用真值位姿；点选只给位置。
- C4 / C6 / C8 有个别 ✗（M8 583 次检查中 4 次），未逐颗定位原因。
- 机械臂末端连杆到机身最小 148 mm、探头末端 73 mm；v1 只检查不规划。
- 独立程序（本机）×8 平均 398 FPS、保护 0 次；t(1,8) / t(1,1) / t(6,1) 切换处偶发 40～46 ms 尖峰，原因未定位。
- 独立程序矩阵流程 974.0 s，编辑器快进 1074.2 s（相同故障计划），相差 100 s，原因未查。

## 下一步
- 修复：矩阵流程用时差 100 s、阶段切换尖峰（Development Build + Profiler）；分发前删除 Burst 调试目录；在目标机按上述命令复测。
- v2 候选（PROGRESS.md “v2 建议”）：初值参与定位与点选朝向、C4 / C8 逐颗定位、§6.1 参数按选型替换、演示屏 RT 提到 2560×1440、§9 后续内容。
