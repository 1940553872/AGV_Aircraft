# HANDOFF（会话交接，≤ 80 行，每个里程碑结束时重写）

更新：2026-10-19，M4 验收后（main = aba7cec）。下一个：M5 SOP 流程编排。

## 现有接口（执行器直接调用，不要重写）
- RobotRuntime（Presentation/App）：组装 SimClock、EventBus、SafetyMonitor（规则 1～3）、地图、NavigationService、MappingRoutine、ArmController、ContactModel、RivetInspectionService；固定子步 0.01 s 推进，控制器内不读 UnityEngine.Time。
- NavigationService：NavigateToAsync、DockAsync、ReverseExitAsync、DriveStraightAsync、PreDockPose。
- LiftColumnController：MoveToHeightAsync、LowerToBottomAsync；工位高度由 LiftHeightPlanner 计算。
- AgvController：ZeroSpeedLock、ReleaseZeroSpeedLock。
- RivetInspectionService：RegisterStationAsync（T3，经 Ready 到 Observe，左侧镜像）、CenterOnRivetAsync（T4）、ContactAndAcquireAsync（T5）、ReleaseAsync（T6）、StowAsync（Observe → Ready → Stow）、InspectRivetAsync（T4～T6 合一，仅 M4 演示）；每颗完成发布 RivetInspectedEvent；记录在 InspectionRecordStore（CSV 导出未实现）。
- 力超限：ArmController 逼近时发布 ContactForceExceededEvent；注入方式 ContactModel.SurfaceOffsetFaultM。
- 采集无效：记录 Verdict = Invalid（耦合质量 < 0.5，或接触力不在 8～12 N）。
- 识别失败：DockingController 或 WristVision 看不到标记时动作返回失败。
- TaskMatrixData.GetExecutorKeys()：JSON 共 37 个执行器键名（前缀 nav / chassis / vision / arm / acq / ut / safety / sched / data）。
- 当前入口：AppBootstrap 运行 NavigationDemoRoutine（建图 → 导航 → R1-S1 停靠 → StationInspectionRoutine → 联锁演示 → 倒车）。

## 关键约定
- 轴向与布局：ADR-008；模型机 1:1.5、厂房 64 × 44 × 12 m、升降柱与联锁：ADR-010；其余见 ADR-012、ADR-015、ADR-016。
- 参考时长（仿真时间）：建图 316 s；单工位约 99 s。
- 任务矩阵 label 不改；t(6,1) 仅工位最后一颗执行；C1～C12 为固定文字 + 教学示例阈值（JSON conditions）。

## 遗留问题
- 见 PROGRESS.md 中标为 M5 的待办。

## 下一步
- M5：用 MissionOrchestrator 取代 NavigationDemoRoutine（已定决策见 M5 启动提示词）。
