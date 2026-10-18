using RivetSim.Core.Config;
using RivetSim.Core.Logging;
using RivetSim.Editor.Tools;
using UnityEditor;
using UnityEngine;

namespace RivetSim.Editor.Generators
{
    /// <summary>
    /// 道具生成器：充电位、货箱、工具车、锥桶、飞机支撑架、轮挡、区域标记板（空白板，M2 贴 AprilTag）。
    /// 货箱、工具车、锥桶按名义尺寸建模，场景摆放时按 CFG_SceneLayout 的障碍物尺寸缩放；
    /// 充电位按 CFG_Agv 的车体尺寸生成，根节点即 AGV 充电位姿。
    /// </summary>
    public static class PropsGenerator
    {
        /// <summary>充电位预制体。</summary>
        public const string ChargingDockPath = ProjectPaths.PrefabsEnvironment + "/PF_ChargingDock.prefab";
        /// <summary>货箱预制体（1 m 立方，按需缩放）。</summary>
        public const string CratePath = ProjectPaths.PrefabsEnvironment + "/PF_Prop_Crate.prefab";
        /// <summary>工具车预制体。</summary>
        public const string ToolCartPath = ProjectPaths.PrefabsEnvironment + "/PF_Prop_ToolCart.prefab";
        /// <summary>锥桶预制体。</summary>
        public const string ConePath = ProjectPaths.PrefabsEnvironment + "/PF_Prop_Cone.prefab";
        /// <summary>飞机支撑架预制体。</summary>
        public const string AircraftStandPath = ProjectPaths.PrefabsEnvironment + "/PF_AircraftStand.prefab";
        /// <summary>轮挡预制体。</summary>
        public const string WheelChockPath = ProjectPaths.PrefabsEnvironment + "/PF_WheelChock.prefab";
        /// <summary>区域标记板预制体。</summary>
        public const string MarkerBoardPath = ProjectPaths.PrefabsEnvironment + "/PF_MarkerBoard.prefab";

        /// <summary>工具车名义尺寸（X, Y, Z），单位 m。</summary>
        public static readonly Vector3 ToolCartNominalSizeM = new Vector3(1.2f, 0.95f, 0.6f);
        /// <summary>锥桶名义尺寸（X, Y, Z），单位 m。</summary>
        public static readonly Vector3 ConeNominalSizeM = new Vector3(0.4f, 0.7f, 0.4f);

        // 外观尺寸，单位 m。
        private const float DockDepthM = 0.25f;
        private const float DockWidthM = 0.6f;
        private const float DockHeightM = 0.6f;
        private const float DockGapM = 0.05f;
        private const float PadMarginM = 0.1f;
        private const float PadLineWidthM = 0.06f;
        private const float FloorLineHeightM = 0.002f;
        private const float StandBaseSizeM = 0.8f;
        private const float StandColumnDiameterM = 0.25f;
        private const float StandPadSizeM = 0.45f;

        /// <summary>生成全部道具预制体。</summary>
        [MenuItem("RivetSim/Generate/Props", priority = 12)]
        public static void Build()
        {
            var agv = GeneratorUtility.LoadConfig<AgvConfig>(ProjectPaths.AgvAsset);
            var layout = GeneratorUtility.LoadConfig<SceneLayoutConfig>(ProjectPaths.SceneLayoutAsset);
            if (agv == null || layout == null) return;

            BuildChargingDock(agv);
            BuildCrate();
            BuildToolCart();
            BuildCone();
            BuildAircraftStand(layout);
            BuildWheelChock();
            BuildMarkerBoard();
            SimLog.Info(LogCategory.System, "道具预制体已生成：7 个。");
        }

        private static void BuildChargingDock(AgvConfig agv)
        {
            Material body = MaterialLibraryBuilder.Get(MaterialLibraryBuilder.RobotDark);
            Material accent = MaterialLibraryBuilder.Get(MaterialLibraryBuilder.RobotAccent);
            Material metal = MaterialLibraryBuilder.Get(MaterialLibraryBuilder.MetalBright);
            Material strip = MaterialLibraryBuilder.Get(MaterialLibraryBuilder.LightStrip);
            Material line = MaterialLibraryBuilder.Get(MaterialLibraryBuilder.LineYellow);

            var root = new GameObject("PF_ChargingDock");
            float agvLength = agv.bodySizeM.x;
            float agvWidth = agv.bodySizeM.y;

            Transform pile = GeneratorUtility.CreateNode("Pile", root.transform,
                new Vector3(0f, 0f, -(agvLength * 0.5f + DockGapM + DockDepthM * 0.5f)));
            GeneratorUtility.CreateBox("Body", pile, new Vector3(0f, DockHeightM * 0.5f, 0f),
                new Vector3(DockWidthM, DockHeightM, DockDepthM), body, true);
            GeneratorUtility.CreateBox("AccentBand", pile, new Vector3(0f, DockHeightM * 0.8f, DockDepthM * 0.5f + 0.002f),
                new Vector3(DockWidthM, 0.06f, 0.004f), accent);
            GeneratorUtility.CreateBox("StatusLight", pile, new Vector3(0f, DockHeightM + 0.01f, 0f),
                new Vector3(DockWidthM * 0.6f, 0.02f, 0.04f), strip);
            foreach (float side in new[] { -1f, 1f })
            {
                GeneratorUtility.CreateBox(side < 0f ? "Contact_L" : "Contact_R", pile,
                    new Vector3(side * 0.12f, 0.15f, DockDepthM * 0.5f + 0.02f), new Vector3(0.08f, 0.03f, 0.04f), metal);
            }
            GeneratorUtility.SetLayerRecursive(pile.gameObject, SimLayers.ObstacleIndex);

            Transform pad = GeneratorUtility.CreateNode("FloorPad", root.transform, Vector3.zero);
            BuildOutline(pad, new Vector2(agvWidth + PadMarginM * 2f, agvLength + PadMarginM * 2f), PadLineWidthM, line);
            GeneratorUtility.SetLayerRecursive(pad.gameObject, SimLayers.EnvironmentIndex);

            GeneratorUtility.SavePrefab(root, ChargingDockPath);
        }

        /// <summary>在父节点下画矩形地面框线，size 为（沿本地 X, 沿本地 Z）。</summary>
        public static void BuildOutline(Transform parent, Vector2 size, float lineWidthM, Material material)
        {
            float y = FloorLineHeightM;
            GeneratorUtility.CreateBox("Line_N", parent, new Vector3(0f, y, size.y * 0.5f), new Vector3(size.x + lineWidthM, FloorLineHeightM, lineWidthM), material);
            GeneratorUtility.CreateBox("Line_S", parent, new Vector3(0f, y, -size.y * 0.5f), new Vector3(size.x + lineWidthM, FloorLineHeightM, lineWidthM), material);
            GeneratorUtility.CreateBox("Line_E", parent, new Vector3(size.x * 0.5f, y, 0f), new Vector3(lineWidthM, FloorLineHeightM, size.y), material);
            GeneratorUtility.CreateBox("Line_W", parent, new Vector3(-size.x * 0.5f, y, 0f), new Vector3(lineWidthM, FloorLineHeightM, size.y), material);
        }

        private static void BuildCrate()
        {
            Material wood = MaterialLibraryBuilder.Get(MaterialLibraryBuilder.Crate);
            Material frame = MaterialLibraryBuilder.Get(MaterialLibraryBuilder.RobotDark);
            var root = new GameObject("PF_Prop_Crate");
            GeneratorUtility.CreateBox("Box", root.transform, new Vector3(0f, 0.5f, 0f), new Vector3(0.96f, 0.96f, 0.96f), wood);
            // 四条竖边框与上下框，1 m 立方名义尺寸。
            foreach (float x in new[] { -0.48f, 0.48f })
            {
                foreach (float z in new[] { -0.48f, 0.48f })
                    GeneratorUtility.CreateBox("Edge", root.transform, new Vector3(x, 0.5f, z), new Vector3(0.05f, 1f, 0.05f), frame);
            }
            foreach (float side in new[] { -0.48f, 0.48f })
            {
                GeneratorUtility.CreateBox("Rim_X", root.transform, new Vector3(0f, 0.975f, side), new Vector3(1f, 0.05f, 0.05f), frame);
                GeneratorUtility.CreateBox("Rim_Z", root.transform, new Vector3(side, 0.975f, 0f), new Vector3(0.05f, 0.05f, 1f), frame);
            }
            GeneratorUtility.CreateBox("Pallet", root.transform, new Vector3(0f, 0.025f, 0f), new Vector3(1f, 0.05f, 1f), frame);
            GeneratorUtility.SetLayerRecursive(root, SimLayers.ObstacleIndex);
            GeneratorUtility.SavePrefab(root, CratePath);
        }

        private static void BuildToolCart()
        {
            Material red = MaterialLibraryBuilder.Get(MaterialLibraryBuilder.ToolCart);
            Material metal = MaterialLibraryBuilder.Get(MaterialLibraryBuilder.MetalBright);
            Material rubber = MaterialLibraryBuilder.Get(MaterialLibraryBuilder.Rubber);
            var root = new GameObject("PF_Prop_ToolCart");
            Vector3 s = ToolCartNominalSizeM;
            float postX = s.x * 0.5f - 0.05f, postZ = s.z * 0.5f - 0.04f;
            foreach (float x in new[] { -postX, postX })
            {
                foreach (float z in new[] { -postZ, postZ })
                {
                    GeneratorUtility.CreateCylinder("Post", root.transform, new Vector3(x, 0.1f + (s.y - 0.15f) * 0.5f, z), Vector3.up, 0.03f, s.y - 0.15f, metal);
                    GeneratorUtility.CreateCylinder("Caster", root.transform, new Vector3(x, 0.05f, z), Vector3.right, 0.1f, 0.04f, rubber);
                }
            }
            foreach (float y in new[] { 0.15f, 0.5f, 0.82f })
                GeneratorUtility.CreateBox("Shelf", root.transform, new Vector3(0f, y, 0f), new Vector3(s.x - 0.04f, 0.03f, s.z - 0.02f), red);
            GeneratorUtility.CreateCylinder("Handle", root.transform, new Vector3(s.x * 0.5f - 0.02f, s.y - 0.03f, 0f), Vector3.forward, 0.03f, s.z - 0.08f, metal);
            GeneratorUtility.CreateBox("ToolBox", root.transform, new Vector3(-0.2f, 0.92f, 0f), new Vector3(0.45f, 0.18f, 0.25f), red);
            GeneratorUtility.SetLayerRecursive(root, SimLayers.ObstacleIndex);
            GeneratorUtility.SavePrefab(root, ToolCartPath);
        }

        private static void BuildCone()
        {
            Material orange = MaterialLibraryBuilder.Get(MaterialLibraryBuilder.Cone);
            Material white = MaterialLibraryBuilder.Get(MaterialLibraryBuilder.LineWhite);
            Material dark = MaterialLibraryBuilder.Get(MaterialLibraryBuilder.Rubber);
            var root = new GameObject("PF_Prop_Cone");
            Vector3 s = ConeNominalSizeM;
            GeneratorUtility.CreateBox("Base", root.transform, new Vector3(0f, 0.015f, 0f), new Vector3(s.x, 0.03f, s.z), dark);
            // 圆柱逐段收窄近似锥体。
            const int segments = 6;
            float bodyHeight = s.y - 0.03f;
            for (int i = 0; i < segments; i++)
            {
                float t = (i + 0.5f) / segments;
                float diameter = Mathf.Lerp(s.x * 0.75f, s.x * 0.15f, t);
                Material m = i == 3 ? white : orange;
                GeneratorUtility.CreateCylinder($"Segment_{i}", root.transform, new Vector3(0f, 0.03f + bodyHeight * t, 0f), Vector3.up,
                    diameter, bodyHeight / segments, m);
            }
            GeneratorUtility.SetLayerRecursive(root, SimLayers.ObstacleIndex);
            GeneratorUtility.SavePrefab(root, ConePath);
        }

        private static void BuildAircraftStand(SceneLayoutConfig layout)
        {
            Material yellow = MaterialLibraryBuilder.Get(MaterialLibraryBuilder.SafetyYellow);
            Material dark = MaterialLibraryBuilder.Get(MaterialLibraryBuilder.RobotDark);
            var root = new GameObject("PF_AircraftStand");
            float h = AircraftPlaceholderGenerator.WingBottomHeightM(layout);
            GeneratorUtility.CreateBox("BasePlate", root.transform, new Vector3(0f, 0.03f, 0f), new Vector3(StandBaseSizeM, 0.06f, StandBaseSizeM), dark, true);
            GeneratorUtility.CreateCylinder("Column", root.transform, new Vector3(0f, h * 0.5f, 0f), Vector3.up, StandColumnDiameterM, h - 0.06f, yellow, true);
            GeneratorUtility.CreateBox("TopPad", root.transform, new Vector3(0f, h - 0.025f, 0f), new Vector3(StandPadSizeM, 0.05f, StandPadSizeM), dark);
            GeneratorUtility.SetLayerRecursive(root, SimLayers.ObstacleIndex);
            GeneratorUtility.SavePrefab(root, AircraftStandPath);
        }

        private static void BuildWheelChock()
        {
            Material yellow = MaterialLibraryBuilder.Get(MaterialLibraryBuilder.SafetyYellow);
            var root = new GameObject("PF_WheelChock");
            // 名义尺寸对应 1:1.5 模型机主机轮（直径约 0.73 m）。
            GeneratorUtility.CreateBox("Base", root.transform, new Vector3(0f, 0.04f, 0f), new Vector3(0.30f, 0.08f, 0.25f), yellow);
            GeneratorUtility.CreateBox("Wedge", root.transform, new Vector3(0f, 0.09f, 0f), Quaternion.Euler(35f, 0f, 0f),
                new Vector3(0.30f, 0.12f, 0.20f), yellow);
            GeneratorUtility.SetLayerRecursive(root, SimLayers.ObstacleIndex);
            GeneratorUtility.SavePrefab(root, WheelChockPath);
        }

        private static void BuildMarkerBoard()
        {
            Material white = MaterialLibraryBuilder.Get(MaterialLibraryBuilder.MarkerBoard);
            Material dark = MaterialLibraryBuilder.Get(MaterialLibraryBuilder.RobotDark);
            var root = new GameObject("PF_MarkerBoard");
            GeneratorUtility.CreateBox("Base", root.transform, new Vector3(0f, 0.015f, 0f), new Vector3(0.3f, 0.03f, 0.3f), dark);
            GeneratorUtility.CreateCylinder("Pole", root.transform, new Vector3(0f, 0.4f, 0f), Vector3.up, 0.03f, 0.8f, dark);
            GeneratorUtility.CreateBox("Frame", root.transform, new Vector3(0f, 0.95f, 0f), new Vector3(0.34f, 0.34f, 0.02f), dark);
            GeneratorUtility.CreateScreenQuad("Tag", root.transform, new Vector3(0f, 0.95f, 0.011f), new Vector2(0.3f, 0.3f), white);
            GeneratorUtility.SetLayerRecursive(root, SimLayers.ObstacleIndex);
            GeneratorUtility.SavePrefab(root, MarkerBoardPath);
        }
    }
}
