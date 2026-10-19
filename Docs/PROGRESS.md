# 开发进度

## 当前里程碑
M6 演示界面 —— 已实现，待验收（2026-10-19）

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

## 最近一次会话
- 日期：2026-10-19
- 完成：M6 演示界面（先在 Sandbox_UI 用假数据完成，再接入 Main）。已定决策 1～12 与计划待决问题 Q1～Q4 按默认执行（遥控仅在工序未运行时可切换；不新增 Presentation 测试；新参数放进 CFG_UiTheme；两台显示器与大屏共用一张 RT）。
  - Simulation（已说明的接口改动）：`OperatorPoseChannel`（t(1,8) 打开通道等待小地图点选，`AutoProvide` 时超时由模拟操作员给出）挂在 `MissionServices.OperatorPose`；`MissionBlackboard.CycleExcludedS` / `RivetCycleTimeS`，`arm.retractHold` 把工位末颗的收臂与降柱时间计入扣除
  - Core：`UiThemeConfig` 新增单元动效、小地图、相机参数（呼吸 1.6 s、Triggered 闪烁 2 次 / 1.2 s、回路高亮 3 s、小地图刷新 0.5 s、机位过渡 0.8 s、演示台停留 2 s 且仅在机位变化时、末端特写 1.4 m / 0.5 m / 35°、阶段机位 2,2,3,3,3,3）
  - Presentation/UI：`DemoConsoleView`（UI_DemoConsole.uxml/.uss）、`TaskCellElement`、`DashedBorderElement`、`LoopArrowElement`、`MiniMapElement`、`HudView`（UI_Hud.uxml/.uss，含 t(1,8) 点选提示与致谢角标）、`IDemoStatusSource`、`IMiniMapSource`、`UiPerfProbe`
  - Presentation/App：`DemoModeController`（自动 / 分步 / 遥控、倍速、分步按阶段暂停）、`DemoUiHub`（HUD / 快捷键 → 模式 / ConditionBus / CameraRig / 全屏 / 初值通道）、`RuntimeMiniMapSource`、`FakeMissionFeed` + `FakeMiniMapSource`、`SandboxUiBootstrap`；`AppBootstrap.SetUpDemoUi`；`MissionRoutine.OperatorPose`
  - Presentation：`CameraControl/CameraRig`（CamAnchor_1～4；跟随 AGV 保持锚点 2 相对起始位姿的偏移；末端特写位于探头 AGV 一侧）；`Input/DemoHotkeys`（1～4、0、Tab、P、N）；`VisualizationToggleInput.Toggle`
  - Editor：`DemoUiGenerator`（`RivetSim/Generate/Demo UI Assets`：T_DemoConsoleRT 1920×1080、M_DemoScreen、三份 PanelSettings；主场景 / Sandbox_UI 挂载）；DemoStation 屏幕改用 M_DemoScreen；Main 不再挂 NavOverlay / InspectionOverlay（类保留）；`DemoUiVerifyRunner`（`RivetSim/Verify/Run Demo UI`）
  - Tests：`OperatorPoseChannelTests` 4 项（含单钉节拍扣除）
- 结果（Docs/Reports/M6.md）：
  - Sandbox_UI 假数据 39 步：8/8 种状态都显示过且样式核对 8/8 通过；回路“下一颗”“换站”均高亮；t(1,8) 小地图点选写回通道
  - 1920×1080 与 2560×1440 下演示屏 391 个元素、HUD 40 个元素均无溢出（A 扫判读一行按设计省略号截断）
  - 界面开销：空闲帧平均 0.03 次 GC 分配 / 帧（242 帧中 2 帧，时间标签每秒刷新）；事件处理平均 1.5～4.4 µs、最长 0.53 ms
  - Main 快速流程（T1 识别失败 1 次）：完成，仿真 1468.6 s；演示屏与仿真单元状态 7462 帧 × 48 单元持续不一致 0 次；t(1,8) 提示弹出、自动演示 3 s 后模拟操作员给出初值；单钉节拍非末颗平均 12.5 s、各工位末颗 12.3 s（已扣除收臂降柱）
- 自检：编译 0 错误 0 警告；EditMode 116/116 通过；Generate All 后主场景 983 个物体（+DemoConsole / Hud，−NavOverlay / InspectionOverlay）；Main / Sandbox_UI Play 0 错误，警告为故障注入与条件 ✗ 的预期日志；已退出 Play 模式
- 截图（Docs/Screenshots/M6/）：1_matrix_fullscreen（全屏矩阵：T5 多个 Running、t(5,7) 采集无效触发闪烁、t(1,8) “已触发”角标、C2～C8 逐条 ✓ / ✗，C5 ✗）；2_scene_monitor（演示台机位：两台显示器与大屏显示演示屏）；3_hud（HUD + 自动机位末端特写）；4_pose_prompt（t(1,8) 小地图点选提示，真实栅格地图 + AGV）
- 遗留问题：
  - 操作员初值写入 `MissionBlackboard.InitialPose` 并写日志，但导航仍用真值位姿（教学简化）；点选只给位置，朝向沿用当前估计
  - C4 / C8 的个别 ✗ 仍未逐颗定位原因（演示屏已逐条显示数值）
  - 验证菜单从 Sandbox_UI 切到 Main 时，编辑器失去焦点会让 delayCall 不执行，需要 eval `DemoUiVerifyRunner.LaunchMain()` 继续
  - 第一次运行 Sandbox_UI 验证时 Console 出现 2 条字体资产 “Importer generated inconsistent result” 导入警告（推测为动态图集运行时新增 ✓ / ✗ 等字形），之后的运行是否复现未单独确认
- 下次第一步：用户验收 M6；之后执行 M7（联调与观感打磨）

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
- [ ] M7：演示区补光 / 主光方向调整，提升演示台近景亮度
- [x] M6：t(1,8) 改为小地图点选初值（M5 由模拟操作员 3 s 后给出）；演示屏逐条显示 ConditionCheckedEvent 的 ✓ / ✗
- [x] M6：单钉节拍统计排除工位末颗的收臂与降柱时间
- [ ] M7：演示台机位（CamAnchor_4）更近、显示器画面更易读；小地图 AGV 标记放大
- [ ] v2：t(1,8) 点选支持拖拽给朝向，并让初值参与定位（目前只写黑板与日志）
