using NUnit.Framework;
using RivetSim.Core.Config;
using RivetSim.Simulation.Robot;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RivetSim.Tests.EditMode
{
    /// <summary>
    /// 运输姿态 Stow 外廓测试（M4，ADR-016）：PF_AGV 在升降柱最低位、机械臂 Stow 时，机械臂网格顶点的水平投影在车体投影内、
    /// 整机最高点不超过移动时高度上限，非相邻连杆自干涉距离 ≥ 10 mm；并验证 ArmController 判定为运输姿态。
    /// </summary>
    public sealed class StowEnvelopeTests
    {
        private const string AgvPrefabPath = "Assets/_Project/Prefabs/Robot/PF_AGV.prefab";
        private const string AgvConfigPath = "Assets/_Project/Config/CFG_Agv.asset";
        private const float SelfClearanceMinM = 0.01f;

        [Test]
        public void Stow_InsideBodyFootprint_BelowTransportHeight()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AgvPrefabPath);
            var agv = AssetDatabase.LoadAssetAtPath<AgvConfig>(AgvConfigPath);
            Assert.That(prefab, Is.Not.Null);
            Assert.That(agv, Is.Not.Null);
            var preview = EditorSceneManager.NewPreviewScene();
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, preview);
            try
            {
                instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                instance.GetComponent<LiftColumnController>().SetExtensionImmediate(0f);
                ArmController arm = instance.GetComponentInChildren<ArmController>();
                arm.SetJointsImmediate(arm.NamedPose(ArmConfig.StowPoseName));
                AgvEnvelope e = AgvFootprint.Measure(instance.transform, arm.transform, true);
                TestContext.WriteLine(e.ToString());
                Assert.That(e.ArmInsideBody(agv.bodySizeM.y, agv.bodySizeM.x), Is.True, e.ToString());
                Assert.That(e.TopM, Is.LessThanOrEqualTo(agv.transportHeightMaxM));
                Assert.That(ArmClearance.MinSelfDistanceM(arm.Rig, out string pair), Is.GreaterThanOrEqualTo(SelfClearanceMinM), pair);
                Assert.That(arm.IsInTransportPose, Is.True);
                arm.SetJointsImmediate(arm.NamedPose(ArmConfig.ObservePoseName));
                Assert.That(arm.IsInTransportPose, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(instance);
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }
    }
}
