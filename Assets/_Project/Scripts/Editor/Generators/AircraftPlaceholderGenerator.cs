using RivetSim.Core.Config;
using RivetSim.Core.Logging;
using RivetSim.Editor.Tools;
using UnityEditor;
using UnityEngine;

namespace RivetSim.Editor.Generators
{
    /// <summary>
    /// 模型机占位体生成器（M2 由 A320 模型替换）：胶囊体机身 + 长方体机翼 / 尾翼 + 圆柱发动机与起落架。
    /// 根节点位于包围盒中心的地面投影，机头朝 +Z；机长、翼展、机高、机身直径、机身中线高度取自 CFG_SceneLayout，
    /// 其余为占位外观尺寸（1:5 A320 的近似值）。
    /// </summary>
    public static class AircraftPlaceholderGenerator
    {
        /// <summary>预制体路径。</summary>
        public const string PrefabPath = ProjectPaths.PrefabsAircraft + "/PF_Aircraft_Placeholder.prefab";

        // 占位外观尺寸，单位 m（相对根节点）。
        private const float WingChordM = 1.2f;
        private const float WingThicknessM = 0.09f;
        private const float WingCenterYM = 0.55f;
        private const float WingCenterZM = 0.4f;
        private const float StabSpanM = 2.5f;
        private const float StabChordM = 0.55f;
        private const float StabThicknessM = 0.05f;
        private const float StabCenterYM = 0.95f;
        private const float StabCenterZM = -3.05f;
        private const float FinChordM = 0.9f;
        private const float FinThicknessM = 0.06f;
        private const float FinCenterZM = -3.1f;
        private const float EngineDiameterM = 0.42f;
        private const float EngineLengthM = 0.85f;
        private const float EngineOffsetXM = 1.15f;
        private const float EngineCenterYM = 0.40f;
        private const float EngineCenterZM = 1.0f;
        private const float NoseGearZM = 2.9f;
        private const float NoseWheelDiameterM = 0.14f;
        private const float MainGearOffsetXM = 0.45f;
        private const float MainGearZM = -0.25f;
        private const float MainWheelDiameterM = 0.22f;
        private const float WheelWidthM = 0.07f;
        private const float StrutDiameterM = 0.05f;

        /// <summary>机翼下表面离地高度，单位 m（支撑架高度）。</summary>
        public const float WingBottomHeightM = WingCenterYM - WingThicknessM * 0.5f;

        /// <summary>机翼支撑架位置（相对根节点），单位 m。</summary>
        public static readonly Vector3[] StandPositions = { new Vector3(-2.6f, 0f, WingCenterZM), new Vector3(2.6f, 0f, WingCenterZM) };

        /// <summary>主起落架机轮位置（相对根节点的地面投影），单位 m。</summary>
        public static readonly Vector3[] MainWheelPositions = { new Vector3(-MainGearOffsetXM, 0f, MainGearZM), new Vector3(MainGearOffsetXM, 0f, MainGearZM) };

        /// <summary>主机轮直径，单位 m（轮挡摆放用）。</summary>
        public const float MainWheelDiameter = MainWheelDiameterM;

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
            float centerY = layout.fuselageCenterlineHeightM;
            float radius = layout.fuselageDiameterM * 0.5f;

            // 机身：胶囊体沿 Z，总长 = 机长。
            GeneratorUtility.CreatePrimitive(PrimitiveType.Capsule, "Fuselage", t, new Vector3(0f, centerY, 0f),
                Quaternion.Euler(90f, 0f, 0f),
                new Vector3(layout.fuselageDiameterM, layout.aircraftLengthM * 0.5f, layout.fuselageDiameterM), skin, true);

            GeneratorUtility.CreateBox("Wing", t, new Vector3(0f, WingCenterYM, WingCenterZM),
                new Vector3(layout.aircraftWingspanM, WingThicknessM, WingChordM), skin, true);
            GeneratorUtility.CreateBox("HorizontalStabilizer", t, new Vector3(0f, StabCenterYM, StabCenterZM),
                new Vector3(StabSpanM, StabThicknessM, StabChordM), skin, true);

            float finBottom = centerY + radius * 0.6f;
            float finHeight = layout.aircraftHeightM - finBottom;
            GeneratorUtility.CreateBox("VerticalFin", t, new Vector3(0f, finBottom + finHeight * 0.5f, FinCenterZM),
                new Vector3(FinThicknessM, finHeight, FinChordM), accent, true);

            foreach (float side in new[] { -1f, 1f })
            {
                string suffix = side < 0f ? "L" : "R";
                GeneratorUtility.CreateCylinder($"Engine_{suffix}", t,
                    new Vector3(side * EngineOffsetXM, EngineCenterYM, EngineCenterZM), Vector3.forward,
                    EngineDiameterM, EngineLengthM, metal, true);
                GeneratorUtility.CreateCylinder($"EngineInlet_{suffix}", t,
                    new Vector3(side * EngineOffsetXM, EngineCenterYM, EngineCenterZM + EngineLengthM * 0.5f), Vector3.forward,
                    EngineDiameterM * 0.85f, 0.01f, dark);
                BuildGear($"MainGear_{suffix}", t, new Vector3(side * MainGearOffsetXM, 0f, MainGearZM),
                    MainWheelDiameterM, WingBottomHeightM, dark, rubber);
            }
            BuildGear("NoseGear", t, new Vector3(0f, 0f, NoseGearZM), NoseWheelDiameterM, centerY - radius, dark, rubber);

            GeneratorUtility.SetLayerRecursive(root, SimLayers.AircraftIndex);
            GeneratorUtility.SavePrefab(root, PrefabPath);
            SimLog.Info(LogCategory.System, $"占位模型机已生成：{PrefabPath}");
        }

        private static void BuildGear(string name, Transform parent, Vector3 groundPosition, float wheelDiameterM,
            float strutTopM, Material strut, Material rubber)
        {
            Transform gear = GeneratorUtility.CreateNode(name, parent, groundPosition);
            float wheelCenterY = wheelDiameterM * 0.5f;
            GeneratorUtility.CreateCylinder("Strut", gear, new Vector3(0f, (wheelCenterY + strutTopM) * 0.5f, 0f), Vector3.up,
                StrutDiameterM, strutTopM - wheelCenterY, strut);
            GeneratorUtility.CreateCylinder("Wheel", gear, new Vector3(0f, wheelCenterY, 0f), Vector3.right,
                wheelDiameterM, WheelWidthM, rubber);
        }
    }
}
