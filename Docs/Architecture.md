# RivetSim 架构说明（v1）

> 工程：`D:\Unity_Project\AGV_Aircraft`　｜　Unity 6000.0.71f1 + URP　｜　根命名空间 `RivetSim`
> v1 原则：**先把内容填完整、流程跑通、演示界面好看**；算法用够用的简化实现，复杂建模留给后续版本（§9）。
> 结构变更（新增程序集、改目录、引入新包）须先在 `Docs/DECISIONS.md` 记录。

---

## 1. v1 范围

| 模块 | v1 做什么 | v1 不做（留到后续） |
|---|---|---|
| 场景 | 厂房 + 三区布局；模型机；检测区铆钉；演示台；障碍物；充电位 | 精细美术、人物 |
| AGV | 参数化底盘外观（尺寸可配），差速运动学，键盘遥控 | 麦克纳姆 / 四轮转向对比、动力学 |
| 建图 | 2D 激光射线 + 真值位姿绘制占据栅格，按预设巡游路线完成建图演示 | 扫描匹配、里程计漂移、回环 |
| 规划与移动 | 栅格 A*、路径平滑、路径跟踪、基于区域标记的低速对接停靠、倒车退出 | Hybrid A*、DWA 动态避障 |
| 机械臂 | UR5e 外观 + 运动学骨架；数值逆解；命名姿态（运输 / 观察）；对中、法向逼近、回退 | 碰撞规避规划、真实力控 |
| 贴合采集 | 虚拟弹簧接触力；贴合 / 释放条件判断 | 声场仿真 |
| 超声 | 合成 A 扫波形（教学简化模型）、闸门判读、铆钉结果着色 | 缺陷定量 |
| 任务流程 | 读取 SOP 任务矩阵 JSON，按阶段并行执行，支持逐钉循环、换站、条件单元 | 基于领域本体自动生成矩阵 |
| 演示界面 | 演示电脑屏幕（任务矩阵、执行情况说明、小地图、A 扫、日志）、全屏切换、HUD | 方案评估面板 |
| 交互 | 自动演示（倍速）、分步演示、故障注入按钮、相机预设、键盘遥控 | 方案评审模式 |
| 硬件设计 | 硬件参数面板（静态展示）；电量与通讯状态指示（简单线性模型） | 稳定裕度、能耗与链路量化对比 |

---

## 2. 目录结构

```
D:\Unity_Project\AGV_Aircraft\
├── CLAUDE.md
├── Docs\                         架构、资产指南、提示词、进度、决策、第三方登记
│   └── Reference\SOP_TaskMatrix_v1.png
├── _Downloads\                   原始下载（Unity 不导入）
└── Assets\
    ├── _Project\
    │   ├── Config\               CFG_*.asset（ScriptableObject）
    │   │   └── TaskMatrix\SOP_TaskMatrix_v1.json
    │   ├── Art\Materials | Textures | Models\Generated
    │   ├── Prefabs\Environment | Robot | Aircraft | DemoStation | Vis
    │   ├── Scenes\Main_TeachingHall.unity
    │   │   └── Sandbox\Sandbox_Robot.unity, Sandbox_UI.unity
    │   ├── Scripts\
    │   │   ├── Core\             RivetSim.Core.asmdef
    │   │   ├── Simulation\       RivetSim.Simulation.asmdef
    │   │   │   ├── World\  Robot\  Navigation\  Inspection\  Mission\
    │   │   ├── Presentation\     RivetSim.Presentation.asmdef
    │   │   │   ├── App\  UI\  Visualization\  CameraControl\  Input\
    │   │   ├── Editor\           RivetSim.Editor.asmdef（Generators\  Tools\）
    │   │   └── Tests\EditMode\   RivetSim.Tests.EditMode.asmdef
    │   └── UI\                   UXML、USS、PanelSettings、字体资源
    ├── ThirdParty\               Robots\UR5e、Aircraft\…、Fonts\、Textures\、AprilTag\
    └── _Archive\                 原有测试内容（M0 时移入）
```

---

## 3. 程序集与模块

### 3.1 RivetSim.Core

| 类型 | 说明 |
|---|---|
| `SimClock` | 仿真时间、倍速（0.5× / 1× / 2× / 4×）、暂停；业务代码一律使用 `SimClock` 的时间 |
| `EventBus` | 强类型发布 / 订阅，事件为 `readonly struct XxxEvent : ISimEvent` |
| `SimLog` | 分类日志（Mission / Nav / Arm / UT / Safety / System），同时输出到 Console 与演示屏日志 |
| `TaskMatrixData` + `TaskMatrixLoader` | 用 Newtonsoft Json 读取 `SOP_TaskMatrix_v1.json`，并校验：48 个单元齐全、id 不重复、回路阶段存在、active 和 conditional 单元的执行器键名非空 |
| 配置类型 | `SimSettings`、`SceneLayoutConfig`、`AgvConfig`、`ArmConfig`、`InspectionConfig`、`UiThemeConfig`（详见 §6） |

### 3.2 RivetSim.Simulation

**World**：`InspectionRegion`（区域：左 / 右侧与若干工位）、`InspectionStation`（工位：AGV 停靠位姿、铆钉排、停靠标记板与蒙皮基准标记）、`Rivet`（ID、位置、法向、真值缺陷 `DefectType`，检测结果在 M4 写入）、`RivetRegistry`（登记表，挂在 Zone_Work/InspectionRegions）、`RegionMarker`（AprilTag：ID、边长、用途 `MarkerRole`）、`RivetLayout` / `DefectAssigner`（排布与缺陷分配的纯计算）、`ChargingDock`。检测区由 `RivetSim/Generate/Rivet Zones` 生成（M2 第三部分 3B）。

**Robot**
- `AgvController`：差速运动学（`DiffDriveKinematics`，按圆弧弦长精确积分），运动学刚体（`MovePosition`），线速度 / 角速度及其加速度限幅；提供 `SetVelocity`、`Stop`、`EmergencyStop`、`ZeroSpeedLock`、`ReleaseZeroSpeedLock`、`IsZeroSpeedLocked`、真值位姿 `Pose`、驱动轮转动动画（M3）。`AgvFootprint` 由渲染器包围盒量得含机械臂的扫掠半径与车头前伸量。
- `LiftColumnController`（升降柱，属主体 S3）：`LiftMotionProfile` 匀速 + 起停缓冲（教学简化模型），`MoveToHeightAsync(安装面高度)`、`LowerToBottomAsync`，到位发布 `LiftArrivedEvent`；`LiftHeightPlanner` 按工位铆钉平均高度 − 舒适偏移计算目标并限制在行程内。
- `SafetyMonitor`（主体 S6）：联锁规则 1 升降柱只在底盘零速锁定后运动；规则 2 底盘行驶或解锁只在升降柱最低位且机械臂运输姿态时允许；违反时拒绝并经 `SimLog`（Safety）记录。依赖只读接口 `IChassisState` / `ILiftState` / `IArmPoseState`（M4 前由 `PlaceholderArmPoseState` 提供）。
- 控制器不自行读取 `Time`：启动入口持有 `SimClock`，每帧以仿真步长调用各控制器的 `Tick`。
- `ArmRig`：由 UR5e 的 `default_kinematics.yaml` 建立 6 关节运动学链；`ArmIkSolver` 为阻尼最小二乘数值逆解；`ArmController` 提供 `MoveToNamedPoseAsync`（Stow / Observe）、`MoveToPoseAsync`（关节插值，末段走直线）、`ApproachAlongNormalAsync`、`RetreatAsync`。
- `ProbeEndEffector`：TCP 坐标系、`ContactModel`（虚拟弹簧 F = k·δ，教学简化模型）。
- `DeviceIndicator`：挂在 S1～S8 对应的设备上，单元执行时发光高亮（设备与主体的对应关系见 §5.3）。

**Navigation**（M3 已实现，见 ADR-015）
- `Lidar2D`：360 线水平射线（`Physics.Raycast`，只检测 Environment / Aircraft / Obstacle 层），按仿真时间 10 Hz，测距噪声 ±2 cm（固定种子）；结果写入占据栅格并供点云显示。
- `OccupancyGridMap`：0.1 m 栅格；使用真值位姿，命中格加分、射线经过格减分（教学简化模型），记录脏矩形供叠加层增量刷新。
- `MappingRoutine`：沿巡游路线（CFG_SceneLayout）以巡航速度行驶完成建图，发布 `MappingProgressEvent`。
- `VirtualKeepOutZone` / `KeepOutMask` / `KeepOutChannel`：A320 可见网格三角形地面投影的栅格掩膜（编辑器生成）与工位接近通道。
- `CostMap`：激光层 ∪ 虚拟层（外扩机身安全距离 0.30 m，通道内不叠加），按车体外接圆膨胀；可通行 / 膨胀区 / 致命区三级，未知格加代价。
- `GridAStar` + `PathSmoother`：8 邻域 A*（禁止切角、起点可逃离膨胀区）+ 视线捷径平滑；终点前加沿目标朝向的引入段。
- `PathFollower`：纯追踪（曲率限速、终点按制动曲线停住、原地转向前检查含机械臂扫掠半径）。`DockingController`：车头对接相机按视场判定停靠标记可见，低速直线对接，记录停靠误差（`DockingError`）。`StraightDrive`：直线前进 / 倒车。
- `NavigationService`：`NavigateToAsync(pose)`、`FollowPathAsync(points)`、`DockAsync(station)`、`ReverseExitAsync(distanceM)`、`DriveStraightAsync`；运动控制在 `Tick` 中按仿真子步执行，每个动作开始前经 SafetyMonitor 规则 2 判定；发布 `PathPlannedEvent`、`NavigationCompletedEvent`、`DockingCompletedEvent`。

**Inspection**
- `AScanSynthesizer`：由始波、界面波、底面多次回波（声时 = 2·厚度 / 声速）、缺陷回波和噪声合成 A 扫波形；耦合质量影响幅值（教学简化模型）。
- `GateJudge`：闸门阈值判读，输出 合格 / 疑似 / 缺陷 / 无效。
- `InspectionRecordStore`：保存检测记录，可导出 CSV 到 `persistentDataPath`。

**Mission**
- `TaskMatrixRuntime`：记录每个单元的状态，见 §5.2。
- `MissionOrchestrator`：执行建图准备，再按 SOP 推进阶段与回路，见 §5.1。
- `ITaskExecutor` + `ExecutorRegistry`：执行器键名（JSON 中的 `executor`）映射到实现。
- `ConditionBus`：条件触发源，包括故障注入（识别失败、力超限、采集无效）、计数器（每 10 颗）和真实事件（采集结果无效）。

### 3.3 RivetSim.Presentation

- `AppBootstrap`：场景唯一入口。加载配置与矩阵 → 注册服务 → 进入默认模式（自动演示）。M3 已建立：在充电位运行时生成 AGV，经 `NavigationRuntime`（与 `SandboxNavDemo` 共用）组装时钟、安全监视、地图、激光与导航服务，运行 `NavigationDemoRoutine`；M5 接入 MissionOrchestrator。
- `AgvTeleopInput`（WASD 遥控、空格急停）、`VisualizationToggleInput`（V 键）；可视化 `OccupancyGridOverlay`、`KeepOutOverlay`、`LidarPointCloudView`、`PathView`；`NavOverlayView` 为 M3 导航叠加信息（M6 由 HUD 取代）。
- `DemoModeController`：自动演示 / 分步演示（每个阶段开始前暂停，点击“下一步”继续）/ 手动遥控。
- `DemoConsoleView`（UI Toolkit）：演示电脑屏幕，见 §7。
- `HudView`：顶部模式、倍速、时间，底部快捷键提示，右侧故障注入按钮。
- `CameraRig`：4 个预设机位，平滑过渡：1 总览、2 跟随 AGV、3 末端特写、4 演示台。自动演示时可按当前阶段自动切换。
- `Visualization`：栅格地图叠加、规划路径、激光点云、停靠目标、铆钉结果环、探头接触指示。可视化对象放在 `Visualization` 层，按 `V` 键开关。

---

## 4. 场景布局（Main_TeachingHall）

- 1 单位 = 1 m，Y 轴向上；世界原点在大厅地面中心；模型机机头朝 +Z。
- 默认尺寸（写在 `SceneLayoutConfig` 中，可调）：大厅 64 × 44 × 12 m（M2 起；M1 为 40 × 24 × 10 m）。作业区在中央，模型机四周留出 ≥ 4 m 的 AGV 通道。移动区环绕作业区，含充电位、起始位、3～5 个障碍物和一处窄通道。演示区靠入口一侧，放演示台 + 双显示器 + 一块大屏。
- **M2 布局取值**（模型机 1:1.5，2026-10-18 确认，CFG_SceneLayout；取代 M1 的 1:5 布局，M1 取值见提交 1b5b059；矩形写作“中心 / 尺寸”，单位 m，x = 世界 X，y = 世界 Z；轴约定见 ADR-008，分区依据 ADR-010）：

| 项 | 取值 |
|---|---|
| 大厅 | 64（X）× 44（Z）× 12 m，X ±32、Z ±22；演示区在 −X 端，入口门在南墙（X = −27.5，宽 3.0，高 3.5）；钢柱跨距 9 m（西墙中心不落柱） |
| 演示区 | (−27.5, 0) / 9 × 44（X −32～−23）；X −23～−20 为 3 m 人行过道，不属于任何区 |
| 移动区（含作业区） | (6, 0) / 52 × 44（X −20～32：西侧移动区 12 m + 作业区 32 m + 东环道 8 m；南北环道各 5.4 m） |
| 作业区 | (8, 0) / 32 × 33.2 |
| 模型机包围盒中心 | (8, 0)，机身沿 Z，机头 Z = +12.525，机身左侧朝向演示区；25.05 × 22.73 × 7.92 m（A320 模型实测，M2 第三部分 3A），机身直径 2.63 m，下缘 / 中线离地 1.25 / 2.63 m；预制体 PF_Aircraft_A320 的原点（主起落架中点）位于 (8, 0.71) |
| 作业通道 | 模型机外轮廓到障碍物 ≥ 4.0 m（`aisleClearanceM`） |
| 障碍物 | O1 货箱堆 (−15, −3.4) / 1.0 × 5.0 × 1.0；O2 货箱堆 (−15, 3.4) / 1.0 × 5.0 × 1.0（O1、O2 间为 1.8 m 窄通道）；O3 工具车 (25.5, 6) / 1.2 × 0.6 × 0.95；O4 工具车 (0, 17.5) / 1.2 × 0.6 × 0.95；O5 锥桶 (16, −17.5) / 0.4 × 0.4 × 0.7 |
| 充电位 / 起始位 | (−18.4, −12) 偏航 90°（到作业区 ≥ 6 m）；(−18, 0) 偏航 90° |
| 巡游路线 | (−18,0) → (−10.5,0)（穿过窄通道）→ (−10.5,−19.3) → (28,−19.3) → (28,19.3) → (−10.5,19.3) → (−10.5,0) → (−18,0)，绕作业区外环一圈，全长 169.2 m（0.6 m/s 约 4.7 min） |
| 演示台 / 大屏 | (−28.5, 0) 偏航 90°（显示器朝 +X，台前观众区进深 ≥ 4 m）；西墙 (−31.8, 0) 朝 +X |

- 布局校验（`SceneLayoutValidator`，AGV 车宽 0.80 m）：最窄通道 1.80 m；巡游时 AGV 外轮廓到机身最小 6.38 m、到模型机包围盒最小 6.38 m、到障碍物最小 0.50 m（窄通道内）；模型机到障碍物最近 4.68 m（O4）；模型机到作业区边界最小 4.08 m；充电位到作业区 10.40 m；演示台前观众区进深 5.10 m；停靠设计 0.30 m。
- 层级根节点：`_System`、`Environment`、`Zone_Work`、`Zone_Mobility`、`Zone_Demo`、`Robot`（运行时生成）、`Cameras`、`UI`、`Visualization`。
- Layers：`Environment`、`Aircraft`、`Obstacle`、`Robot`、`Visualization`。激光射线只检测前三层。A320 的机身外壳与主 / 前起落架轮胎带 MeshCollider（M3 起轮胎可被离地 0.30 m 的激光扫到）。
- 虚拟禁行区（M3，ADR-015）：`Zone_Work/VirtualKeepOut`，A320 可见网格三角形地面投影的 0.1 m 栅格掩膜，12,499 格（125.0 m²）；4 个工位各开一条接近通道（开口 2.40 × 3.20 m）。AGV 由 `_System/AppBootstrap` 在充电位运行时生成。
- 检测区（CFG_Inspection，M2 第三部分 3B）：左右各 1 个区域 × 2 个工位 × 12 颗铆钉，共 48 颗；区域中心在主起落架后 4.85 m（避开右侧机身 4.30～4.33 m 处的竖缝），工位间距 1.5 m（工位中心在主起落架后 4.10 / 5.60 m），铆钉排离地 2.15 m、钉距 25 mm，位置与法向由射线投到机身外壳得到；停靠位姿车头朝 +Z、机械臂基座轴线到蒙皮 0.55 m（世界坐标 R1-S1 (6.222, −3.540)、R1-S2 (6.250, −5.040)、R2-S1 (9.778, −3.540)、R2-S2 (9.750, −5.040)）；停靠标记板在停靠位姿前方 1.0 m、向外 0.8 m（ID 0～3，0.20 m），基准标记贴在铆钉排前端蒙皮上（ID 4～7，0.10 m），充电桩正面 ID 8；真值缺陷按种子 42 取 5 颗（R1-S1-07 腐蚀减薄、R1-S1-10 孔边裂纹、R1-S1-12 铆钉松动、R2-S1-09 孔边裂纹、R2-S1-12 腐蚀减薄）。12 颗可以触发一次“每 10 颗基准复测”。

---

## 5. SOP 任务流程

### 5.1 执行语义（`MissionOrchestrator`）

```
建图准备（不在矩阵内，屏幕显示“准备：环境建图”）
for 区域 in 区域列表:
    执行 T1（全局导航至该区域）
    for 工位 in 区域.工位列表:
        执行 T2（精确停靠） → T3（注册）
        for 铆钉 in 工位.铆钉:
            执行 T4（对中） → T5（贴合采集） → T6（释放退出）
            # 实线回路“下一颗”：还有铆钉 → 回到 T4
        # 虚线回路“换站”：同区域还有工位 → 回到 T2；否则转下一区域 T1
检测完成 → 返回充电位 → 显示汇总
```
- 同一阶段内所有 `active` 单元**并行启动**，全部完成后阶段结束。
- `standby` 单元不执行，显示灰字“待命”。
- `conditional` 单元进入阶段时为“已布防”；对应 `trigger` 发生后激活，阶段须等待它完成。若 `onComplete = retryPhase`，完成后重新执行该阶段，最多重试 `maxRetries` 次。
- `runWhen` 不满足的 active 单元标记为“跳过”。例如 t(6,1) 只在本工位最后一颗铆钉时执行。
- 每个单元的执行时长 = 实际动作时长；纯逻辑单元（安全许可、调度、数据）使用 `SimSettings` 中的标称时长（默认 0.6 s），便于观众看清。

### 5.2 单元状态与显示

| 状态 | 含义 | 屏幕样式 |
|---|---|---|
| Idle | 未到该阶段 | 普通边框，正文色 |
| Standby | 待命 | 灰字 |
| Armed | 条件单元已布防 | 虚线边框，灰色 |
| Running | 正在执行 | **粗实线高亮边框** + 轻微呼吸光 + 左上角进度条 |
| Done | 本轮已完成 | 角标 ✓，绿色细边 |
| Skipped | 条件不满足 | 文字变淡 |
| Triggered | 条件单元被激活 | 虚线边框变琥珀色并闪烁两次，然后转 Running |
| Failed | 失败 | 红色边框 |

逐钉循环时，T4～T6 三行的状态在每颗铆钉开始时重置为 Idle；右侧回路箭头“下一颗 / 换站”被走到时高亮。

### 5.3 主体 → 执行器 → 场景设备

| 主体 | 执行器前缀 | 调用对象 | 高亮设备 |
|---|---|---|---|
| S1 导航 | `nav.*`、`chassis.*` | NavigationService / AgvController | AGV 底盘 |
| S2 视觉 | `vision.*` | 简化视觉（视场可见性判定 + 噪声） | 腕部相机 |
| S3 机械臂 | `arm.*` | ArmController | 机械臂 |
| S4 贴合采集 | `acq.*` | ProbeEndEffector + ContactModel | 探头末端 |
| S5 超声接口 | `ut.*` | AScanSynthesizer + GateJudge | 车载超声仪盒 |
| S6 安全 | `safety.*` | SafetyMonitor（检查条件，发放许可） | 车载安全控制盒（指示灯） |
| S7 调度 | `sched.*` | MissionOrchestrator 内部 | 车载工控机 |
| S8 数据 | `data.*` | InspectionRecordStore / 轨迹记录 | 演示台电脑 |

JSON 中出现的执行器键名都必须在 `ExecutorRegistry` 中注册；启动时 `TaskMatrixLoader` 会报告缺失的键名。

---

## 6. 配置（ScriptableObject，`Assets/_Project/Config/`）

| 资产 | 关键字段 |
|---|---|
| `CFG_SimSettings` | 随机种子、默认倍速、纯逻辑单元标称时长、默认演示模式 |
| `CFG_SceneLayout` | 大厅尺寸、三区矩形、障碍物位置、充电位、巡游建图路线 |
| `CFG_Agv` | 车体长 / 宽 / 高、轮距、最大速度、最大加速度、雷达安装位姿、机械臂安装位姿、电池容量（占位）、通讯方式（占位） |
| `CFG_Arm` | 运动学参数来源（UR5e yaml）、关节限位、关节最大速度、命名姿态 |
| `CFG_Inspection` | 区域 / 工位 / 铆钉排布、钉距、蒙皮厚度、声速、探头频率、目标接触力、力超限阈值、缺陷比例 |
| `CFG_UiTheme` | 配色、字号、屏幕分辨率（见 §7） |
| `CFG_Navigation` | 栅格分辨率、未知格代价、纯追踪前视与减速距离、到点容差、预停靠距离、对接低速段、标记估计噪声、停靠合格容差、工位接近通道边距（M3，ADR-015） |

所有参数默认值见 §6.1；修改时只改配置资产，并在 `notes` 字段注明来源。

### 6.1 v1 默认参数（预估值）

以下为 v1 采用的**预估值**，取自常见同类设备的量级，用于把场景和流程搭起来；后续按实际选型修改配置即可，不需要改代码。

**场地与模型机**
| 参数 | 值 |
|---|---|
| 大厅（长 × 宽 × 高） | 64（X）× 44（Z）× 12 m（2026-10-18 随模型机 1:1.5 调整，原 40 × 24 × 10 m） |
| 模型机 | A320-200 外形，缩比 1:1.5，按机长统一缩放导入的模型（系数 0.139595）：机长 25.05 m、翼展 22.73 m、机高 7.92 m、机身直径 2.63 m（2026-10-18 实测；真机 37.57 × 35.80 × 11.76 m，模型自身比例与真机略有差异，原估算翼展 23.87 m、机高 7.84 m） |
| 机身下缘离地 / 机身中线离地 / 机身顶部 | 1.25 m / 2.63 m / 约 3.95 m（2026-10-18 实测；原点在主起落架中点地面投影，包围盒中心在其后 0.71 m；主起落架半轮距 2.68 m） |
| 作业区 / 作业通道 | 作业区约 32（X）× 33（Z）m；模型机外轮廓到障碍物 ≥ 4 m |
| 检测区 | 机身左、右侧各 1 个区域，位于机翼后缘（主起落架后约 3.3 m）与平尾前缘（约 8.9 m）之间的后机身侧壁直段；每区 2 个工位，工位间距 1.5 m；每工位 1 排 12 颗铆钉，铆钉排离地 2.15 m（该处机身半宽 1.23 m，法向朝外偏下约 16°）；可达性验证见 Docs/Reference/M2_Reachability.md |
| 铆钉 | 沉头铆钉，钉头直径 8 mm，钉距 25 mm（按实物尺寸，不随模型机缩放） |
| 蒙皮 | 铝合金 2 mm，纵波声速 6320 m/s |
| 真值缺陷比例 | 10%（类型：孔边裂纹、腐蚀减薄、铆钉松动） |

**AGV**
| 参数 | 值 |
|---|---|
| 车体（长 × 宽 × 高） | 1.20 × 0.80 × 0.45 m（上装平台面高 0.45 m；因加装升降柱由 1.00 × 0.70 m 加宽加长） |
| 驱动 | 差速，轮距 0.66 m，驱动轮直径 0.20 m，前后各 1 个万向轮 |
| 整机质量 | 约 180 kg（含升降柱、机械臂与上装设备） |
| 速度 | 巡航 0.6 m/s（上限 1.0 m/s），接近工位 0.15 m/s，对接 0.05 m/s；加速度 0.5 m/s²；角速度上限 60°/s；角加速度 120°/s²（M3） |
| 车头对接相机 | 车头中心离地 0.40 m，视场 90° × 60°，识别距离 0.3～4.0 m（教学简化模型，归 S2；M3） |
| 导航 | 栅格 0.1 m；膨胀半径 = 车体外接圆 0.721 m；虚拟禁行区外扩 0.30 m；预停靠距离 1.5 m；工位接近通道开口 2.40 × 3.20 m；停靠合格 ±10 mm / ±0.5°（CFG_Navigation，M3） |
| 倒车退出距离 | 0.6 m |
| 激光雷达 | 车头中心，离地 0.30 m；360 线、10 Hz、量程 20 m、测距噪声 ±2 cm |
| 升降柱 | 装在平台中心前移 0.15 m 处；截面 0.25 × 0.25 m，固定段高 0.35 m，行程 0～1.40 m，升降速度 0.08 m/s，起停缓冲加速度 0.16 m/s²（满行程 18.0 s），到位精度 ±1 mm；外观为固定段 + 4 节嵌套伸缩段 |
| 升降柱目标高度 | 机械臂安装面 = 工位铆钉平均高度 − 0.30 m（CFG_Arm `comfortTargetAboveMountM`），限制在行程内；铆钉排 2.0～2.3 m 时安装面约 1.85 m |
| 机械臂安装 | 装在升降柱顶部法兰；安装面离地 0.80 m（最低）～2.20 m（最高） |
| 腕部相机 | 装在 wrist_3 法兰侧面，视场 60° × 45°，工作距离 0.15～0.40 m |
| 车载设备盒 | 超声仪（S5）0.30 × 0.20 × 0.10 m；安全控制盒（S6）0.20 × 0.15 × 0.10 m；工控机（S7）0.25 × 0.20 × 0.08 m |
| 电池 | 48 V / 40 Ah（约 1.9 kWh），v1 按线性放电显示电量 |
| 通讯 | 车载 Wi-Fi 6 连接演示台电脑，v1 只显示连接状态与延迟（固定约 20 ms） |
| 安全距离 | 底盘外轮廓距机身 ≥ 0.30 m |
| 移动时整机高度 | 升降柱降到最低、机械臂运输姿态时约 1.35 m |

**机械臂与探头**
| 参数 | 值 |
|---|---|
| 机械臂 | UR5e：臂展 0.85 m，负载 5 kg，关节限位与速度取自 yaml |
| 末端速度 | 常规 0.25 m/s；近距离限速区（距表面 < 100 mm）内 0.05 m/s |
| 探头末端长度 | 法兰到探头端面 0.15 m（含浮动夹持行程 ±3 mm） |
| 探头 | 单晶接触式，10 MHz，晶片直径 6 mm，带延迟块 |
| 接触力 | 目标 10 N，允许 8～12 N，超限阈值 20 N |
| 单钉采集时长 | 0.5 s（A 扫采样率 100 MHz，显示 2 µs 时间窗） |

**演示区**
| 参数 | 值 |
|---|---|
| 演示台 | 1.60 × 0.80 × 0.75 m |
| 显示器 | 2 台 27 英寸（16:9），屏幕中心离地 1.10 m；左屏显示任务矩阵，右屏显示三维总览 |
| 大屏 | 86 英寸（约 1.90 × 1.07 m），底边离地 1.00 m，可选 |



---

## 7. 演示界面视觉规范

**整体风格**：深色工业控制台风格，信息分区清楚，重点状态用颜色和粗边框表现，不用花哨动画。

| 角色 | 色值 |
|---|---|
| 背景 / 面板 / 分隔线 | `#0E1621` / `#152232` / `#263A52` |
| 正文 / 次要文字 / 待命灰 | `#E6EEF7` / `#8FA3B8` / `#566577` |
| 强调（标题、回路箭头） | `#2EC4E6` |
| 正在执行（粗边框） | `#FF9F1C` |
| 完成 / 条件激活 / 失败 | `#3DD68C` / `#FFC857` / `#FF5D5D` |

- 字体：Noto Sans SC。字号：标题 28、表头 18、单元 16、说明 18、日志 14（基于 1920×1080）。
- **屏幕布局（1920×1080）**
  - 顶栏：标题“AGV 飞机铆钉超声检测 · SOP 任务矩阵”、模式、倍速、仿真时间、区域 / 工位 / 铆钉进度（如“区域 1/2 · 工位 2/2 · 铆钉 7/12”）。
  - 左侧约 62%：任务矩阵。第一行为 `SOP=(P,A,T)` 加 a1～a8（代号、名称）；第一列为 p1～p6（代号、名称）；单元显示 `t(i,j)` 与任务文字；右侧画回路箭头“下一颗”（实线）和“换站”（虚线）；下方图例与 JSON 的 `legend`、`loopNote` 一致。
  - 右侧约 38%，自上而下：当前阶段卡片（阶段名 + 进度）、“当前执行情况说明”（逐条列出本阶段各主体正在做什么，取自 `explain`）、小地图（栅格 + 路径 + AGV）、A 扫波形（闸门线 + 判读结论）。
  - 底栏：事件日志滚动（最近 6 条，按类别着色）。
- **虚线边框**：USS 不支持虚线边框，需写自定义元素 `DashedBorderElement`，在 `generateVisualContent` 中用 `Painter2D` 分段绘制。
- **场景内显示器**：`PanelSettings.targetTexture` 渲染到 1920×1080 RenderTexture，再贴到显示器屏幕网格上（Unity 6.0 没有 UI Toolkit 世界空间面板）。按 `Tab` 切换为全屏叠加层，复用同一份 UXML，此时可交互（点击单元查看说明）。
- **3D 观感**：URP 柔和阴影；后处理只开 ACES 色调映射、轻微 Bloom 和 Vignette；地面用深灰环氧材质，配黄黑警示边线；机器人主体浅灰，配橙色点缀。

---

## 8. 测试（EditMode）

`TaskMatrixLoader` 能正确读取 JSON（48 个单元、3 条回路）；`GridAStar` 可达 / 不可达；差速运动学；`ArmIkSolver` 收敛（随机可达位姿成功率 ≥ 95%，位置误差 < 1 mm）；`AScanSynthesizer` 底面回波位置与厚度对应；`MissionOrchestrator` 用假执行器跑完 1 区域 × 1 工位 × 3 颗铆钉，且状态序列正确（含一次条件单元触发）。

---

## 9. 后续版本（v1 不实现）

- v2 硬件设计验证：麦克纳姆 / 四轮独立转向底盘对比；整机质心与静态稳定裕度；电源拓扑与能耗；通讯链路延迟与丢包；方案 A/B 评估面板与报告导出。
- v2 任务调度：领域本体（主体能力、任务前后置条件、资源约束）驱动的任务矩阵生成、校验与应急调度。
- v3 算法真实性：里程计漂移 + 扫描匹配 SLAM、Hybrid A* / DWA、机械臂碰撞规避、更真实的 A 扫模型。
- v3 外部接口：ROS 2 桥接（ROS-TCP-Connector）。
