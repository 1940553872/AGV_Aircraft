# 架构决策记录

## ADR-001 v1 只做“基础完整 + 界面美观”（2026-10-17）
硬件方案量化对比、电源 / 通讯建模、真实 SLAM、ROS 2 桥接推迟到后续版本（Architecture §9）。

## ADR-002 底盘与机械臂采用运动学驱动（2026-10-17）
底盘：运动学刚体 + 差速运动学，不使用 WheelCollider。机械臂：由 UR5e yaml 搭建运动学骨架，不使用 ArticulationBody，也不依赖 URDF Importer（v0.5.2 版本较旧，与 Unity 6 的兼容性有风险）。接触力使用虚拟弹簧（教学简化模型）。

## ADR-003 SOP 任务矩阵用 JSON 数据驱动（2026-10-17）
矩阵内容以用户提供的 6×8 矩阵为准，存放在 Config/TaskMatrix/SOP_TaskMatrix_v1.json；代码只实现执行语义与执行器，不写死流程。

## ADR-004 演示屏用 UI Toolkit + RenderTexture（2026-10-17）
Unity 6.0 没有 UI Toolkit 世界空间面板，因此用 PanelSettings.targetTexture 渲染到显示器，全屏时复用同一份 UXML。USS 不支持虚线边框，用 Painter2D 自定义元素绘制。

## ADR-005 场景与资产只通过 Editor 操作生成（2026-10-17）
通过 Unity CLI + pipeline 调用编辑器脚本生成场景、预制体和材质，不手工编辑 YAML，生成器保持幂等。
