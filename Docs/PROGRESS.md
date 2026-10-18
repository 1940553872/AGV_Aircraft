# 开发进度

## 当前里程碑
M1 场景白盒与生成模型 —— 已实现，待用户验收（2026-10-18）

## 环境（2026-10-17 核实）
- Unity 6000.0.71f1，URP 17.0.4，Input System 1.19，AI Navigation 2.0.11
- Unity CLI 1.0.0-beta.13 + com.unity.pipeline 0.8.0-exp.1，unity status = ready（端口 7800）
- 原有测试内容（HelloWorld.unity、SampleScene.unity、HelloSphere.cs、Materials/、TutorialInfo/、Readme.asset）已于 M0 归档到 Assets/_Archive/；Settings/（URP 资产）与 InputSystem_Actions 被管线和输入系统引用，留在原位
- Newtonsoft Json 3.2.1 已显式加入 manifest（ADR-006）
- Git：远端 https://github.com/1940553872/AGV_Aircraft（main）

## 资产准备（2026-10-17）
- 已就绪：UR5e、Airbus A320-200（.glb）、Noto Sans SC、AprilTag ID 0～11（详见 _Downloads/README.md）
- 待下载：Poly Haven 3 套材质（直链见 _Downloads/Textures/*/SOURCE.md）
- M2 注意：A320 原始单位约为真机的 4.7 倍，需按 1:5 缩放到机长 7.5 m；约 73 万三角面，需精简；需确认名为 Plane_8 的物体是否为地面或底座

## 已验收
| 里程碑 | 日期 | 备注 |
|---|---|---|
| M0 工程整理与骨架 | 2026-10-18 | 用户核对：矩阵打印与参考图一致、24 个 EditMode 测试通过、CFG 默认参数正确；提交 fb320a7 |

## 最近一次会话
- 日期：2026-10-18
- 完成：M1 场景白盒与生成模型（待验收）
  - CFG_SceneLayout 写入经确认的布局取值（三区、5 个障碍物、充电位、起始位、巡游路线、模型机中心、演示台 / 大屏 / 入口门），表格见 Architecture §4；轴约定见 ADR-008
  - Core：`ObstacleSpec`、`ObstacleKind`、`SizeAxes`；`ZoneRect` / `PlanarPose` 增加构造函数与换算属性；`SceneLayoutConfig.obstacles` 改为 `ObstacleSpec[]`，新增 `aircraftCenterM`、`demoDeskPose`、`bigScreenPose`、入口门 3 项
  - Simulation：`SceneLayoutValidator` + `SceneLayoutReport`（包含关系、作业通道、路线可通行、最窄通道、最小距离）
  - Editor：`RivetSim/Generate/*` 生成器（Material Library、Zone Labels、Props、Aircraft Placeholder、End Effector、Arm Placeholder、AGV、Demo Station、Main Teaching Hall Scene、Sandbox Scenes、All），`HallGenerator`、`SceneLightingSetup`、`GeneratorUtility`；`RivetSim/Debug/Capture M1 Screenshots`；Editor asmdef 增加 URP Core / Universal Runtime 引用
  - 生成物：29 个材质 + 5 个地面文字材质、T_HazardStripe、T_ZoneLabel_*、VP_Main、13 个预制体、Main_TeachingHall（Build Settings 第 0 个）、Sandbox_Robot、Sandbox_UI、UI_PanelSettings_Sandbox + UI_Theme_Runtime.tss
  - 提前导入 NotoSansSC-Bold.ttf 与 OFL.txt 到 `Assets/ThirdParty/Fonts/NotoSansSC/`（地面文字烘焙用）
  - 文档：ADR-008、ADR-009；Architecture §4 布局取值表；ThirdPartyAssets.md 字体路径
- 布局校验结果：最窄通道 1.60 m（O1 与 O2 之间，AGV 宽 0.70 m，单侧余量 0.45 m）；巡游路线全长 60 m，AGV 外轮廓到机身最小 1.40 m（机头处），到障碍物最小 0.45 m（窄通道）；模型机到障碍物最小 5.40 m，到作业区边界最小 3.25 m；停靠设计 AGV 底盘到机身 0.30 m（M2 / M3 验证）
- 截图（Docs/Screenshots/M1/）：01_TopDown 俯视总览（带标注）、02_AGV AGV 近景、03_DemoStation 演示台近景、04_GameView_Play Play 模式总览机位
- 自检：编译 0 错误 0 警告；EditMode 31/31 通过（新增 7 个）；Play 模式 Console 0 错误 0 警告；`Generate/All` 连续执行两次场景节点数（400）与资产数（69）不变；场景已保存，已退出 Play 模式
- 遗留问题：
  - 演示台靠西墙，近景偏暗；M7 观感打磨时考虑补光或调整主光方向
  - 占位模型机机翼为无后掠长方体；工位建议放在机翼后缘与平尾之间（机身中心 Z 约 −1.0 / −2.2），M2 按真实模型确定
  - `capture_game_view --save_path` 会把相对路径解析到 `Assets/` 下，存图后须移出并删除误建目录（本次已处理）
- 下次第一步：用户验收 M1（布局与截图）；通过后执行 M2（导入外部资产）

## 参数与流程约定（2026-10-17 确认）
- 硬件与场地参数：v1 采用 Architecture §6.1 的预估值（模型机 A320 外形缩比 1:5、AGV 1.00 × 0.70 × 0.45 m、UR5e、10 MHz 探头、接触力 10 N 等），后续按实际选型修改配置。
- t(6,1)“倒车退出 换站”：只在本工位最后一颗铆钉完成时执行；v1 不为它设计故障处理分支。
- 条件 C1～C12：v1 使用 JSON `conditions` 中的固定文字与教学示例阈值，后续版本细化。
- 随机种子：42（CFG_SimSettings，2026-10-18 确认）。
- Layers 固定为 8 Environment、9 Aircraft、10 Obstacle、11 Robot、12 Visualization（常量见 `SimLayers`）；Fixed Timestep 0.01 s。
- 配置资产 CFG_* 由 `RivetSim/Setup/Create Default Configs` 生成，只补缺不覆盖（ADR-007）；调参直接改资产并在 `notes` 注明来源。

## 待确认参数（后续版本）
| 参数 | v1 取值 | 配置 |
|---|---|---|
| 实际模型机型号与缩比 | A320 外形 1:5 | CFG_SceneLayout / CFG_Inspection |
| AGV 选型与运动方式 | 差速、1.00 × 0.70 × 0.45 m | CFG_Agv |
| 机械臂选型 | UR5e | CFG_Arm |
| 探头与检测工艺参数 | 10 MHz、2 mm 铝蒙皮、8～12 N | CFG_Inspection |
| C1～C12 的正式定义 | 固定示例文字 | SOP_TaskMatrix_v1.json |

## 待办（超出当前里程碑的想法）
- [ ] M5：启动时用 `TaskMatrixData.GetExecutorKeys()` 与 `ExecutorRegistry` 核对缺失的执行器键名（目前 JSON 中共 37 个）
- [x] M1：确定 `CFG_SceneLayout` 中尺寸向量（长 × 宽 × 高）与世界坐标轴的对应关系（ADR-008）
- [ ] M3 / M5：AGV 改为由 AppBootstrap 运行时生成，删除主场景中的 AGV_Preview（ADR-009）
- [ ] M7：演示区补光 / 主光方向调整，提升演示台近景亮度
