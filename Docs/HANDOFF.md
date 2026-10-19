# HANDOFF（会话交接，≤ 80 行，每个里程碑结束时重写）

更新：2026-10-19，M5 实现完成、待用户验收。下一个：M6 演示界面。

## 现有接口（执行器直接调用，不要重写）
- RobotRuntime（Presentation/App）：SimClock、EventBus、SafetyMonitor、地图、NavigationService、MappingRoutine、ArmController、ContactModel、RivetInspectionService；`Chassis`、`Lift`；`FastForwardSimSecondsPerFrame`（Verify 用）。
- RivetInspectionService：T3 `MoveToObserveAsync` / `RegisterDatumAsync` / `RemeasureDatumAsync`；T4 `MoveToPredictedAsync` / `MeasureRivetCenterAsync` / `CorrectToMeasuredAsync`；
  T5 `ApproachContactAsync` / `AcquireAsync`（→ `LastAcquisition`）/ `JudgeLastAcquisition` / `CommitRecord`；T6 `RetreatToDistanceAsync`；`StowAsync`；M4 组合方法保留。
- 入口：AppBootstrap 按 `CFG_SimSettings.missionEntry`：Mission（默认）→ `MissionRoutine`；SubsystemDemo → 旧 NavigationDemoRoutine（不再维护）。
- MissionRoutine：建图 → `MissionOrchestrator.RunAsync` → 返回充电位（前方 1.5 m + 倒车）→ CSV（persistentDataPath/Reports）。属性 `Conditions`、`Orchestrator`、`Trace`、`Evaluator`、`Registry`。
- MissionOrchestrator（Simulation/Mission）：按 JSON 阶段 / runWhen / trigger / retryPhase / loops 执行；`Cursor`、`Matrix`（TaskMatrixRuntime）、`Validate()`、`RivetUnrecovered` 钩子。
- 执行器：`Simulation/Mission/Executors/` 下 Nav / Vision / Arm / Acq / Ut / Safety / Sched / Data 八个注册类，`MissionExecutorCatalog.RegisterAll`；共享状态 `MissionBlackboard`（工位级 / 铆钉级 / 阶段级自动清零）。
- 阶段内依赖：`PhaseDependencyTable`（代码常量，Architecture §5.1 有说明）。
- 事件（Core/Mission）：`CellStateChangedEvent`（cell / actor / phase / 状态 / 区域工位铆钉 / 时间 / 说明）、`ConditionCheckedEvent`（cellId、conditionId、passed、value、valueText）、`PhaseEnteredEvent`（含 Attempt）、`LoopTakenEvent`。
- 故障注入：`ConditionBus.Inject(FaultKind)`（F5 识别失败 / F6 力超限 / F7 采集无效；M6 HUD 按钮调用同一接口），只布设一次性物理故障，真实触发来自仿真结果。
- 设备发光：`DeviceIndicator`（Presentation/Visualization）在 PF_AGV（a1～a7）与 PF_DemoStation（a8）上，由 `DeviceIndicatorGenerator` 生成，AppBootstrap 启动时 `FindObjectsByType` 后订阅。
- 验证：`RivetSim/Verify/Run Mission (Fast)`（`MissionVerifyRunner`）→ Docs/Reports/M5.md；截图时刻暂停并写 SessionState `RivetSim.M5Verify.Stage`（shot1 / shot2），外部 capture 后 eval `MissionVerifyRunner.Resume()` / `Finish()`。

## 关键约定
- 轴向与布局：ADR-008；模型机 1:1.5、厂房、升降柱与联锁：ADR-010；其余见 ADR-012、ADR-015、ADR-016。
- 参考时长（仿真，种子 42）：建图 316.5 s；矩阵流程 1081.8 s；返回 82.8 s；合计 1481 s（4 倍速约 6.2 min）。
- 日志格式 `[T5][S4] t(5,4) 接触采集 C2～C8 开始 / 完成`；计数触发记 Info，故障 / 事件触发记 Warning。
- 纯视觉物体名称前缀 `SimLayers.VisualOnlyPrefix`，外廓量测与间隙检查跳过。
- SimLog 订阅在进入 Play（SubsystemRegistration）时被清空，编辑器工具须在流程开始后订阅。
- 主场景物体数 983（Generate All 后）。
- 任务矩阵 label 不改；t(6,1) 仅工位最后一颗执行；C1～C12 只上报不阻断。

## 遗留问题
- 工位末颗“单钉节拍”含收臂降柱（约 40 s）。
- C4 / C8 各有个别 ✗ 未逐颗定位原因。
- 识别失败为教学简化，操作员初值只写日志。

## 下一步
- 用户验收 M5（截图 Docs/Screenshots/M5/01、02 与 Docs/Reports/M5.md）。
- M6：演示界面订阅上述事件；t(1,8) 改为小地图点选；HUD 故障按钮。
