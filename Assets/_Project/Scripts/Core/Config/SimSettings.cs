using UnityEngine;

namespace RivetSim.Core.Config
{
    /// <summary>
    /// 仿真全局设置（CFG_SimSettings）：随机种子、倍速、纯逻辑单元标称时长、默认演示模式、任务矩阵数据源。
    /// </summary>
    [CreateAssetMenu(fileName = "CFG_SimSettings", menuName = "RivetSim/Config/Sim Settings")]
    public sealed class SimSettings : ScriptableObject
    {
        [TextArea(2, 6)]
        [Tooltip("参数来源与修改说明")]
        public string notes = "v1 预估值，见 Docs/Architecture.md §5.1、§6.1。";

        [Header("随机与时间")]
        [Tooltip("随机种子（缺陷分布、噪声等），相同种子可复现同一次演示")]
        public int randomSeed = 42;

        [Tooltip("可选倍速（升序）")]
        public float[] speedOptions = { 0.5f, 1f, 2f, 4f };

        [Tooltip("默认倍速，须在可选倍速内")]
        public float defaultSpeedMultiplier = 1f;

        [Tooltip("纯逻辑单元（安全许可、调度、数据）的标称执行时长，单位 s")]
        [Min(0f)]
        public float logicCellNominalDurationS = 0.6f;

        [Header("演示")]
        [Tooltip("启动后进入的演示模式")]
        public DemoMode defaultDemoMode = DemoMode.Auto;

        [Header("任务矩阵")]
        [Tooltip("SOP 任务矩阵 JSON（Config/TaskMatrix/SOP_TaskMatrix_v1.json）")]
        public TextAsset taskMatrixJson;
    }
}
