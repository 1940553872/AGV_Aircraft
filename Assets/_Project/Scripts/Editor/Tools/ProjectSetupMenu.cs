using System;
using RivetSim.Core.Config;
using RivetSim.Core.Logging;
using UnityEditor;
using UnityEngine;

namespace RivetSim.Editor.Tools
{
    /// <summary>
    /// M0 工程设置菜单：建目录、设置 Layers 与 Fixed Timestep、生成默认配置资产。全部可重复执行。
    /// </summary>
    public static class ProjectSetupMenu
    {
        // 物理与运动学固定步长（M0 约定），单位 s。
        private const float FixedTimestepS = 0.01f;

        private static readonly (int Index, string Name)[] Layers =
        {
            (SimLayers.EnvironmentIndex, SimLayers.Environment),
            (SimLayers.AircraftIndex, SimLayers.Aircraft),
            (SimLayers.ObstacleIndex, SimLayers.Obstacle),
            (SimLayers.RobotIndex, SimLayers.Robot),
            (SimLayers.VisualizationIndex, SimLayers.Visualization),
        };

        /// <summary>依次执行全部 M0 设置。</summary>
        [MenuItem("RivetSim/Setup/Run All M0 Setup", priority = 0)]
        public static void RunAll()
        {
            CreateProjectFolders();
            ApplyLayersAndTime();
            CreateDefaultConfigs();
        }

        /// <summary>按 Architecture §2 创建 _Project 子目录，已存在的跳过。</summary>
        [MenuItem("RivetSim/Setup/Create Project Folders", priority = 20)]
        public static void CreateProjectFolders()
        {
            int created = 0;
            foreach (string relative in ProjectPaths.ProjectFolders)
            {
                created += EnsureFolder($"{ProjectPaths.ProjectRoot}/{relative}");
            }
            AssetDatabase.SaveAssets();
            SimLog.Info(LogCategory.System, $"目录检查完成，新建 {created} 个。");
        }

        /// <summary>写入自定义 Layers 并设置 Fixed Timestep。</summary>
        [MenuItem("RivetSim/Setup/Apply Layers And Time Settings", priority = 21)]
        public static void ApplyLayersAndTime()
        {
            UnityEngine.Object tagManager = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0];
            var serialized = new SerializedObject(tagManager);
            SerializedProperty layers = serialized.FindProperty("layers");

            foreach ((int index, string name) in Layers)
            {
                SerializedProperty slot = layers.GetArrayElementAtIndex(index);
                if (slot.stringValue == name) continue;
                if (!string.IsNullOrEmpty(slot.stringValue))
                {
                    SimLog.Error(LogCategory.System, $"Layer {index} 已被“{slot.stringValue}”占用，未写入 {name}。");
                    continue;
                }
                slot.stringValue = name;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();

            // 直接改 Time.fixedDeltaTime 不会标记 TimeManager 为已修改，需通过 SerializedObject 写入才能保存到磁盘。
            // Unity 6 中 Fixed Timestep 序列化为 RationalTime：时长 = m_Count / (m_Numerator / m_Denominator)。
            UnityEngine.Object timeManager = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TimeManager.asset")[0];
            var timeSerialized = new SerializedObject(timeManager);
            SerializedProperty fixedStep = timeSerialized.FindProperty("Fixed Timestep");
            double ticksPerSecond = (double)fixedStep.FindPropertyRelative("m_Rate.m_Numerator").uintValue
                                    / fixedStep.FindPropertyRelative("m_Rate.m_Denominator").uintValue;
            fixedStep.FindPropertyRelative("m_Count").longValue = (long)Math.Round(FixedTimestepS * ticksPerSecond);
            timeSerialized.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.SaveAssets();
            SimLog.Info(LogCategory.System, $"Layers 已设置；Fixed Timestep = {Time.fixedDeltaTime:0.####} s。");
        }

        /// <summary>在 Config/ 下为每种配置创建默认资产 CFG_*，已存在的不覆盖。</summary>
        [MenuItem("RivetSim/Setup/Create Default Configs", priority = 22)]
        public static void CreateDefaultConfigs()
        {
            EnsureFolder(ProjectPaths.ConfigFolder);

            var simSettings = EnsureConfig<SimSettings>("CFG_SimSettings");
            EnsureConfig<SceneLayoutConfig>("CFG_SceneLayout");
            var agv = EnsureConfig<AgvConfig>("CFG_Agv");
            var arm = EnsureConfig<ArmConfig>("CFG_Arm");
            var inspection = EnsureConfig<InspectionConfig>("CFG_Inspection");
            EnsureConfig<UiThemeConfig>("CFG_UiTheme");
            EnsureConfig<NavigationConfig>("CFG_Navigation");
            EnsureConfig<RenderLookConfig>("CFG_RenderLook");

            if (simSettings.taskMatrixJson == null)
            {
                simSettings.taskMatrixJson = AssetDatabase.LoadAssetAtPath<TextAsset>(ProjectPaths.TaskMatrixJson);
                if (simSettings.taskMatrixJson == null)
                    SimLog.Error(LogCategory.System, $"找不到任务矩阵 JSON：{ProjectPaths.TaskMatrixJson}");
                EditorUtility.SetDirty(simSettings);
            }

            FillMissingM4Values(agv, arm, inspection);
            ApplyM7Defaults(simSettings);
            AssetDatabase.SaveAssets();
            SimLog.Info(LogCategory.System, "默认配置资产检查完成。");
        }

        // M4（ADR-016）：已有资产中“待 M4 确定”的值（空的命名姿态、为 0 的闸门阈值）按配置类默认值补上，并更新 notes；
        // 新增字段在资产加载时已取默认值，这里写回磁盘。人工调过的值不覆盖（ADR-007）。
        private static void FillMissingM4Values(AgvConfig agv, ArmConfig arm, InspectionConfig inspection)
        {
            var armDefaults = ScriptableObject.CreateInstance<ArmConfig>();
            var inspectionDefaults = ScriptableObject.CreateInstance<InspectionConfig>();
            try
            {
                var poses = new System.Collections.Generic.List<NamedJointPose>(arm.namedPoses ?? new NamedJointPose[0]);
                foreach (NamedJointPose pose in armDefaults.namedPoses)
                {
                    if (poses.Exists(p => p.poseName == pose.poseName)) continue;
                    poses.Add(pose);
                    SimLog.Info(LogCategory.System, $"CFG_Arm：补写 M4 命名姿态 {pose.poseName}");
                }
                arm.namedPoses = poses.ToArray();
                if (arm.notes.Contains("命名姿态在 M4 确定")) arm.notes = armDefaults.notes;
                if (inspection.gateAThresholdRatio <= 0f && inspection.gateBThresholdRatio <= 0f)
                {
                    inspection.gateAThresholdRatio = inspectionDefaults.gateAThresholdRatio;
                    inspection.gateBThresholdRatio = inspectionDefaults.gateBThresholdRatio;
                    SimLog.Info(LogCategory.System, "CFG_Inspection：写入 M4 闸门阈值");
                }
                if (inspection.notes.Contains("闸门阈值在 M4 确定")) inspection.notes = inspectionDefaults.notes;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(armDefaults);
                UnityEngine.Object.DestroyImmediate(inspectionDefaults);
            }
            EditorUtility.SetDirty(agv);
            EditorUtility.SetDirty(arm);
            EditorUtility.SetDirty(inspection);
        }

        // M7：默认倍速由 1 倍改为 2 倍（只改仍为 M6 默认值 1 倍的资产，人工调过的值不覆盖）。
        private const string M7SpeedNote = "M7：默认倍速 2 倍。";

        private static void ApplyM7Defaults(SimSettings settings)
        {
            if (settings.notes.Contains(M7SpeedNote)) return;
            if (Mathf.Approximately(settings.defaultSpeedMultiplier, 1f)) settings.defaultSpeedMultiplier = 2f;
            settings.notes = settings.notes.TrimEnd() + "\n" + M7SpeedNote;
            EditorUtility.SetDirty(settings);
            SimLog.Info(LogCategory.System, $"CFG_SimSettings：默认倍速 {settings.defaultSpeedMultiplier:0.#} 倍");
        }

        private static T EnsureConfig<T>(string assetName) where T : ScriptableObject
        {
            string path = $"{ProjectPaths.ConfigFolder}/{assetName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;

            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            SimLog.Info(LogCategory.System, $"已创建 {path}");
            return asset;
        }

        // 逐级创建目录，返回新建的层数。
        private static int EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return 0;

            int split = path.LastIndexOf("/", StringComparison.Ordinal);
            string parent = path.Substring(0, split);
            int created = EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(split + 1));
            return created + 1;
        }
    }
}
