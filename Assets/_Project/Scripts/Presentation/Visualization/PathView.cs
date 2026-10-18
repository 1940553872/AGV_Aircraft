using System;
using RivetSim.Core.Config;
using RivetSim.Core.Events;
using RivetSim.Simulation.Navigation;
using RivetSim.Simulation.Robot;
using UnityEngine;

namespace RivetSim.Presentation.Visualization
{
    /// <summary>
    /// 规划路径与目标位姿显示：路径为青色线，目标位姿为橙色箭头（指向车头方向），对接车道期间同样显示。
    /// 订阅 <see cref="PathPlannedEvent"/>，线条由编辑器生成器预先配置好材质与宽度。
    /// </summary>
    public sealed class PathView : MonoBehaviour
    {
        // 线条离地高度与箭头尺寸（外观常量，ADR-009）。
        private const float HeightM = 0.03f;
        private const float ArrowLengthM = 0.9f;
        private const float ArrowHalfWidthM = 0.35f;

        [SerializeField, Tooltip("路径线")]
        private LineRenderer pathLine;

        [SerializeField, Tooltip("目标位姿箭头")]
        private LineRenderer goalArrow;

        private IDisposable _subscription;

        /// <summary>编辑器生成器写入引用。</summary>
        public void Configure(LineRenderer path, LineRenderer arrow)
        {
            pathLine = path;
            goalArrow = arrow;
        }

        /// <summary>订阅规划事件。</summary>
        public void Initialize(EventBus bus)
        {
            _subscription = bus.Subscribe<PathPlannedEvent>(OnPathPlanned);
            pathLine.positionCount = 0;
            goalArrow.positionCount = 0;
        }

        private void OnPathPlanned(PathPlannedEvent e)
        {
            var points = e.Path.Points;
            pathLine.positionCount = points.Count;
            for (int i = 0; i < points.Count; i++) pathLine.SetPosition(i, new Vector3(points[i].x, HeightM, points[i].y));
            DrawArrow(e.Goal);
        }

        private void DrawArrow(PlanarPose goal)
        {
            Vector2 h = DiffDriveKinematics.Heading(goal.yawDeg);
            Vector2 r = DiffDriveKinematics.Right(goal.yawDeg);
            Vector2 c = goal.positionM;
            Vector2 tip = c + h * (0.5f * ArrowLengthM);
            Vector2 tail = c - h * (0.5f * ArrowLengthM);
            Vector2 left = c - r * ArrowHalfWidthM;
            Vector2 right = c + r * ArrowHalfWidthM;
            Vector2[] shape = { tail, tip, left, tip, right };
            goalArrow.positionCount = shape.Length;
            for (int i = 0; i < shape.Length; i++) goalArrow.SetPosition(i, new Vector3(shape[i].x, HeightM, shape[i].y));
        }

        private void OnDestroy() => _subscription?.Dispose();
    }
}
