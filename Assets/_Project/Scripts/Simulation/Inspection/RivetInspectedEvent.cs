using RivetSim.Core.Events;
using RivetSim.Simulation.World;

namespace RivetSim.Simulation.Inspection
{
    /// <summary>
    /// 单钉检测完成事件：铆钉、检测记录与 A 扫波形（供结果环、A 扫显示与演示屏使用）。
    /// </summary>
    public readonly struct RivetInspectedEvent : ISimEvent
    {
        /// <summary>铆钉。</summary>
        public readonly Rivet Rivet;

        /// <summary>检测记录。</summary>
        public readonly InspectionRecord Record;

        /// <summary>A 扫波形。</summary>
        public readonly AScanWaveform Waveform;

        /// <summary>创建事件。</summary>
        public RivetInspectedEvent(Rivet rivet, InspectionRecord record, AScanWaveform waveform)
        {
            Rivet = rivet;
            Record = record;
            Waveform = waveform;
        }
    }
}
