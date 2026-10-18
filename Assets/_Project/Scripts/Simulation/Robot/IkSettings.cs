using RivetSim.Core.Config;

namespace RivetSim.Simulation.Robot
{
    /// <summary>
    /// 阻尼最小二乘逆解参数（取自 CFG_Arm，ADR-016）。
    /// </summary>
    public readonly struct IkSettings
    {
        /// <summary>单个初值的最大迭代次数。</summary>
        public readonly int MaxIterations;

        /// <summary>位置收敛阈值，单位 m。</summary>
        public readonly float PositionToleranceM;

        /// <summary>姿态收敛阈值，单位 °。</summary>
        public readonly float OrientationToleranceDeg;

        /// <summary>阻尼系数 λ。</summary>
        public readonly float DampingLambda;

        /// <summary>单次迭代关节角步长上限，单位 °。</summary>
        public readonly float MaxStepDeg;

        /// <summary>随机初值次数。</summary>
        public readonly int RandomRestarts;

        /// <summary>姿态误差（弧度）折算为长度的权重，单位 m。</summary>
        public readonly float OrientationWeightM;

        /// <summary>创建参数。</summary>
        public IkSettings(int maxIterations, float positionToleranceM, float orientationToleranceDeg, float dampingLambda, float maxStepDeg,
            int randomRestarts, float orientationWeightM)
        {
            MaxIterations = maxIterations;
            PositionToleranceM = positionToleranceM;
            OrientationToleranceDeg = orientationToleranceDeg;
            DampingLambda = dampingLambda;
            MaxStepDeg = maxStepDeg;
            RandomRestarts = randomRestarts;
            OrientationWeightM = orientationWeightM;
        }

        /// <summary>从机械臂配置读取。</summary>
        public static IkSettings From(ArmConfig arm) => new IkSettings(arm.ikMaxIterations, arm.ikPositionToleranceM, arm.ikOrientationToleranceDeg,
            arm.ikDampingLambda, arm.ikMaxStepDeg, arm.ikRandomRestarts, arm.ikOrientationWeightM);
    }
}
