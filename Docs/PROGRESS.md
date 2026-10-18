# 开发进度

## 当前里程碑
M3 AGV 建图、规划与移动 —— 未开始（M2 已于 2026-10-18 由用户宣布验收通过，按第一、二部分结束；第三部分“导入外部资产”未实现，推迟为独立任务，须在 M4 前完成，见 ADR-013）

## 环境（2026-10-17 核实）
- Unity 6000.0.71f1，URP 17.0.4，Input System 1.19，AI Navigation 2.0.11
- Unity CLI 1.0.0-beta.13 + com.unity.pipeline 0.8.0-exp.1，unity status = ready（端口 7800）
- 原有测试内容（HelloWorld.unity、SampleScene.unity、HelloSphere.cs、Materials/、TutorialInfo/、Readme.asset）已于 M0 归档到 Assets/_Archive/；Settings/（URP 资产）与 InputSystem_Actions 被管线和输入系统引用，留在原位
- Newtonsoft Json 3.2.1 已显式加入 manifest（ADR-006）
- Git：远端 https://github.com/1940553872/AGV_Aircraft（main）

## 资产准备（2026-10-17）
- 已就绪：UR5e、Airbus A320-200（.glb）、Noto Sans SC、AprilTag ID 0～11（详见 _Downloads/README.md）
- Poly Haven 3 套材质已下载（2K JPG，文件时间 2026-10-17）；各 SOURCE.md 的“下载日期”尚未填写
- 许可核对（2026-10-18）：UR5e BSD-3、A320 CC BY 4.0（必须署名）、AprilTag BSD-2、Noto Sans SC OFL 1.1、Poly Haven CC0，均齐全
- A320 GLB 解析结果（2026-10-18，供导入任务使用）：223 个网格、732,032 三角面、60 个材质；机身沿模型 X 轴、机头在 −X 端、Y 向上；按机长 179.45 单位缩放到 25.05 m（系数 0.1396）后翼展 22.86 m、机高 8.09 m、机身直径 2.64 m、中线离地约 2.63 m、下缘约 1.25 m（与 §6.1 估算 23.87 / 7.84 m 有出入）
  - **Plane_8 是主机翼，不是地面，须保留**；Cube.017_196、Cube.018_198、Cylinder.135_200 为主起落架附近约 36 × 36 单位的竖直方板、最低点低于地面 10 单位，疑似底座 / 辅助物体，导入后截图确认
  - 三角面大户：发动机短舱 Object_50 12.6 万、风扇叶片 Cylinder.023 9.0 万、短舱第二层 Object_51 3.8 万、主起落架 2 × 3.6 万、驾驶舱内饰（机头 x < −40 的小件，不含前起落架机轮）约 7.4 万；只隐藏不可见件约降到 53 万，仍超 50 万预算，需减面（方案待定）
  - 机身外壳为 Cylinder.027_68（约 2.5 万面），只给它加 MeshCollider；检测区（机翼后缘 ～ 平尾前缘）按模型坐标约 4.2 m 长
- UR5e：`default_kinematics.yaml` 与 SOURCE.md 摘录一致；`visual_parameters.yaml` 给出 7 个网格偏移（wrist_3 含 −0.5 mm 修正）；DAE 单位米、Z 向上

## 已验收
| 里程碑 | 日期 | 备注 |
|---|---|---|
| M0 工程整理与骨架 | 2026-10-18 | 用户核对：矩阵打印与参考图一致、24 个 EditMode 测试通过、CFG 默认参数正确；提交 fb320a7 |
| M1 场景白盒与生成模型 | 2026-10-18 | 用户核对 4 张截图与布局校验数值（最窄通道 1.60 m、巡游时 AGV 到机身 1.40 m）；31 个 EditMode 测试通过；提交 1b5b059 |
| M2 第一部分 厂房与布局重建（1:1.5） | 2026-10-18 | 用户核对俯视截图与校验数值（最窄通道 1.80 m、巡游时 AGV 到机身 6.38 m、模型机到障碍物 4.68 m）；33 个 EditMode 测试通过；提交 dc33a19 |
| M2 第二部分 AGV 升降柱 | 2026-10-18 | 用户核对最低位 / 最高位截图（安装面 0.80 / 2.20 m）与沙盒联锁演示日志；47 个 EditMode 测试通过；提交 d5e00ce |
| M2 厂房与布局重建 ＋ AGV 升降柱（第三部分推迟） | 2026-10-18 | 用户宣布 M2 验收通过；会话结束自检：编译 0 错误 0 警告、EditMode 47/47、Main 与 Sandbox_Robot Play 模式 0 错误；main 位于本次交接提交 |

## 最近一次会话
- 日期：2026-10-18
- 完成：
  - M2 第一部分：厂房与布局重建（模型机 1:1.5、厂房 64 × 44 × 12 m、外环巡游路线、两项新校验），提交 dc33a19，ADR-011
  - M2 第二部分：AGV 升降柱（参数、生成、`LiftColumnController` / `LiftHeightPlanner` / `SafetyMonitor` 两条联锁、`AgvController` 最小子集、Sandbox_Robot 联锁演示），提交 d5e00ce，ADR-012
  - M2 第三部分：只完成了计划与资产检查（许可核对、A320 / UR5e 结构解析，结果记入上方“资产准备”）；用户表示计划需要调整，随后宣布 M2 验收通过并结束本会话，**导入外部资产未实现**（ADR-013）
  - 两个分支 `m2/hall-layout-1to1.5`、`m2/agv-lift-column` 均已快进合并到 main 并推送
- 修改文件（全会话）：
  - Core：Config/{SceneLayoutConfig, AgvConfig, ArmConfig}.cs
  - Simulation：World/{SceneLayoutValidator, SceneLayoutReport}.cs；Robot/{AgvController, LiftColumnController, LiftMotionProfile, LiftArrivedEvent, LiftHeightPlanner, LiftHeightPlan, SafetyMonitor, IChassisState, ILiftState, IArmPoseState, PlaceholderArmPoseState}.cs
  - Presentation：App/SandboxLiftDemo.cs
  - Editor：Generators/{AircraftPlaceholderGenerator, AgvGenerator, HallGenerator, MainSceneBuilder, PropsGenerator, SandboxSceneBuilder}.cs；Tools/ScreenshotTool.cs
  - Tests：EditMode/{SceneLayoutValidatorTests（改）, LiftMotionProfileTests, LiftHeightPlannerTests, SafetyMonitorTests}.cs
  - 资产：Config/CFG_SceneLayout、CFG_Agv、CFG_Arm；Prefabs/**、Scenes/Main_TeachingHall、Scenes/Sandbox/*、Art/Textures/T_ZoneLabel_*、Art/Materials/M_Hazard_Stripe_*、Art/VP_Main（均由生成器重建）
  - 文档：Architecture §3.2 / §4 / §6.1；DECISIONS ADR-011～013；PROGRESS；Screenshots/M2/01～07
- 会话结束自检（2026-10-18）：unity status = ready；重新编译后 0 错误 0 警告；EditMode 47/47 通过；Main_TeachingHall Play 模式 0 错误 0 警告；Sandbox_Robot Play 模式 0 错误（1 条警告为演示步骤 1 预期的联锁拒绝）；已退出 Play 模式，打开的场景为 Main_TeachingHall 且无未保存改动；工作区干净
- 遗留问题：
  - **导入外部资产未做**（UR5e、A320、AprilTag、Poly Haven 材质、Noto Sans SC Regular / Medium 的 UI 字体资源、RivetZoneGenerator、可达性验证）：M4 依赖 UR5e 与铆钉区，须在 M4 前完成；用户对第三部分计划的调整意见尚未给出。上一版计划中的待决问题：拆分为 3A / 3B、三角面预算的处理方式、是否改用实测外形尺寸、CFG_Inspection 新参数
  - 主场景仍是占位模型机（按 1:5 外观比例放大）：M3 的虚拟禁行区先按 CFG_SceneLayout 的外形参数与占位体生成；导入 A320 后需用实测外轮廓复核（翼展可能由 23.87 改为 22.86 m）
  - CFG_Inspection 的 `stationSpacingM` 为 1.2 m，与 Architecture §6.1 的 1.5 m 不一致，导入任务前确认
  - `AgvController` 只有最小子集：角速度无加速度限幅、无车轮动画、无导航接口（M3 补充）；主场景 AGV_Preview 带控制器但无人推进（ADR-009）
  - 升降柱到位判定在 ±1 mm 精度带内，实际用时比理论梯形曲线少约 0.11 s，测试已按此修正
  - 任务矩阵与升降柱的对应（label 不变）：t(1,3) / t(2,3) 最低位，t(3,3) 升到工位高度，t(6,3) 收臂并降到最低，t(6,1) 降到最低后倒车；执行器在 M5 实现
  - 演示区近景偏暗（M7 补光）；巡游路线 169.2 m（0.6 m/s 约 4.7 min）需配合倍速
  - `capture_game_view --save_path` 会把相对路径解析到 `Assets/` 下：验收截图用 `RivetSim/Debug/Capture Layout Screenshots` / `Capture Lift Screenshots`
  - 在 PowerShell 里给 `unity command eval --code` 传含双引号的代码会被剥掉引号；改用 Bash 调用 CLI，或把代码写进文件后用 `eval_file`；Python 输出中文时设 `PYTHONIOENCODING=utf-8`
- 下次第一步：用户决定先做 M3（PromptTemplates.md 的 M3 模板，先在 Sandbox_Robot 开发）还是先补做“导入外部资产”；M3 启动时先回复计划，确认后再实现

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
- [x] M2：模型机 1:1.5、厂房 64 × 44 × 12 m、AGV 升降柱（硬件 + 动作 + 联锁），见 ADR-010～012
- [ ] M4 前：导入外部资产（原 M2 第三部分，PromptTemplates M2 模板“第三部分”；ADR-013），开始前听取用户对计划的调整意见
- [ ] v2：升降柱升高后的整机稳定裕度分析（质心高度上升）
- [ ] M5：启动时用 `TaskMatrixData.GetExecutorKeys()` 与 `ExecutorRegistry` 核对缺失的执行器键名（目前 JSON 中共 37 个）
- [x] M1：确定 `CFG_SceneLayout` 中尺寸向量（长 × 宽 × 高）与世界坐标轴的对应关系（ADR-008）
- [ ] M3 / M5：AGV 改为由 AppBootstrap 运行时生成，删除主场景中的 AGV_Preview（ADR-009）
- [ ] M7：演示区补光 / 主光方向调整，提升演示台近景亮度
