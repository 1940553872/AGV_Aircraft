using RivetSim.Core.Config;
using RivetSim.Core.Logging;
using RivetSim.Editor.Tools;
using UnityEditor;
using UnityEngine;

namespace RivetSim.Editor.Generators
{
    /// <summary>
    /// 模型机占位体生成器（M2 第三部分由 A320 模型替换）：胶囊体机身 + 长方体机翼 / 尾翼 + 圆柱发动机与起落架。
    /// 根节点位于包围盒中心的地面投影，机头朝 +Z；机长、翼展、机高、机身直径、机身中线高度取自 CFG_SceneLayout，
    /// 其余占位外观尺寸按“相对机长的比例”换算（比例取自 M1 的 1:5 占位外观），缩比改变时无需修改本文件。
    /// </summary>
    public static class AircraftPlaceholderGenerator
    {
        /// <summary>预制体路径。</summary>
        public const string PrefabPath = ProjectPaths.PrefabsAircraft + "/PF_Aircraft_Placeholder.prefab";

        // 占位外观尺寸：相对机长的比例（无量纲），位置相对根节点。
        private const float WingChordRatio = 0.16f;
        private const float WingThicknessRatio = 0.012f;
        private const float WingCenterYRatio = 0.0733f;
        private const float WingCenterZRatio = 0.0533f;
        private const float StabSpanRatio = 0.3333f;
        private const float StabChordRatio = 0.0733f;
        private const float StabThicknessRatio = 0.0067f;
        private const float StabCenterYRatio = 0.1267f;
        private const float StabCenterZRatio = -0.4067f;
        private const float FinChordRatio = 0.12f;
        private const float FinThicknessRatio = 0.008f;
        private const float FinCenterZRatio = -0.4133f;
        private const float EngineDiameterRatio = 0.056f;
        private const float EngineLengthRatio = 0.1133f;
        private const float EngineOffsetXRatio = 0.1533f;
        private const float EngineCenterYRatio = 0.0533f;
        private const float EngineCenterZRatio = 0.1333f;
        private const float NoseGearZRatio = 0.3867f;
        private const float NoseWheelDiameterRatio = 0.0187f;
        private const float MainGearOffsetXRatio = 0.06f;
        private const float MainGearZRatio = -0.0333f;
        private const float MainWheelDiameterRatio = 0.0293f;
        private const float WheelWidthRatio = 0.0093f;
        private const float StrutDiameterRatio = 0.0067f;
        private const float StandOffsetXRatio = 0.3467f;

        /// <summary>机翼下表面离地高度，单位 m（支撑架高度）。</summary>
        public static float WingBottomHeightM(SceneLayoutConfig layout) =>
            layout.aircraftLengthM * (WingCenterYRatio - WingThicknessRatio * 0.5f);

        /// <summary>机翼支撑架位置（相对根节点），单位 m。</summary>
        public static Vector3[] StandPositions(SceneLayoutConfig layout)
        {
            float l = layout.aircraftLengthM;
            return new[] { new Vector3(-StandOffsetXRatio * l, 0f, WingCenterZRatio * l), new Vector3(StandOffsetXRatio * l, 0f, WingCenterZRatio * l) };
        }

        /// <summary>主起落架机轮位置（相对根节点的地面投影），单位 m。</summary>
        public static Vector3[] MainWheelPositions(SceneLayoutConfig layout)
        {
            float l = layout.aircraftLengthM;
            return new[] { new Vector3(-MainGearOffsetXRatio * l, 0f, MainGearZRatio * l), new Vector3(MainGearOffsetXRatio * l, 0f, MainGearZRatio * l) };
        }

        /// <summary>主机轮直径，单位 m（轮挡摆放用）。</summary>
        public static float MainWheelDiameterM(SceneLayoutConfig layout) => layout.aircraftLengthM * MainWheelDiameterRatio;

        /// <summary>生成占位模型机预制体。</summary>
        [MenuItem("RivetSim/Generate/Aircraft Placeholder", priority = 13)]
        public static void Build()
        {
            var layout = GeneratorUtility.LoadConfig<SceneLayoutConfig>(ProjectPaths.SceneLayoutAsset);
            if (layout == null) return;

            Material skin = MaterialLibraryBuilder.Get(MaterialLibraryBuilder.AircraftSkin);
            Material accent = MaterialLibraryBuilder.Get(MaterialLibraryBuilder.AircraftAccent);
            Material metal = MaterialLibraryBuilder.Get(MaterialLibraryBuilder.MetalBright);
            Material dark = MaterialLibraryBuilder.Get(MaterialLibraryBuilder.RobotDark);
            Material rubber = MaterialLibraryBuilder.Get(MaterialLibraryBuilder.Rubber);

            var root = new GameObject("PF_Aircraft_Placeholder");
            Transform t = root.transform;
            float l = layout.aircraftLengthM;
            float centerY = layout.fuselageCenterlineHeightM;
            float radius = layout.fuselageDiameterM * 0.5f;
            float wingCenterY = WingCenterYRatio * l, wingCenterZ = WingCenterZRatio * l;
            float strutDiameter = StrutDiameterRatio * l, wheelWidth = WheelWidthRatio * l;

            // 机身：胶囊体沿 Z，总长 = 机长。
            GeneratorUtility.CreatePrimitive(PrimitiveType.Capsule, "Fuselage", t, new Vector3(0f, centerY, 0f),
                Quaternion.Euler(90f, 0f, 0f),
                new Vector3(layout.fuselageDiameterM, l * 0.5f, layout.fuselageDiameterM), skin, true);

            GeneratorUtility.CreateBox("Wing", t, new Vector3(0f, wingCenterY, wingCenterZ),
                new Vector3(layout.aircraftWingspanM, WingThicknessRatio * l, WingChordRatio * l), skin, true);
            GeneratorUtility.CreateBox("HorizontalStabilizer", t, new Vector3(0f, StabCenterYRatio * l, StabCenterZRatio * l),
                new Vector3(StabSpanRatio * l, StabThicknessRatio * l, StabChordRatio * l), skin, true);

            float finBottom = centerY + radius * 0.6f;
            float finHeight = layout.aircraftHeightM - finBottom;
            GeneratorUtility.CreateBox("VerticalFin", t, new Vector3(0f, finBottom + finHeight * 0.5f, FinCenterZRatio * l),
                new Vector3(FinThicknessRatio * l, finHeight, FinChordRatio * l), accent, true);

            float engineDiameter = EngineDiameterRatio * l, engineLength = EngineLengthRatio * l;
            float engineX = EngineOffsetXRatio * l, engineY = EngineCenterYRatio * l, engineZ = EngineCenterZRatio * l;
            Vector3[] mainWheels = MainWheelPositions(layout);
            foreach (float side in new[] { -1f, 1f })
            {
                string suffix = side < 0f ? "L" : "R";
                GeneratorUtility.CreateCylinder($"Engine_{suffix}", t,
                    new Vector3(side * engineX, engineY, engineZ), Vector3.forward,
                    engineDiameter, engineLength, metal, true);
                GeneratorUtility.CreateCylinder($"EngineInlet_{suffix}", t,
                    new Vector3(side * engineX, engineY, engineZ + engineLength * 0.5f), Vector3.forward,
                    engineDiameter * 0.85f, 0.01f, dark);
                BuildGear($"MainGear_{suffix}", t, mainWheels[side < 0f ? 0 : 1],
                    MainWheelDiameterM(layout), WingBottomHeightM(layout), strutDiameter, wheelWidth, dark, rubber);
            }
            BuildGear("NoseGear", t, new Vector3(0f, 0f, NoseGearZRatio * l), NoseWheelDiameterRatio * l, centerY - radius,
                strutDiameter, wheelWidth, dark, rubber);

            GeneratorUtility.SetLayerRecursive(root, SimLayers.AircraftIndex);
            GeneratorUtility.SavePrefab(root, PrefabPath);
            SimLog.Info(LogCategory.System, $"占位模型机已生成：{PrefabPath}");
        }

        private static void BuildGear(string name, Transform parent, Vector3 groundPosition, float wheelDiameterM,
            float strutTopM, float strutDiameterM, float wheelWidthM, Material strut, Material rubber)
        {
            Transform gear = GeneratorUtility.CreateNode(name, parent, groundPosition);
            float wheelCenterY = wheelDiameterM * 0.5f;
            GeneratorUtility.CreateCylinder("Strut", gear, new Vector3(0f, (wheelCenterY + strutTopM) * 0.5f, 0f), Vector3.up,
                strutDiameterM, strutTopM - wheelCenterY, strut);
            GeneratorUtility.CreateCylinder("Wheel", gear, new Vector3(0f, wheelCenterY, 0f), Vector3.right,
                wheelDiameterM, wheelWidthM, rubber);
        }
    }
}
