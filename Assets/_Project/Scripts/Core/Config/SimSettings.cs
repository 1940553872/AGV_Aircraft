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

        [Tooltip("默认倍速，须在可选倍速内（M7 起默认 2 倍）")]
        public float defaultSpeedMultiplier = 2f;

        [Tooltip("纯逻辑单元（安全许可、调度、数据）的标称执行时长，单位 s")]
        [Min(0f)]
        public float logicCellNominalDurationS = 0.6f;

        [Header("演示")]
        [Tooltip("启动后进入的演示模式")]
        public DemoMode defaultDemoMode = DemoMode.Auto;

        [Tooltip("主场景启动入口（M5）：Mission 为 SOP 完整工序；SubsystemDemo 为 M3 / M4 子系统演示（仅调试，不再维护）")]
        public MissionEntry missionEntry = MissionEntry.Mission;

        [Header("模拟操作员（M5，t(1,8) 识别失败时给出初值）")]
        [Tooltip("识别失败后模拟操作员给出初值的等待时间，单位 s（M6 改为小地图点选）")]
        [Min(0f)]
        public float simulatedOperatorDelayS = 3f;

        [Tooltip("模拟操作员初值的位置噪声标准差，单位 m（真值位姿 + 噪声）")]
        [Min(0f)]
        public float simulatedOperatorPositionNoiseM = 0.05f;

        [Tooltip("模拟操作员初值的航向噪声标准差，单位 °")]
        [Min(0f)]
        public float simulatedOperatorYawNoiseDeg = 1f;

        [Header("故障注入（M5，F5 / F6 / F7 与 Verify 菜单）")]
        [Tooltip("力超限注入：接触表面比注册预测提前，使逼近起点即压缩该量，单位 m（超过浮动行程后按硬限位刚度计力）")]
        [Min(0f)]
        public float faultForceCompressionM = 0.004f;

        [Tooltip("采集无效注入：耦合质量乘以该系数（模拟耦合剂不足），低于无效阈值即判为无效")]
        [Range(0f, 1f)]
        public float faultCouplingFactor = 0.3f;

        [Header("快速验证（M5 Verify 菜单）")]
        [Tooltip("快速验证时每帧推进的仿真时间，单位 s（按固定子步推进，与真实帧间隔无关）")]
        [Min(0f)]
        public float fastVerifySimSecondsPerFrame = 0.25f;

        [Header("任务矩阵")]
        [Tooltip("SOP 任务矩阵 JSON（Config/TaskMatrix/SOP_TaskMatrix_v1.json）")]
        public TextAsset taskMatrixJson;

        /// <summary>默认倍速是否在可选倍速内。</summary>
        public bool IsDefaultSpeedValid()
        {
            if (speedOptions == null) return false;
            foreach (float option in speedOptions)
                if (Mathf.Approximately(option, defaultSpeedMultiplier)) return true;
            return false;
        }

        private void OnValidate()
        {
            // 默认倍速不在列表内时取最接近的可选倍速。
            if (speedOptions == null || speedOptions.Length == 0 || IsDefaultSpeedValid()) return;
            float best = speedOptions[0];
            foreach (float option in speedOptions)
                if (Mathf.Abs(option - defaultSpeedMultiplier) < Mathf.Abs(best - defaultSpeedMultiplier)) best = option;
            defaultSpeedMultiplier = best;
        }
    }
}
