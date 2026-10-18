# 开发进度

## 当前里程碑
M2 厂房与布局重建（1:1.5）＋ AGV 升降柱 ＋ 导入外部资产 —— 未开始（M1 已于 2026-10-18 验收；M2 分三部分，见 PromptTemplates.md 与 ADR-010）

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

## 最近一次会话
- 日期：2026-10-18
- 完成：M1 场景白盒与生成模型（已验收，提交 1b5b059）
  - CFG_SceneLayout 写入 M1 布局取值（三区、5 个障碍物、充电位、起始位、巡游路线、模型机中心、演示台 / 大屏 / 入口门）；尺寸向量轴约定见 ADR-008
  - Core：`ObstacleSpec`、`ObstacleKind`、`SizeAxes`；`ZoneRect` / `PlanarPose` 增加构造函数与换算属性；`SceneLayoutConfig.obstacles` 改为 `ObstacleSpec[]`，新增 `aircraftCenterM`、`demoDeskPose`、`bigScreenPose`、入口门 3 项
  - Simulation：`SceneLayoutValidator` + `SceneLayoutReport`（三区包含关系、作业通道、路线可通行、最窄通道、最小距离）
  - Editor：`RivetSim/Generate/*` 幂等生成器（Material Library、Zone Labels、Props、Aircraft Placeholder、End Effector、Arm Placeholder、AGV、Demo Station、Main Teaching Hall Scene、Sandbox Scenes、All）；`HallGenerator`、`SceneLightingSetup`、`GeneratorUtility`；`RivetSim/Debug/Capture M1 Screenshots`
  - 生成物：34 个材质、T_HazardStripe、T_ZoneLabel_* ×5、VP_Main、13 个预制体、Main_TeachingHall（Build Settings 第 0 个）、Sandbox_Robot、Sandbox_UI、UI_PanelSettings_Sandbox + UI_Theme_Runtime.tss
  - 布局校验：最窄通道 1.60 m；巡游路线 60 m，AGV 外轮廓到机身最小 1.40 m；模型机到障碍物最小 5.40 m；停靠设计 0.30 m
- 修改文件：
  - 代码：Assets/_Project/Scripts/Core/Config/{SceneLayoutConfig, ZoneRect, PlanarPose}.cs（改）、{ObstacleSpec, ObstacleKind, SizeAxes}.cs（新）；Scripts/Simulation/World/{SceneLayoutValidator, SceneLayoutReport}.cs；Scripts/Editor/Generators/*.cs（13 个）；Scripts/Editor/Tools/{ProjectPaths（改）, ScreenshotTool}.cs；Scripts/Editor/RivetSim.Editor.asmdef（加 URP 引用）；Scripts/Tests/EditMode/{SceneLayoutValidatorTests, SizeAxesTests}.cs
  - 资产：Config/CFG_SceneLayout.asset；Art/Materials/*、Art/Textures/*、Art/VP_Main.asset；Prefabs/**；Scenes/Main_TeachingHall.unity、Scenes/Sandbox/*；UI/*；ThirdParty/Fonts/NotoSansSC/（Bold + OFL）；ProjectSettings/EditorBuildSettings.asset
  - 文档：Docs/DECISIONS.md（ADR-008、ADR-009）、Docs/Architecture.md §4、Docs/ThirdPartyAssets.md、Docs/PROGRESS.md、Docs/Screenshots/M1/*.png
  - 用户在会话外修改、随本次交接一并提交：ADR-010（模型机 1:1.5、厂房 64 × 44 × 12 m、AGV 加装升降柱与联锁）；Architecture §4 / §6.1；AssetGuide、PromptTemplates（M2 拆为三部分，M3～M5 补充升降柱与虚拟禁行区）；_Downloads/README.md 与 A320 SOURCE.md；SOP_TaskMatrix_v1.json 中 7 个单元的 `explain`（加入升降柱描述，`label` 未改）
- 会话结束自检（2026-10-18）：unity status = ready；编译 0 错误 0 警告；EditMode 31/31 通过（含 JSON 改动后的矩阵读取测试）；Play 模式 Console 0 错误 0 警告；场景无未保存改动，已退出 Play 模式；已推送 GitHub
- 遗留问题（M2 第一部分需处理）：
  - CFG_SceneLayout、CFG_Agv 仍是 M1 取值（1:5、40 × 24 × 10 m、车体 1.00 × 0.70 m）。按 ADR-007，Create Default Configs 不会覆盖已有资产：先改配置类默认值，再用 eval 执行 `EditorUtility.CopySerialized` 写入资产（M1 做法），或在确认后直接修改资产字段
  - `AircraftPlaceholderGenerator` 的外观常量（机翼弦长、平尾、发动机、起落架位置等）按 1:5 写成，需改为按机长比例计算或重设
  - `HallGenerator` 的地面文字高度、`MainSceneBuilder` 相机锚点偏移、`ScreenshotTool` 标注位置都按 40 × 24 m 大厅调过，扩到 64 × 44 m 后要复查
  - `SceneLayoutValidatorTests.DefaultLayout_KeyDistancesMatchPlan` 写死了 M1 的数值（1.60 / 1.40 / 5.40 / 3.25 / 60），改布局时同步更新
  - Architecture §4 的 M1 布局表已标注“被 M2 取代”，M2 完成后替换
  - 演示台靠西墙，近景偏暗（M7 补光）
  - `capture_game_view --save_path` 会把相对路径解析到 `Assets/` 下：存图后须移出 Assets 并用 `AssetDatabase.DeleteAsset` 删除误建目录；验收截图优先用 `RivetSim/Debug/Capture M1 Screenshots`（直接写入 Docs/Screenshots/）
  - 在 PowerShell 里给 `unity command eval --code` 传含双引号的代码会被剥掉引号；改用 Bash 调用 CLI，或把代码写进文件后用 `eval_file`
- 下次第一步：执行 PromptTemplates.md 的 M2 第一部分（厂房与布局重建，模型机 1:1.5）。先回复计划，给出新的三区 / 障碍物 / 充电位 / 起始位 / 巡游路线取值，以及 CFG_Agv 新车体尺寸的写入方式，我确认后再实现

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
- [ ] M2：模型机 1:1.5、厂房 64 × 44 × 12 m、AGV 升降柱（硬件 + 动作 + 联锁），见 ADR-010 与 PromptTemplates 的 M2
- [ ] v2：升降柱升高后的整机稳定裕度分析（质心高度上升）
- [ ] M5：启动时用 `TaskMatrixData.GetExecutorKeys()` 与 `ExecutorRegistry` 核对缺失的执行器键名（目前 JSON 中共 37 个）
- [x] M1：确定 `CFG_SceneLayout` 中尺寸向量（长 × 宽 × 高）与世界坐标轴的对应关系（ADR-008）
- [ ] M3 / M5：AGV 改为由 AppBootstrap 运行时生成，删除主场景中的 AGV_Preview（ADR-009）
- [ ] M7：演示区补光 / 主光方向调整，提升演示台近景亮度
