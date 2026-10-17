# CLAUDE.md — RivetSim（AGV 飞机铆钉超声检测示教仿真）

每次会话开始时自动读取。这里只列强制规则，详细设计见 `Docs/Architecture.md`。
开始任务前先读 `Docs/PROGRESS.md`，了解当前里程碑与遗留问题。

## 当前版本目标：v1（基础完整 + 演示界面好看）

- 三区场景齐全：作业区（模型机 + 检测区铆钉）、移动区（AGV 建图 / 规划 / 移动）、演示区（演示台 + 电脑屏幕）。
- 整套工序能自动跑通：建图准备 → SOP 任务矩阵 T1～T6（含逐钉循环、换站、条件触发单元）。
- 演示电脑屏幕显示 SOP 任务矩阵与当前执行情况说明，视觉效果清晰美观。
- **v1 不做**：多硬件方案对比、电源 / 通讯链路建模、真实 SLAM 扫描匹配、动力学仿真、ROS 2 桥接。这些放到 `Docs/Architecture.md` §9 的后续版本，不要提前实现。

## 本机环境（已核实，不要更改）

- Unity Editor 6000.0.71f1（Unity 6.0 LTS），安装位置 `D:\Unity 6000.0.71f1`
- 工程路径 `D:\Unity_Project\AGV_Aircraft`
- 渲染：URP 17.0.4（PC_RPAsset / PC_Renderer）。输入：Input System 1.19（只启用新输入系统，`OnMouseDown` 无效，点击需用射线检测）
- 已有包：AI Navigation 2.0.11、Test Framework、Timeline、uGUI、com.unity.pipeline 0.8.0-exp.1（会带入 Newtonsoft Json）
- Unity CLI 1.0.0-beta.13：`C:\Users\19405\AppData\Local\Unity\bin\unity.exe`。旧终端若找不到 `unity`，用此完整路径。

## 与 Unity Editor 的交互方式（强制）

1. 开始前先运行 `unity status`，确认状态为 ready；未连接时报告原因，不要重装或改配置。
2. 场景、预制体、材质一律**通过 Editor 操作生成**：写编辑器脚本（`Assets/_Project/Scripts/Editor/`，带 `[MenuItem("RivetSim/...")]`），再通过 `unity command` 调用 Editor（菜单执行 / scripts eval）。**禁止手工编辑 `.unity`、`.prefab`、`.asset` 的 YAML 文本。**
3. 生成器必须幂等：重复执行时覆盖上一次的生成结果，不产生重复对象。
4. 修改脚本后等待编译完成，并检查 Console：错误为 0，且不新增警告。
5. 切换或修改场景前确认没有未保存改动；完成后保存场景，并退出 Play 模式。
6. 可视结果要截取 Game 视图图片自检；交给我验收时说明截图对应的内容。
7. 可用命令以 `unity command` 列出的为准，不要臆造命令。

## 目录规则

- 本项目内容只放在 `Assets/_Project/`；外部模型导入到 `Assets/ThirdParty/`，导入后不改原件，调整一律在 `_Project/Prefabs` 的 Prefab Variant 中进行。
- 原始下载文件放在工程根目录 `_Downloads/`（不在 Assets 内，Unity 不会导入）。由 Claude 挑选需要的文件复制进 `Assets/ThirdParty/`。
- 现有测试内容（HelloWorld 场景、HelloSphere.cs、SampleScene、TutorialInfo、Readme.asset、Materials）在 M0 中用 `AssetDatabase.MoveAsset` 移到 `Assets/_Archive/`，不删除。
- 脚本路径与命名空间一致：`Scripts/Simulation/Navigation/AStarPlanner.cs` → `RivetSim.Simulation.Navigation`。
- 命名前缀：预制体 `PF_`、材质 `M_`、贴图 `T_`、配置 `CFG_`、UXML/USS 用 `UI_`。

## 程序集（单向依赖）

```
RivetSim.Core  ←  RivetSim.Simulation  ←  RivetSim.Presentation
        ↑                  ↑                       ↑
        └────────── RivetSim.Editor（仅编辑器）────┘        RivetSim.Tests.EditMode → Core, Simulation
```
- Core：配置类型、任务矩阵数据模型与 JSON 读取、事件总线、仿真时钟、日志。
- Simulation：机器人、导航、检测、任务编排、场景世界对象。不得引用 Presentation。
- Presentation：UI、可视化、相机、输入、启动入口。只读取状态、订阅事件、调用公开方法。

## SOP 任务矩阵（以用户给定为准）

- 数据源：`Assets/_Project/Config/TaskMatrix/SOP_TaskMatrix_v1.json`；参考图：`Docs/Reference/SOP_TaskMatrix_v1.png`。
- 6 个阶段 p1～p6（T1 全局导航、T2 精确停靠、T3 注册、T4 对中、T5 贴合采集、T6 释放退出）× 8 个主体 a1～a8（S1 导航、S2 视觉、S3 机械臂、S4 贴合采集、S5 超声接口、S6 安全、S7 调度、S8 数据）。
- 单元类型：active（实线）、standby（灰字待命）、conditional（虚线，条件触发）；当前执行单元用粗实线高亮（图中示例为 t(5,4)）。
- 回路：实线“下一颗”（T6→T4，工位内逐钉）；虚线“换站”（同一区域换站转 T2，其他区域转 T1）。
- t(6,1)“倒车退出 换站”只在本工位最后一颗铆钉完成时执行，v1 不为它设计故障处理分支。
- t(4,7)、t(5,4)、t(6,4) 按 JSON 的 `conditionRefs` 逐项判定条件 C1、C2～C8、C9～C12（v1 为固定文字，见 JSON 的 `conditions`）。
- **单元文字、类型、回路一律从 JSON 读取，代码里不写死流程**。改流程只改 JSON；若需要新增执行器，先说明。
- 不得擅自修改 JSON 中 `label` 文字；`explain` 可以润色，但须告诉我。

## 编码规则

- 参数放进 ScriptableObject 配置（`Assets/_Project/Config/`），禁止魔法数字；物理量带单位后缀（`M`、`Mm`、`Mps`、`Deg`、`S`、`N`），内部计算用 SI 单位。
- 禁用 `GameObject.Find`、`SendMessage`；`FindObjectsByType` 只允许在启动入口使用。日志统一用 `SimLog`，不直接调用 `Debug.Log`。
- 任务流程用 `Awaitable` 加 `CancellationToken` 编写，不用协程。
- 简化模型（A 扫信号、接触力、建图）在注释和 UI 中标注“教学简化模型”。
- 公开类型写中文 XML 注释；一个文件一个公开类型。
- UI 用 UI Toolkit（UXML + USS），中文字体用 Noto Sans SC。

## 工作方式

1. 每次只做一个里程碑（见 `Docs/PromptTemplates.md`）。先回复计划（文件清单 + 验收步骤），我确认后再实现。
2. 超出当前里程碑的想法写进 `Docs/PROGRESS.md` 的“待办”，不要顺手实现。
3. 硬件与场地参数使用 `Docs/Architecture.md` §6.1 的 v1 预估值；不要自行另编参数，需要新增参数时先说明。
4. 完成定义：编译 0 错误、Play 模式下 Console 无错误、相关 EditMode 测试通过、已截图自检、`Docs/PROGRESS.md` 已更新。
5. 回复用中文，代码标识符用英文。
