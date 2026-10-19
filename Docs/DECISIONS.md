# 架构决策记录

## ADR-001 v1 只做“基础完整 + 界面美观”（2026-10-17）
硬件方案量化对比、电源 / 通讯建模、真实 SLAM、ROS 2 桥接推迟到后续版本（Architecture §9）。

## ADR-002 底盘与机械臂采用运动学驱动（2026-10-17）
底盘：运动学刚体 + 差速运动学，不使用 WheelCollider。机械臂：由 UR5e yaml 搭建运动学骨架，不使用 ArticulationBody，也不依赖 URDF Importer（v0.5.2 版本较旧，与 Unity 6 的兼容性有风险）。接触力使用虚拟弹簧（教学简化模型）。

## ADR-003 SOP 任务矩阵用 JSON 数据驱动（2026-10-17）
矩阵内容以用户提供的 6×8 矩阵为准，存放在 Config/TaskMatrix/SOP_TaskMatrix_v1.json；代码只实现执行语义与执行器，不写死流程。

## ADR-004 演示屏用 UI Toolkit + RenderTexture（2026-10-17）
Unity 6.0 没有 UI Toolkit 世界空间面板，因此用 PanelSettings.targetTexture 渲染到显示器，全屏时复用同一份 UXML。USS 不支持虚线边框，用 Painter2D 自定义元素绘制。

## ADR-005 场景与资产只通过 Editor 操作生成（2026-10-17）
通过 Unity CLI + pipeline 调用编辑器脚本生成场景、预制体和材质，不手工编辑 YAML，生成器保持幂等。

## ADR-006 显式依赖 Newtonsoft Json（2026-10-18）
任务矩阵读取使用 Newtonsoft Json。此前它只是 `com.unity.pipeline`（实验版）带入的间接依赖，pipeline 升级或移除时可能随之消失，因此在 `Packages/manifest.json` 中显式加入 `com.unity.nuget.newtonsoft-json` 3.2.1（与当时锁定版本相同）。`RivetSim.Core` 与 `RivetSim.Tests.EditMode` 通过 `precompiledReferences` 引用 `Newtonsoft.Json.dll`。

## ADR-007 配置资产生成只补缺、不覆盖（2026-10-18）
CLAUDE.md 要求生成器重复执行时覆盖上一次的结果。该规则适用于场景、预制体、材质等**生成物**；CFG_* 配置资产是**人工调参的数据源**，覆盖会丢失调整。因此 `RivetSim/Setup/Create Default Configs` 只创建缺少的 CFG 资产，已有资产保持不变；默认值写在配置类的字段初始值中（来自 Architecture §6.1 / §7），需要恢复默认时手动删除对应资产后重新执行菜单。

## ADR-008 尺寸向量轴约定与布局坐标（2026-10-18）
配置中的尺寸向量一律按（长, 宽, 高）存放在 (x, y, z) 分量，转 Unity 缩放时由 `RivetSim.Core.Config.SizeAxes` 统一换算：大厅与演示台这类“长沿左右”的物体用 `LengthAlongX`（X = 长，Y = 高，Z = 宽）；AGV 车体与车载设备盒这类“长沿前后”的物体用 `LengthAlongZ`（X = 宽，Y = 高，Z = 长，前进方向为本地 +Z）。大厅长边沿世界 X，演示区在 −X 一端；`ZoneRect` / `PlanarPose` / 路线点的 x = 世界 X、y = 世界 Z；偏航角 0° 指向 +Z、顺时针为正，与 `Quaternion.Euler(0, yaw, 0)` 一致。M1 布局取值经用户确认后写入 CFG_SceneLayout（见 Architecture §4）。

## ADR-009 主场景暂放 AGV 预览实例；生成器外观尺寸用命名常量（2026-10-18）
Architecture §4 规定 `Robot` 根节点在运行时生成 AGV。M1 为了布局验收，在起始位放一台 `AGV_Preview`（PF_AGV 实例），M3 / M5 改为由 AppBootstrap 运行时生成后删除。生成器中只影响外观的尺寸（法兰直径、灯带、立柱间距、墙厚、占位模型机的机翼弦长等）以命名常量写在编辑器生成器内，不进入 CFG；功能参数（车体、轮距、雷达与机械臂安装位姿、设备盒、工具长度、大厅、演示台、显示器）一律从 CFG 读取。后处理配置资产使用新前缀 `VP_`（VP_Main）。地面中文文字用 Noto Sans SC Bold 动态字形烘焙成贴图（T_ZoneLabel_*），不引入 TextMeshPro。

## ADR-010 模型机缩比 1:1.5、厂房 64 × 44 × 12 m、AGV 加装升降柱（2026-10-18）
- 模型机由 1:5 改为 1:1.5：25.05 × 23.87 × 7.84 m。作业区 = 外轮廓四周各加 4 m，约 32（X，翼展方向）× 33（Z，机身方向）m；机身仍沿 Z，侧面朝向演示区，便于观众看到 AGV 在机身侧壁作业。
- 厂房由 40 × 24 × 10 m 改为 64 × 44 × 12 m。X 方向：演示区 9 m + 过道 3 m + 西侧移动区 12 m（充电位、窄通道、障碍物）+ 作业区 32 m + 东侧环道 8 m；Z 方向：作业区 33 m + 两侧环道各约 5.5 m；高度：尾翼 7.84 m 以上留出桁架与灯具空间。原“面积 ×2（56 × 34 m）”方案放不下 1:1.5 模型机，作废。
- 铆钉排离地约 2.0～2.3 m，原安装面 0.45 m 的 UR5e 够不到，因此加装升降柱（行程 1.40 m，安装面 0.80～2.20 m）；车体加大到 1.20 × 0.80 m 以保证稳定。
- 联锁规则：升降柱只在底盘零速锁定后运动；底盘只在升降柱降到最低且机械臂处于运输姿态时运动。升降柱属于主体 S3（机械臂）的执行范围，任务矩阵 label 不变。
- 2D 激光雷达（离地 0.30 m）只能扫到起落架和发动机，扫不到机身与机翼，因此 M3 的路径规划需叠加模型机外轮廓投影的“虚拟禁行区”，只在工位处开放接近通道。

## ADR-011 M2 布局：外环巡游路线、新增两项布局校验、占位外观按机长比例（2026-10-18）
- 巡游建图路线走作业区外的环道（约 169 m），穿过西侧移动区的 1.8 m 窄通道，覆盖全部障碍物；不在作业区内贴机翼绕行。路线较长，演示时用 `CFG_SimSettings` 的倍速压缩。
- CFG_SceneLayout 新增 `chargingDockMinToWorkZoneM`（6 m）与 `demoAudienceDepthMinM`（4 m），由 `SceneLayoutValidator` 检查充电位到作业区距离与演示台前观众区进深（来自 PromptTemplates M2 1-2）。
- 占位模型机的外观常量改为“相对机长的比例”（比例取自 M1 的 1:5 占位外观），缩比变化时不需改生成器；支撑架与轮挡位置由同一组比例计算。
- 已有 CFG 资产按 ADR-007 不被菜单覆盖：先改配置类默认值，确认资产现值与旧默认值一致（无人工调参）后，CFG_SceneLayout 用 `EditorUtility.CopySerialized` 整份重写，CFG_Agv 只经 `SerializedObject` 改车体、质量、轮距、雷达位置与 `notes`。

## ADR-012 升降柱与联锁的实现方式（2026-10-18）
- 升降柱运动用纯计算类 `LiftMotionProfile`（匀速 + 起停缓冲，教学简化模型），剩余距离进入到位精度（±1 mm）即判定到位；`LiftColumnController` 只负责驱动伸缩段与异步接口，便于 EditMode 测试。新增参数 `liftAccelerationMps2`（0.16 m/s²）与 CFG_Arm `comfortTargetAboveMountM`（0.30 m）经用户确认。
- CFG_Agv 删除 `armMount`，机械臂安装高度由升降柱推算（`ArmMountHeightM(伸出量)` = 平台面 + 固定段 + 伸出量），避免同一高度存两份。
- `SafetyMonitor` 为纯 C# 类，依赖只读状态接口；M2 第二部分提前实现 `AgvController` 最小子集（零速锁定、速度指令、线加速度限幅）供联锁使用，M3 在其上补充导航与车轮动画，不产生临时桩代码。
- 控制器提供 `Tick(仿真步长)`，由启动入口（Sandbox 中为 `SandboxLiftDemo`，正式场景为 M5 的 AppBootstrap）按 `SimClock` 推进；控制器内不读取 `UnityEngine.Time`。

## ADR-013 M2 第三部分拆为 3A / 3B；A320 与 UR5e 的导入方式（2026-10-18）
- 更正：本条早先记为“M2 以第一、二部分结束、导入外部资产推迟”，用户随后说明 M2 尚未结束，该记录作废。M2 第三部分按用户确认拆为 3A（导入外部资产）与 3B（RivetZoneGenerator 与可达性验证），3A 截图确认后再做 3B。
- glTFast 采用 Package Manager 给出的兼容版本 `com.unity.cloud.gltfast` 6.20.0。
- A320 三角面：用户确认不减面，接受约 73 万（只隐藏完全包在机身内的驾驶舱件约 4.3 千面，可见 727,718 面）。早先估算的“驾驶舱内饰 7.4 万面”实为前起落架；包围盒看似异常的薄板（Cube.017_196 等、Cube.077_48）经截图确认是可见的舱门 / 发动机部件，均保留。
- A320 结构：`PF_Aircraft_A320_Model` 为 glb 的 Prefab Variant（隐藏内部件、层设为 Aircraft、只给机身外壳 Cylinder.027_68 加 MeshCollider），外层 `PF_Aircraft_A320` 内嵌该 Variant 并做统一缩放（机长 / 原始机长 = 0.139595）与旋转（glTFast 导入后机头朝 +X，再绕 Y 轴 −90° 使机头朝 +Z）；外层根节点在主起落架中点的地面投影（地面 = 主机轮最低点）。之所以加外层：Variant 根节点的旋转在实例化时会被场景摆放覆盖。外形尺寸按顶点计算（渲染器包围盒会把斜放的薄片放大）。
- 模型机外形改用实测值写入 CFG_SceneLayout（用户确认）：翼展 22.73 m、机高 7.92 m、机身直径 2.63 m、中线离地 2.63 m、下缘离地 1.25 m；机长 25.05 m 为缩放基准。
- UR5e：按 `ur_macro.xacro` 的连杆链与三个 yaml 生成 `PF_Arm_UR5e`（`ArmRig` + 6 个 `ArmJoint`，不用 ArticulationBody）。ROS → Unity 统一由 `RosFrame` 换算（位置 (−y, z, x)，旋转 C·R·Cᵀ，关节角取反）；Unity 导入 Z 向上的 DAE 时网格坐标为 (−x, z, −y)，实测后每个网格再绕 Y 轴 +90° 校正。UR 原材质（Standard）换成 URP 材质 M_UR5e_*，颜色沿用原材质。零位与竖直姿态的正运动学与 yaml 一致（EditMode 测试）。
- 材质：地面 / 墙面 / 演示台台面 / AGV 平台改用 Poly Haven 贴图，由 ARM 图派生金属度 / 光滑度（R = 金属度，A = 1 − 粗糙度）与 AO 贴图；UI 默认字体为 Noto Sans SC Regular 的动态 FontAsset。

## ADR-014 检测区排布、标记与可达性验证（2026-10-18）
- 检测区参数写入 CFG_Inspection（用户确认）：工位间距 1.2 → 1.5 m（§6.1）；新增区域中心位置 `regionCenterAftOfMainGearM`、铆钉排高度 `rivetRowHeightM` 2.15 m、停靠距离 `dockingStandoffM` 0.55 m、停靠标记板偏移 `dockingMarkerAheadM` 1.0 m 与 `dockingMarkerLateralM` 0.8 m（工位间距 1.5 m、车长 1.2 m，正前方会落入相邻工位停车区）、AprilTag ID 与边长。区域中心由 5.0 调为 4.85 m：右侧机身 4.30～4.33 m 处有约 3 cm 的竖缝（疑似舱门分缝），5.0 m 时 R2-S1-04 落在缝上。
- 铆钉位置与法向由水平射线投到机身外壳碰撞体得到；漏检时在 ±2 mm 内偏移重试。真值缺陷用 `DefectAssigner`（种子取自 CFG_SimSettings，数量 = 四舍五入（总数 × 比例））。
- “底盘距机身 ≥ 0.30 m”按车体 / 升降柱外形到机身外壳的三维最近距离判定（用户确认），允许 AGV 伸到机身下方。
- 基准标记贴在蒙皮上（铆钉排前端旁），停靠标记为地面立杆标记板；充电位标记贴在充电桩正面。
- 可达性用编辑器内的阻尼最小二乘数值逆解采样验证（`ReachabilityChecker`），不进入运行时程序集；正式逆解 `ArmIkSolver` 仍在 M4。机械臂连杆与机身的碰撞未检查（M4）。

## ADR-015 M3 导航：虚拟禁行区、接近通道、机械臂前伸处理、AppBootstrap 范围与新参数（2026-10-18）
- 新增配置 `CFG_Navigation`（`NavigationConfig`）：栅格 0.1 m、未知格附加代价 1.0、纯追踪前视 0.8 m、减速距离 1.0 m、到点容差 0.05 m / 1.0°、预停靠距离 1.5 m、对接低速段 0.30 m、标记位姿估计噪声 σ 3 mm / 0.2°、停靠合格容差 ±10 mm / ±0.5°、接近通道边距（用户确认）。CFG_Agv 新增最大角加速度 120 °/s² 与车头对接相机（车头中心离地 0.40 m、视场 90° × 60°、识别 0.3～4.0 m，教学简化模型，归 S2）。
- 虚拟禁行区：`KeepOutZoneGenerator` 把 A320 全部可见网格三角形（72.8 万个）的地面投影按 0.1 m 保守栅格化（三角形与格相交即标记，不过滤高度，含机翼、平尾、发动机、起落架；只投影顶点会在大三角形内部留空洞，故不用），掩膜按位压缩存入场景组件 `VirtualKeepOutZone`，格点与导航栅格对齐。结果 12,499 格（125.0 m²）。
- 代价地图：激光层 ∪ 虚拟层；虚拟层先外扩机身安全距离 0.30 m（CFG_Agv 已有参数），两层再按车体外接圆半径 0.721 m 膨胀（由车体尺寸推算），通道外车体外廓距投影 ≥ 0.30 m。格分可通行 / 膨胀区 / 致命区；A* 起点在膨胀区内（如充电桩前）时允许先在膨胀区内逃离，致命区始终不可进入。
- 接近通道（每工位一条，停靠坐标系矩形）：纵向从停靠位姿车头前 0.20 m 到预停靠位姿车尾后 0.30 m（长 3.20 m），横向内侧到车道中心线 0.60 m、外侧 1.80 m（开口宽 2.40 m）。通道内不叠加虚拟层（含外扩与膨胀），激光层照常。内侧 0.60 m 时车体最多伸到距机身中线 0.78 m 处，该处机身下表面离地约 1.51 m，高于行驶时最高点 1.03 m。实测停靠时车体内侧边距机身二维投影约 0.06 m，因此必须开通道。
- 激光扫描：A320 原只有机身外壳有碰撞体，离地 0.30 m 的激光扫不到任何飞机部件；Variant 中给主 / 前起落架 4 个轮胎网格加 MeshCollider（Aircraft 层），修订 ADR-013“只给机身外壳加碰撞体”。轮挡高约 0.15 m，低于激光平面，扫不到。
- 机械臂零位前伸（M4 前）：平移规划只按车体外接圆 0.721 m 膨胀（1.8 m 窄通道中心线距障碍 0.90 m 仍可通过）；原地转向按含机械臂的扫掠半径（运行时由渲染器包围盒量得 1.069 m）检查激光层，对接直线段按车宽 × 车头前伸 1.034 m 检查激光层。M4 定义运输姿态 Stow 后复核并去掉这项特殊检查。
- 控制方式：入口每帧按仿真时间以固定子步（项目 Fixed Timestep 0.01 s）推进“导航控制 → 底盘 → 升降柱 → 激光”，控制精度与倍速无关；异步方法只等待运动任务结束。底盘行驶联锁只在由静止起步时判定一次，避免每个子步重复记录。
- 到点与对接：规划到“终点沿目标朝向后退前视距离”的引入点再直线进入；终点段直接瞄准终点、进入容差圈后锁定转向。对接时标记可见则每个子步用“真值 + 高斯噪声”更新目标估计，标记移出视场（距停靠位约 0.4 m 内）后沿用最后估计，按制动曲线停车，停靠误差以真值位姿计算。
- AppBootstrap 在 M3 建立（用户确认），范围限于：时钟、事件总线、在充电位运行时生成 AGV、安全监视、地图与导航服务、可视化与遥控、M3 演示流程（`NavigationDemoRoutine`）；主场景删除 AGV_Preview（ADR-009），布局截图改为临时实例化 PF_AGV。M5 在其上接入 MissionOrchestrator。

## ADR-016 M4 机械臂与超声检测：命名姿态、逆解、间隙检查、教学简化模型参数、沙盒机身段（2026-10-19）
- 计划与第 4 节各项经用户确认（2026-10-18）。实现中有三处偏离计划，均为满足已确认的校核要求：
  1. Stow 由计划的 (0, −180, −165, −180, −90, 0)° 改为 (0, −180, −120, 45, −90, 0)°：原姿态前臂折到底，与基座外壳只隔 1.8 mm（自干涉要求 ≥ 10 mm），改为前臂上扬、腕部收在车体中后部。网格顶点实测（升降柱最低位）：机械臂横向 −0.210～+0.076 m、纵向 −0.335～+0.225 m（车体投影 ±0.40 / ±0.60 m 内），整机最高点 1.034 m（≤ 1.35 m），非相邻连杆最小 26.9 mm（基座 / 上臂），机械臂离车体设备盒 45 mm。
  2. 新增命名姿态 Ready (0, −90, 90, −90, −90, 0)°（直立过渡）：左侧工位直接从 Stow 关节插值到镜像 Observe 时，末端离机身只有 1～2 mm；改为 Stow → Ready → Observe，收臂时反向（末颗 → Observe → Ready → Stow）。镜像规则定为基座角 −180°（从车尾一侧转过去），−61° 的镜像为 −241°。
  3. 间隙检查除机身外壳外，加查机械臂到本车车体（底盘、升降柱、设备盒渲染器包围盒，基座与肩部连杆除外），要求与连杆相同（30 mm）：逆解曾给出肘部朝下、贴近车体的解。
- Observe = R1-S1 基准观察位姿的逆解结果取整（−61, −82, 146, −82, −243, −98)°：肘上构型、腕部相机朝上、光轴沿基准标记法向、距标记 0.30 m。
- 运输姿态校核：`RivetSim/Debug/Check Stow Envelope`（网格顶点，报告 Docs/Reference/M4_StowEnvelope.md）与 EditMode 测试 `StowEnvelopeTests`；运行时 `RobotRuntime` 启动时用渲染器包围盒再校核一次。整机扫掠半径 0.729 m 来自车体两端附件（防撞条 / 对接相机），比车体外接圆 0.721 m 大 8 mm，小于 0.1 m 栅格，导航膨胀半径保持 0.721 m（用户确认）。导航删除 ADR-015 的“含机械臂扫掠半径”原地转向检查与“含机械臂前伸”对接车道检查。
- `IArmPoseState` 由 `ArmController` 实现（运输姿态 = 无动作且各关节与 Stow 相差 ≤ 0.5°）；`PlaceholderArmPoseState` 用 AssetDatabase.MoveAsset 归档到 Assets/_Archive/Scripts。SafetyMonitor 新增规则 3：机械臂离开运输姿态只在底盘零速锁定后允许（用户确认）。
- 逆解 `ArmIkSolver`：阻尼最小二乘，误差 = [位置差; 旋转向量 × 0.2 m]，几何雅可比由 `ArmKinematics`（从 ArmRig 提取的纯计算模型）给出；初值依次为当前关节角、Observe 及其镜像、Stow、内置 8 组、随机 8 组（固定种子），取第一个收敛且通过间隙检查的解。工具姿态：工具轴沿 −法向，腕部相机优先朝上，失败时绕工具轴试 90° / −90° / 180°。直线运动每个子步以当前关节角为初值迭代 ≤ 30 次跟踪，跟踪误差超过收敛阈值 5 倍或关节角速度超过 yaml 上限（接近奇异）即停止。
- 连杆间隙 `ArmClearance`：UR5eArmGenerator 从各连杆外观网格顶点按 20 mm（探头末端 10 mm）网格抽稀得到表面采样点；先以连杆包围球 + 搜索上限筛选，再对每点 `Physics.CheckSphere` 二分半径（8 次，0.8 mm 分辨率）求到 Aircraft 层碰撞体的距离。目标解与关节插值路径（12 点）都检查；只检查不规划（v1 不做碰撞规避）。验证工具 `RivetSim/Debug/Check Station Reachability` 改用运行时逆解，报告 Docs/Reference/M4_ArmReach.md（取代 M2_Reachability.md 的方法，旧报告保留）。
- 教学简化模型参数（用户确认，写入 CFG_Arm / CFG_Inspection）：见 Architecture §6.1。实现说明：判读用的包络为整流后一周期滑动平均 × π/2，对 σ ≈ 0.06 µs 的窄脉冲约为峰值的 90%（如一次底面波 0.8 显示为 71%～74%），阈值按满屏高度比例直接比较；闸门 B 只认包络的局部峰，避免把底面回波前沿判为缺陷。接触力逐子步（0.01 s、5 mm/s）判定，停止时 10.0～10.9 N（直线跟踪的残余误差使个别颗超出一步）。
- 场景：沙盒用 `FuselageSectionBuilder` 从 A320 机身外壳截取主起落架后 2.6～7.1 m 的整圈三角形（5,868 个），保存为 Art/Models/Generated/A320_FuselageSection.asset 与 PF_FuselageSection，按整机根节点位姿摆放；`RivetZoneGenerator.Build(work, aircraft, 工位过滤)` 只生成 R1-S1 / R2-S1，铆钉与真值缺陷与主场景一致。M3 导航演示保留但停用，禁行区改由机身段栅格化；默认入口 `SandboxInspectionDemo`（AGV 已在 R1-S1 停靠位）。主场景在 M3 演示的 R1-S1 停靠后执行单工位检测，并增加联锁演示 2（机械臂离开运输姿态时解锁被拒绝）。
- 结构：M3 的 `NavigationRuntime` 改名为 `RobotRuntime`（加机械臂、接触力、检测服务，子步顺序 导航 → 底盘 → 升降柱 → 机械臂 → 激光）；新增 Simulation/Inspection 目录与 Presentation/UI 目录（命名空间 RivetSim.Simulation.Inspection / RivetSim.Presentation.UI，程序集不变）。CFG_Agv 新增 `transportHeightMaxM`（§6.1 已有的 1.35 m，用于校核）。

## ADR-017 M8 v1.1 小更新：倍速保护、副屏、机位防遮挡、随机故障、矩阵文字（2026-10-19）
- 模板第 1～10 项（无第 8 项，按编号笔误处理）及计划待决问题均按默认方案执行（用户确认）。
- 倍速 ×8：仿真仍按 0.01 s 固定子步推进，倍速只改每帧子步数；风险只在帧率，由 `SpeedGuard` 按窗口统计帧率，低于 `CFG_SimSettings.speedGuardMinFps` 自动降到 ×4 并在 HUD 提示。
- 右屏“检测结果与设备状态”与左屏复用同一 UI_DemoConsole.uxml（第二页 dc-status-page），独立 RenderTexture；大屏切换采用替换屏幕材质，不新建第二套 UI。
- 电量与通讯延迟为显示用的教学简化模型 `TeachingPowerCommModel`（参数在 CFG_Agv），不属于 CLAUDE.md 所说 v1 不做的“电源 / 通讯链路建模”，不影响流程。
- 故障改为 `RandomFaultScheduler` 按种子自动注入（每类每轮一次），删除 HUD 故障面板与 F5～F7；`ConditionBus.Inject` 保留为内部接口。
- 任务矩阵显示文字按用户提供的 M8_label_update.json 替换（用户授权修改 label），原文保留在 `labelOriginal` / `nameOriginal`；润色 explain 11 处，清单见 Docs/Reference/SOP_TaskMatrix_v1.md。
- 场景提亮只改 CFG 与材质参数（前后对照见 Docs/Reports/M8.md §8）；演示屏材质基础色按 2^−EV 抵消曝光补偿，保证屏幕文字亮度不变。
