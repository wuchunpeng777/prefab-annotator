using UnityEditor;
using PrefabAnnotator.Core;

namespace PrefabAnnotator.UI
{
    /// <summary>
    /// 注释功能的菜单项
    /// 根据当前状态显示"开启"或"关闭"
    /// 支持中英文切换
    /// </summary>
    public static class DescriptionMenu
    {
        private const string MENU_PATH_ENABLE = "Tools/Prefab Annotator/Enable";
        private const string MENU_PATH_DISABLE = "Tools/Prefab Annotator/Disable";
        private const string MENU_PATH_LANG_CHINESE = "Tools/Prefab Annotator/Language/Chinese";
        private const string MENU_PATH_LANG_ENGLISH = "Tools/Prefab Annotator/Language/English";

        #region 开启/关闭注释功能

        // 开启注释功能（当前已关闭时显示）
        [MenuItem(MENU_PATH_ENABLE, false, 100)]
        private static void EnableDescription()
        {
            DescriptionSettings.IsEnabled = true;
            EditorApplication.RepaintHierarchyWindow();
        }

        [MenuItem(MENU_PATH_ENABLE, true)]
        private static bool EnableDescriptionValidate()
        {
            // 只有当前关闭时才显示"开启"选项
            return !DescriptionSettings.IsEnabled;
        }

        // 关闭注释功能（当前已开启时显示）
        [MenuItem(MENU_PATH_DISABLE, false, 100)]
        private static void DisableDescription()
        {
            DescriptionSettings.IsEnabled = false;
            EditorApplication.RepaintHierarchyWindow();
        }

        [MenuItem(MENU_PATH_DISABLE, true)]
        private static bool DisableDescriptionValidate()
        {
            // 只有当前开启时才显示"关闭"选项
            return DescriptionSettings.IsEnabled;
        }

        #endregion

        #region 语言切换

        // 切换到中文
        [MenuItem(MENU_PATH_LANG_CHINESE, false, 200)]
        private static void SwitchToChinese()
        {
            Localization.CurrentLanguage = Localization.Language.Chinese;
            // 刷新所有Editor窗口
            EditorApplication.RepaintHierarchyWindow();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

        [MenuItem(MENU_PATH_LANG_CHINESE, true)]
        private static bool SwitchToChineseValidate()
        {
            // 当前是中文时打勾
            Menu.SetChecked(MENU_PATH_LANG_CHINESE, Localization.IsChinese);
            return true;
        }

        // 切换到英文
        [MenuItem(MENU_PATH_LANG_ENGLISH, false, 201)]
        private static void SwitchToEnglish()
        {
            Localization.CurrentLanguage = Localization.Language.English;
            // 刷新所有Editor窗口
            EditorApplication.RepaintHierarchyWindow();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

        [MenuItem(MENU_PATH_LANG_ENGLISH, true)]
        private static bool SwitchToEnglishValidate()
        {
            // 当前是英文时打勾
            Menu.SetChecked(MENU_PATH_LANG_ENGLISH, Localization.IsEnglish);
            return true;
        }

        #endregion
    }
}
