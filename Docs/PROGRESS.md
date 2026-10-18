# 开发进度

## 当前里程碑
M2 厂房与布局重建（1:1.5）＋ AGV 升降柱 ＋ 导入外部资产 —— 第一、二部分已验收；第三部分拆为 3A / 3B（ADR-013）：3A 导入外部资产已于 2026-10-18 验收并合并到 main，3B 检测区与可达性计划中。（会话中曾误记“M2 已结束”，用户已更正）

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
- 完成：M2 第一、二部分验收并合并到 main（339f87e）；M2 第三部分 3A——导入外部资产，已验收（提交 0501bf7，分支 `m2/import-assets-a` 已合并到 main）
  - 用户确认：拆为 3A / 3B；A320 不减面（约 73 万）；模型机外形改用实测值。见 ADR-013
  - 依赖：`com.unity.cloud.gltfast` 6.20.0
  - Editor/Importers：`ThirdPartyImporter`（`RivetSim/Import/Third Party Assets`，复制 + 导入设置）、`UrYamlReader`（UR yaml 最小读取器，支持 `!degrees`）
  - Simulation/Robot：`RosFrame`（ROS → Unity 换算）、`ArmJoint`、`ArmRig`
  - Editor/Generators：`UR5eArmGenerator`（PF_Arm_UR5e，CFG_Arm.kinematicsYaml 指向导入的 yaml）、`AircraftA320Builder` + `AircraftMeasurement`（PF_Aircraft_A320_Model Variant + 外层 PF_Aircraft_A320，实测外形）、`PolyHavenMaterialBuilder`（地面 / 墙面 / 台面 / AGV 平台贴图 + 派生金属度光滑度与 AO 贴图）、`UiFontAssetBuilder`（Noto Sans SC 三字重 FontAsset，主题默认 Regular）；`MainSceneBuilder` 优先用 A320（按实测包围盒中心对齐 aircraftCenterM，轮挡按实测机轮，不放机翼支撑架）；`AgvGenerator` 优先用 UR5e，平台用 M_AGV_Deck；`GenerateAllMenu` 加入新生成器
  - Editor/Tools：`ArmJointTestCapture`（`RivetSim/Debug/Capture Arm Joint Test`）；`ScreenshotTool` 新增 `Capture Asset Import Screenshots`、`Capture UR5e Lift Screenshots`
  - CFG：CFG_SceneLayout 外形改为实测（翼展 22.73、机高 7.92、机身直径 2.63、中线 2.63、下缘 1.25 m）；CFG_Arm.kinematicsYaml 已指定
  - UR5e 核对：6 个关节原点与 default_kinematics.yaml 一致，限位 elbow ±180°、其余 ±360°，速度均 180°/s；零位 tool0 = (−0.2329, 0.0628, 0.8172)（Unity，= ROS (0.8172, 0.2329, 0.0628)），竖直姿态 tool0 高 1.0794 m，与理论一致；6 个关节 ±90° 拼图中网格均跟随正确连杆
  - A320 实测：机长 25.05、翼展 22.73、机高 7.92、机身直径 2.63、中线离地 2.63、下缘离地 1.25 m；主起落架半轮距 2.68 m、机轮半径 0.39 m；包围盒中心在主起落架后 0.71 m
  - 布局复核（实测外形）：校验通过；最窄通道 1.80 m；巡游时 AGV 到机身 / 包围盒最小 6.38 m；模型机到障碍物 4.68 m；到作业区边界 4.08 m；充电位到作业区 10.40 m；观众区进深 5.10 m
  - Generate All 连续两次：主场景物体总数均为 936
- 修改文件：
  - 代码：Scripts/Core/Config/SceneLayoutConfig.cs；Scripts/Simulation/Robot/{RosFrame, ArmJoint, ArmRig}.cs；Scripts/Editor/Importers/{ThirdPartyImporter, UrYamlReader}.cs；Scripts/Editor/Generators/{UR5eArmGenerator, AircraftA320Builder, AircraftMeasurement, PolyHavenMaterialBuilder, UiFontAssetBuilder, MainSceneBuilder, AgvGenerator, GenerateAllMenu}.cs；Scripts/Editor/Tools/{ArmJointTestCapture, ScreenshotTool}.cs；Scripts/Tests/EditMode/{RosFrameTests（新）, SceneLayoutValidatorTests}.cs
  - 资产：Assets/ThirdParty/**；Prefabs/Robot/PF_Arm_UR5e、Prefabs/Aircraft/PF_Aircraft_A320{,_Model}；Art/Materials/M_UR5e_*、M_AGV_Deck 等；Art/Textures/Generated/*；UI/Fonts/*、UI/UI_Theme_Runtime.tss；Config/CFG_SceneLayout、CFG_Arm；场景（重新生成）；Packages/manifest.json、packages-lock.json
  - 文档：Docs/ThirdPartyAssets.md、Docs/Architecture.md §4 / §6.1、Docs/DECISIONS.md（ADR-013 改写）、Docs/Reference/A320_Hierarchy.md、Docs/PROGRESS.md、Docs/Screenshots/M2/08～12；_Downloads/Textures/*/SOURCE.md（下载日期）
- 会话自检（2026-10-18）：unity status = ready；编译 0 错误 0 警告；EditMode 51/51 通过；Main_TeachingHall Play 模式 0 错误 0 警告；Sandbox_Robot Play 模式 0 错误（2 条警告为演示预期的联锁拒绝）；场景无未保存改动，已退出 Play 模式
- 遗留问题：
  - 3B 未做：RivetZoneGenerator（左右各 1 区 × 2 工位 × 12 颗、投影到机身外壳、10% 真值缺陷）、AprilTag 标记板、UR5e 可达性验证；需先确认 CFG_Inspection 新参数（`stationSpacingM` 1.2 → 1.5 m、`rivetRowHeightM` 2.15 m、标记 ID 与边长、`dockingStandoffM`）
  - 场景整体偏暗（新贴图更暗），M7 补光；地面混凝土贴图偏褐，必要时调 M_Floor_Epoxy 色调
  - A320 三角面约 73 万（用户接受）；M7 性能检查时复核帧率
  - 占位臂 `PF_Arm_Placeholder` 与 `PlaceholderArmPoseState` 保留到 M4；占位模型机生成器保留为无 A320 时的后备
  - 本会话 Unity Editor 曾两次失去 Pipeline 连接（一次需重启 Editor）：长时间 eval 会触发 5 s 主线程超时，大量导入时等待 `unity status` 恢复后再继续
  - `capture_game_view --save_path` 会把相对路径解析到 `Assets/` 下：验收截图用 RivetSim/Debug 下的截图菜单
  - 在 PowerShell 里给 `unity command eval --code` 传含双引号的代码会被剥掉引号；改用 Bash 调用 CLI，或用 `eval_file`；Bash heredoc 传给 Python 的中文会按 GBK 解码，改文件时用 Edit 工具或 `io.open(..., encoding='utf-8')`
- 下次第一步：给出 3B 计划（先定 CFG_Inspection 新参数），确认后实现

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
- [ ] M2：模型机 1:1.5、厂房 64 × 44 × 12 m、AGV 升降柱（第一、二部分已验收）、导入外部资产（3A 已验收、3B 未开始），见 ADR-010～013
- [ ] v2：升降柱升高后的整机稳定裕度分析（质心高度上升）
- [ ] M5：启动时用 `TaskMatrixData.GetExecutorKeys()` 与 `ExecutorRegistry` 核对缺失的执行器键名（目前 JSON 中共 37 个）
- [x] M1：确定 `CFG_SceneLayout` 中尺寸向量（长 × 宽 × 高）与世界坐标轴的对应关系（ADR-008）
- [ ] M3 / M5：AGV 改为由 AppBootstrap 运行时生成，删除主场景中的 AGV_Preview（ADR-009）
- [ ] M7：演示区补光 / 主光方向调整，提升演示台近景亮度
