using System;
using System.Collections.Generic;
using UnityEngine;

namespace RivetSim.Simulation.Robot
{
    /// <summary>
    /// 机械臂数值逆解（阻尼最小二乘，ADR-016）：误差 = [位置差; 姿态差（旋转向量）× 权重]，几何雅可比由各关节原点与转轴解析计算；
    /// 每次迭代后关节角限制在限位内。依次尝试“调用方给的初值 → 内置初值 → 随机初值（固定种子）”，取第一个收敛且被接受的解
    /// （接受判据由调用方给出，如连杆间隙检查）。目标位姿为 TCP 在机械臂基座坐标系中的位姿。
    /// </summary>
    public sealed class ArmIkSolver
    {
        // 内置初值（°）：肘上构型（基座朝前 / 左 / 右 / 后）、Observe 及其镜像、一组翻腕构型（取自 M2 可达性验证）。
        private static readonly float[][] BuiltInSeeds =
        {
            new[] { 0f, -90f, 90f, -90f, -90f, 0f },
            new[] { 90f, -90f, 90f, -90f, -90f, 0f },
            new[] { -90f, -90f, 90f, -90f, -90f, 0f },
            new[] { -61f, -82f, 146f, -82f, -243f, -98f },
            new[] { -241f, -82f, 146f, -82f, -243f, -98f },
            new[] { 90f, -80f, 110f, -210f, -90f, 0f },
            new[] { 0f, -60f, -90f, -120f, 90f, 0f },
            new[] { 180f, -90f, 90f, -90f, -90f, 0f },
        };

        private const float RandomSeedRangeDeg = 180f;

        private readonly ArmKinematics _kinematics;
        private readonly IkSettings _settings;
        private readonly System.Random _random;
        private readonly int _n;
        private readonly Vector3[] _origins;
        private readonly Vector3[] _axes;
        private readonly double[,] _jacobian;
        private readonly double[,] _system = new double[6, 7];
        private readonly double[] _error = new double[6];

        /// <summary>运动学模型。</summary>
        public ArmKinematics Kinematics => _kinematics;

        /// <summary>创建逆解器；seed 为随机初值的种子。</summary>
        public ArmIkSolver(ArmKinematics kinematics, IkSettings settings, int seed)
        {
            _kinematics = kinematics ?? throw new ArgumentNullException(nameof(kinematics));
            _settings = settings;
            _random = new System.Random(seed);
            _n = kinematics.JointCount;
            _origins = new Vector3[_n];
            _axes = new Vector3[_n];
            _jacobian = new double[6, _n];
        }

        /// <summary>
        /// 求解：依次尝试 seeds、内置初值与随机初值；收敛且 accept 返回 true（accept 为空时不检查）的解写入 solution。
        /// </summary>
        public IkResult Solve(Pose target, IReadOnlyList<float[]> seeds, Func<float[], bool> accept, float[] solution)
        {
            var q = new float[_n];
            int iterations = 0, tried = 0, rejected = 0;
            float bestPos = float.MaxValue, bestAng = float.MaxValue;
            int total = (seeds?.Count ?? 0) + BuiltInSeeds.Length + _settings.RandomRestarts;
            for (int s = 0; s < total; s++)
            {
                FillSeed(s, seeds, q);
                tried++;
                bool converged = Refine(target, q, _settings.MaxIterations, out float pos, out float ang, out int used);
                iterations += used;
                if (pos + ang * 0.001f < bestPos + bestAng * 0.001f)
                {
                    bestPos = pos;
                    bestAng = ang;
                }
                if (!converged) continue;
                if (accept != null && !accept(q))
                {
                    rejected++;
                    continue;
                }
                Array.Copy(q, solution, _n);
                return new IkResult(true, pos, ang, iterations, tried, rejected);
            }
            return new IkResult(false, bestPos, bestAng, iterations, tried, rejected);
        }

        /// <summary>从 q 出发迭代（就地修改 q），收敛时返回 true；用于直线运动的逐步跟踪。</summary>
        public bool Refine(Pose target, float[] q, int maxIterations, out float positionErrorM, out float angleErrorDeg, out int iterations)
        {
            float w = _settings.OrientationWeightM;
            for (iterations = 0; ; iterations++)
            {
                Pose tip = _kinematics.Forward(q, _origins, _axes);
                Vector3 ep = target.position - tip.position;
                Vector3 er = RotationError(target.rotation, tip.rotation, out angleErrorDeg);
                positionErrorM = ep.magnitude;
                if (positionErrorM < _settings.PositionToleranceM && angleErrorDeg < _settings.OrientationToleranceDeg) return true;
                if (iterations >= maxIterations) return false;

                for (int c = 0; c < _n; c++)
                {
                    Vector3 jv = Vector3.Cross(_axes[c], tip.position - _origins[c]);
                    Vector3 jw = _axes[c] * w;
                    _jacobian[0, c] = jv.x;
                    _jacobian[1, c] = jv.y;
                    _jacobian[2, c] = jv.z;
                    _jacobian[3, c] = jw.x;
                    _jacobian[4, c] = jw.y;
                    _jacobian[5, c] = jw.z;
                }
                _error[0] = ep.x;
                _error[1] = ep.y;
                _error[2] = ep.z;
                _error[3] = er.x * w;
                _error[4] = er.y * w;
                _error[5] = er.z * w;
                DampedStep(q);
            }
        }

        // 旋转向量（弧度）：把当前姿态转到目标姿态的世界系旋转。
        private static Vector3 RotationError(Quaternion target, Quaternion current, out float angleDeg)
        {
            Quaternion d = target * Quaternion.Inverse(current);
            if (d.w < 0f) d = new Quaternion(-d.x, -d.y, -d.z, -d.w);
            var v = new Vector3(d.x, d.y, d.z);
            float sinHalf = v.magnitude;
            float angle = 2f * Mathf.Atan2(sinHalf, d.w);
            angleDeg = angle * Mathf.Rad2Deg;
            return sinHalf < 1e-9f ? Vector3.zero : v * (angle / sinHalf);
        }

        // Δq = Jᵀ (J Jᵀ + λ² I)⁻¹ e，逐关节限幅后加到 q 并限制在限位内。
        private void DampedStep(float[] q)
        {
            double lambda2 = _settings.DampingLambda * _settings.DampingLambda;
            for (int r = 0; r < 6; r++)
            {
                for (int c = 0; c < 6; c++)
                {
                    double sum = 0;
                    for (int k = 0; k < _n; k++) sum += _jacobian[r, k] * _jacobian[c, k];
                    _system[r, c] = sum + (r == c ? lambda2 : 0);
                }
                _system[r, 6] = _error[r];
            }
            SolveInPlace(_system);
            for (int k = 0; k < _n; k++)
            {
                double sum = 0;
                for (int r = 0; r < 6; r++) sum += _jacobian[r, k] * _system[r, 6];
                float stepDeg = Mathf.Clamp((float)sum * Mathf.Rad2Deg, -_settings.MaxStepDeg, _settings.MaxStepDeg);
                q[k] = _kinematics.Clamp(k, q[k] + stepDeg);
            }
        }

        // 高斯消元（列主元），结果写回第 6 列。
        private static void SolveInPlace(double[,] a)
        {
            const int n = 6;
            for (int col = 0; col < n; col++)
            {
                int pivot = col;
                for (int r = col + 1; r < n; r++)
                    if (Math.Abs(a[r, col]) > Math.Abs(a[pivot, col])) pivot = r;
                if (pivot != col)
                {
                    for (int c = 0; c <= n; c++) (a[col, c], a[pivot, c]) = (a[pivot, c], a[col, c]);
                }
                double d = a[col, col];
                for (int r = col + 1; r < n; r++)
                {
                    double f = a[r, col] / d;
                    for (int c = col; c <= n; c++) a[r, c] -= f * a[col, c];
                }
            }
            for (int r = n - 1; r >= 0; r--)
            {
                double sum = a[r, n];
                for (int c = r + 1; c < n; c++) sum -= a[r, c] * a[c, n];
                a[r, n] = sum / a[r, r];
            }
        }

        private void FillSeed(int index, IReadOnlyList<float[]> seeds, float[] q)
        {
            int given = seeds?.Count ?? 0;
            if (index < given)
            {
                Array.Copy(seeds[index], q, _n);
            }
            else if (index < given + BuiltInSeeds.Length)
            {
                Array.Copy(BuiltInSeeds[index - given], q, _n);
            }
            else
            {
                for (int i = 0; i < _n; i++) q[i] = (float)((_random.NextDouble() * 2.0 - 1.0) * RandomSeedRangeDeg);
            }
            for (int i = 0; i < _n; i++) q[i] = _kinematics.Clamp(i, q[i]);
        }
    }
}
