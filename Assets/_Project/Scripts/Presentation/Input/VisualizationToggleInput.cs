using UnityEngine;
using UnityEngine.InputSystem;

namespace RivetSim.Presentation.Input
{
    /// <summary>
    /// 可视化开关：按 V 显示 / 隐藏 Visualization 根节点下的全部叠加层（地图、禁行区、点云、路径）。
    /// </summary>
    public sealed class VisualizationToggleInput : MonoBehaviour
    {
        [SerializeField, Tooltip("Visualization 根节点")]
        private GameObject visualizationRoot;

        /// <summary>编辑器生成器写入引用。</summary>
        public void Configure(GameObject root) => visualizationRoot = root;

        /// <summary>可视化是否显示。</summary>
        public bool IsVisible => visualizationRoot != null && visualizationRoot.activeSelf;

        /// <summary>切换显示（V 键与 HUD 按钮共用）。</summary>
        public void Toggle()
        {
            if (visualizationRoot != null) visualizationRoot.SetActive(!visualizationRoot.activeSelf);
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.vKey.wasPressedThisFrame) return;
            Toggle();
        }
    }
}
