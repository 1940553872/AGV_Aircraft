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
