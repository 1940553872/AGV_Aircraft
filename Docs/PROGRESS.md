# 开发进度

## 当前里程碑
M2 厂房与布局重建（1:1.5）＋ AGV 升降柱 ＋ 导入外部资产 —— 第一部分已于 2026-10-18 验收并合并到 main；第二部分（AGV 升降柱）已完成、待用户验收（分支 m2/agv-lift-column）；第三部分未开始（M2 分三部分，见 PromptTemplates.md 与 ADR-010）

## 环境（2026-10-17 核实）
- Unity 6000.0.71f1，URP 17.0.4，Input System 1.19，AI Navigation 2.0.11
- Unity CLI 1.0.0-beta.13 + com.unity.pipeline 0.8.0-exp.1，unity status = ready（端口 7800）
- 原有测试内容（HelloWorld.unity、SampleScene.unity、HelloSphere.cs、Materials/、TutorialInfo/、Readme.asset）已于 M0 归档到 Assets/_Archive/；Settings/（URP 资产）与 InputSystem_Actions 被管线和输入系统引用，留在原位
- Newtonsoft Json 3.2.1 已显式加入 manifest（ADR-006）
- Git：远端 https://github.com/1940553872/AGV_Aircraft（main）

## 资产准备（2026-10-17）
- 已就绪：UR5e、Airbus A320-200（.glb）、Noto Sans SC、AprilTag ID 0～11（详见 _Downloads/README.md）
- 待下载：Poly Haven 3 套材质（直链见 _Downloads/Textures/*/SOURCE.md）
- M2 注意：A320 原始单位约为真机的 4.7 倍，需按 1:1.5 缩放到机长 25.05 m；约 73 万三角面，需精简；需确认名为 Plane_8 的物体是否为地面或底座

## 已验收
| 里程碑 | 日期 | 备注 |
|---|---|---|
| M0 工程整理与骨架 | 2026-10-18 | 用户核对：矩阵打印与参考图一致、24 个 EditMode 测试通过、CFG 默认参数正确；提交 fb320a7 |
| M1 场景白盒与生成模型 | 2026-10-18 | 用户核对 4 张截图与布局校验数值（最窄通道 1.60 m、巡游时 AGV 到机身 1.40 m）；31 个 EditMode 测试通过；提交 1b5b059 |
| M2 第一部分 厂房与布局重建（1:1.5） | 2026-10-18 | 用户核对俯视截图与校验数值（最窄通道 1.80 m、巡游时 AGV 到机身 6.38 m、模型机到障碍物 4.68 m）；33 个 EditMode 测试通过；提交 dc33a19 |

## 最近一次会话
- 日期：2026-10-18
- 完成：M2 第一部分验收并合并到 main（0dfdc7a）；M2 第二部分——AGV 升降柱（硬件更新 + 动作逻辑 + 联锁），待用户验收，分支 `m2/agv-lift-column`
  - 用户确认：现在实现 `AgvController` 最小子集；`liftAccelerationMps2` = 0.16 m/s²、`comfortTargetAboveMountM` = 0.30 m；沙盒“升到 2.0 m”指安装面离地 2.0 m（伸出 1.20 m）。见 ADR-012
  - Core：`AgvConfig` 新增升降柱 7 个参数与 `ArmMountHeightM` / `ExtensionForMountHeightM`，删除 `armMount`；`ArmConfig` 新增 `comfortTargetAboveMountM`
  - Simulation/Robot：`LiftMotionProfile`、`LiftColumnController`、`LiftArrivedEvent`、`LiftHeightPlanner` + `LiftHeightPlan`、`SafetyMonitor`、`IChassisState` / `ILiftState` / `IArmPoseState`、`PlaceholderArmPoseState`、`AgvController`（最小子集）
  - Presentation/App：`SandboxLiftDemo`（Sandbox_Robot 演示入口，4× 倍速）
  - Editor：`AgvGenerator` 生成固定段 + 4 节伸缩段 + 顶部法兰，占位臂挂在法兰上，根节点加运动学刚体、`AgvController`、`LiftColumnController`；`SandboxSceneBuilder` 放置演示入口并调整机位；`ScreenshotTool` 新增 `RivetSim/Debug/Capture Lift Screenshots`
  - CFG：CFG_Agv 新字段取类默认值，经 `SerializedObject` 更新 `notes`；CFG_Arm 新字段取默认值
  - 沙盒演示日志（Play 模式）：未锁定升降被拒绝 → 零速锁定 → 升到安装面 2.000 m（15.4 s）→ 升起时解锁被拒绝 → 降到 0.800 m（15.4 s）→ 解锁 → 行驶 1.00 m 停车；总用时 39.7 s（仿真时间），0 错误，2 条警告均为预期的联锁拒绝
  - Generate All 连续两次：主场景物体总数均为 489（AGV_Preview 带升降柱）；布局校验数值与第一部分一致
- 修改文件：
  - 代码：Scripts/Core/Config/{AgvConfig, ArmConfig}.cs；Scripts/Simulation/Robot/*.cs（11 个新文件）；Scripts/Presentation/App/SandboxLiftDemo.cs；Scripts/Editor/Generators/{AgvGenerator, SandboxSceneBuilder}.cs；Scripts/Editor/Tools/ScreenshotTool.cs；Scripts/Tests/EditMode/{LiftMotionProfileTests, LiftHeightPlannerTests, SafetyMonitorTests}.cs
  - 资产：Config/CFG_Agv.asset、CFG_Arm.asset；Prefabs/Robot/PF_AGV.prefab 等（重新生成）；Scenes/Main_TeachingHall.unity、Scenes/Sandbox/*
  - 文档：Docs/Architecture.md §3.2、§6.1；Docs/DECISIONS.md（ADR-012）；Docs/PROGRESS.md；Docs/Screenshots/M2/{06_Lift_Lowest, 07_Lift_Highest}.png
- 会话结束自检（2026-10-18）：unity status = ready；编译 0 错误 0 警告；EditMode 47/47 通过；Sandbox_Robot 与主场景 Play 模式 Console 0 错误；场景无未保存改动，已退出 Play 模式
- 遗留问题：
  - 升降柱时长测试：到位判定在 ±1 mm 精度带内，实际用时比理论梯形曲线少约 0.11 s（√(2·精度 / 加速度)），测试已按此修正
  - `AgvController` 角速度未做加速度限幅、无车轮动画，M3 补充；主场景 AGV_Preview 带控制器但无人推进（M3 / M5 由 AppBootstrap 生成并推进，ADR-009）
  - 任务矩阵对应（label 不变）：t(1,3) / t(2,3) 保持最低位，t(3,3) 升到工位作业高度，t(6,3) 收臂并降到最低，t(6,1) 等降到最低后倒车；执行器在 M5 实现
  - 占位模型机只是示意，M2 第三部分用 A320 替换并复核作业区与铆钉排高度
  - CFG_Inspection 的 `stationSpacingM` 仍为 1.2 m，与 Architecture §6.1 的 1.5 m 不一致，第三部分前确认
  - 演示区近景偏暗（M7 补光）；巡游路线 169 m 需配合倍速
  - `capture_game_view --save_path` 会把相对路径解析到 `Assets/` 下：验收截图用 `RivetSim/Debug/Capture Layout Screenshots` / `Capture Lift Screenshots`
  - 在 PowerShell 里给 `unity command eval --code` 传含双引号的代码会被剥掉引号；改用 Bash 调用 CLI，或把代码写进文件后用 `eval_file`
- 下次第一步：用户验收第二部分并合并分支后，执行 PromptTemplates.md 的 M2 第三部分（导入外部资产）。先检查各 SOURCE.md 的许可信息，回复计划，我确认后再实现

## 参数与流程约定（2026-10-17 确认）
- 硬件与场地参数：v1 采用 Architecture §6.1 的预估值（模型机 A320 外形缩比 1:1.5、厂房 64 × 44 × 12 m、AGV 1.20 × 0.80 × 0.45 m 加升降柱（见 ADR-010）、UR5e、10 MHz 探头、接触力 10 N 等），后续按实际选型修改配置。
- t(6,1)“倒车退出 换站”：只在本工位最后一颗铆钉完成时执行；v1 不为它设计故障处理分支。
- 条件 C1～C12：v1 使用 JSON `conditions` 中的固定文字与教学示例阈值，后续版本细化。
- 随机种子：42（CFG_SimSettings，2026-10-18 确认）。
- Layers 固定为 8 Environment、9 Aircraft、10 Obstacle、11 Robot、12 Visualization（常量见 `SimLayers`）；Fixed Timestep 0.01 s。
- 配置资产 CFG_* 由 `RivetSim/Setup/Create Default Configs` 生成，只补缺不覆盖（ADR-007）；调参直接改资产并在 `notes` 注明来源。

## 待确认参数（后续版本）
| 参数 | v1 取值 | 配置 |
|---|---|---|
| 实际模型机型号与缩比 | A320 外形 1:1.5 | CFG_SceneLayout / CFG_Inspection |
| AGV 选型与运动方式 | 差速、1.20 × 0.80 × 0.45 m、升降柱行程 1.40 m | CFG_Agv |
| 机械臂选型 | UR5e | CFG_Arm |
| 探头与检测工艺参数 | 10 MHz、2 mm 铝蒙皮、8～12 N | CFG_Inspection |
| C1～C12 的正式定义 | 固定示例文字 | SOP_TaskMatrix_v1.json |

## 待办（超出当前里程碑的想法）
- [ ] M2：模型机 1:1.5、厂房 64 × 44 × 12 m（第一部分已验收）、AGV 升降柱（硬件 + 动作 + 联锁），见 ADR-010 与 PromptTemplates 的 M2
- [ ] v2：升降柱升高后的整机稳定裕度分析（质心高度上升）
- [ ] M5：启动时用 `TaskMatrixData.GetExecutorKeys()` 与 `ExecutorRegistry` 核对缺失的执行器键名（目前 JSON 中共 37 个）
- [x] M1：确定 `CFG_SceneLayout` 中尺寸向量（长 × 宽 × 高）与世界坐标轴的对应关系（ADR-008）
- [ ] M3 / M5：AGV 改为由 AppBootstrap 运行时生成，删除主场景中的 AGV_Preview（ADR-009）
- [ ] M7：演示区补光 / 主光方向调整，提升演示台近景亮度
