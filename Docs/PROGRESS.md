# 开发进度

## 当前里程碑
M6 演示界面 —— 未开始（M5 SOP 流程编排已于 2026-10-19 验收）

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
| M5 SOP 流程编排 | 2026-10-19 | 用户核对截图 01～02 与 Docs/Reports/M5.md（48/48 颗、检出 5/5、误报 0；仿真 1481 s，4 倍速约 6.2 min；三种故障各一次均激活并恢复；执行器键名 37 个缺失 0）；112 个 EditMode 测试通过；提交 cf617c2 |

## 最近一次会话
- 日期：2026-10-19
- 完成：M5 SOP 流程编排（在 Main 中开发，MissionOrchestrator 取代 NavigationDemoRoutine；文档提交 4c0418b“docs: 省额度工作流程”）。已定决策 1～10 与计划待决问题按默认执行：
  1. retryPhase 用完仍失败：本颗记为“无效”、单元标 Failed，直接进入 T6，流程继续；
  2. 每颗 T4～T6 的纯逻辑单元也按 0.6 s；
  3. 识别失败注入点：区域 2 的 T1 整机识别，激活 t(1,8)；
  4. 设备节点缺失时生成器报错，不建占位；
  5. 加 F5 / F6 / F7 热键。
  - Core：`Core/Mission`（`CellState`、`CellStateChangedEvent`、`ConditionCheckedEvent`、`PhaseEnteredEvent`、`LoopTakenEvent`）、`MissionEntry`；`SimSettings` 新增入口枚举、模拟操作员（3 s、位置噪声 0.05 m、航向 1°）、故障注入（力超限压缩 4 mm、耦合系数 0.3）、快速验证步长 0.25 s；`SimClock.AdvanceSim`；`SimLayers.VisualOnlyPrefix`
  - Simulation/Mission（新）：`MissionOrchestrator`、`TaskMatrixRuntime`、`ExecutorRegistry`、`ITaskExecutor`、`DelegateExecutor`、`PhaseDependencyTable`、`ConditionBus`、`ConditionEvaluator`、`MissionBlackboard`、`MissionServices`、`MissionCursor`、`IMissionPlan`、`RegistryMissionPlan`、`ScheduledFaultInjector`、`FaultInjectionPoint`、`MissionTrace` 等；`Executors/` 下 S1～S8 共 8 个注册类覆盖 37 个键名
  - Simulation/Inspection：`RivetInspectionService` 拆分 T3（观察位姿 / 基准注册 / 基准复测）、T4（规划 / 钉心复测 / 视觉修正）、T5（接触 / 采集 / 判读 / 保存）、T6（回退到指定距离），M4 组合方法保留；新增 `AcquisitionSample`、`InspectionCsvExporter`；`ContactModel.CouplingFaultFactor`
  - Presentation：`MissionRoutine`（建图 → 矩阵流程 → 返回充电位 → CSV）、`MissionRunReport`、`MissionLaunchOptions`、`DeviceIndicator`（MaterialPropertyBlock）、`FaultHotkeys`；`AppBootstrap` 按入口枚举分支；`RobotRuntime` 快速推进
  - Editor：`DeviceIndicatorGenerator`（`RivetSim/Generate/Device Indicators`，AGV 7 个设备 62 个外壳、演示台 1 个设备 2 个外壳；AgvGenerator / DemoStationGenerator 末尾自动调用）、`MaterialLibraryBuilder` 新增 M_DeviceGlow、`MissionVerifyRunner`（`RivetSim/Verify/Run Mission (Fast)`）
  - Tests：`MissionOrchestratorTests` 8 项（循环与换站、状态序列、力超限重试、重试用尽、每 10 颗计数、键名核对、依赖表、阈值解析）
  - Docs：Architecture §5.1 补依赖说明 4 行；报告 Docs/Reports/M5.md、M5_inspection.csv
- 结果（种子 42，详见 Docs/Reports/M5.md）：
  - 2 区域 × 2 工位 × 12 颗 = 48 颗全部完成；仿真总用时 1481.0 s（建图 316.5 + 矩阵流程 1081.8 + 返回 82.8），4 倍速约 6.2 min；快速验证真实用时约 60 s（等效约 25 倍速）
  - 检出 5 / 5、漏检 0、误报 0、无效 0；阶段执行 156 次（含 2 次重试）；执行器键名 37 个，缺失 0
  - 三种故障各一次均激活对应虚线单元并恢复：t(1,8) 3 s 后给出初值（偏差 67 mm / −0.7°）；t(5,6) 超限 86 N 后受控退出、T5 重试 1/1 成功；t(5,7) 耦合 0.30 无效、重贴后 T5 重试 1/2 成功；t(6,2) 每个工位第 10 颗后复测一次
  - 条件判定 583 次、未满足 4 次：C4 一次（0.67 mm，未定位原因，推测为钉心复测噪声）、C6 一次（采集无效故障，预期）、C8 两次（厚度误差 0.20 mm，未逐颗定位）
- 自检：编译 0 错误 0 警告；EditMode 112/112 通过；Generate All 后主场景 983 个物体（M4 981 + 演示台电脑 2 个发光外壳），设备发光生成器重复执行结果不变；Main Play 0 错误，警告均为故障注入、条件 ✗ 与单元失败的预期日志；场景无未保存改动，已退出 Play 模式
- 截图（Docs/Screenshots/M5/）：01 T5 进行中（R1-S1 第 6 颗力控接触，S3 机械臂与升降柱发橙光）；02 流程结束后结果环（缺陷最多的工位，红 = 缺陷、绿 = 合格）
- 遗留问题：
  - 工位最后一颗的“单钉节拍”含收臂与降柱（约 40 s），叠加层平均节拍因此偏大
  - C1～C12 判定结果只上报、不阻断流程；C4 / C8 的 ✗ 尚未逐颗定位原因（M6 演示屏显示时一并核对）
  - 整机识别为教学简化：不做轮廓匹配，只在注入时失败；操作员初值仅写日志，不影响定位（导航用真值位姿）
- 2026-10-19 用户验收通过。
- 验收后修复（2026-10-19，用户发现）：返回充电位停在停车框外（实测纵向 −695 mm）。原因：`NavigationService.TryPlan` 在终点后方加引入点，AGV 从终点前方驶来时路径要越过终点再折返，终点段按“终点在车头方向的投影”判定到达，停在了引入点。修复：起点已在终点前方（沿目标朝向）时不加引入点，直接到终点后原地转向；`MissionRoutine` 记录停车误差，超出到点容差时报警告。复测停车误差纵向 −1.4 mm、横向 −7.7 mm、偏航 +0.30°；48/48 颗、检出 5/5、误报 0 不变；总用时 1478.0 s；EditMode 112/112。同区域换站（从工位 1 倒车后去工位 2 预停靠位姿）属同一几何情况，此前靠 T2 对接前的距离检查补导航掩盖，现已一并修复。截图 03_Charger_Parked.png
- 下次第一步：执行 PromptTemplates.md 的 M6（演示界面），订阅 CellStateChangedEvent / ConditionCheckedEvent / LoopTakenEvent，HUD 故障按钮调用 `ConditionBus.Inject`

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
- [x] M5：启动时用 `TaskMatrixData.GetExecutorKeys()` 与 `ExecutorRegistry` 核对缺失的执行器键名（目前 JSON 中共 37 个）
- [x] M1：确定 `CFG_SceneLayout` 中尺寸向量（长 × 宽 × 高）与世界坐标轴的对应关系（ADR-008）
- [x] M3 / M5：AGV 改为由 AppBootstrap 运行时生成，删除主场景中的 AGV_Preview（ADR-009；M3 完成，见 ADR-015）
- [x] M4：定义运输姿态 Stow 后复核整机外廓，去掉导航中“含机械臂扫掠半径”的原地转向 / 对接车道检查（2026-10-19，ADR-016）
- [x] M5：返回充电位复用 M3 的“导航到充电位前方 1.5 m + 直线倒车”
- [x] M5：T3～T6 执行器直接包 `RivetInspectionService` 的方法；ConditionBus 订阅 `ContactForceExceededEvent`（力超限）与判读为“无效”的记录（采集无效）；`InspectionRecordStore` 导出 CSV
- [x] M5：“每 10 颗基准复测”改用 `RivetInspectionService.RemeasureDatumAsync`（从铆钉位姿经观察姿态复测）
- [ ] M6：演示屏 A 扫复用 `AScanGraphElement`；沙盒去掉 M3 导航叠加面板，由 HUD 取代
- [ ] M7：演示区补光 / 主光方向调整，提升演示台近景亮度
- [ ] M6：t(1,8) 改为小地图点选初值（M5 由模拟操作员 3 s 后给出）；演示屏逐条显示 ConditionCheckedEvent 的 ✓ / ✗
- [ ] M6：单钉节拍统计排除工位末颗的收臂与降柱时间
