# _Downloads：原始下载文件存放处

此文件夹在 Assets 之外，Unity 不会导入。由 Claude Code 在 M2 挑选需要的文件，复制到 `Assets/ThirdParty/`。
网址与说明见 `../Docs/AssetGuide.md`；每项资产的来源与许可见各自文件夹中的 SOURCE.md。

## 当前状态（2026-10-17）
| 文件夹 | 内容 | 状态 | 复制到（M2） |
|---|---|---|---|
| Robot_UR5e/ | UR5e 网格（dae/stl）、config/ur5e/*.yaml、urdf/、LICENSE | ✅ 已就绪 | Assets/ThirdParty/Robots/UR5e/ |
| Aircraft/Airbus_A320-200/ | airbus_a320-200.glb（CC BY 4.0，需署名） | ✅ 已就绪（按 1:1.5 缩放） | Assets/ThirdParty/Aircraft/Airbus_A320-200/ |
| Fonts/NotoSansSC/ | Regular / Medium / Bold + OFL.txt | ✅ 已就绪 | Assets/ThirdParty/Fonts/NotoSansSC/ |
| AprilTag/tag36h11/ | ID 0～11 共 12 张 PNG + LICENSE | ✅ 已就绪 | Assets/ThirdParty/AprilTag/tag36h11/ |
| Textures/PolyHaven_*/ | 3 套 PBR 材质（地坪、金属板、波纹钢板） | ⏳ 待下载（直链见各 SOURCE.md） | Assets/ThirdParty/Textures/PolyHaven/ |
| HDRI/ | 室内 HDRI | 可选，未下载 | — |
| _RawArchive/ | 完整 UR 仓库、其余字重与可变字体 | 仅存档，不导入 | — |

命名规则：文件夹名不使用空格；每个资产一个文件夹，内含 SOURCE.md。
