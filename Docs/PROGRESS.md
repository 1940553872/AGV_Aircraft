# 开发进度

## 当前里程碑
M2 厂房与布局重建（1:1.5）＋ AGV 升降柱 ＋ 导入外部资产 —— 第一、二部分已验收；第三部分拆为 3A / 3B（ADR-013）：3A 导入外部资产已于 2026-10-18 验收并合并到 main；3B 检测区与可达性已完成、待用户验收（分支 m2/rivet-zones-b）。（会话中曾误记“M2 已结束”，用户已更正）

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

## 最近一次会话
- 日期：2026-10-18
- 完成：M2 第三部分 3A 验收并合并到 main（246b3d1）；3B——检测区与可达性，待用户验收，分支 `m2/rivet-zones-b`
  - 用户确认：CFG_Inspection 全部新参数；安全距离按三维间隙判定；基准标记贴在蒙皮上；停靠标记板前方 1.0 m 再向外 0.8 m；区域中心 5.0 → 4.85 m（避开右侧机身竖缝）。见 ADR-014
  - Core：`InspectionConfig` 工位间距 1.5 m，新增区域位置、铆钉排高度、停靠距离、标记偏移与 AprilTag 参数
  - Simulation/World：`InspectionRegion`、`InspectionStation`、`Rivet`、`RivetRegistry`、`RegionMarker`、`DefectType`、`RegionSide`、`MarkerRole`、`RivetLayout`、`DefectAssigner`
  - Editor：`RivetZoneGenerator`（`RivetSim/Generate/Rivet Zones`，主场景生成时自动调用）、`AprilTagMaterialBuilder`（M_AprilTag_xx）；`PropsGenerator` 标记板改为深色边框 + 白底 + 标记、充电桩正面贴 ID 8；`ReachabilityChecker`（`RivetSim/Debug/Check Station Reachability`，报告 Docs/Reference/M2_Reachability.md）；`ScreenshotTool` 新增 `Capture Rivet Zone Screenshots`
  - 结果：2 个区域、4 个工位、48 颗铆钉（每工位 12 颗）；真值缺陷 5 颗：R1-S1-07 腐蚀减薄、R1-S1-10 孔边裂纹、R1-S1-12 铆钉松动、R2-S1-09 孔边裂纹、R2-S1-12 腐蚀减薄
  - 可达性：4 个工位在升降柱伸出 0.30～1.40 m（安装面 1.10～2.20 m）时 12 颗全部可达；LiftHeightPlanner 给出 1.05 m（安装面 1.85 m），该高度下 12/12；三维间隙：车体 0.895 m、升降柱 0.383～0.395 m（要求 ≥ 0.30 m）
  - Generate All 连续两次：主场景物体总数均为 1078
- 修改文件：
  - 代码：Scripts/Core/Config/InspectionConfig.cs；Scripts/Simulation/World/*.cs（10 个新文件）；Scripts/Editor/Generators/{RivetZoneGenerator, AprilTagMaterialBuilder}.cs（新）、{PropsGenerator, MainSceneBuilder}.cs；Scripts/Editor/Tools/{ReachabilityChecker（新）, ScreenshotTool, ProjectPaths}.cs；Scripts/Tests/EditMode/{RivetLayoutTests, DefectAssignerTests}.cs（新）
  - 资产：Config/CFG_Inspection.asset；Art/Materials/M_AprilTag_00～08；Prefabs/Environment/PF_MarkerBoard、PF_ChargingDock 等（重新生成）；Scenes/Main_TeachingHall.unity、Scenes/Sandbox/*
  - 文档：Docs/Architecture.md §3.2 / §4 / §6.1、Docs/DECISIONS.md（ADR-014）、Docs/Reference/M2_Reachability.md、Docs/PROGRESS.md、Docs/Screenshots/M2/13～15
- 会话自检（2026-10-18）：unity status = ready；编译 0 错误 0 警告；EditMode 59/59 通过；Main_TeachingHall Play 模式 0 错误 0 警告；Sandbox_Robot Play 模式 0 错误（1 条警告为演示预期的联锁拒绝）；场景无未保存改动，已退出 Play 模式
- 遗留问题：
  - UR5e 预制体默认是零位（手臂伸直），运输姿态 Stow / 观察姿态 Observe 在 M4 写入 CFG_Arm.namedPoses；目前联锁用 `PlaceholderArmPoseState`（视为运输姿态）
  - 可达性只验证了探头末端，机械臂连杆与机身的碰撞未检查（M4）
  - 铆钉与标记都在 Environment 层；M3 激光只扫离地 0.30 m，看不到它们，视觉识别在 M4 / M5 按视场判定
  - 场景整体偏暗（M7 补光）；A320 三角面约 73 万（M7 复核帧率）
  - Unity Editor 长时间 eval 会触发 5 s 主线程超时；大量导入时等待 `unity status` 恢复后再继续
  - 在 PowerShell 里给 `unity command eval --code` 传含双引号的代码会被剥掉引号；改用 Bash 调用 CLI，或用 `eval_file`；Bash heredoc 传给 Python 的中文可能按 GBK 解码，改文件时用 Edit 工具或 `io.open(..., encoding='utf-8')`
- 下次第一步：用户验收 3B（截图 13～15 与可达性报告）后合并分支，M2 整体验收；之后执行 PromptTemplates.md 的 M3

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
- [ ] M2：模型机 1:1.5、厂房 64 × 44 × 12 m、AGV 升降柱（第一、二部分已验收）、导入外部资产（3A 已验收、3B 待验收），见 ADR-010～013
- [ ] v2：升降柱升高后的整机稳定裕度分析（质心高度上升）
- [ ] M5：启动时用 `TaskMatrixData.GetExecutorKeys()` 与 `ExecutorRegistry` 核对缺失的执行器键名（目前 JSON 中共 37 个）
- [x] M1：确定 `CFG_SceneLayout` 中尺寸向量（长 × 宽 × 高）与世界坐标轴的对应关系（ADR-008）
- [ ] M3 / M5：AGV 改为由 AppBootstrap 运行时生成，删除主场景中的 AGV_Preview（ADR-009）
- [ ] M7：演示区补光 / 主光方向调整，提升演示台近景亮度
