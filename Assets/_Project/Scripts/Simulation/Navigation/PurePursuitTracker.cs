using System.Collections.Generic;
using UnityEngine;

namespace RivetSim.Simulation.Navigation
{
    /// <summary>
    /// 纯追踪（Pure Pursuit）几何计算：在折线上找车体投影点（只向前推进）与前视点，给出前视点方位误差、曲率与剩余路程。
    /// 前视点超出折线末端时沿最后一段延长线取点，使接近终点时转向保持平稳。
    /// </summary>
    public sealed class PurePursuitTracker
    {
        private IReadOnlyList<Vector2> _points;
        private int _segment;
        private float _totalLengthM;
        private float[] _cumulativeM;

        /// <summary>沿折线已走过的路程（投影点处），单位 m。</summary>
        public float ProgressM { get; private set; }

        /// <summary>折线总长，单位 m。</summary>
        public float TotalLengthM => _totalLengthM;

        /// <summary>剩余路程（投影点到终点），单位 m。</summary>
        public float RemainingM => _totalLengthM - ProgressM;

        /// <summary>开始跟踪一条折线（至少 2 个顶点）。</summary>
        public void Reset(IReadOnlyList<Vector2> points)
        {
            _points = points;
            _segment = 0;
            ProgressM = 0f;
            _cumulativeM = new float[points.Count];
            for (int i = 1; i < points.Count; i++) _cumulativeM[i] = _cumulativeM[i - 1] + Vector2.Distance(points[i - 1], points[i]);
            _totalLengthM = _cumulativeM[points.Count - 1];
        }

        /// <summary>
        /// 计算前视点；返回前视点相对车头的方位误差（°，顺时针为正）与纯追踪曲率（1/m，向右转为正）。
        /// </summary>
        public Vector2 Compute(Vector2 positionM, float yawDeg, float lookaheadM, out float headingErrorDeg, out float curvaturePerM)
        {
            AdvanceProjection(positionM);
            Vector2 target = PointAt(ProgressM + lookaheadM);
            Vector2 d = target - positionM;
            float bearing = Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg;
            headingErrorDeg = Mathf.DeltaAngle(yawDeg, bearing);
            float distance = d.magnitude;
            curvaturePerM = distance > 1e-4f ? 2f * Mathf.Sin(headingErrorDeg * Mathf.Deg2Rad) / distance : 0f;
            return target;
        }

        /// <summary>沿折线路程 s 处的点；s 超过总长时沿最后一段延长。</summary>
        public Vector2 PointAt(float s)
        {
            int last = _points.Count - 1;
            if (s >= _totalLengthM)
            {
                Vector2 dir = (_points[last] - _points[last - 1]).normalized;
                return _points[last] + dir * (s - _totalLengthM);
            }
            int i = Mathf.Max(_segment, 0);
            while (i < last - 1 && _cumulativeM[i + 1] < s) i++;
            float segLength = _cumulativeM[i + 1] - _cumulativeM[i];
            float t = segLength > 1e-6f ? (s - _cumulativeM[i]) / segLength : 0f;
            return Vector2.Lerp(_points[i], _points[i + 1], Mathf.Clamp01(t));
        }

        // 投影点只在当前段与后续段中查找，避免折线回绕时跳回已走过的部分。
        private void AdvanceProjection(Vector2 p)
        {
            int last = _points.Count - 1;
            float bestDistance = float.MaxValue;
            int bestSegment = _segment;
            float bestS = ProgressM;
            for (int i = _segment; i < last; i++)
            {
                Vector2 a = _points[i], b = _points[i + 1];
                Vector2 ab = b - a;
                float len2 = ab.sqrMagnitude;
                float t = len2 > 1e-8f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2) : 0f;
                float dist = (a + ab * t - p).sqrMagnitude;
                if (dist < bestDistance)
                {
                    bestDistance = dist;
                    bestSegment = i;
                    bestS = _cumulativeM[i] + Mathf.Sqrt(len2) * t;
                }
                // 已找到的投影点明显更近时停止向后搜索。
                if (dist > bestDistance && _cumulativeM[i] - bestS > 2f * Mathf.Sqrt(bestDistance) + 1f) break;
            }
            _segment = bestSegment;
            ProgressM = Mathf.Max(ProgressM, bestS);
        }
    }
}
