namespace RivetSim.Core.Events
{
    /// <summary>
    /// 仿真事件标记接口。事件一律定义为 <c>readonly struct XxxEvent : ISimEvent</c>，通过 <see cref="EventBus"/> 发布。
    /// </summary>
    public interface ISimEvent
    {
    }
}
