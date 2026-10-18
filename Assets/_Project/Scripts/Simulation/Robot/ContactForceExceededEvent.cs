using RivetSim.Core.Events;

namespace RivetSim.Simulation.Robot
{
    /// <summary>
    /// 接触力超限事件（超过 CFG_Inspection.contactForceLimitN），M5 由 ConditionBus 订阅为“力超限”条件。
    /// </summary>
    public readonly struct ContactForceExceededEvent : ISimEvent
    {
        /// <summary>超限时的接触力，单位 N。</summary>
        public readonly float ForceN;

        /// <summary>超限阈值，单位 N。</summary>
        public readonly float LimitN;

        /// <summary>动作说明。</summary>
        public readonly string Label;

        /// <summary>创建事件。</summary>
        public ContactForceExceededEvent(float forceN, float limitN, string label)
        {
            ForceN = forceN;
            LimitN = limitN;
            Label = label;
        }
    }
}
