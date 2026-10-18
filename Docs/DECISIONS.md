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

## ADR-006 显式依赖 Newtonsoft Json（2026-10-18）
任务矩阵读取使用 Newtonsoft Json。此前它只是 `com.unity.pipeline`（实验版）带入的间接依赖，pipeline 升级或移除时可能随之消失，因此在 `Packages/manifest.json` 中显式加入 `com.unity.nuget.newtonsoft-json` 3.2.1（与当时锁定版本相同）。`RivetSim.Core` 与 `RivetSim.Tests.EditMode` 通过 `precompiledReferences` 引用 `Newtonsoft.Json.dll`。

## ADR-007 配置资产生成只补缺、不覆盖（2026-10-18）
CLAUDE.md 要求生成器重复执行时覆盖上一次的结果。该规则适用于场景、预制体、材质等**生成物**；CFG_* 配置资产是**人工调参的数据源**，覆盖会丢失调整。因此 `RivetSim/Setup/Create Default Configs` 只创建缺少的 CFG 资产，已有资产保持不变；默认值写在配置类的字段初始值中（来自 Architecture §6.1 / §7），需要恢复默认时手动删除对应资产后重新执行菜单。

## ADR-008 尺寸向量轴约定与布局坐标（2026-10-18）
配置中的尺寸向量一律按（长, 宽, 高）存放在 (x, y, z) 分量，转 Unity 缩放时由 `RivetSim.Core.Config.SizeAxes` 统一换算：大厅与演示台这类“长沿左右”的物体用 `LengthAlongX`（X = 长，Y = 高，Z = 宽）；AGV 车体与车载设备盒这类“长沿前后”的物体用 `LengthAlongZ`（X = 宽，Y = 高，Z = 长，前进方向为本地 +Z）。大厅长边沿世界 X，演示区在 −X 一端；`ZoneRect` / `PlanarPose` / 路线点的 x = 世界 X、y = 世界 Z；偏航角 0° 指向 +Z、顺时针为正，与 `Quaternion.Euler(0, yaw, 0)` 一致。M1 布局取值经用户确认后写入 CFG_SceneLayout（见 Architecture §4）。

## ADR-009 主场景暂放 AGV 预览实例；生成器外观尺寸用命名常量（2026-10-18）
Architecture §4 规定 `Robot` 根节点在运行时生成 AGV。M1 为了布局验收，在起始位放一台 `AGV_Preview`（PF_AGV 实例），M3 / M5 改为由 AppBootstrap 运行时生成后删除。生成器中只影响外观的尺寸（法兰直径、灯带、立柱间距、墙厚、占位模型机的机翼弦长等）以命名常量写在编辑器生成器内，不进入 CFG；功能参数（车体、轮距、雷达与机械臂安装位姿、设备盒、工具长度、大厅、演示台、显示器）一律从 CFG 读取。后处理配置资产使用新前缀 `VP_`（VP_Main）。地面中文文字用 Noto Sans SC Bold 动态字形烘焙成贴图（T_ZoneLabel_*），不引入 TextMeshPro。
