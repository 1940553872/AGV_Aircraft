using RivetSim.Core.Config;
using UnityEngine;

namespace RivetSim.Editor.Generators
{
    /// <summary>
    /// 厂房生成器：地面、墙体（南墙开入口门）、钢柱、屋顶桁架（不做顶板，俯视可见）、顶灯，
    /// 以及三区地面标线与区域文字。尺寸取自 CFG_SceneLayout；由 MainSceneBuilder 调用。
    /// </summary>
    public static class HallGenerator
    {
        // 厂房外观尺寸，单位 m。
        private const float FloorThicknessM = 0.1f;
        private const float WallThicknessM = 0.3f;
        private const float DadoHeightM = 1.2f;
        private const float ColumnSizeM = 0.4f;
        // 9 m 跨距使 44 m 宽的西墙为奇数跨，墙中心（大屏位置）不落柱。
        private const float ColumnSpacingM = 9f;
        private const float TrussDepthM = 0.6f;
        private const float TrussWidthM = 0.25f;
        private const float LampDropM = 1.5f;
        private const float LampSizeXM = 2.0f;
        private const float LampSizeZM = 0.5f;
        private const float LampRowSpacingM = 14f;
        private const float MarkingHeightM = 0.002f;
        private const float LabelHeightAboveFloorM = 0.004f;
        private const float DemoLineWidthM = 0.10f;
        private const float StartLineWidthM = 0.05f;
        private const float WorkLabelHeightM = 2.0f;
        private const float MobilityLabelHeightM = 2.8f;
        private const float DemoLabelHeightM = 2.0f;
        private const float DemoLabelInsetM = 3.5f;
        private const float SmallLabelHeightM = 0.6f;

        /// <summary>生成厂房主体（放在 Environment 根节点下，Environment 层）。</summary>
        public static void BuildHall(Transform root, SceneLayoutConfig layout)
        {
            Material floor = MaterialLibraryBuilder.Get(MaterialLibraryBuilder.FloorEpoxy);
            Material wall = MaterialLibraryBuilder.Get(MaterialLibraryBuilder.Wall);
            Material steel = MaterialLibraryBuilder.Get(MaterialLibraryBuilder.Steel);
            Material door = MaterialLibraryBuilder.Get(MaterialLibraryBuilder.Door);
            Material lamp = MaterialLibraryBuilder.Get(MaterialLibraryBuilder.LampEmissive);
            Material yellow = MaterialLibraryBuilder.Get(MaterialLibraryBuilder.SafetyYellow);

            float lx = layout.hallSizeM.x, lz = layout.hallSizeM.y, h = layout.hallSizeM.z;
            float hx = lx * 0.5f, hz = lz * 0.5f, wt = WallThicknessM;

            GeneratorUtility.CreateBox("Floor", root, new Vector3(0f, -FloorThicknessM * 0.5f, 0f),
                new Vector3(lx + wt * 2f, FloorThicknessM, lz + wt * 2f), floor, true);

            // 墙体：北、东、西整面；南墙在入口门处分段并加门楣。
            Transform walls = GeneratorUtility.CreateNode("Walls", root, Vector3.zero);
            GeneratorUtility.CreateBox("Wall_N", walls, new Vector3(0f, h * 0.5f, hz + wt * 0.5f), new Vector3(lx + wt * 2f, h, wt), wall, true);
            GeneratorUtility.CreateBox("Wall_E", walls, new Vector3(hx + wt * 0.5f, h * 0.5f, 0f), new Vector3(wt, h, lz), wall, true);
            GeneratorUtility.CreateBox("Wall_W", walls, new Vector3(-hx - wt * 0.5f, h * 0.5f, 0f), new Vector3(wt, h, lz), wall, true);
            float doorX0 = layout.entranceDoorCenterXM - layout.entranceDoorWidthM * 0.5f;
            float doorX1 = layout.entranceDoorCenterXM + layout.entranceDoorWidthM * 0.5f;
            float southZ = -hz - wt * 0.5f;
            CreateSpanX("Wall_S_West", walls, -hx - wt, doorX0, 0f, h, southZ, wt, wall);
            CreateSpanX("Wall_S_East", walls, doorX1, hx + wt, 0f, h, southZ, wt, wall);
            CreateSpanX("Wall_S_Lintel", walls, doorX0, doorX1, layout.entranceDoorHeightM, h, southZ, wt, wall);
            Transform entrance = GeneratorUtility.CreateNode("Entrance", root, new Vector3(layout.entranceDoorCenterXM, 0f, -hz));
            GeneratorUtility.CreateBox("Door", entrance, new Vector3(0f, layout.entranceDoorHeightM * 0.5f, -wt * 0.5f),
                new Vector3(layout.entranceDoorWidthM, layout.entranceDoorHeightM, 0.08f), door, true);
            GeneratorUtility.CreateBox("Frame", entrance, new Vector3(0f, layout.entranceDoorHeightM + 0.05f, 0.01f),
                new Vector3(layout.entranceDoorWidthM + 0.2f, 0.1f, 0.02f), yellow);

            // 墙裙：内墙面下部深色护板。
            Transform dado = GeneratorUtility.CreateNode("Dado", root, Vector3.zero);
            GeneratorUtility.CreateBox("Dado_N", dado, new Vector3(0f, DadoHeightM * 0.5f, hz - 0.01f), new Vector3(lx, DadoHeightM, 0.02f), steel);
            GeneratorUtility.CreateBox("Dado_E", dado, new Vector3(hx - 0.01f, DadoHeightM * 0.5f, 0f), new Vector3(0.02f, DadoHeightM, lz), steel);
            GeneratorUtility.CreateBox("Dado_W", dado, new Vector3(-hx + 0.01f, DadoHeightM * 0.5f, 0f), new Vector3(0.02f, DadoHeightM, lz), steel);

            BuildColumnsAndRoof(root, layout, steel, MaterialLibraryBuilder.Get(MaterialLibraryBuilder.RoofInner), lamp);
            GeneratorUtility.SetLayerRecursive(root.gameObject, Core.Config.SimLayers.EnvironmentIndex);
        }

        private static void BuildColumnsAndRoof(Transform root, SceneLayoutConfig layout, Material steel, Material roofInner, Material lamp)
        {
            float lx = layout.hallSizeM.x, lz = layout.hallSizeM.y, h = layout.hallSizeM.z;
            float hx = lx * 0.5f, hz = lz * 0.5f, c = ColumnSizeM;
            int baysX = Mathf.Max(1, Mathf.RoundToInt(lx / ColumnSpacingM));
            int baysZ = Mathf.Max(1, Mathf.RoundToInt(lz / ColumnSpacingM));
            float stepX = lx / baysX, stepZ = lz / baysZ;

            Transform columns = GeneratorUtility.CreateNode("Columns", root, Vector3.zero);
            for (int i = 0; i <= baysX; i++)
            {
                float x = Mathf.Clamp(-hx + i * stepX, -hx + c * 0.5f, hx - c * 0.5f);
                GeneratorUtility.CreateBox($"Column_S{i}", columns, new Vector3(x, h * 0.5f, -hz + c * 0.5f), new Vector3(c, h, c), steel, true);
                GeneratorUtility.CreateBox($"Column_N{i}", columns, new Vector3(x, h * 0.5f, hz - c * 0.5f), new Vector3(c, h, c), steel, true);
            }
            for (int j = 1; j < baysZ; j++)
            {
                float z = -hz + j * stepZ;
                GeneratorUtility.CreateBox($"Column_W{j}", columns, new Vector3(-hx + c * 0.5f, h * 0.5f, z), new Vector3(c, h, c), steel, true);
                GeneratorUtility.CreateBox($"Column_E{j}", columns, new Vector3(hx - c * 0.5f, h * 0.5f, z), new Vector3(c, h, c), steel, true);
            }

            // 屋顶只做桁架与檩条，不做顶板，俯视截图可透过。
            Transform roof = GeneratorUtility.CreateNode("Roof", root, Vector3.zero);
            for (int i = 0; i <= baysX; i++)
            {
                float x = Mathf.Clamp(-hx + i * stepX, -hx + c * 0.5f, hx - c * 0.5f);
                GeneratorUtility.CreateBox($"Truss_{i}", roof, new Vector3(x, h - TrussDepthM * 0.5f, 0f), new Vector3(TrussWidthM, TrussDepthM, lz), roofInner);
            }
            foreach (float z in new[] { -hz * 0.5f, 0f, hz * 0.5f })
                GeneratorUtility.CreateBox("Purlin", roof, new Vector3(0f, h - 0.1f, z), new Vector3(lx, 0.15f, 0.15f), roofInner);

            // 顶灯：每个 X 向跨间一列，按 LampRowSpacingM 沿 Z 均分成若干排。
            Transform lamps = GeneratorUtility.CreateNode("Lamps", root, Vector3.zero);
            int lampRows = Mathf.Max(2, Mathf.RoundToInt(lz / LampRowSpacingM));
            for (int i = 0; i < baysX; i++)
            {
                float x = -hx + (i + 0.5f) * stepX;
                for (int j = 0; j < lampRows; j++)
                {
                    float z = -hz + (j + 0.5f) * lz / lampRows;
                    Transform fixture = GeneratorUtility.CreateNode($"Lamp_{i}_{j}", lamps, new Vector3(x, h - LampDropM, z));
                    GeneratorUtility.CreateBox("Housing", fixture, new Vector3(0f, 0.05f, 0f), new Vector3(LampSizeXM + 0.06f, 0.08f, LampSizeZM + 0.06f), steel);
                    GeneratorUtility.CreateBox("Emitter", fixture, Vector3.zero, new Vector3(LampSizeXM, 0.02f, LampSizeZM), lamp);
                    GeneratorUtility.CreateBox("Rod", fixture, new Vector3(0f, LampDropM * 0.5f, 0f), new Vector3(0.02f, LampDropM, 0.02f), steel);
                }
            }
        }

        /// <summary>生成三区地面标线、区域文字与起始位标记。</summary>
        public static void BuildZoneMarkings(Transform workRoot, Transform mobilityRoot, Transform demoRoot,
            SceneLayoutConfig layout, AgvConfig agv)
        {
            float y = MarkingHeightM;
            float w = MaterialLibraryBuilder.HazardLineWidthM;

            // 作业区：黄黑警示线框。
            Transform workLines = GeneratorUtility.CreateNode("Markings", workRoot, Vector3.zero);
            ZoneRect work = layout.workZone;
            Material hazardX = MaterialLibraryBuilder.Get(MaterialLibraryBuilder.HazardX);
            Material hazardZ = MaterialLibraryBuilder.Get(MaterialLibraryBuilder.HazardZ);
            Vector2 min = work.MinM, max = work.MaxM, center = work.centerM;
            GeneratorUtility.CreateBox("Hazard_N", workLines, new Vector3(center.x, y, max.y), new Vector3(work.sizeM.x + w, MarkingHeightM, w), hazardX);
            GeneratorUtility.CreateBox("Hazard_S", workLines, new Vector3(center.x, y, min.y), new Vector3(work.sizeM.x + w, MarkingHeightM, w), hazardX);
            GeneratorUtility.CreateBox("Hazard_E", workLines, new Vector3(max.x, y, center.y), Quaternion.Euler(0f, 90f, 0f),
                new Vector3(work.sizeM.y - w, MarkingHeightM, w), hazardZ);
            GeneratorUtility.CreateBox("Hazard_W", workLines, new Vector3(min.x, y, center.y), Quaternion.Euler(0f, 90f, 0f),
                new Vector3(work.sizeM.y - w, MarkingHeightM, w), hazardZ);
            Label(workLines, ZoneLabelBaker.Work, new Vector3(center.x, 0f, min.y + WorkLabelHeightM * 0.5f + 0.15f), WorkLabelHeightM);

            // 移动区：文字放在作业区与东墙之间。
            Transform mobilityLines = GeneratorUtility.CreateNode("Markings", mobilityRoot, Vector3.zero);
            ZoneRect mobility = layout.mobilityZone;
            float mobilityLabelX = (work.MaxM.x + mobility.MaxM.x) * 0.5f;
            Label(mobilityLines, ZoneLabelBaker.Mobility, new Vector3(mobilityLabelX, 0f, mobility.centerM.y), MobilityLabelHeightM);
            BuildStartMarker(mobilityLines, layout.startPose, agv);
            Vector3 dock = layout.chargingDockPose.WorldPosition;
            Label(mobilityLines, ZoneLabelBaker.Charging, dock + new Vector3(0f, 0f, -(agv.bodySizeM.y * 0.5f + 0.45f)), SmallLabelHeightM);

            // 演示区：与移动区分界的黄色实线，文字靠近入口。
            Transform demoLines = GeneratorUtility.CreateNode("Markings", demoRoot, Vector3.zero);
            ZoneRect demo = layout.demoZone;
            Material yellow = MaterialLibraryBuilder.Get(MaterialLibraryBuilder.LineYellow);
            GeneratorUtility.CreateBox("Boundary", demoLines, new Vector3(demo.MaxM.x, y, demo.centerM.y),
                new Vector3(DemoLineWidthM, MarkingHeightM, demo.sizeM.y), yellow);
            Label(demoLines, ZoneLabelBaker.Demo, new Vector3(demo.centerM.x, 0f, demo.MinM.y + DemoLabelInsetM), DemoLabelHeightM);

            foreach (Transform t in new[] { workLines, mobilityLines, demoLines })
                GeneratorUtility.SetLayerRecursive(t.gameObject, SimLayers.EnvironmentIndex);
        }

        private static void BuildStartMarker(Transform parent, PlanarPose pose, AgvConfig agv)
        {
            Material white = MaterialLibraryBuilder.Get(MaterialLibraryBuilder.LineWhite);
            Transform marker = GeneratorUtility.CreateNode("StartMarker", parent, pose.WorldPosition, pose.WorldRotation);
            float margin = 0.1f;
            PropsGenerator.BuildOutline(marker, new Vector2(agv.bodySizeM.y + margin * 2f, agv.bodySizeM.x + margin * 2f), StartLineWidthM, white);
            float arrow = agv.bodySizeM.x * 0.3f;
            GeneratorUtility.CreateBox("Arrow_Shaft", marker, new Vector3(0f, MarkingHeightM, 0f), new Vector3(StartLineWidthM, MarkingHeightM, arrow * 2f), white);
            foreach (float side in new[] { -1f, 1f })
            {
                GeneratorUtility.CreateBox(side < 0f ? "Arrow_L" : "Arrow_R", marker,
                    new Vector3(side * arrow * 0.25f, MarkingHeightM, arrow * 0.75f), Quaternion.Euler(0f, side * -45f, 0f),
                    new Vector3(StartLineWidthM, MarkingHeightM, arrow * 0.7f), white);
            }
            Label(parent, ZoneLabelBaker.Start,
                pose.WorldPosition + pose.WorldRotation * new Vector3(agv.bodySizeM.y * 0.5f + 0.45f, 0f, 0f), SmallLabelHeightM);
        }

        private static void Label(Transform parent, string id, Vector3 position, float heightM)
        {
            Material material = ZoneLabelBaker.GetMaterial(id);
            float width = heightM * ZoneLabelBaker.GetAspect(id);
            GeneratorUtility.CreateFloorQuad($"Label_{id}", parent, position + Vector3.up * LabelHeightAboveFloorM,
                new Vector2(width, heightM), 0f, material);
        }

        private static void CreateSpanX(string name, Transform parent, float x0, float x1, float y0, float y1, float z,
            float thickness, Material material)
        {
            if (x1 <= x0 || y1 <= y0) return;
            GeneratorUtility.CreateBox(name, parent, new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, z),
                new Vector3(x1 - x0, y1 - y0, thickness), material, true);
        }
    }
}
