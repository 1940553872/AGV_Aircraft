# 开发进度

## 当前里程碑
**v1 已交付并验收**（2026-10-19，M0～M7 全部验收）。后续：独立程序构建（需单独指令）或 v2

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
| M2 第三部分 3B 检测区与可达性 | 2026-10-18 | 用户核对截图 13～15 与可达性报告（48 颗铆钉、5 颗真值缺陷；4 个工位规划高度下 12/12 可达；车体 / 升降柱三维间隙 0.895 / 0.383 m）；59 个 EditMode 测试通过；提交 48a1141 |
| **M2 厂房与布局重建（1:1.5）＋ AGV 升降柱 ＋ 导入外部资产（整体）** | 2026-10-18 | 用户整体验收；包含上面 M2 各部分 |
| M3 AGV 建图、规划与移动 | 2026-10-18 | 用户核对截图 16～21 与数值（建图 182.75 m / 316.3 s；充电位 → R1-S1 导航 26.23 m / 56.4 s；停靠误差 纵向 +2.2 mm、横向 +0.7 mm、偏航 −0.04°，合格；联锁拒绝正确）；82 个 EditMode 测试通过；提交 e63ee3c |
| M4 机械臂与超声检测 | 2026-10-19 | 用户核对截图 22～29 与数值（Stow 外廓在车体投影内、最高点 1.034 m；4 个工位 48/48 可达，连杆到机身最小 150 mm；随机位姿逆解 99.6% 收敛、最大 0.200 mm；R1-S1 检出 3 / 误报 0，平均单钉节拍 4.71 s）；104 个 EditMode 测试通过；提交 97f0059 |
| M5 SOP 流程编排 | 2026-10-19 | 用户核对截图 01～02 与 Docs/Reports/M5.md（48/48 颗、检出 5/5、误报 0；仿真 1481 s，4 倍速约 6.2 min；三种故障各一次均激活并恢复；执行器键名 37 个缺失 0）；112 个 EditMode 测试通过；提交 cf617c2 |
| M6 演示界面 | 2026-10-19 | 用户核对截图 1～4 与 Docs/Reports/M6.md（8/8 种状态样式通过；1920×1080 / 2560×1440 无溢出；演示屏与仿真状态持续不一致 0；t(1,8) 点选提示与初值写回；单钉节拍扣除收臂降柱）；116 个 EditMode 测试通过；提交 34ec026 |
| M7 联调与观感打磨（v1 交付） | 2026-10-19 | 用户核对 4 张宣传截图与 Docs/Reports/M7.md（2 倍速完整演示约 12:09；分步演示 154 次暂停 / 继续；1080p 平均 366 FPS；激光 / 栅格 / 小地图 0 分配；大屏放大 3 倍）；EditMode 120/120；提交 2981c10 |

## 最近一次会话
- 日期：2026-10-19
- 完成：M7 联调与观感打磨（v1 收尾）。已定决策 1～9 与计划待决问题 Q1～Q4 按默认执行；会话中用户追加：墙上大屏放大 3 倍
  - Core：`RenderLookConfig`（CFG_RenderLook：主光 / 阴影 / 环境光 / 后处理 / 演示区补光）；`Diagnostics/GcProbe` + `ProbeModule`（模块级 GC.Alloc 计数，默认关闭）；SimSettings 默认倍速 2 倍并校验在 speedOptions 内；UiThemeConfig 新增小地图 AGV 标记尺寸、CamAnchor_4 位姿；SceneLayout 大屏 5.70 × 3.21 m
  - Editor：`SceneLightingSetup` 改读配置并生成演示区补光、写 PC_RPAsset 阴影；`UiLayoutCheck`（从 M6 验证提取，M6 / M7 共用）；`GameViewResolution`；`M7DemoVerifyRunner`（`RivetSim/Verify/Run M7 Demo`）
  - Presentation：删除 AppBootstrap 的场景字段 `demoSpeedMultiplier`（=4，曾覆盖配置）；小地图贴图改为直接写像素缓冲（消除每次刷新的分配）；分步模式下 t(1,8) 仍允许模拟操作员超时给初值；`DemoUiHub.Mode`
  - Tests：`M7ConfigTests` 4 项
- 结果（Docs/Reports/M7.md）：
  - 自动演示（2 倍速）完成：仿真 1458.4 s，**真实 2 倍速约 12:09**；48 颗；完整时间线见报告 §1.1
  - 分步演示完成：阶段进入 154 次 = 暂停 154 次，暂停期间仿真不推进；含 t(1,8) 识别失败，模拟操作员给出初值
  - 1920×1080 / 2560×1440 下主场景演示屏 308 个元素、HUD 40 个元素无溢出
  - 1080p 平均 366 FPS，1% 低帧 72～157 FPS（Editor 测量）；激光、栅格、小地图运行中 0 分配
  - §6.1 仍为预估值的参数表见报告 §3
- 自检：编译 0 错误 0 警告；EditMode 120/120 通过；Generate All 后主场景 986 个物体（+DemoLights 3 个）；Play 0 错误
- 截图（Docs/Screenshots/M7/，1280×720）：M7_01_Overview（建图总览）、M7_02_FollowNav（跟随 AGV 导航）、M7_03_Closeup（末端特写接触采集）、M7_04_DemoStation（演示台与 3 倍大屏）
- 2026-10-19 用户验收通过（v1 交付）。
- 下次第一步：按用户指令进行 Windows 独立程序构建（模板 M7 第 5 项），或从 v2 建议中选定下一个里程碑

## v1 已知限制
- 建图、A 扫、接触力、视觉均为教学简化模型；导航用真值位姿，无里程计漂移与扫描匹配。
- t(1,8) 操作员初值只写黑板与日志，不参与定位；点选只给位置，朝向沿用当前估计。
- C4 / C8 有个别 ✗（本次 576 次检查中 3 次），未逐颗定位原因；C1～C12 为固定示例文字与阈值，只上报不阻断。
- 硬件与场地参数多为 §6.1 预估值（见 Docs/Reports/M7.md §3），未按实际选型标定。
- 帧率在 Editor 中测得，单帧最低约 19 FPS 的偶发尖峰未在独立程序中复测；未做独立程序构建。
- 第一次运行 Sandbox_UI 时出现过 2 条字体资产导入警告，之后未复现。

## v2 建议
- 操作员初值参与定位（作为扫描匹配初值），小地图点选支持拖拽给朝向。
- 逐颗定位 C4 / C8 的 ✗：导出每颗的回波幅值与闸门读数，与真值缺陷对照，校准 A 扫模型。
- Architecture §9：v2 硬件设计验证（底盘方案对比、质心与稳定裕度、电源与通讯链路）、本体驱动的任务矩阵生成与应急调度；v3 扫描匹配 SLAM、Hybrid A* / DWA、机械臂碰撞规避、更真实的 A 扫、ROS 2 桥接。
- 按实际选型替换 §6.1 预估参数（AGV、雷达、相机、升降柱、探头与超声仪、实训场地）。
- 独立程序构建与目标机性能复测；演示屏可选更高分辨率 RT 以配合大屏。

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
- [x] M2：模型机 1:1.5、厂房 64 × 44 × 12 m、AGV 升降柱、导入外部资产与检测区（2026-10-18 整体验收），见 ADR-010～014
- [ ] v2：升降柱升高后的整机稳定裕度分析（质心高度上升）
- [x] M5：启动时用 `TaskMatrixData.GetExecutorKeys()` 与 `ExecutorRegistry` 核对缺失的执行器键名（目前 JSON 中共 37 个）
- [x] M1：确定 `CFG_SceneLayout` 中尺寸向量（长 × 宽 × 高）与世界坐标轴的对应关系（ADR-008）
- [x] M3 / M5：AGV 改为由 AppBootstrap 运行时生成，删除主场景中的 AGV_Preview（ADR-009；M3 完成，见 ADR-015）
- [x] M4：定义运输姿态 Stow 后复核整机外廓，去掉导航中“含机械臂扫掠半径”的原地转向 / 对接车道检查（2026-10-19，ADR-016）
- [x] M5：返回充电位复用 M3 的“导航到充电位前方 1.5 m + 直线倒车”
- [x] M5：T3～T6 执行器直接包 `RivetInspectionService` 的方法；ConditionBus 订阅 `ContactForceExceededEvent`（力超限）与判读为“无效”的记录（采集无效）；`InspectionRecordStore` 导出 CSV
- [x] M5：“每 10 颗基准复测”改用 `RivetInspectionService.RemeasureDatumAsync`（从铆钉位姿经观察姿态复测）
- [x] M6：演示屏 A 扫复用 `AScanGraphElement`；沙盒去掉 M3 导航叠加面板，由 HUD 取代
- [x] M7：演示区补光 / 主光方向调整，提升演示台近景亮度（CFG_RenderLook）
- [x] M6：t(1,8) 改为小地图点选初值（M5 由模拟操作员 3 s 后给出）；演示屏逐条显示 ConditionCheckedEvent 的 ✓ / ✗
- [x] M6：单钉节拍统计排除工位末颗的收臂与降柱时间
- [x] M7：演示台机位（CamAnchor_4）更近、显示器画面更易读；小地图 AGV 标记放大（大屏放大 3 倍）
- [ ] v2：t(1,8) 点选支持拖拽给朝向，并让初值参与定位（目前只写黑板与日志）
- [ ] v2：演示屏 RenderTexture 提高到 2560×1440 或 4K，配合 258 英寸大屏
