using System.Collections.Generic;
using System.Text;
using RivetSim.Core.Config;
using RivetSim.Core.Logging;
using RivetSim.Editor.Tools;
using RivetSim.Simulation.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RivetSim.Editor.Generators
{
    /// <summary>
    /// 检测区生成器（M2 第三部分 3B）：在主场景 Zone_Work/InspectionRegions 下按 CFG_Inspection 生成左右两侧各 1 个区域、
    /// 每区若干工位、每工位一排铆钉。铆钉位置与法向由水平射线投到机身外壳碰撞体（Aircraft 层）得到；按随机种子（CFG_SimSettings）
    /// 设置真值缺陷；每个工位生成地面停靠标记板与蒙皮上的基准标记（AprilTag）。重复执行时覆盖上次结果。
    /// </summary>
    public static class RivetZoneGenerator
    {
        /// <summary>检测区根节点名称。</summary>
        public const string RootName = "InspectionRegions";

        // 外观与投射几何，单位 m。
        private const float RayStartOutboardM = 5f;
        private const float RayLengthM = 10f;
        private const float RivetThicknessM = 0.001f;
        private const float SurfaceOffsetM = 0.0005f;
        private const float DatumGapM = 0.03f;

        /// <summary>生成检测区（需先生成主场景）。</summary>
        [MenuItem("RivetSim/Generate/Rivet Zones", priority = 35)]
        public static void BuildInMainScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ProjectPaths.MainScene)
            {
                SimLog.Error(LogCategory.System, $"请先打开 {ProjectPaths.MainScene}。");
                return;
            }
            Build(scene);
            EditorSceneManager.SaveScene(scene);
        }

        /// <summary>在已打开的主场景中生成检测区（由 MainSceneBuilder 调用）。</summary>
        public static void Build(Scene scene)
        {
            var inspection = GeneratorUtility.LoadConfig<InspectionConfig>(ProjectPaths.InspectionAsset);
            var agv = GeneratorUtility.LoadConfig<AgvConfig>(ProjectPaths.AgvAsset);
            var sim = GeneratorUtility.LoadConfig<SimSettings>(ProjectPaths.SimSettingsAsset);
            Transform work = GeneratorUtility.FindRoot(scene, "Zone_Work");
            Transform aircraft = work != null ? work.Find("Aircraft") : null;
            if (inspection == null || agv == null || sim == null || aircraft == null)
            {
                SimLog.Error(LogCategory.System, "检测区生成失败：缺少 CFG 或主场景中的 Zone_Work/Aircraft。");
                return;
            }

            Transform old = work.Find(RootName);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            Transform root = GeneratorUtility.CreateNode(RootName, work, Vector3.zero);
            Physics.SyncTransforms();

            GameObject boardPrefab = GeneratorUtility.LoadPrefab(PropsGenerator.MarkerBoardPath);
            Material rivetMaterial = MaterialLibraryBuilder.Get(MaterialLibraryBuilder.MetalBright);
            Material white = MaterialLibraryBuilder.Get(MaterialLibraryBuilder.MarkerBoard);
            float[] stationOffsets = RivetLayout.StationOffsetsM(inspection.stationsPerRegion, inspection.stationSpacingM);
            float[] rivetOffsets = RivetLayout.RivetOffsetsM(inspection.rivetsPerStation, inspection.rivetPitchMm * 0.001f);
            int total = inspection.regionCount * inspection.stationsPerRegion * inspection.rivetsPerStation;
            DefectType[] defects = DefectAssigner.Assign(total, inspection.defectRatio, sim.randomSeed);

            var regions = new List<InspectionRegion>();
            int rivetCounter = 0, stationCounter = 0, missed = 0;
            var report = new StringBuilder();
            for (int r = 0; r < inspection.regionCount; r++)
            {
                RegionSide side = r % 2 == 0 ? RegionSide.Left : RegionSide.Right;
                float sideSign = side == RegionSide.Left ? -1f : 1f;
                Transform regionNode = GeneratorUtility.CreateNode(RivetLayout.RegionId(r), root, Vector3.zero);
                var stations = new List<InspectionStation>();
                for (int s = 0; s < inspection.stationsPerRegion; s++, stationCounter++)
                {
                    float stationZ = -inspection.regionCenterAftOfMainGearM + stationOffsets[s];
                    string stationId = RivetLayout.StationId(r, s);
                    // 停靠位姿：车头朝模型机机头方向，机械臂基座轴线到蒙皮的水平距离为 dockingStandoffM。工位节点放在停靠位姿上。
                    PlanarPose dock = DockingPose(aircraft, agv, inspection, sideSign, stationZ);
                    Transform stationNode = GeneratorUtility.CreateNode(stationId, regionNode, Vector3.zero);
                    stationNode.SetPositionAndRotation(dock.WorldPosition, dock.WorldRotation);

                    var rivets = new Rivet[rivetOffsets.Length];
                    for (int i = 0; i < rivetOffsets.Length; i++, rivetCounter++)
                    {
                        if (!Project(aircraft, sideSign, inspection.rivetRowHeightM, stationZ + rivetOffsets[i], out RaycastHit hit))
                        {
                            missed++;
                            continue;
                        }
                        string id = RivetLayout.RivetId(r, s, i);
                        Transform node = GeneratorUtility.CreateNode(id, stationNode, Vector3.zero);
                        node.SetPositionAndRotation(hit.point + hit.normal * SurfaceOffsetM, Quaternion.LookRotation(hit.normal, Vector3.up));
                        GeneratorUtility.CreateCylinder("Head", node, Vector3.zero, Vector3.forward,
                            inspection.rivetHeadDiameterMm * 0.001f, RivetThicknessM, rivetMaterial);
                        rivets[i] = node.gameObject.AddComponent<Rivet>();
                        rivets[i].Configure(id, r, s, i, hit.normal, defects[rivetCounter]);
                    }

                    // 基准标记：贴在铆钉排前端（机头方向）旁的蒙皮上。
                    RegionMarker datum = null;
                    float datumZ = stationZ + rivetOffsets[rivetOffsets.Length - 1] + DatumGapM + inspection.datumTagSizeM * 0.5f + PropsGenerator.TagBorderM;
                    if (Project(aircraft, sideSign, inspection.rivetRowHeightM, datumZ, out RaycastHit datumHit))
                    {
                        Transform node = GeneratorUtility.CreateNode("DatumMarker", stationNode, Vector3.zero);
                        node.SetPositionAndRotation(datumHit.point + datumHit.normal * SurfaceOffsetM, Quaternion.LookRotation(datumHit.normal, Vector3.up));
                        int datumId = inspection.datumTagIds[stationCounter];
                        GeneratorUtility.CreateScreenQuad("Backing", node, Vector3.zero, Vector2.one * (inspection.datumTagSizeM + PropsGenerator.TagBorderM * 2f), white);
                        GeneratorUtility.CreateScreenQuad("Tag", node, new Vector3(0f, 0f, PropsGenerator.TagLayerOffsetM),
                            Vector2.one * inspection.datumTagSizeM, AprilTagMaterialBuilder.Get(datumId));
                        datum = node.gameObject.AddComponent<RegionMarker>();
                        datum.Configure(datumId, inspection.datumTagSizeM, MarkerRole.Datum);
                    }

                    RegionMarker docking = BuildDockingMarker(stationNode, boardPrefab, inspection, dock, sideSign, aircraft,
                        inspection.dockingTagIds[stationCounter]);

                    InspectionStation station = stationNode.gameObject.AddComponent<InspectionStation>();
                    station.Configure(stationId, r, s, dock, System.Array.FindAll(rivets, x => x != null), docking, datum);
                    stations.Add(station);
                    report.Append($"{stationId}：停靠 ({dock.positionM.x:0.000}, {dock.positionM.y:0.000}) 偏航 {dock.yawDeg:0}°，" +
                                  $"铆钉 {station.Rivets.Count} 颗，停靠标记 ID {docking?.TagId}，基准标记 ID {datum?.TagId}\n");
                }
                InspectionRegion region = regionNode.gameObject.AddComponent<InspectionRegion>();
                region.Configure(RivetLayout.RegionId(r), r, side, stations.ToArray());
                regions.Add(region);
            }
            RivetRegistry registry = root.gameObject.AddComponent<RivetRegistry>();
            registry.Configure(regions.ToArray());
            GeneratorUtility.SetLayerRecursive(root.gameObject, SimLayers.EnvironmentIndex);

            foreach (Rivet rivet in registry.GetAllRivets())
            {
                if (rivet.Defect != DefectType.None) report.Append($"真值缺陷：{rivet.RivetId} {rivet.Defect}\n");
            }
            if (missed > 0) SimLog.Error(LogCategory.System, $"有 {missed} 颗铆钉未投到机身外壳，请检查 regionCenterAftOfMainGearM / rivetRowHeightM。");
            SimLog.Info(LogCategory.System, $"检测区已生成：{regions.Count} 个区域、{stationCounter} 个工位、{registry.GetAllRivets().Count} 颗铆钉（种子 {sim.randomSeed}）\n{report}");
        }

        // 从机身外侧沿模型机本地 X 水平射向机身，只检测 Aircraft 层（仅机身外壳带碰撞体）。
        // 射线恰好穿过网格接缝时会漏检（如 R2-S1-04 处），此时先在高度方向、再沿机身方向小幅偏移重试（≤ 2 mm，小于钉头半径）。
        private static bool Project(Transform aircraft, float sideSign, float heightM, float localZ, out RaycastHit hit)
        {
            Vector3 direction = aircraft.TransformDirection(new Vector3(-sideSign, 0f, 0f));
            foreach (Vector2 jitter in SeamJitterM)
            {
                Vector3 origin = aircraft.TransformPoint(new Vector3(sideSign * RayStartOutboardM, 0f, localZ + jitter.y));
                origin.y = heightM + jitter.x;
                if (Physics.Raycast(origin, direction, out hit, RayLengthM, 1 << SimLayers.AircraftIndex)) return true;
            }
            hit = default;
            return false;
        }

        private static readonly Vector2[] SeamJitterM =
        {
            Vector2.zero, new Vector2(0.0005f, 0f), new Vector2(-0.0005f, 0f), new Vector2(0.001f, 0f), new Vector2(-0.001f, 0f),
            new Vector2(0.002f, 0f), new Vector2(-0.002f, 0f), new Vector2(0f, 0.001f), new Vector2(0f, -0.001f),
            new Vector2(0f, 0.002f), new Vector2(0f, -0.002f),
        };

        /// <summary>按工位中心（模型机本地 Z）计算 AGV 停靠位姿（世界坐标）。</summary>
        public static PlanarPose DockingPose(Transform aircraft, AgvConfig agv, InspectionConfig inspection, float sideSign, float stationLocalZ)
        {
            Project(aircraft, sideSign, inspection.rivetRowHeightM, stationLocalZ, out RaycastHit hit);
            Vector3 heading = aircraft.forward;
            Vector3 outward = aircraft.right * sideSign;
            Vector3 armBase = hit.point + outward * inspection.dockingStandoffM;
            Vector3 center = armBase - heading * agv.liftMount.positionM.z;
            return new PlanarPose(new Vector2(center.x, center.z), aircraft.eulerAngles.y);
        }

        private static RegionMarker BuildDockingMarker(Transform stationNode, GameObject boardPrefab, InspectionConfig inspection,
            PlanarPose dock, float sideSign, Transform aircraft, int tagId)
        {
            if (boardPrefab == null) return null;
            Vector3 heading = aircraft.forward;
            Vector3 position = dock.WorldPosition + heading * inspection.dockingMarkerAheadM + aircraft.right * (sideSign * inspection.dockingMarkerLateralM);
            GameObject board = GeneratorUtility.InstantiatePrefab(boardPrefab, stationNode, Vector3.zero, Quaternion.identity, "DockingMarker");
            board.transform.SetPositionAndRotation(position, Quaternion.LookRotation(-heading, Vector3.up));
            Transform tag = board.transform.Find("Tag");
            if (tag != null)
            {
                Material material = AprilTagMaterialBuilder.Get(tagId);
                if (material != null) tag.GetComponent<Renderer>().sharedMaterial = material;
                tag.localScale = new Vector3(inspection.dockingTagSizeM, inspection.dockingTagSizeM, 1f);
            }
            RegionMarker marker = board.AddComponent<RegionMarker>();
            marker.Configure(tagId, inspection.dockingTagSizeM, MarkerRole.Docking);
            return marker;
        }
    }
}
