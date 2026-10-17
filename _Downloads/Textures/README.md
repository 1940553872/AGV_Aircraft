# PBR 材质贴图（Poly Haven，CC0）

所选 3 套材质均来自 Poly Haven（https://polyhaven.com），许可为 **CC0**，可任意使用，无需署名。
每套只需下载 2K JPG 的 3 张图：漫反射（diff）、法线（nor_gl，OpenGL 格式）、ARM（R=AO、G=粗糙度、B=金属度），文件放进对应子文件夹。

| 子文件夹 | 材质 | 用途 | 3 个文件合计 |
|---|---|---|---|
| PolyHaven_concrete_floor_painted/ | Concrete Floor Painted 涂装混凝土地坪 | 厂房地面（可叠加区域线） | 约 7.9 MB |
| PolyHaven_metal_plate/ | Metal Plate 金属板 | AGV 车体顶板、演示台台面、检修平台 | 约 9.1 MB |
| PolyHaven_corrugated_iron_02/ | Corrugated Iron 02 波纹钢板 | 厂房墙面 | 约 8.5 MB |

每个子文件夹的 SOURCE.md 里有页面网址和 3 个文件的直链。

导入说明（给 M2）：复制到 `Assets/ThirdParty/Textures/PolyHaven/<名称>/`；nor_gl 设为 Normal Map；URP Lit 的金属度 / 光滑度贴图由 Claude Code 从 ARM 生成（光滑度 = 1 − 粗糙度）。
