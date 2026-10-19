using RivetSim.Simulation.Mission;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RivetSim.Presentation.Input
{
    /// <summary>
    /// 故障注入热键（M5 测试用，M6 的 HUD 按钮调用同一 ConditionBus 接口）：F5 识别失败、F6 力超限、F7 采集无效。
    /// </summary>
    public sealed class FaultHotkeys : MonoBehaviour
    {
        private ConditionBus _conditions;

        /// <summary>绑定条件总线。</summary>
        public void Initialize(ConditionBus conditions) => _conditions = conditions;

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (_conditions == null || keyboard == null) return;
            if (keyboard.f5Key.wasPressedThisFrame) _conditions.Inject(FaultKind.RecognitionFailed);
            if (keyboard.f6Key.wasPressedThisFrame) _conditions.Inject(FaultKind.ContactForceExceeded);
            if (keyboard.f7Key.wasPressedThisFrame) _conditions.Inject(FaultKind.AcquisitionInvalid);
        }
    }
}
