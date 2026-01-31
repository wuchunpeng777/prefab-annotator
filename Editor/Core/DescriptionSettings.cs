using UnityEditor;

namespace PrefabAnnotator.Core
{
    /// <summary>
    /// 注释功能的设置管理器
    /// 使用 EditorPrefs 保存设置到本地
    /// </summary>
    public static class DescriptionSettings
    {
        private const string ENABLED_KEY = "PrefabAnnotator_Enabled";

        /// <summary>
        /// 注释功能是否启用（默认关闭）
        /// </summary>
        public static bool IsEnabled
        {
            get => EditorPrefs.GetBool(ENABLED_KEY, false);
            set
            {
                if (IsEnabled != value)
                {
                    EditorPrefs.SetBool(ENABLED_KEY, value);
                    OnEnabledChanged?.Invoke(value);
                }
            }
        }

        /// <summary>
        /// 当启用状态改变时触发的事件
        /// </summary>
        public static event System.Action<bool> OnEnabledChanged;

        /// <summary>
        /// 切换启用状态
        /// </summary>
        public static void Toggle()
        {
            IsEnabled = !IsEnabled;
        }
    }
}
