using RivetSim.Core.Config;
using RivetSim.Core.Logging;
using RivetSim.Editor.Tools;
using RivetSim.Simulation.Robot;
using UnityEditor;
using UnityEngine;

namespace RivetSim.Editor.Generators
{
    /// <summary>
    /// AGV 生成器：参数化底盘（车体、驱动轮、万向轮、雷达、电池仓、急停、灯带、升降柱、车载设备盒 S5 / S6 / S7）
    /// 与简易六轴占位臂（M2 第三部分由 UR5e 替换）。升降柱为固定段 + 多节嵌套伸缩段 + 顶部法兰，机械臂挂在最上一节的法兰上；
    /// 根节点挂 AgvController（运动学刚体）与 LiftColumnController。尺寸、轮距、雷达与升降柱参数取自 CFG_Agv。
    /// 根节点在底盘地面投影中心，前进方向为本地 +Z。
    /// </summary>
    public static class AgvGenerator
    {
        /// <summary>AGV 预制体路径。</summary>
        public const string AgvPrefabPath = ProjectPaths.PrefabsRobot + "/PF_AGV.prefab";

        /// <summary>占位臂预制体路径。</summary>
        public const string ArmPrefabPath = ProjectPaths.PrefabsRobot + "/PF_Arm_Placeholder.prefab";

        /// <summary>机械臂安装节点名称。</summary>
        public const string ArmMountName = "ArmMount";

        /// <summary>法兰节点名称。</summary>
        public const string FlangeName = "Flange";

        /// <summary>升降柱根节点名称。</summary>
        public const string LiftName = "LiftColumn";

        // 底盘外观尺寸，单位 m。
        private const float GroundClearanceM = 0.06f;
        private const float LidarSlotHalfHeightM = 0.04f;
        private const float LidarSlotDepthM = 0.10f;
        private const float LidarDiameterM = 0.10f;
        private const float LidarHeightM = 0.07f;
        private const float WheelWidthM = 0.05f;
        private const float CasterDiameterM = 0.08f;
        private const float DeckThicknessM = 0.006f;
        private const float DeviceMarginM = 0.04f;
        private const float DeviceGapM = 0.03f;

        // 升降柱外观：伸缩段节数、每节截面内缩、节间顶面错位（避免重面闪烁）、法兰与段间搭接，单位 m。
        private const int LiftStageCount = 4;
        private const float LiftStageInsetM = 0.03f;
        private const float LiftStageTopStepM = 0.004f;
        private const float LiftStageOverlapM = 0.05f;
        private const float LiftFlangeThicknessM = 0.02f;
        private const float LiftFlangeDiameterM = 0.18f;

        // 占位臂连杆长度，单位 m（取 UR5e 公开 DH 参数的近似值，仅作外观；M2 以 yaml 为准）。
        private const float ShoulderHeightM = 0.1625f;
        private const float UpperArmM = 0.425f;
        private const float ForearmM = 0.3922f;
        private const float Wrist1OffsetM = 0.1333f;
        private const float Wrist2LengthM = 0.0997f;
        private const float Wrist3LengthM = 0.0996f;

        // 占位臂收拢姿态（关节角，单位 °），工具朝下、整体位于车体上方。
        private static readonly float[] StowPoseDeg = { 0f, -20f, 140f, -30f, 0f, 0f };

        /// <summary>依次生成占位臂与 AGV。</summary>
        [MenuItem("RivetSim/Generate/AGV", priority = 16)]
        public static void Build()
        {
            BuildArmPlaceholder();
            BuildAgv();
        }

        /// <summary>生成简易六轴占位臂（含探头末端嵌套预制体）。</summary>
        [MenuItem("RivetSim/Generate/Arm Placeholder", priority = 15)]
        public static void BuildArmPlaceholder()
        {
            GameObject endEffector = GeneratorUtility.LoadPrefab(EndEffectorGenerator.PrefabPath);
            if (endEffector == null) return;

            Material body = MaterialLibraryBuilder.Get(MaterialLibraryBuilder.RobotBody);
            Material dark = MaterialLibraryBuilder.Get(MaterialLibraryBuilder.RobotDark);
            Material accent = MaterialLibraryBuilder.Get(MaterialLibraryBuilder.RobotAccent);

            var root = new GameObject("PF_Arm_Placeholder");
            Transform t = root.transform;
            GeneratorUtility.CreateCylinder("BaseHousing", t, new Vector3(0f, 0.045f, 0f), Vector3.up, 0.13f, 0.09f, body);
            GeneratorUtility.CreateCylinder("BaseRing", t, new Vector3(0f, 0.01f, 0f), Vector3.up, 0.135f, 0.02f, accent);

            Transform j1 = Joint("J1_ShoulderPan", t, Vector3.zero, Vector3.up, StowPoseDeg[0]);
            GeneratorUtility.CreateCylinder("Housing", j1, new Vector3(0f, ShoulderHeightM, 0f), Vector3.right, 0.12f, 0.14f, body);
            Transform j2 = Joint("J2_ShoulderLift", j1, new Vector3(0f, ShoulderHeightM, 0f), Vector3.right, StowPoseDeg[1]);
            GeneratorUtility.CreateCylinder("UpperArm", j2, new Vector3(0f, UpperArmM * 0.5f, 0f), Vector3.up, 0.085f, UpperArmM, body);
            Transform j3 = Joint("J3_Elbow", j2, new Vector3(0f, UpperArmM, 0f), Vector3.right, StowPoseDeg[2]);
            GeneratorUtility.CreateCylinder("Housing", j3, Vector3.zero, Vector3.right, 0.10f, 0.12f, dark);
            GeneratorUtility.CreateCylinder("Forearm", j3, new Vector3(0f, ForearmM * 0.5f, 0f), Vector3.up, 0.07f, ForearmM, body);
            Transform j4 = Joint("J4_Wrist1", j3, new Vector3(0f, ForearmM, 0f), Vector3.right, StowPoseDeg[3]);
            GeneratorUtility.CreateCylinder("Housing", j4, new Vector3(Wrist1OffsetM * 0.5f, 0f, 0f), Vector3.right, 0.08f, Wrist1OffsetM, dark);
            Transform j5 = Joint("J5_Wrist2", j4, new Vector3(Wrist1OffsetM, Wrist2LengthM, 0f), Vector3.up, StowPoseDeg[4]);
            GeneratorUtility.CreateCylinder("Housing", j5, new Vector3(0f, -Wrist2LengthM * 0.5f, 0f), Vector3.up, 0.075f, Wrist2LengthM, body);
            Transform j6 = Joint("J6_Wrist3", j5, new Vector3(0f, 0f, Wrist3LengthM), Vector3.forward, StowPoseDeg[5]);
            GeneratorUtility.CreateCylinder("Housing", j6, new Vector3(0f, 0f, -Wrist3LengthM * 0.5f), Vector3.forward, 0.07f, Wrist3LengthM, dark);

            Transform flange = GeneratorUtility.CreateNode(FlangeName, j6, Vector3.zero);
            GeneratorUtility.InstantiatePrefab(endEffector, flange, Vector3.zero, Quaternion.identity, "EndEffector");

            GeneratorUtility.SetLayerRecursive(root, SimLayers.RobotIndex);
            GeneratorUtility.SavePrefab(root, ArmPrefabPath);
            SimLog.Info(LogCategory.System, $"占位臂已生成：{ArmPrefabPath}");
        }

        /// <summary>生成 AGV 预制体（嵌套占位臂）。</summary>
        public static void BuildAgv()
        {
            var agv = GeneratorUtility.LoadConfig<AgvConfig>(ProjectPaths.AgvAsset);
            // 有 UR5e 时使用它（M2 第三部分），否则退回占位臂。
            GameObject arm = AssetDatabase.LoadAssetAtPath<GameObject>(UR5eArmGenerator.PrefabPath);
            if (arm == null) arm = GeneratorUtility.LoadPrefab(ArmPrefabPath);
            if (agv == null || arm == null) return;

            Material body = MaterialLibraryBuilder.Get(MaterialLibraryBuilder.RobotBody);
            Material accent = MaterialLibraryBuilder.Get(MaterialLibraryBuilder.RobotAccent);
            Material dark = MaterialLibraryBuilder.Get(MaterialLibraryBuilder.RobotDark);
            Material rubber = MaterialLibraryBuilder.Get(MaterialLibraryBuilder.Rubber);
            Material strip = MaterialLibraryBuilder.Get(MaterialLibraryBuilder.LightStrip);
            Material lidarWindow = MaterialLibraryBuilder.Get(MaterialLibraryBuilder.LidarWindow);

            var root = new GameObject("PF_AGV");
            Transform t = root.transform;
            float length = agv.bodySizeM.x;
            float width = agv.bodySizeM.y;
            float height = agv.bodySizeM.z;
            GeneratorUtility.AddBoxCollider(root, new Vector3(0f, (height + GroundClearanceM) * 0.5f, 0f),
                new Vector3(width, height - GroundClearanceM, length));

            // 车体分三层，中层前端留出雷达槽。
            Transform chassis = GeneratorUtility.CreateNode("Chassis", t, Vector3.zero);
            float slotBottom = agv.lidarMount.positionM.y - LidarSlotHalfHeightM;
            float slotTop = agv.lidarMount.positionM.y + LidarSlotHalfHeightM;
            GeneratorUtility.CreateBox("Lower", chassis, new Vector3(0f, (GroundClearanceM + slotBottom) * 0.5f, 0f),
                new Vector3(width, slotBottom - GroundClearanceM, length), body);
            GeneratorUtility.CreateBox("Middle", chassis, new Vector3(0f, (slotBottom + slotTop) * 0.5f, -LidarSlotDepthM * 0.5f),
                new Vector3(width, slotTop - slotBottom, length - LidarSlotDepthM), body);
            GeneratorUtility.CreateBox("Upper", chassis, new Vector3(0f, (slotTop + height) * 0.5f, 0f),
                new Vector3(width, height - slotTop, length), body);
            GeneratorUtility.CreateBox("Deck", chassis, new Vector3(0f, height + DeckThicknessM * 0.5f, 0f),
                new Vector3(width - 0.02f, DeckThicknessM, length - 0.02f), MaterialLibraryBuilder.EnsureLitMaterial(PolyHavenMaterialBuilder.AgvDeck));
            foreach (float side in new[] { -1f, 1f })
            {
                GeneratorUtility.CreateBox(side < 0f ? "AccentStripe_L" : "AccentStripe_R", chassis,
                    new Vector3(side * (width * 0.5f + 0.002f), slotTop + 0.03f, 0f), new Vector3(0.004f, 0.03f, length * 0.9f), accent);
                GeneratorUtility.CreateBox(side < 0f ? "Bumper_F" : "Bumper_B", chassis,
                    new Vector3(0f, GroundClearanceM + 0.06f, side * -(length * 0.5f + 0.012f)), new Vector3(width * 0.94f, 0.08f, 0.024f), rubber);
                GeneratorUtility.CreateBox(side < 0f ? "LightStrip_F" : "LightStrip_B", chassis,
                    new Vector3(0f, height - 0.02f, side * -(length * 0.5f + 0.002f)), new Vector3(width * 0.8f, 0.015f, 0.004f), strip);
            }
            GeneratorUtility.CreateBox("BatteryDoor", chassis, new Vector3(-(width * 0.5f + 0.002f), (GroundClearanceM + slotBottom) * 0.5f, -0.1f),
                new Vector3(0.004f, (slotBottom - GroundClearanceM) * 0.7f, length * 0.4f), dark);
            Transform estop = GeneratorUtility.CreateNode("EStop", chassis, new Vector3(width * 0.3f, slotTop + 0.04f, -(length * 0.5f)));
            GeneratorUtility.CreateCylinder("Base", estop, new Vector3(0f, 0f, -0.01f), Vector3.back, 0.07f, 0.02f,
                MaterialLibraryBuilder.Get(MaterialLibraryBuilder.SafetyYellow));
            GeneratorUtility.CreateCylinder("Button", estop, new Vector3(0f, 0f, -0.035f), Vector3.back, 0.05f, 0.03f,
                MaterialLibraryBuilder.Get(MaterialLibraryBuilder.EStopRed));

            // 驱动轮与万向轮。
            float wheelRadius = agv.driveWheelDiameterM * 0.5f;
            foreach (float side in new[] { -1f, 1f })
            {
                GeneratorUtility.CreateCylinder(side < 0f ? "DriveWheel_L" : "DriveWheel_R", t,
                    new Vector3(side * agv.wheelTrackM * 0.5f, wheelRadius, 0f), Vector3.right, agv.driveWheelDiameterM, WheelWidthM, rubber);
            }
            for (int i = 0; i < agv.casterCount; i++)
            {
                float z = (i % 2 == 0 ? 1f : -1f) * (length * 0.5f - 0.12f);
                GeneratorUtility.CreateSphere(i % 2 == 0 ? "Caster_F" : "Caster_B", t, new Vector3(0f, CasterDiameterM * 0.5f, z), CasterDiameterM, rubber);
            }

            // 激光雷达。
            Transform lidar = GeneratorUtility.CreateNode("Lidar", t, agv.lidarMount.positionM, Quaternion.Euler(agv.lidarMount.eulerDeg));
            GeneratorUtility.CreateCylinder("Housing", lidar, Vector3.zero, Vector3.up, LidarDiameterM, LidarHeightM, dark);
            GeneratorUtility.CreateCylinder("Window", lidar, Vector3.zero, Vector3.up, LidarDiameterM + 0.004f, LidarHeightM * 0.4f, lidarWindow);

            // 升降柱与占位臂（挂在最上一节的法兰上）。
            Transform[] stages = BuildLiftColumn(t, agv, body, dark, accent, out Transform armMount);
            GeneratorUtility.InstantiatePrefab(arm, armMount, Vector3.zero, Quaternion.identity, "Arm");

            BuildDevices(t, agv, length, width, height + DeckThicknessM, dark);

            // 运行时组件：运动学刚体底盘与升降柱控制器。
            var rigidbody = root.AddComponent<Rigidbody>();
            rigidbody.isKinematic = true;
            rigidbody.useGravity = false;
            root.AddComponent<AgvController>().Configure(agv);
            root.AddComponent<LiftColumnController>().Configure(agv, stages);

            GeneratorUtility.SetLayerRecursive(root, SimLayers.RobotIndex);
            GeneratorUtility.SavePrefab(root, AgvPrefabPath);
            SimLog.Info(LogCategory.System, $"AGV 已生成：{AgvPrefabPath}（{length:0.00} × {width:0.00} × {height:0.00} m）");
        }

        // 车载设备盒沿车尾一排摆放：S5 超声仪、S7 工控机、S6 安全控制盒；每个盒子顶部有指示灯节点。
        private static void BuildDevices(Transform parent, AgvConfig agv, float length, float width, float deckTop, Material dark)
        {
            Material body = MaterialLibraryBuilder.Get(MaterialLibraryBuilder.RobotBody);
            Material accent = MaterialLibraryBuilder.Get(MaterialLibraryBuilder.RobotAccent);
            Material indicator = MaterialLibraryBuilder.Get(MaterialLibraryBuilder.IndicatorOff);
            (string Name, Vector3 SizeLwh, Material Material)[] devices =
            {
                ("Device_S5_UT", agv.utInstrumentBoxSizeM, body),
                ("Device_S7_IPC", agv.ipcBoxSizeM, dark),
                ("Device_S6_Safety", agv.safetyBoxSizeM, accent),
            };

            float x = -width * 0.5f + DeviceMarginM;
            foreach ((string name, Vector3 sizeLwh, Material material) in devices)
            {
                Vector3 size = SizeAxes.LengthAlongZ(sizeLwh);
                float z = -length * 0.5f + DeviceMarginM + size.z * 0.5f;
                Transform device = GeneratorUtility.CreateNode(name, parent, new Vector3(x + size.x * 0.5f, deckTop, z));
                GeneratorUtility.CreateBox("Box", device, new Vector3(0f, size.y * 0.5f, 0f), size, material);
                GeneratorUtility.CreateBox("Indicator", device, new Vector3(0f, size.y + 0.006f, size.z * 0.3f),
                    new Vector3(size.x * 0.4f, 0.012f, 0.03f), indicator);
                x += size.x + DeviceGapM;
            }

            Transform ipc = parent.Find("Device_S7_IPC");
            if (ipc != null)
            {
                GeneratorUtility.CreateCylinder("Antenna", ipc, new Vector3(0.06f, agv.ipcBoxSizeM.z + 0.075f, -0.06f), Vector3.up, 0.012f, 0.15f, dark);
            }
            if (x - DeviceGapM > width * 0.5f)
                SimLog.Warning(LogCategory.System, "车载设备盒总宽超出车体宽度，请检查 CFG_Agv。");
        }

        // 固定段 + 多节伸缩段：伸缩段收在固定段内（底部伸入平台下方 LiftStageOverlapM），第 i 节随伸出量上升 (i + 1) / 节数。
        // 最上一节顶面为机械臂安装面，最低位时离地 = 平台面 + 固定段高度。
        private static Transform[] BuildLiftColumn(Transform parent, AgvConfig agv, Material body, Material dark, Material accent,
            out Transform armMount)
        {
            Transform lift = GeneratorUtility.CreateNode(LiftName, parent, agv.liftMount.positionM, Quaternion.Euler(agv.liftMount.eulerDeg));
            float topM = agv.liftFixedHeightM - LiftFlangeThicknessM;
            Vector2 section = agv.liftSectionSizeM;

            float fixedTop = topM - LiftStageTopStepM * LiftStageCount;
            GeneratorUtility.CreateBox("FixedSection", lift, new Vector3(0f, fixedTop * 0.5f, 0f), new Vector3(section.x, fixedTop, section.y), body);
            GeneratorUtility.CreateBox("FixedCollar", lift, new Vector3(0f, fixedTop - 0.015f, 0f),
                new Vector3(section.x + 0.01f, 0.03f, section.y + 0.01f), accent);

            var stages = new Transform[LiftStageCount];
            for (int i = 0; i < LiftStageCount; i++)
            {
                Transform stage = GeneratorUtility.CreateNode($"Stage_{i + 1}", lift, Vector3.zero);
                float stageTop = topM - LiftStageTopStepM * (LiftStageCount - 1 - i);
                float stageHeight = stageTop + LiftStageOverlapM;
                Vector2 size = section - Vector2.one * (LiftStageInsetM * (i + 1));
                GeneratorUtility.CreateBox("Tube", stage, new Vector3(0f, stageTop - stageHeight * 0.5f, 0f),
                    new Vector3(size.x, stageHeight, size.y), i % 2 == 0 ? dark : body);
                stages[i] = stage;
            }

            Transform top = stages[LiftStageCount - 1];
            Transform flange = GeneratorUtility.CreateNode("TopFlange", top, new Vector3(0f, topM, 0f));
            GeneratorUtility.CreateCylinder("Plate", flange, new Vector3(0f, LiftFlangeThicknessM * 0.5f, 0f), Vector3.up,
                LiftFlangeDiameterM, LiftFlangeThicknessM, dark);
            armMount = GeneratorUtility.CreateNode(ArmMountName, top, new Vector3(0f, agv.liftFixedHeightM, 0f));
            return stages;
        }

        private static Transform Joint(string name, Transform parent, Vector3 localPosition, Vector3 axis, float angleDeg)
        {
            return GeneratorUtility.CreateNode(name, parent, localPosition, Quaternion.AngleAxis(angleDeg, axis));
        }
    }
}
