using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace RivetSim.Editor.Tools
{
    /// <summary>
    /// 设置 Game 视图为固定分辨率（M7 性能验证用；经反射调用编辑器内部 API，失败时返回 false 并说明原因）。
    /// </summary>
    public static class GameViewResolution
    {
        private const int FixedResolutionType = 1;

        /// <summary>选中（必要时新增）宽 × 高的固定分辨率。</summary>
        public static bool TrySet(int width, int height, out string info)
        {
            try
            {
                Assembly asm = typeof(EditorWindow).Assembly;
                Type sizesType = asm.GetType("UnityEditor.GameViewSizes");
                Type singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
                object sizes = singleton.GetProperty("instance").GetValue(null);
                object groupType = sizesType.GetProperty("currentGroupType").GetValue(sizes);
                object group = sizesType.GetMethod("GetGroup").Invoke(sizes, new[] { groupType });
                Type groupClass = group.GetType();
                int count = (int)groupClass.GetMethod("GetTotalCount").Invoke(group, null);
                MethodInfo getSize = groupClass.GetMethod("GetGameViewSize");
                int index = -1;
                for (int i = 0; i < count && index < 0; i++)
                {
                    object size = getSize.Invoke(group, new object[] { i });
                    Type st = size.GetType();
                    if ((int)st.GetProperty("width").GetValue(size) == width && (int)st.GetProperty("height").GetValue(size) == height &&
                        Convert.ToInt32(st.GetProperty("sizeType").GetValue(size)) == FixedResolutionType)
                        index = i;
                }
                if (index < 0)
                {
                    Type sizeClass = asm.GetType("UnityEditor.GameViewSize");
                    Type sizeTypeEnum = asm.GetType("UnityEditor.GameViewSizeType");
                    object custom = Activator.CreateInstance(sizeClass, Enum.ToObject(sizeTypeEnum, FixedResolutionType), width, height, $"RivetSim {width}x{height}");
                    groupClass.GetMethod("AddCustomSize").Invoke(group, new[] { custom });
                    index = (int)groupClass.GetMethod("GetTotalCount").Invoke(group, null) - 1;
                }
                Type gameViewType = asm.GetType("UnityEditor.GameView");
                UnityEngine.Object[] views = Resources.FindObjectsOfTypeAll(gameViewType);
                EditorWindow view = views.Length > 0 ? (EditorWindow)views[0] : EditorWindow.GetWindow(gameViewType);
                PropertyInfo selected = gameViewType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (selected != null && selected.CanWrite) selected.SetValue(view, index);
                else gameViewType.GetMethod("SizeSelectionCallback", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    ?.Invoke(view, new object[] { index, null });
                view.Repaint();
                info = $"Game 视图固定 {width}×{height}（索引 {index}）";
                return true;
            }
            catch (Exception e)
            {
                info = $"无法设置 Game 视图分辨率：{e.GetType().Name} {e.Message}";
                return false;
            }
        }
    }
}
