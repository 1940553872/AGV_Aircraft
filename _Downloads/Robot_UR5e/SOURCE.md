# UR5e 机械臂模型与运动学参数

- 名称：Universal Robots ROS 2 Description（ur_description），仅保留 UR5e 相关文件
- 来源网址：https://github.com/UniversalRobots/Universal_Robots_ROS2_Description
- 分支 / 版本：rolling 分支，package.xml 版本 4.3.1（CHANGELOG 日期 2026-05-06）
- 下载日期：2026-10-17
- 许可：BSD-3-Clause（见本目录 LICENSE）。仓库中 UR8LONG、UR15、UR18、UR20、UR30 的网格另受 UR“图形文档使用条款”约束，UR5e 不在其列。
- 署名要求：BSD-3 要求在再分发时保留版权声明与许可文本；在演示界面“致谢”中注明“UR5e model © Universal Robots, BSD-3-Clause”。

## 目录结构（2026-10-17 整理）
```
Robot_UR5e/
├── LICENSE, package.xml
├── meshes/ur5e/visual/      base, shoulder, upperarm, forearm, wrist1, wrist2, wrist3 (.dae)  ← Unity 导入用
├── meshes/ur5e/collision/   同名 7 个 .stl（Unity 不能直接导入 STL，v1 改用凸包碰撞体，仅作参考）
├── config/ur5e/             default_kinematics / joint_limits / physical_parameters / visual_parameters (.yaml)
└── urdf/                    ur.urdf.xacro、ur_macro.xacro 等（读取 visual 网格的原点偏移时参考）
```
完整仓库（含其他型号）已移到 `_Downloads/_RawArchive/Universal_Robots_ROS2_Description-rolling/`，Unity 不使用。

## 关键参数摘录（来自 config/ur5e）
| 关节原点 | x | y | z | roll |
|---|---|---|---|---|
| shoulder | 0 | 0 | 0.1625 | 0 |
| upper_arm | 0 | 0 | 0 | π/2 |
| forearm | −0.425 | 0 | 0 | 0 |
| wrist_1 | −0.3922 | 0 | 0.1333 | 0 |
| wrist_2 | 0 | −0.0997 | 0 | π/2 |
| wrist_3 | 0 | 0.0996 | 0 | 见文件 |

- 关节限位：elbow ±180°，其余 ±360°；各关节最大速度 180°/s。
- 连杆质量（kg）：shoulder 3.761、upper_arm 8.058、forearm 2.846、wrist_1 1.37、wrist_2 1.3、wrist_3 0.365（base 4.0，文件注明可能不准）。

## 导入说明（给 M2）
- 坐标为 ROS 惯例（右手系，Z 轴向上，单位 m），转换到 Unity（左手系，Y 轴向上）只在 `ArmRig` 构建时进行一次。
- visual 网格的原点偏移按 `urdf/ur_macro.xacro` 中各 link 的 `<visual><origin>` 处理；最新版本修正过 wrist3 网格偏移（CHANGELOG 4.3.1）。
- 目标位置：`Assets/ThirdParty/Robots/UR5e/`（只复制 visual/*.dae 和 config/ur5e/*.yaml）。
