using System.Collections.Generic;
using System.IO;
using RivetSim.Core.Logging;
using RivetSim.Editor.Generators;
using RivetSim.Simulation.Robot;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RivetSim.Editor.Tools
{
    /// <summary>
    /// UR5e 关节测试截图（M2 第三部分 3A）：在 Sandbox_Robot 中临时放一台 PF_Arm_UR5e，依次拍零位与 6 个关节各 ±90°，
    /// 拼成 4 × 4 网格输出到 Docs/Screenshots/M2/10_UR5e_Joints.png，用于检查网格是否跟随正确的连杆。临时对象不保存进场景。
    /// </summary>
    public static class ArmJointTestCapture
    {
        private const string OutputPath = ProjectPaths.ScreenshotsFolder + "/M2/10_UR5e_Joints.png";
        private const int Columns = 4;
        private const int Rows = 4;
        private static readonly Vector2Int TilePx = new Vector2Int(480, 360);
        private const float TestAngleDeg = 90f;
        // 临时机械臂放在 AGV 旁的空地上，相机从侧前方看向肩部高度。
        private static readonly Vector3 ArmPositionM = new Vector3(-3f, 0f, 0f);
        private static readonly Vector3 CameraOffsetM = new Vector3(1.6f, 1.0f, 1.9f);
        private static readonly Vector3 LookOffsetM = new Vector3(0f, 0.45f, 0.25f);
        private const float CameraFovDeg = 45f;
        private const float LabelHeightM = 1.35f;
        private const float LabelCharacterSize = 0.012f;
        private const int LabelFontSize = 64;
        private static readonly Color EmptyTileColor = new Color(0.10f, 0.12f, 0.15f);

        /// <summary>拍摄关节测试拼图（需打开 Sandbox_Robot）。</summary>
        [MenuItem("RivetSim/Debug/Capture Arm Joint Test")]
        public static void Capture()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ProjectPaths.SandboxRobotScene)
            {
                SimLog.Error(LogCategory.System, $"请先打开 {ProjectPaths.SandboxRobotScene}。");
                return;
            }
            GameObject prefab = GeneratorUtility.LoadPrefab(UR5eArmGenerator.PrefabPath);
            if (prefab == null) return;

            bool wasDirty = scene.isDirty;
            var temp = new List<Object>();
            var sheet = new Texture2D(TilePx.x * Columns, TilePx.y * Rows, TextureFormat.RGB24, false);
            var fill = new Color[sheet.width * sheet.height];
            for (int i = 0; i < fill.Length; i++) fill[i] = EmptyTileColor;
            sheet.SetPixels(fill);
            try
            {
                var arm = (GameObject)Object.Instantiate(prefab);
                arm.hideFlags = HideFlags.DontSave;
                temp.Add(arm);
                arm.transform.SetPositionAndRotation(ArmPositionM, Quaternion.identity);
                ArmRig rig = arm.GetComponent<ArmRig>();

                var cameraGo = new GameObject("__JointTestCamera") { hideFlags = HideFlags.DontSave };
                temp.Add(cameraGo);
                Vector3 look = ArmPositionM + LookOffsetM;
                cameraGo.transform.position = ArmPositionM + CameraOffsetM;
                cameraGo.transform.rotation = Quaternion.LookRotation(look - cameraGo.transform.position);
                var camera = cameraGo.AddComponent<Camera>();
                SceneLightingSetup.ConfigureCamera(camera, CameraFovDeg);
                camera.enabled = false;

                var labelGo = new GameObject("__JointTestLabel") { hideFlags = HideFlags.DontSave };
                temp.Add(labelGo);
                labelGo.transform.position = ArmPositionM + Vector3.up * LabelHeightM;
                labelGo.transform.rotation = cameraGo.transform.rotation;
                var label = labelGo.AddComponent<TextMesh>();
                var font = AssetDatabase.LoadAssetAtPath<Font>(ProjectPaths.NotoSansScBold);
                label.font = font;
                label.fontSize = LabelFontSize;
                label.characterSize = LabelCharacterSize;
                label.anchor = TextAnchor.MiddleCenter;
                label.color = Color.yellow;
                labelGo.GetComponent<MeshRenderer>().sharedMaterial = font.material;

                int tile = 0;
                RenderTile(rig, camera, label, sheet, tile++, -1, 0f);
                for (int j = 0; j < rig.JointCount; j++)
                {
                    RenderTile(rig, camera, label, sheet, tile++, j, TestAngleDeg);
                    RenderTile(rig, camera, label, sheet, tile++, j, -TestAngleDeg);
                }
                sheet.Apply();
                Directory.CreateDirectory(Path.GetDirectoryName(OutputPath) ?? ".");
                File.WriteAllBytes(OutputPath, sheet.EncodeToPNG());
            }
            finally
            {
                Object.DestroyImmediate(sheet);
                foreach (Object o in temp)
                {
                    if (o != null) Object.DestroyImmediate(o);
                }
                if (!wasDirty) EditorSceneManager.SaveScene(scene);
            }
            SimLog.Info(LogCategory.System, $"关节测试拼图已输出：{OutputPath}（第 1 格零位，其后依次为 J1～J6 的 +90° / −90°）");
        }

        private static void RenderTile(ArmRig rig, Camera camera, TextMesh label, Texture2D sheet, int index, int joint, float angleDeg)
        {
            var angles = new float[rig.JointCount];
            if (joint >= 0) angles[joint] = angleDeg;
            rig.SetJointAnglesDeg(angles);
            label.text = joint < 0 ? "零位" : $"J{joint + 1} {angleDeg:+0;-0}°";

            var rt = new RenderTexture(TilePx.x, TilePx.y, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            camera.targetTexture = rt;
            camera.Render();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            int col = index % Columns, row = Rows - 1 - index / Columns;
            sheet.ReadPixels(new Rect(0, 0, TilePx.x, TilePx.y), col * TilePx.x, row * TilePx.y);
            RenderTexture.active = previous;
            camera.targetTexture = null;
            rt.Release();
            Object.DestroyImmediate(rt);
        }
    }
}
