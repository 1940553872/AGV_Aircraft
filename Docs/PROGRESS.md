# 开发进度

## 当前里程碑
M2 厂房与布局重建（1:1.5）＋ AGV 升降柱 ＋ 导入外部资产 —— 第一部分已完成、待用户验收（分支 m2/hall-layout-1to1.5）；第二、三部分未开始（M2 分三部分，见 PromptTemplates.md 与 ADR-010）

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
- 完成：M2 第一部分——厂房与布局重建（模型机 1:1.5），待用户验收；分支 `m2/hall-layout-1to1.5`
  - 取值经用户确认（巡游路线选外环方案 A；CFG_Agv 本部分只改车体 / 轮距 / 质量 / 雷达位置；新增两项校验参数），见 Architecture §4 与 ADR-011
  - Core：`SceneLayoutConfig` 默认值改为 M2 布局，新增 `chargingDockMinToWorkZoneM`（6 m）、`demoAudienceDepthMinM`（4 m）；`AgvConfig` 车体 1.20 × 0.80 × 0.45 m、轮距 0.66 m、质量 180 kg、雷达 z = 0.60 m
  - Simulation：`SceneLayoutValidator` 新增充电位到作业区、演示台前观众区进深两项检查；`SceneLayoutReport` 新增对应字段
  - Editor：`AircraftPlaceholderGenerator` 外观常量改为相对机长的比例；`PropsGenerator` 支撑架 / 轮挡按新尺寸；`HallGenerator` 钢柱跨距 9 m、顶灯 3 排、地面文字加大；`MainSceneBuilder` 总览机位移到西南角、末端特写锚点对准后机身侧壁；`ScreenshotTool` 菜单改为 `RivetSim/Debug/Capture Layout Screenshots`，输出 5 张图到 Docs/Screenshots/M2/
  - CFG 写入：先核对两份资产现值与 M1 默认值逐项一致（无人工调参），再用 eval_file 执行：CFG_SceneLayout 整份 `CopySerialized`，CFG_Agv 经 `SerializedObject` 只改 5 个字段（脚本未入库，做法见 ADR-011）
  - 布局校验：最窄通道 1.80 m；巡游路线 169.2 m，AGV 外轮廓到机身最小 6.38 m（到包围盒 6.17 m，到障碍物 0.50 m）；模型机到障碍物最近 4.68 m（O4）；模型机到作业区边界 4.07 m；充电位到作业区 10.40 m；观众区进深 5.10 m
  - Generate All 连续执行两次，主场景物体总数均为 476，无重复对象
- 修改文件：
  - 代码：Scripts/Core/Config/{SceneLayoutConfig, AgvConfig}.cs；Scripts/Simulation/World/{SceneLayoutValidator, SceneLayoutReport}.cs；Scripts/Editor/Generators/{AircraftPlaceholderGenerator, PropsGenerator, HallGenerator, MainSceneBuilder}.cs；Scripts/Editor/Tools/ScreenshotTool.cs；Scripts/Tests/EditMode/SceneLayoutValidatorTests.cs（新增 2 个测试）
  - 资产（重新生成）：Config/CFG_SceneLayout.asset、CFG_Agv.asset；Prefabs/**；Scenes/Main_TeachingHall.unity、Scenes/Sandbox/*；Art/Textures/T_ZoneLabel_*、Art/Materials/M_Hazard_Stripe_*、Art/VP_Main.asset
  - 文档：Docs/Architecture.md §4（M2 布局表替换 M1 表）、Docs/DECISIONS.md（ADR-011）、Docs/PROGRESS.md、Docs/Screenshots/M2/*.png（01 俯视总览、02 总览机位、03 从演示区看机身侧面、04 AGV 近景、05 演示台近景）
- 会话结束自检（2026-10-18）：unity status = ready；编译 0 错误 0 警告；EditMode 33/33 通过；Play 模式 Console 0 错误 0 警告；场景无未保存改动，已退出 Play 模式（运行测试时 Console 出现 1 条 EventBus 测试故意触发的异常日志，属预期）
- 遗留问题：
  - 占位模型机按 M1 比例放大，机翼 / 发动机 / 起落架只是示意；M2 第三部分用 A320 模型替换，并用实测外轮廓复核作业区与铆钉排高度
  - CFG_Inspection 的 `stationSpacingM` 仍为 1.2 m，与 Architecture §6.1 的 1.5 m 不一致，M2 第三部分（RivetZoneGenerator）前确认并修正
  - 机械臂仍装在平台面（0.45 m），升降柱与安装高度在 M2 第二部分加入；车载设备盒布置届时一并复查
  - 演示区近景偏暗（M7 补光）
  - 巡游路线全长 169 m，0.6 m/s 下约 4.7 min；M3 / M6 建图演示时需配合倍速
  - `capture_game_view --save_path` 会把相对路径解析到 `Assets/` 下：验收截图优先用 `RivetSim/Debug/Capture Layout Screenshots`
  - 在 PowerShell 里给 `unity command eval --code` 传含双引号的代码会被剥掉引号；改用 Bash 调用 CLI，或把代码写进文件后用 `eval_file`
- 下次第一步：用户验收第一部分（俯视截图与校验数值）并合并分支后，执行 PromptTemplates.md 的 M2 第二部分（AGV 升降柱）。先回复计划，我确认后再实现

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
- [ ] M2：模型机 1:1.5、厂房 64 × 44 × 12 m（第一部分已完成，待验收）、AGV 升降柱（硬件 + 动作 + 联锁），见 ADR-010 与 PromptTemplates 的 M2
- [ ] v2：升降柱升高后的整机稳定裕度分析（质心高度上升）
- [ ] M5：启动时用 `TaskMatrixData.GetExecutorKeys()` 与 `ExecutorRegistry` 核对缺失的执行器键名（目前 JSON 中共 37 个）
- [x] M1：确定 `CFG_SceneLayout` 中尺寸向量（长 × 宽 × 高）与世界坐标轴的对应关系（ADR-008）
- [ ] M3 / M5：AGV 改为由 AppBootstrap 运行时生成，删除主场景中的 AGV_Preview（ADR-009）
- [ ] M7：演示区补光 / 主光方向调整，提升演示台近景亮度
