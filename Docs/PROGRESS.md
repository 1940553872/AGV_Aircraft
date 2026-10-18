# 开发进度

## 当前里程碑
M3 AGV 建图、规划与移动 —— 已实现，待用户验收（2026-10-18；M2 已于同日整体验收）

## 环境（2026-10-17 核实）
- Unity 6000.0.71f1，URP 17.0.4，Input System 1.19，AI Navigation 2.0.11
- Unity CLI 1.0.0-beta.13 + com.unity.pipeline 0.8.0-exp.1，unity status = ready（端口 7800）
- 原有测试内容（HelloWorld.unity、SampleScene.unity、HelloSphere.cs、Materials/、TutorialInfo/、Readme.asset）已于 M0 归档到 Assets/_Archive/；Settings/（URP 资产）与 InputSystem_Actions 被管线和输入系统引用，留在原位
- Newtonsoft Json 3.2.1 已显式加入 manifest（ADR-006）
- Git：远端 https://github.com/1940553872/AGV_Aircraft（main）

## 资产准备（2026-10-17 下载；2026-10-18 已导入，3A）
- 许可核对：UR5e BSD-3、A320 CC BY 4.0（必须署名）、AprilTag BSD-2、Noto Sans SC OFL 1.1、Poly Haven CC0，均齐全；Poly Haven 各 SOURCE.md 的下载日期已按文件时间补记为 2026-10-17
- 全部经 `RivetSim/Import/Third Party Assets` 复制到 Assets/ThirdParty/（原件不改），登记见 Docs/ThirdPartyAssets.md
- A320：glTFast 6.20.0 导入后机头朝 +X、Y 向上；按机长缩放 0.139595；Plane_8 只是翼尖小件（不是地面），保留；只隐藏机身内驾驶舱件 8 个节点（约 4.3 千面），可见 727,718 面（用户确认不减面）；层级清单 Docs/Reference/A320_Hierarchy.md
- UR5e：7 个 DAE 网格共约 12.2 万面；Unity 导入 Z 向上 DAE 的网格坐标为 (−x, z, −y)，每个网格再绕 Y 轴 +90° 与 RosFrame 换算一致

## 已验收
| 里程碑 | 日期 | 备注 |
|---|---|---|
| M0 工程整理与骨架 | 2026-10-18 | 用户核对：矩阵打印与参考图一致、24 个 EditMode 测试通过、CFG 默认参数正确；提交 fb320a7 |
| M1 场景白盒与生成模型 | 2026-10-18 | 用户核对 4 张截图与布局校验数值（最窄通道 1.60 m、巡游时 AGV 到机身 1.40 m）；31 个 EditMode 测试通过；提交 1b5b059 |
| M2 第一部分 厂房与布局重建（1:1.5） | 2026-10-18 | 用户核对俯视截图与校验数值（最窄通道 1.80 m、巡游时 AGV 到机身 6.38 m、模型机到障碍物 4.68 m）；33 个 EditMode 测试通过；提交 dc33a19 |
| M2 第二部分 AGV 升降柱 | 2026-10-18 | 用户核对最低位 / 最高位截图（安装面 0.80 / 2.20 m）与沙盒联锁演示日志；47 个 EditMode 测试通过；提交 d5e00ce |
| M2 第三部分 3A 导入外部资产 | 2026-10-18 | 用户核对截图 08～12（A320 总览 / 侧面、UR5e 关节测试、AGV 带 UR5e 升降柱最低 / 最高位、材质）；51 个 EditMode 测试通过；提交 0501bf7 |
| M2 第三部分 3B 检测区与可达性 | 2026-10-18 | 用户核对截图 13～15 与可达性报告（48 颗铆钉、5 颗真值缺陷；4 个工位规划高度下 12/12 可达；车体 / 升降柱三维间隙 0.895 / 0.383 m）；59 个 EditMode 测试通过；提交 48a1141 |
| **M2 厂房与布局重建（1:1.5）＋ AGV 升降柱 ＋ 导入外部资产（整体）** | 2026-10-18 | 用户整体验收；包含上面 M2 各部分 |

## 最近一次会话
- 日期：2026-10-18
- 完成：M3 AGV 建图、规划与移动（先 Sandbox_Robot，后接入 Main），待用户验收。计划与 4 项决定经用户确认（机轮碰撞体、机械臂零位前伸的处理、AppBootstrap 在 M3 建立、新参数取值），见 ADR-015
  - Core：新增 `NavigationConfig`（CFG_Navigation）；CFG_Agv 新增最大角加速度 120 °/s² 与车头对接相机（离地 0.40 m、90° × 60°、0.3～4.0 m）、`BodyCircumscribedRadiusM`
  - Simulation/Robot：`AgvController` 补角加速度限幅、急停、真值位姿、驱动轮转动动画（未重写）；新增 `DiffDriveKinematics`（圆弧弦长积分）、`AgvFootprint`（含机械臂扫掠半径 1.069 m、车头前伸 1.034 m）
  - Simulation/Navigation（新）：GridGeometry、OccupancyGridMap、OccupancyState、Lidar2D、LidarScan、MappingRoutine、VirtualKeepOutZone、KeepOutMask、KeepOutChannel、CostMap、GridAStar、PathSmoother、PlannedPath、PurePursuitTracker、PathFollower、MotionProfile、IMotionTask、MotionStatus、StraightDrive、MarkerVisibility、DockingController、DockingError、DockingResult、NavigationResult、NavigationService，以及 4 个事件
  - Simulation/World：`RegionMarker` 记录标记中心本地位置（立杆标记板的标记在杆顶）
  - Presentation：`AppBootstrap`（主场景入口，充电位运行时生成 AGV）、`NavigationRuntime`（固定子步 0.01 s 推进）、`NavigationDemoRoutine`、`SandboxNavDemo`；Input：`AgvTeleopInput`、`VisualizationToggleInput`；Visualization：`OccupancyGridOverlay`、`KeepOutOverlay`、`LidarPointCloudView`、`PathView`、`NavOverlayView`、`GroundOverlayMesh`；UI：`UI_NavOverlay.uxml/.uss`；asmdef 加 Unity.InputSystem
  - Editor：新增 `KeepOutZoneGenerator`（`RivetSim/Generate/Virtual Keep-Out`，主场景生成时自动调用）、`NavigationSceneSetup`；`AircraftA320Builder` 给 4 个起落架轮胎加 MeshCollider；`AgvGenerator` 车轮转动节点 + 轮毂标记、对接相机、Lidar2D；`SandboxSceneBuilder` 重建 Sandbox_Robot（20 × 14 m、矮围墙、1.8 m 窄通道、模拟机身禁行区、测试工位 SB-S1；M2 升降柱演示保留但停用）；`MainSceneBuilder` 删除 AGV_Preview、加 AppBootstrap 与可视化；`MaterialLibraryBuilder` 新增 M_Vis_*（URP Unlit）；`GenerateAll` 先补建 CFG；`ScreenshotTool` 布局截图临时实例化 PF_AGV
  - 虚拟禁行区：72.8 万个三角形保守栅格化到 232 × 254 格，投影内 12,499 格（125.0 m²）；4 条接近通道，开口 2.40 × 3.20 m
  - 结果（Main，4× 倍速，种子 42）：
    | 项目 | 路径长度 | 实际里程 | 用时（仿真） |
    |---|---|---|---|
    | 建图（驶离充电位 1.50 m + 巡游路线 181.25 m） | 182.75 m | 181.05 m | 316.3 s（实际约 79 s），已观测 279,549 格（99.3%） |
    | 导航 充电位 → R1-S1 预停靠位姿（驶离 1.50 m + A* 24.73 m） | 26.23 m | 26.05 m | 56.4 s |
    | 对接 R1-S1（识别停靠标记 ID 0） | 1.50 m | 1.499 m | 14.0 s |
    | 倒车退出 | 0.60 m | 0.599 m | 4.3 s |
    - 停靠误差：纵向 +2.2 mm、横向 +0.7 mm、偏航 −0.04°（容差 ±10 mm / ±0.5°，合格）
    - 联锁：升降柱升到安装面 1.850 m 时解锁被拒绝、倒车被拒绝；降到最低后解锁，倒车退出完成
    - Sandbox：建图 55.00 m / 93.8 s，导航 12.40 m / 29.7 s，SB-S1 停靠误差 纵向 +2.2 mm、横向 +0.7 mm、偏航 −0.03°
  - Generate All 连续两次：主场景物体总数均为 977（删除 AGV_Preview 后）
- 修改文件：
  - 代码：Scripts/Core/Config/{NavigationConfig（新）, AgvConfig}.cs；Scripts/Simulation/Robot/{AgvController, DiffDriveKinematics（新）, AgvFootprint（新）}.cs；Scripts/Simulation/Navigation/*.cs（30 个新文件）；Scripts/Simulation/World/RegionMarker.cs；Scripts/Presentation/App/{AppBootstrap, NavigationRuntime, NavigationDemoRoutine, SandboxNavDemo}.cs（新）、Input/*.cs（新）、Visualization/*.cs（新）、RivetSim.Presentation.asmdef；Scripts/Editor/Generators/{KeepOutZoneGenerator, NavigationSceneSetup}.cs（新）、{AgvGenerator, AircraftA320Builder, MainSceneBuilder, SandboxSceneBuilder, MaterialLibraryBuilder, RivetZoneGenerator, GenerateAllMenu}.cs；Scripts/Editor/Tools/{ProjectPaths, ProjectSetupMenu, ScreenshotTool}.cs；Scripts/Tests/EditMode/{GridAStarTests, CostMapTests, DiffDriveKinematicsTests, NavigationGeometryTests}.cs（新）
  - 资产：Config/CFG_Navigation.asset（新）、CFG_Agv.asset；UI/UI_NavOverlay.uxml/.uss（新）；Art/Materials/M_Vis_*（新）；Prefabs（PF_AGV、PF_Aircraft_A320_Model 等重新生成）；Scenes/Main_TeachingHall.unity、Scenes/Sandbox/*
  - 文档：Docs/Architecture.md §3.2 / §3.3 / §4 / §6 / §6.1、Docs/DECISIONS.md（ADR-015）、Docs/Reference/A320_Hierarchy.md、Docs/PROGRESS.md、Docs/Screenshots/M3/16～21
- 会话自检（2026-10-18）：unity status = ready；编译 0 错误 0 警告；EditMode 82/82 通过（新增 23 个）；Main_TeachingHall Play 模式 0 错误，3 条警告均为联锁演示预期的拒绝；Sandbox_Robot Play 模式 0 错误 0 警告；场景无未保存改动，已退出 Play 模式
- 截图（Docs/Screenshots/M3/）：16 沙盒全流程结束（地图、禁行区、通道、停靠误差标注）；17 主场景建图进行中（53%，北侧机身阴影区尚未扫到）；18 建图完成后俯视；18b 同一时刻临时隐藏 A320 渲染器，显示禁行区全貌与 4 条接近通道；19 充电位 → R1-S1 导航中（青色路径、橙色目标箭头）；20 R1-S1 停靠后联锁演示（升降柱升起、倒车被拒绝、停靠误差标注）；21 倒车退出后
- 遗留问题：
  - UR5e 仍为零位（手臂伸出车头前 0.43 m），原地转向 / 对接车道按含机械臂半径检查激光层；M4 定义 Stow 后复核并去掉该检查
  - Editor 失去焦点时 Play 模式不推进（Player 设置 Run In Background 为关）；本次用 eval 在 Play 中临时设 `Application.runInBackground = true`，未改项目设置
  - `capture_game_view` 的 save_path 只能写在 Assets 下；截图改为取内联图片自行解码保存，避免在 Assets 里产生文件
  - 对接噪声取自固定种子，沙盒与主场景的停靠误差序列相同（可复现，非巧合）
  - 演示中途退出 Play 时，正在运动的升降柱会记录一条“运动被取消”警告（预期行为）
  - 场景整体偏暗（M7 补光）；A320 三角面约 73 万（M7 复核帧率）
  - 在 PowerShell 里给 `unity command eval --code` 传含双引号的代码会被剥掉引号；改用 Bash 调用 CLI，或用 `eval_file`
- 下次第一步：用户验收 M3（截图 16～21 与上表数值）；通过后执行 PromptTemplates.md 的 M4（机械臂与检测），先回复计划

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
- [x] M2：模型机 1:1.5、厂房 64 × 44 × 12 m、AGV 升降柱、导入外部资产与检测区（2026-10-18 整体验收），见 ADR-010～014
- [ ] v2：升降柱升高后的整机稳定裕度分析（质心高度上升）
- [ ] M5：启动时用 `TaskMatrixData.GetExecutorKeys()` 与 `ExecutorRegistry` 核对缺失的执行器键名（目前 JSON 中共 37 个）
- [x] M1：确定 `CFG_SceneLayout` 中尺寸向量（长 × 宽 × 高）与世界坐标轴的对应关系（ADR-008）
- [x] M3 / M5：AGV 改为由 AppBootstrap 运行时生成，删除主场景中的 AGV_Preview（ADR-009；M3 完成，见 ADR-015）
- [ ] M4：定义运输姿态 Stow 后复核整机外廓，去掉导航中“含机械臂扫掠半径”的原地转向 / 对接车道检查（ADR-015）
- [ ] M5：返回充电位复用 M3 的“导航到充电位前方 1.5 m + 直线倒车”
- [ ] M7：演示区补光 / 主光方向调整，提升演示台近景亮度
