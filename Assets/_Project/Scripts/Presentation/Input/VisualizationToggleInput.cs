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

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || visualizationRoot == null || !keyboard.vKey.wasPressedThisFrame) return;
            visualizationRoot.SetActive(!visualizationRoot.activeSelf);
        }
    }
}
