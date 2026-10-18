# Claude Code 提示词模板（v1）

用法：每个新会话先粘贴 §1，再粘贴当前里程碑的模板（§2）；日常修改用 §3。`{{…}}` 处由你填写。
一个会话只做一个里程碑；前一个验收通过，再开始下一个。

## 0. 里程碑总览

| 里程碑 | 内容 | 前置条件 |
|---|---|---|
| M0 | 工程整理：归档测试内容、目录、程序集、Core、矩阵 JSON 读取 | — |
| M1 | 场景白盒与生成模型：厂房三区、AGV、探头末端、演示台、道具 | M0 |
| M2 | 厂房与布局重建（模型机 1:1.5、厂房 64 × 44 × 12 m）＋ AGV 升降柱 ＋ 导入外部资产 | M1 ＋ 下载完成 |
| M3 | AGV：遥控、激光建图、A* 规划、路径跟踪、对接停靠 | M1 |
| M4 | 机械臂与检测：逆解、命名姿态、对中 / 接触 / 回退、A 扫、判读 | M2 |
| M5 | SOP 流程编排：按矩阵 JSON 执行，含循环、换站、条件单元、故障注入 | M3 ＋ M4 |
| M6 | 演示界面：电脑屏幕矩阵与执行说明、HUD、相机预设 | M5（可先在 Sandbox_UI 用假数据并行开发） |
| M7 | 联调与观感打磨：自动 / 分步演示、后处理、性能、验收 | M6 |

---

## 1. 会话启动模板

```text
你在开发 RivetSim（AGV 飞机铆钉超声检测示教仿真）。工程 D:\Unity_Project\AGV_Aircraft，Unity 6000.0.71f1 + URP。
请先阅读 CLAUDE.md 和 Docs/PROGRESS.md，以及 Docs/Architecture.md 中与本次任务相关的章节；然后用 unity-cli 技能运行 unity status，确认已连接 Editor。

本次任务：{{里程碑编号与名称 / 具体任务}}

先不要写代码。请回复：
1. 本次目标（3 行以内）；
2. 将新建或修改的文件清单（路径 + 所属程序集）；
3. 需要通过 Editor 执行的操作（生成器菜单、场景保存等）；
4. 需要我决定的问题（没有就写“无”）；
5. 验收步骤与要提供的截图。
我确认后再实现。完成后按 CLAUDE.md 的“完成定义”自检，并更新 Docs/PROGRESS.md。
```

---

## 2. 里程碑模板

### M0 工程整理与骨架
```text
【M0 工程整理与骨架】
1. 用 AssetDatabase.MoveAsset 把现有测试内容（Scenes/HelloWorld.unity、Scenes/SampleScene.unity、Scripts/HelloSphere.cs、Materials/、TutorialInfo/、Readme.asset）移到 Assets/_Archive/，不删除。移动后确认编译正常。
2. 按 Docs/Architecture.md §2 建立 Assets/_Project/ 目录（Config/TaskMatrix/SOP_TaskMatrix_v1.json 已存在，保留不动）。
3. 建立 5 个 asmdef（Core、Simulation、Presentation、Editor、Tests.EditMode），引用关系符合 CLAUDE.md。
4. 实现 Core：SimClock、EventBus、SimLog、TaskMatrixData、TaskMatrixLoader（Newtonsoft Json，含 Architecture §3.1 所列校验），以及 §6 的配置类型（只写字段，带中文 Tooltip 和单位后缀）。在 Config/ 下为每种配置创建一个默认资产 CFG_*。
5. 新增 Layers：Environment、Aircraft、Obstacle、Robot、Visualization；Fixed Timestep 设为 0.01。
6. EditMode 测试：读取 SOP_TaskMatrix_v1.json 后共 48 个单元、3 条回路；t(5,4) 为示例单元；t(1,8)、t(5,6)、t(5,7)、t(6,2) 为条件单元；standby 单元共 9 个、active 单元共 35 个。
7. 菜单 RivetSim/Debug/Print Task Matrix：在 Console 中按 6×8 表格打印矩阵，便于我核对。
验收：编译 0 错误；测试通过；打印出的矩阵与 Docs/Reference/SOP_TaskMatrix_v1.png 一致；列出最终目录树。
禁止：本阶段不建场景内容。
```

### M1 场景白盒与生成模型
```text
【M1 场景白盒与生成模型】
1. 编写编辑器生成器（RivetSim/Generate/...，幂等）：HallGenerator、AgvGenerator、EndEffectorGenerator、DemoStationGenerator、PropsGenerator、MaterialLibraryBuilder（见 Docs/AssetGuide.md §3），参数取自 CFG_SceneLayout、CFG_Agv。
2. 建立 Main_TeachingHall.unity：按 Architecture §4 布置三区，尺寸取 Architecture §6.1 的默认参数；模型机暂时用胶囊体 + 长方体占位（1:5 A320：机长 7.5 m、翼展 7.2 m、机身直径 0.79 m、机身中线离地 0.80 m）；AGV 按 1.00 × 0.70 × 0.45 m 生成，上面暂放简易六轴占位臂。
3. 视觉风格按 Architecture §7 的 3D 部分：深灰环氧地面加黄黑警示线，机器人浅灰配橙色点缀，三区地面贴区域名称。
4. 建立 Sandbox_Robot.unity（地面 + 灯光 + AGV）和 Sandbox_UI.unity（空场景 + UIDocument）。
验收：提供 3 张截图（俯视总览并标注三区尺寸、AGV 近景、演示台近景）；说明最窄通道宽度、AGV 到机身的最小距离。我会据此调整布局。
```

### M2 厂房与布局重建、AGV 升降柱、导入外部资产
```text
【M2 厂房与布局重建、AGV 升降柱、导入外部资产】
依据 ADR-010 与 Architecture §6.1（已更新）。分三部分依次完成，每部分完成后截图给我确认，再进入下一部分；上下文过长时可在部分之间交接并开新会话。

第一部分：厂房与布局重建（模型机 1:1.5）
1-1. 模型机按 1:1.5：25.05 × 23.87 × 7.84 m，机身沿 Z、机头朝 +Z，侧面朝向演示区。厂房改为 64（X）× 44（Z）× 12 m；作业区约 32 × 33 m（外轮廓四周各 ≥ 4 m）。
1-2. 按 ADR-010 的分区重排 CFG_SceneLayout：演示区（−X 端，深 9 m，演示台前 ≥ 4 m 观众站位）、西侧移动区（充电位离作业区 ≥ 6 m，保留一处 1.8 m 窄通道和 3～5 个障碍物）、东侧与南北环道；巡游建图路线绕作业区一圈并覆盖移动区。
1-3. 占位模型机按新尺寸生成（机身直径 2.63 m、机身中线离地约 2.60 m、平尾、发动机、起落架）；钢柱跨距、桁架、顶灯、总览相机按新尺寸重排。
1-4. 更新 Architecture §4 的布局取值表；运行 RivetSim/Generate/All 两次确认幂等，运行 SceneLayoutValidator。
验收：俯视总览截图（标注三区尺寸），报告最窄通道、巡游时 AGV 到机身最小距离、模型机到障碍物最近距离。

第二部分：AGV 升降柱（硬件更新 + 动作逻辑）
2-1. 配置：CFG_Agv 新增升降柱参数（安装位置、截面、固定段高度、行程 0～1.40 m、速度 0.08 m/s、到位精度），车体改为 1.20 × 0.80 × 0.45 m、轮距 0.66 m，取值见 Architecture §6.1。
2-2. 生成器：AgvGenerator 生成升降柱（固定段 + 伸缩段 + 顶部法兰），机械臂挂在伸缩段法兰下；车载设备盒重新布置，不与升降柱干涉。
2-3. 运行时（RivetSim.Simulation/Robot）：LiftColumnController（目标高度、匀速 + 起停缓冲运动、到位事件、MoveToHeightAsync / LowerToBottomAsync）；LiftHeightPlanner（按工位铆钉平均高度和 UR5e 舒适工作区计算升降柱目标高度，并限制在行程内）。
2-4. 联锁（SafetyMonitor 中实现规则，供 S6 使用）：升降柱只在底盘零速锁定后运动；底盘只在升降柱最低位且机械臂运输姿态时运动；违反时拒绝指令并通过 SimLog 记录。
2-5. 与任务矩阵的对应（label 不变，explain 已更新）：t(1,3)/t(2,3) 升降柱最低位；t(3,3) 升到工位作业高度后进入观察位姿；t(6,3) 工位结束收臂并降到最低；t(6,1) 等升降柱降到最低后才倒车。执行器在 M5 实现，本部分只提供可调用的接口。
2-6. Sandbox_Robot 演示：零速锁定 → 升到 2.0 m → 降到最低 → 解锁行驶；未锁定时下发升降指令应被拒绝。
2-7. EditMode 测试：升降运动时间与行程一致、限位、联锁规则（两条）。
验收：AGV 升降柱最低位与最高位两张近景截图；联锁测试结果。

第三部分：导入外部资产
资产已放在 _Downloads/，详情见 _Downloads/README.md 与各文件夹的 SOURCE.md：
- UR5e：_Downloads/Robot_UR5e/（meshes/ur5e/visual/*.dae、config/ur5e/*.yaml、urdf/）
- 模型机：_Downloads/Aircraft/Airbus_A320-200/airbus_a320-200.glb（CC BY 4.0；原始单位约为真机 4.7 倍；约 73 万三角面）
- 字体：_Downloads/Fonts/NotoSansSC/；AprilTag：_Downloads/AprilTag/tag36h11/；贴图：_Downloads/Textures/PolyHaven_*/{{已下载 / 未下载}}
- 模型机仿真尺寸：缩比 1:1.5（机长 25.05 m），参数见 Architecture §6.1 与 ADR-010
请完成（第三部分）：
1. 先检查各文件夹的 SOURCE.md；缺少许可信息时停下来问我。
2. UR5e：把 visual/*.dae 复制到 Assets/ThirdParty/Robots/UR5e/；读取 default_kinematics.yaml 与 joint_limits.yaml，生成 PF_Arm_UR5e（6 关节层级，每个关节有正确的旋转轴和限位，挂 ArmRig 组件，不使用 ArticulationBody）。逐项列出关节参数并与 yaml 核对；把 6 个关节分别转动 ±90°，截图检查网格是否跟随正确的连杆。
3. 模型机：安装 com.unity.cloud.gltfast 后导入 A320；先列出层级，确认 Plane_8 等物体是否为地面或底座（是则移除）、隐藏客舱内饰等不可见部件，把三角面降到 50 万以内；制作 PF_Aircraft_A320（Variant），机头朝向按 ADR-008 与 Architecture §4 的轴向约定，原点设在主起落架中点地面投影，按上面的尺寸统一缩放；只给机身外壳加 MeshCollider，层设为 Aircraft；替换占位体。
4. RivetZoneGenerator：按 CFG_Inspection 在机身左右两侧后机身侧壁（机翼后缘与平尾前缘之间）各生成 1 个区域、每区 2 个工位、每工位 12 颗铆钉，投影贴合机身表面；按缺陷比例 10% 随机设置真值缺陷（随机种子取自 CFG_SimSettings）；每个工位生成 1 块停靠标记板和 1 块基准标记板（AprilTag ID 分配见 _Downloads/AprilTag/tag36h11/SOURCE.md）。
5. 用 Noto Sans SC（Regular / Medium / Bold）创建 UI Toolkit 字体资源；AprilTag 按其 SOURCE.md 的导入设置（Point 过滤、无压缩）制作标记板；如已下载贴图，用 Poly Haven 材质替换纯色材质（光滑度 = 1 − ARM 的 G 通道）。
6. 更新 Docs/ThirdPartyAssets.md（CC BY 模型记录署名信息）。
7. 用导入后的实测外轮廓复核作业区与铆钉排高度；对每个工位验证：升降柱取某一高度时，UR5e 能以法向姿态到达该工位全部 12 颗铆钉（先用简单数值逆解或采样验证，正式逆解在 M4）。
验收：截图（总览、AGV 在工位旁升降柱升起的近景、检测区铆钉近景）；报告三角面总数、每个工位的铆钉数、各工位所需升降柱高度与可达性结果。
```

### M3 AGV 建图、规划与移动
```text
【M3 AGV 建图、规划与移动】（先在 Sandbox_Robot 中开发，再接入 Main）
按 Architecture §3.2 Robot / Navigation 实现：
1. AgvController（差速运动学、限幅、零速锁定、车轮动画）；键盘遥控 WASD，空格急停。
2. Lidar2D（360 线、10 Hz、点云可视化）；OccupancyGridMap（0.1 m，教学简化模型）；MappingRoutine（按 CFG_SceneLayout 的巡游路线行驶并建图，地图半透明叠加在地面上，逐步“长出来”）。
3. 虚拟禁行区：2D 激光雷达（离地 0.30 m）扫不到机身和机翼，规划时把模型机外轮廓投影（含机翼、平尾、发动机）作为虚拟禁行区叠加到代价地图上，只在各工位处开放接近通道（见 ADR-010）。
4. GridAStar（膨胀半径取自 AGV 外廓）+ PathSmoother + PathFollower；NavigationService.NavigateToAsync / DockAsync（识别区域标记后低速对接，记录停靠误差）/ ReverseExitAsync。
5. 可视化：规划路径（青色线）、目标位姿、停靠误差标注、虚拟禁行区（半透明红色）。
6. 移动前检查联锁：升降柱最低位且机械臂运输姿态。
7. EditMode 测试：A* 可达与不可达、差速运动学、禁行区生效。
验收：在 Main 中演示“建图 → 从充电位导航到区域 1 工位 1 → 对接停车 → 倒车退出”，并截图；报告路径长度、用时、停靠误差。
```

### M4 机械臂与超声检测
```text
【M4 机械臂与超声检测】（Sandbox_Robot：AGV 停在一段带铆钉的机身旁）
0. 先由 LiftHeightPlanner 确定本工位升降柱高度（M2 已实现），逆解基座取升降柱顶部法兰的当前位姿。
1. ArmIkSolver（阻尼最小二乘，考虑关节限位，失败时换初值重试）；ArmController：MoveToNamedPoseAsync（Stow / Observe，姿态写在 CFG_Arm）、MoveToPoseAsync、ApproachAlongNormalAsync、RetreatAsync。
2. ProbeEndEffector + ContactModel（虚拟弹簧，教学简化模型）：沿法向逼近，接触力达到 10 N（允许 8～12 N）即停；超过 20 N 时发出“力超限”条件。
3. AScanSynthesizer + GateJudge（教学简化模型）；对每颗铆钉执行一次“对中 → 接触 → 采集 → 判读 → 回退”；铆钉旁显示结果环（绿 = 合格，黄 = 疑似，红 = 缺陷，灰 = 无效）。
4. 简化视觉：钉心复测 = 视场可见性判定 + 小幅位置噪声；基准注册 = 读取区域标记位姿。
5. EditMode 测试：IK 收敛率与精度（Architecture §8）、A 扫底面回波位置。
验收：录制或截图一个工位 12 颗铆钉的连续检测；报告平均单钉节拍、检出与误报数量。
```

### M5 SOP 流程编排
```text
【M5 SOP 流程编排】（Main）
1. 严格按 Docs/Architecture.md §5 实现 TaskMatrixRuntime、MissionOrchestrator、ExecutorRegistry 和全部执行器（键名以 SOP_TaskMatrix_v1.json 为准）。纯逻辑单元使用标称时长。
2. 条件单元：t(1,8) 识别失败 → 弹出提示，让操作员在小地图上点选初值（自动演示模式下 3 s 后自动给出）；t(5,6) 力超限 → 受控退出后重试 T5；t(5,7) 采集无效 → 重贴后重试 T5；t(6,2) 每 10 颗铆钉执行一次基准复测。
3. 回路：下一颗（T6→T4）、同区域换站（→T2）、其他区域（→T1）；全部完成后返回充电位，导出 CSV 检测记录。
   - t(6,1)“倒车退出 换站”只在本工位最后一颗铆钉完成时执行，其余逐钉循环中显示为“跳过”；v1 不为它设计故障处理分支。
   - t(4,7)、t(5,4)、t(6,4) 按 JSON 中的 conditionRefs 逐项判定条件 C1、C2～C8、C9～C12（文字与阈值见 JSON 的 conditions），判定结果通过事件发给演示屏逐条显示 ✓ / ✗。
4. ConditionBus 提供故障注入接口（M6 的 HUD 按钮会调用），本阶段先用键盘 F5 / F6 / F7 测试。
5. S3 执行器同时负责升降柱：t(1,3)/t(2,3) 保持最低位，t(3,3) 升到工位高度，t(6,3) 在工位结束时降到最低；t(1,6)/t(2,6)/t(3,6)/t(6,6) 的许可按 ADR-010 的联锁规则判定；t(6,1) 等待升降柱到最低位后才执行。
6. S1～S8 对应设备的 DeviceIndicator 发光联动（S3 执行期间升降柱与机械臂同时高亮）。
7. 每个单元状态变化都通过 EventBus 发布 CellStateChangedEvent；日志按“[T5][S4] t(5,4) 接触采集 开始 / 完成”的格式输出。
8. EditMode 测试：用假执行器验证状态序列（含循环与一次条件触发）。
验收：4 倍速完整跑完 2 区域 × 2 工位 × 12 颗铆钉；分别注入三种故障各一次，确认对应虚线单元被激活且流程能恢复；附关键日志片段。
```

### M6 演示界面
```text
【M6 演示界面】（先在 Sandbox_UI 用假数据完成，再接入 Main）
严格按 Docs/Architecture.md §7 的视觉规范实现：
1. UI_DemoConsole.uxml/.uss：顶栏、任务矩阵（6×8，行列表头与参考图一致；单元状态样式见 §5.2）、回路箭头“下一颗 / 换站”、图例、当前阶段卡片、当前执行情况说明（执行 t(5,4)、t(6,4) 时逐条列出 C2～C8 / C9～C12 及 ✓）、小地图、A 扫波形、底部日志。
2. DashedBorderElement（Painter2D 分段绘制虚线边框）；Running 单元为粗实线橙色边框加轻微呼吸光。
3. 场景显示器：PanelSettings.targetTexture 渲染到 RenderTexture 并贴到显示器；按 Tab 切换全屏，全屏时可点击单元弹出说明卡（任务文字、所属主体、执行器、explain）。
4. HudView：模式切换（自动 / 分步 / 遥控）、倍速、暂停、下一步、故障注入按钮（识别失败、力超限、采集无效）、可视化开关（V）、相机预设 1～4。
5. CameraRig：总览、跟随 AGV、末端特写、演示台；自动演示时按阶段自动切换（T1/T2 跟随 AGV，T4～T6 末端特写，阶段切换时短暂显示演示台）。
6. 致谢角标：显示 CC BY 模型的署名。
验收：截图——全屏矩阵（含一个 Running 单元和一个已触发的条件单元）、场景内显示器、HUD；Sandbox_UI 中假数据可以逐步驱动全部 8 种单元状态。
```

### M7 联调与观感打磨
```text
【M7 联调与观感打磨】
1. 自动演示与分步演示完整跑通；默认 2 倍速。
2. 观感：URP 后处理（ACES、轻微 Bloom、Vignette）、柔和阴影、灯光层次；检查 UI 在 1920×1080 和 2560×1440 下的显示。
3. 性能：1080p 平均帧率 ≥ 60；激光与栅格更新不产生每帧 GC；Profiler 截图。
4. 列出 Architecture §6.1 中仍为预估值的参数，供后续按实际选型替换。
5. 构建 Windows 独立程序到 Builds/v1/，确认可以离线运行。
6. 更新 Docs/PROGRESS.md 为“v1 交付”，并写出已知限制与 v2 建议。
验收：完整演示流程的时间线说明（几分几秒发生什么）+ 4 张宣传级截图。
```

---

## 3. 通用模板

### 3.1 修改功能或界面
```text
【修改】{{要改什么，以及期望的效果}}
范围：只改 {{模块 / 文件}}；参数放进配置；先列出改动文件，我确认后实现；完成后截图对比修改前后。
```

### 3.2 修改 SOP 任务矩阵
```text
【修改任务矩阵】{{如：t(4,4) 由待命改为 “xxx”，执行器 xxx}}
只修改 Assets/_Project/Config/TaskMatrix/SOP_TaskMatrix_v1.json，必要时新增执行器；运行 RivetSim/Debug/Print Task Matrix 和相关测试；说明对流程的影响。
```

### 3.3 Bug 修复
```text
【Bug】现象：{{…}}　复现步骤：{{…}}　期望：{{…}}　Console：{{粘贴}}
先定位根因（文件与行号）并说明原因，再做最小修改；补一个能复现该问题的测试；不要顺带重构。
```

### 3.4 导入单项资产
```text
【导入资产】{{名称}}，原始文件在 _Downloads/{{…}}，用途 {{…}}，目标尺寸 {{…}}。
按 Docs/AssetGuide.md §4 导入，制作 Prefab Variant，登记 ThirdPartyAssets.md，附截图和三角面数。
```

### 3.5 视觉打磨
```text
【视觉打磨】对象：{{演示界面 / 某区域 / 机器人}}；问题：{{…}}；参考：{{截图或描述}}
遵守 Architecture §7 的配色与字体规范；只调样式和材质，不改逻辑；给出修改前后截图。
```

### 3.6 会话结束交接
```text
【会话结束】按完成定义自检并报告；更新 Docs/PROGRESS.md（完成内容、修改文件、遗留问题、下次第一步）；如有架构相关决定，写入 Docs/DECISIONS.md；给出下次会话可直接粘贴的启动提示词。
```
