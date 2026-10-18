# 开发进度

## 当前里程碑
M5 SOP 流程编排 —— 未开始（M4 机械臂与超声检测已于 2026-10-19 验收）

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
| M3 AGV 建图、规划与移动 | 2026-10-18 | 用户核对截图 16～21 与数值（建图 182.75 m / 316.3 s；充电位 → R1-S1 导航 26.23 m / 56.4 s；停靠误差 纵向 +2.2 mm、横向 +0.7 mm、偏航 −0.04°，合格；联锁拒绝正确）；82 个 EditMode 测试通过；提交 e63ee3c |
| M4 机械臂与超声检测 | 2026-10-19 | 用户核对截图 22～29 与数值（Stow 外廓在车体投影内、最高点 1.034 m；4 个工位 48/48 可达，连杆到机身最小 150 mm；随机位姿逆解 99.6% 收敛、最大 0.200 mm；R1-S1 检出 3 / 误报 0，平均单钉节拍 4.71 s）；104 个 EditMode 测试通过；提交 97f0059 |

## 最近一次会话
- 日期：2026-10-18～2026-10-19
- 完成：M4 机械臂与超声检测（先 Sandbox_Robot，后接入 Main），2026-10-19 用户验收通过（提交 97f0059，直接提交在 main）。计划与第 4 节 8 项按建议执行（Stow 校核方法、膨胀半径保持 0.721 m、联锁规则 3、Observe 镜像、沙盒机身段 + 停用 M3 导航演示、接入 Main、新参数取值、占位类归档），见 ADR-016。实现中三处偏离计划（均为满足已确认的校核要求，ADR-016 第 1 条）：
  1. Stow 改为 (0, −180, −120, 45, −90, 0)°（计划值前臂离基座仅 1.8 mm，自干涉不合格）；
  2. 新增直立过渡姿态 Ready (0, −90, 90, −90, −90, 0)°，Stow ↔ Observe 都经它过渡，镜像取基座角 −180°（左侧工位直接插值时末端离机身仅 1～2 mm）；
  3. 间隙检查加查本车车体（同 30 mm 要求）。
  - Core：`ArmConfig` 新增命名姿态 Stow / Ready / Observe、关节运动、逆解、检测动作、间隙、简化视觉参数；`InspectionConfig` 新增虚拟弹簧、耦合质量、A 扫合成、缺陷回波模型、闸门参数（闸门 A / B 由 0 改为 0.30 / 0.25）；`AgvConfig` 新增 `transportHeightMaxM`（§6.1 的 1.35 m）；`NamedJointPose` 加构造函数
  - Simulation/Robot：`ArmKinematics`、`ArmIkSolver`、`IkSettings`、`IkResult`、`IkStatistics`、`TrapezoidProfile`、`JointTrajectory`、`ArmController`（实现 IArmPoseState）、`ArmClearance`、`ArmLinkSamples`、`ClearanceResult`、`ContactModel`、`ContactForceExceededEvent`、`ProbeEndEffector`、`ArmMotionResult`、`AgvEnvelope`（新）；`ArmRig`（TCP / 相机 / 采样点）、`ArmJoint`（零位旋转）、`SafetyMonitor`（规则 3）、`AgvFootprint`（外廓量测）修改；`PlaceholderArmPoseState` 归档到 Assets/_Archive/Scripts
  - Simulation/Inspection（新）：`WristVision`、`StationRegistration`、`AScanSynthesizer`、`AScanWaveform`、`GateJudge`、`GateResult`、`InspectionVerdict`、`InspectionRecord`、`InspectionRecordStore`、`RivetInspectionService`、`RivetInspectedEvent`、`GaussianNoise`；`World/Rivet` 加运行时检测结果
  - Simulation/Navigation：`NavigationService` / `PathFollower` / `DockingController` 删除含机械臂扫掠半径与车头前伸的检查
  - Presentation：`NavigationRuntime` 改名 `RobotRuntime`（加机械臂、接触力、检测服务与运输姿态校核）；新增 `StationInspectionRoutine`、`SandboxInspectionDemo`、`InspectionSceneViews`；`AppBootstrap` / `NavigationDemoRoutine`（停靠后单工位检测 + 联锁演示 2）/ `SandboxNavDemo` / `SandboxLiftDemo` 适配；Visualization：`RivetResultRingView`、`WristCameraFrustumView`、`ProbeContactView`；UI（新目录）：`InspectionOverlayView`、`AScanGraphElement`、`UI_InspectionOverlay.uxml/.uss`
  - Editor：新增 `FuselageSectionBuilder`（`RivetSim/Generate/Fuselage Section`）、`StowEnvelopeCheck`（`RivetSim/Debug/Check Stow Envelope`）；`ReachabilityChecker` 改用运行时逆解并检查连杆间隙；`ScreenshotTool` 新增 `Capture Stow Envelope Screenshots`；`UR5eArmGenerator`（ArmController、连杆采样点）、`EndEffectorGenerator`（ProbeEndEffector）、`AgvGenerator`（预制体默认 Stow）、`RivetZoneGenerator`（工位过滤）、`KeepOutZoneGenerator`（公开栅格化）、`SandboxSceneBuilder`（机身段沙盒）、`MainSceneBuilder` / `NavigationSceneSetup`（检测可视化）、`MaterialLibraryBuilder`（M_Vis_Result_* / M_Vis_CameraFrustum）、`ProjectSetupMenu`（补写 M4 配置值）、`GenerateAllMenu`
  - Tests：新增 `ArmIkSolverTests`、`ArmMotionProfileTests`、`InspectionModelTests`、`StowEnvelopeTests`，`SafetyMonitorTests` 加规则 3
- 结果：
  - 运输姿态 Stow（网格顶点，升降柱最低位）：机械臂横向 −0.210～+0.076 m、纵向 −0.335～+0.225 m（在 1.20 × 0.80 m 车体投影内），整机最高点 1.034 m（≤ 1.35 m），非相邻连杆最小 26.9 mm；整机扫掠半径 0.729 m（零位 1.069 m）、车头前伸 0.652 m（零位 1.034 m）。报告 Docs/Reference/M4_StowEnvelope.md
  - 可达性与连杆间隙（Main 4 个工位，伸出 1.050 m）：对中 / 接触位姿 48/48 可达、钉心 48/48 可见、基准标记 4/4 可见；逆解最大位置误差 0.198 mm、姿态误差 0.133°；连杆到机身最小 150 mm、末端最小 75 mm；全部路径合格。随机位姿逆解 500 个：498 收敛（99.6%），位置误差平均 0.133 mm / 最大 0.200 mm，最大姿态误差 0.497°。报告 Docs/Reference/M4_ArmReach.md
  - 沙盒 R1-S1（2× 倍速，种子 42）：12 颗，平均单钉节拍 4.71 s（T4～T6；第 1 颗含进近 7.05 s，其余约 4.5 s）；工位合计 99.0 s（升柱 13.5 s、T3 注册 7.4 s、12 颗 56.5 s、收臂与降柱 21.5 s）；真值缺陷 3 颗（07 减薄、10 裂纹、12 松动）全部检出，漏检 0、误报 0、疑似 0、无效 0；接触力 10.05～10.90 N；最大对中误差 0.31 mm；基准注册误差 0.61 mm；逆解 39/39 收敛，最大 0.197 mm
  - Main（4× 倍速）：建图、导航、对接与 M3 一致（导航 26.05 m / 56.4 s；停靠误差 纵向 +2.2 mm、横向 +0.7 mm、偏航 −0.04°，合格）；R1-S1 检测 检出 3、误报 0，平均节拍 4.74 s，逆解 39/39；联锁 1（升降柱升起时解锁 / 倒车被拒绝）与联锁 2（机械臂不在运输姿态时解锁被拒绝）正确；倒车退出 0.60 m / 4.3 s
  - Generate All 连续两次：主场景物体数均为 981（M3 977 + 4 个检测可视化节点）
- 自检（2026-10-19）：unity status = ready；编译 0 错误 0 警告；EditMode 104/104 通过（新增 22 个）；Sandbox_Robot 与 Main_TeachingHall Play 模式 0 错误 0 警告（Main 的 4 条警告均为预期的联锁演示：联锁 1 的解锁拒绝、倒车拒绝及其导航未完成记录，联锁 2 的解锁拒绝）；场景无未保存改动，已退出 Play 模式
- 截图（Docs/Screenshots/M4/）：22a / 22b Stow 外廓侧视 / 俯视（车体投影框、1.35 m 高度框、Stow 最高点线、机械臂投影框）；23 沙盒总览（升降柱升起，探头贴在第 5 颗）；24 T3 基准注册（Observe 后对准基准标记 ID 4，视场锥）；25 T5 接触近景（接触力 10.10 N、压缩 2.02 mm，A 扫为 R1-S1-02 合格波形）；26 一个工位 12 颗完成后的结果环（07 / 10 / 12 红，其余绿）；27b / 27c / 27d A 扫：减薄（闸门 B 回波深 1.42 mm）/ 孔边裂纹（深 1.01 mm）/ 松动（底面回波 34%），合格波形见 25；28 Main R1-S1 检测中（第 8 颗）；29 Main 倒车退出后（Stow、升降柱最低）
- 遗留问题：
  - 判读包络约为脉冲峰值的 90%（一次底面波 0.8 显示为 71%～74%），阈值按满屏高度比例直接比较；如需显示值与合成幅值一致，可在 M6 改用解析包络（教学展示问题，不影响判读）
  - 接触力停止值 10.0～10.9 N：逐子步判定，个别颗受直线跟踪残余误差影响超出一步（仍在 8～12 N 内）
  - 机身段为整圈三角形截取（只保留三个顶点都在范围内的三角形），两端截面为锯齿状；仅用于沙盒
  - 沙盒左上角仍显示 M3 导航叠加面板（只用到阶段与时钟，建图 / 导航为“未开始”）
  - 力超限路径只在 EditMode 测试中验证（`ContactModel.SurfaceOffsetFaultM` 故障注入），演示中由 M5 故障注入按钮触发
  - Editor 失去焦点时 Play 不推进：Play 中经 eval 临时设 `Application.runInBackground = true`（未改项目设置）；截图通过 EditorApplication.update 钩子在指定时刻暂停后取 capture_game_view 内联图片
  - 场景整体偏暗（M7 补光）；A320 三角面约 73 万（M7 复核帧率）
- 下次第一步：执行 PromptTemplates.md 的 M5（SOP 流程编排），先回复计划，确认后再实现；可先处理待办中标为 M5 的条目（T3～T6 执行器包 RivetInspectionService、ConditionBus 订阅力超限 / 采集无效、CSV 导出、每 10 颗基准复测、返回充电位、启动时核对执行器键名）

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
- [x] M4：定义运输姿态 Stow 后复核整机外廓，去掉导航中“含机械臂扫掠半径”的原地转向 / 对接车道检查（2026-10-19，ADR-016）
- [ ] M5：返回充电位复用 M3 的“导航到充电位前方 1.5 m + 直线倒车”
- [ ] M5：T3～T6 执行器直接包 `RivetInspectionService` 的方法；ConditionBus 订阅 `ContactForceExceededEvent`（力超限）与判读为“无效”的记录（采集无效）；`InspectionRecordStore` 导出 CSV
- [ ] M5：“每 10 颗基准复测”复用 `RivetInspectionService.RegisterStationAsync`
- [ ] M6：演示屏 A 扫复用 `AScanGraphElement`；沙盒去掉 M3 导航叠加面板，由 HUD 取代
- [ ] M7：演示区补光 / 主光方向调整，提升演示台近景亮度
