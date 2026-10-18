# 开发进度

## 当前里程碑
M1 场景白盒与生成模型 —— 未开始（M0 已于 2026-10-18 验收）

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
- 完成：M0 工程整理与骨架（已验收）
  - 归档旧测试内容到 `Assets/_Archive/`（AssetDatabase.MoveAsset，GUID 保留），归档场景已移出 Build Settings
  - 按 Architecture §2 建立 `_Project/` 目录；5 个 asmdef（Core ← Simulation ← Presentation；Editor 仅编辑器；Tests.EditMode → Core、Simulation）
  - Core：`SimClock`、`EventBus` + `ISimEvent`、`SimLog`（含 `LogCategory` / `SimLogLevel` / `SimLogEntry`）、任务矩阵数据模型与 `TaskMatrixLoader`（§3.1 校验，外加条件引用、trigger、id 与行列一致性检查）
  - 配置类型 6 种（SimSettings、SceneLayoutConfig、AgvConfig、ArmConfig、InspectionConfig、UiThemeConfig）及辅助结构体；`Config/` 下已生成 CFG_* 默认资产，`CFG_SimSettings.taskMatrixJson` 指向矩阵 JSON
  - 新增 `SimLayers`（Layer 名称、编号与激光层掩码常量），Layers 8～12 = Environment / Aircraft / Obstacle / Robot / Visualization；Fixed Timestep = 0.01 s
  - 编辑器菜单：`RivetSim/Setup/*`（建目录、Layers 与时间步、默认配置，均可重复执行；配置资产已存在时不覆盖）、`RivetSim/Debug/Print Task Matrix`
  - EditMode 测试 24 个全部通过；打印矩阵已与 SOP_TaskMatrix_v1.png 逐格核对一致
- 修改文件：Assets/_Project/**（Scripts、Config/CFG_*.asset、空目录骨架）、Assets/_Archive/**、Packages/manifest.json、Packages/packages-lock.json、ProjectSettings/TagManager.asset、TimeManager.asset、EditorBuildSettings.asset、Docs/DECISIONS.md（ADR-006、ADR-007）、Docs/PROGRESS.md
- 遗留问题：
  - 未定参数留空（随机种子已定为 42）：三区矩形、障碍物、充电位、起始位、巡游路线 → M1；机械臂 Stow / Observe 姿态、闸门 A/B 阈值 → M4
  - 当前打开的是归档场景 `_Archive/Scenes/HelloWorld.unity`；M1 建立 Main_TeachingHall 后切换
  - JSON 未改动（label、explain 均未修改）
- 会话结束自检（2026-10-18）：unity status = ready；编译 0 错误 0 警告；EditMode 24/24 通过；Play 模式 Console 无错误；无未保存场景；已推送 GitHub
- 下次第一步：执行 PromptTemplates.md 的 M1（场景白盒与生成模型）；先确定三区矩形、障碍物、充电位、起始位、巡游路线的取值，以及 CFG_SceneLayout 尺寸向量与世界坐标轴的对应关系，经确认后写入 CFG_SceneLayout

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
- [ ] M1：确定 `CFG_SceneLayout` 中尺寸向量（长 × 宽 × 高）与世界坐标轴的对应关系
