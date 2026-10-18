# 模型资产指南（v1）

规则：**下载的原始文件放进工程根目录 `_Downloads\` 下对应的子文件夹**（Unity 不会导入这里的文件）。之后由 Claude Code 挑选需要的文件，复制到 `Assets\ThirdParty\` 并制作预制体。
每个资产文件夹里都有一个 `SOURCE.md`，记录来源、许可、文件检查结果和导入说明。

---

## 0. 当前下载状态（2026-10-17）

| 资产 | 状态 | 位置 |
|---|---|---|
| UR5e（ur_description 4.3.1） | ✅ 已就绪 | `_Downloads\Robot_UR5e\` |
| 模型机：Airbus A320-200（Dlourine，CC BY 4.0） | ✅ 已就绪；按 1:1.5 缩放（机长 25.05 m），面数需精简 | `_Downloads\Aircraft\Airbus_A320-200\` |
| Noto Sans SC（Regular / Medium / Bold） | ✅ 已就绪 | `_Downloads\Fonts\NotoSansSC\` |
| AprilTag tag36h11 ID 0～11 | ✅ 已就绪 | `_Downloads\AprilTag\tag36h11\` |
| Poly Haven 材质 ×3 | ⏳ 待下载（直链见各 SOURCE.md） | `_Downloads\Textures\PolyHaven_*\` |
| 室内 HDRI | 可选 | `_Downloads\HDRI\` |

---

## 1. 需要下载的资产（v1）

| # | 资产 | 必需？ | 下载网址 | 放到 |
|---|---|---|---|---|
| 1 | UR5e 机械臂网格与运动学参数 | **必需** | https://github.com/UniversalRobots/Universal_Robots_ROS2_Description （Code → Download ZIP） | `_Downloads\Robot_UR5e\` |
| 2 | 模型机外形 | **必需** | 见 §2 | `_Downloads\Aircraft\<名称>\` |
| 3 | 中文字体 Noto Sans SC | **必需** | https://fonts.google.com/noto/specimen/Noto+Sans+SC | `_Downloads\Fonts\NotoSansSC\` |
| 4 | AprilTag 标记图片 | 建议 | https://github.com/AprilRobotics/apriltag-imgs （tag36h11 目录） | `_Downloads\AprilTag\tag36h11\` |
| 5 | PBR 材质贴图 | 建议 | https://polyhaven.com/textures | `_Downloads\Textures\PolyHaven_<名称>\` |
| 6 | 室内环境 HDRI | 可选 | https://polyhaven.com/hdris （分类选 Indoor） | `_Downloads\HDRI\` |

### 1.1 UR5e（必需）
1. 打开仓库页，点绿色 **Code → Download ZIP**，解压。
2. 把下面这些文件复制到 `_Downloads\Robot_UR5e\`，保持原有目录层级：
   - `meshes\ur5e\visual\` 下的 7 个 `.dae`（base、shoulder、upperarm、forearm、wrist1、wrist2、wrist3）
   - `meshes\ur5e\collision\` 下的 7 个 `.stl`
   - `config\ur5e\` 下的 4 个 yaml：default_kinematics、joint_limits、physical_parameters、visual_parameters
   - `urdf\` 整个文件夹
   - 根目录的 `LICENSE`
3. 许可：仓库为 BSD-3-Clause；UR5e 的网格不在 UR 另行限制的型号之列（受限的是 UR8LONG、UR15、UR18、UR20、UR30）。
4. 说明：v1 不使用 URDF Importer。Unity 可以直接导入 `.dae`，Claude Code 会读取 yaml 中的关节参数，自行搭建运动学骨架，从而避开该插件版本较旧带来的兼容风险。

### 1.2 Noto Sans SC（必需）
- 解压后把 `static\` 目录中的 `NotoSansSC-Regular.ttf`、`NotoSansSC-Bold.ttf` 以及 `OFL.txt` 放进去。如果压缩包里只有可变字体 `NotoSansSC-VariableFont_wght.ttf`，就放这个文件。许可为 SIL OFL，可免费商用。

### 1.3 AprilTag（建议）
- 只需要 `tag36h11\` 目录中的 `tag36_11_00000.png` ～ `tag36_11_00011.png`（12 张）和 `LICENSE`，放到 `_Downloads\AprilTag\tag36h11\`。

### 1.4 材质贴图（建议，用于提升观感）
- 来源 ambientCG 或 Poly Haven，均为 CC0，免署名。下载 **2K、JPG** 格式即可，每套一个子文件夹：
  - 地面：Concrete 或 Epoxy floor 一套
  - 车体：Painted Metal 一套
  - 机身：Brushed / Aluminium Metal 一套
  - 轮胎：Rubber 一套
  - 厂房墙面：Corrugated Metal / Metal Plates 一套
- 放到 `_Downloads\Textures\<来源>_<名称>\`，例如 `_Downloads\Textures\ambientCG_Concrete034\`。

---

## 2. 模型机外形（必需，三选一）

| 优先级 | 方案 | 网址 | 格式与许可 | 备注 |
|---|---|---|---|---|
| ① 最佳 | 你们实际模型机的 CAD | — | STEP / STL | 检测区位置能与实物一致；STEP 需先用 FreeCAD 导出为 OBJ / STL |
| ✅ 已选用 | Sketchfab「Airbus A320-200」（作者 Dlourine） | https://sketchfab.com/3d-models/airbus-a320-200-02470bf974e645f78f5d9385a333e9e0 | .glb；CC BY 4.0（需署名）；约 73 万三角面 | 原始单位约为真机的 4.7 倍，需缩放；面数需精简到 50 万以内 |
| 备选 | Sketchfab「Cessna172」（作者 KOG_THORNS） | https://sketchfab.com/3d-models/cessna172-d1b15841c29c43d0862667300bad55a4 | 可下载；CC BY 4.0（需署名）；约 31 万三角面 | 下载时选 **glTF → .glb**，需要免费注册 Sketchfab |
| ③ 备选 | OpenGameArt「Cessna 172」低面数 | https://opengameart.org/content/cessna-172 | .blend；CC-BY 4.0 | 需要用 Blender 导出 FBX |
| 占位 | Sketchfab「Free Cessna 172 Replica (Low-Poly)」 | https://sketchfab.com/3d-models/free-cessna-172-replica-low-poly-basic-model-aa0f72aead044664a0555f047cb36262 | CC BY；约 1k 三角面 | 太粗糙，只适合临时占位 |

- 放到 `_Downloads\Aircraft\<名称>\`，例如 `_Downloads\Aircraft\Cessna172_KOG\`。同时把模型页面的作者名和许可抄进 `SOURCE.md`，CC BY 模型须在演示界面的“致谢”中署名。
- 选用 `.glb` 时，Claude Code 会在 M2 安装 Unity glTFast 包（`com.unity.cloud.gltfast`）来导入。
- 在 Sketchfab 自行挑选其他模型时，筛选条件为 Downloadable 加 CC0 或 CC BY；避开标注 NoAI 的模型；机身表面要连续（便于投影生成铆钉）；三角面最好不超过 50 万。

---

## 3. 不需要下载、由 Claude Code 生成的资产

| 资产 | 生成方式 |
|---|---|
| 厂房（地面、墙、钢柱、门、灯）与三区地面标线 | 编辑器生成器 `HallGenerator` |
| AGV 底盘（车体、车轮、雷达、电池仓、急停、灯带、机械臂底座） | `AgvGenerator`（尺寸取自 `CFG_Agv`） |
| 车载设备盒：超声仪、安全控制盒、工控机（用于 S5 / S6 / S7 高亮） | `AgvGenerator` |
| 探头末端（快换法兰、浮动夹持、探头、耦合剂管）与腕部相机 | `EndEffectorGenerator` |
| 检测区蒙皮贴片与铆钉阵列 | `RivetZoneGenerator`（投影到模型机表面） |
| 区域标记板、充电位、障碍物（工具车、货箱、锥桶） | `PropsGenerator` |
| 演示台、主机、双显示器、大屏 | `DemoStationGenerator` |
| 飞机支撑架、轮挡、安全围栏 | `PropsGenerator` |
| 缺少贴图时使用的纯色材质 | `MaterialLibraryBuilder` |

---

## 4. 导入规范（Claude Code 执行）

- 统一为 1 单位 = 1 m、Y 轴向上；导入后整体尺寸误差 < 1%。
- 面数预算：模型机 ≤ 50 万，机械臂 ≤ 15 万，整个场景 ≤ 200 万；1080p 下帧率 ≥ 60 FPS。
- 材质全部转为 URP/Lit。碰撞体：机械臂连杆用凸网格；模型机用 MeshCollider，放在 `Aircraft` 层。
- 原件不改，调整在 `Assets/_Project/Prefabs/` 的 Prefab Variant 中进行。每导入一项，在 `Docs/ThirdPartyAssets.md` 登记一行。
