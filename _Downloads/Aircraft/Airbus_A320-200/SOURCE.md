# 模型机外形：Airbus A320-200

- 名称：Airbus A320-200
- 文件：airbus_a320-200.glb（约 23 MB，glTF 2.0 二进制）
- 来源网址：https://sketchfab.com/3d-models/airbus-a320-200-02470bf974e645f78f5d9385a333e9e0
- 作者：Dlourine（https://sketchfab.com/fDlruosne），使用 Blender 制作
- 发布日期：2025-05-22　｜　下载日期：2026-10-17
- 许可：CC BY 4.0（http://creativecommons.org/licenses/by/4.0/），GLB 内嵌的 asset.extras 信息与此一致；页面无 NoAI 标注
- **署名要求（必须）**：演示界面“致谢”中写明：
  “Airbus A320-200” by Dlourine (https://sketchfab.com/fDlruosne), licensed under CC BY 4.0, scaled and simplified for this simulation.
- 商标说明：Airbus、A320 为空客公司商标。本模型仅用于教学仿真外观，不暗示与空客有任何关联；对外展示时不要使用空客标志做宣传。

## 文件检查结果（2026-10-17）
| 项目 | 数值 | 对 M2 的影响 |
|---|---|---|
| 三角面 | 约 73.2 万（页面标注 738k），223 个网格、60 个材质 | 超过 50 万的预算：M2 需隐藏客舱内饰等不可见部件，或减面 |
| 贴图 | 无图片贴图，只有材质颜色（使用 KHR_materials_specular） | glTFast 可直接导入；可按需改成本项目的 URP 材质 |
| 原始尺寸 | 包围盒约 179 × 68 × 164（原单位，机身沿 X 轴） | 约为真机的 4.7 倍，**单位不是米**，导入后必须按目标尺寸统一缩放 |
| 场景节点 | 第一个子节点名为 “Plane_8”，可能是地面或底座 | M2 需确认并移除非飞机物体 |

真机参考尺寸：机长 37.57 m、翼展 35.80 m、机高 11.76 m。

## 仿真中的模型机尺寸（v1 采用）
缩比 1:5：机长 7.5 m、翼展 7.2 m、机高 2.35 m、机身直径约 0.79 m（详见 Docs/Architecture.md §6.1）。
原始文件按机长统一缩放：缩放系数 = 7.5 ÷ 导入后的实测机长。

## 导入说明（给 M2）
- 安装 Unity glTFast（`com.unity.cloud.gltfast`）后，复制到 `Assets/ThirdParty/Aircraft/Airbus_A320-200/`。
- 制作 Prefab Variant `PF_Aircraft_A320`：统一缩放、机头朝 +Z、主起落架中点作为原点；加 MeshCollider（只给机身外壳加），层设为 `Aircraft`。
