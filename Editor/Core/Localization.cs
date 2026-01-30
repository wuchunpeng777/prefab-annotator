using UnityEditor;

namespace PrefabAnnotator.Core
{
    /// <summary>
    /// 本地化管理器 - 支持中英文切换
    /// </summary>
    public static class Localization
    {
        /// <summary>
        /// 支持的语言枚举
        /// </summary>
        public enum Language
        {
            Chinese,
            English
        }

        private const string LANGUAGE_KEY = "PrefabAnnotator_Language";

        /// <summary>
        /// 当前语言（默认英文）
        /// </summary>
        public static Language CurrentLanguage
        {
            get => (Language)EditorPrefs.GetInt(LANGUAGE_KEY, (int)Language.English);
            set
            {
                if (CurrentLanguage != value)
                {
                    EditorPrefs.SetInt(LANGUAGE_KEY, (int)value);
                }
            }
        }

        /// <summary>
        /// 是否为中文
        /// </summary>
        public static bool IsChinese => CurrentLanguage == Language.Chinese;

        /// <summary>
        /// 是否为英文
        /// </summary>
        public static bool IsEnglish => CurrentLanguage == Language.English;

        #region 本地化文本

        // ============== Inspector面板相关 ==============

        /// <summary>
        /// 展开所有注释节点按钮
        /// </summary>
        public static string Inspector_ExpandAnnotatedNodes => IsChinese ? "展开所有注释节点" : "Expand Annotated Nodes";

        /// <summary>
        /// 描述标签
        /// </summary>
        public static string Inspector_DescriptionLabel => IsChinese ? "描述" : "Description";

        /// <summary>
        /// 取消按钮
        /// </summary>
        public static string Inspector_Cancel => IsChinese ? "取消" : "Cancel";

        /// <summary>
        /// 保存按钮
        /// </summary>
        public static string Inspector_Save => IsChinese ? "保存" : "Save";

        /// <summary>
        /// 忽略节点复选框
        /// </summary>
        public static string Inspector_IgnoreNode => IsChinese ? "忽略此节点及子节点" : "Ignore this node and children";

        /// <summary>
        /// 当前节点被忽略的提示
        /// </summary>
        public static string Inspector_IgnoredHint => IsChinese 
            ? "此节点及其所有子节点将不会被导出" 
            : "This node and all its children will not be exported";

        /// <summary>
        /// 父节点被忽略的提示
        /// </summary>
        public static string Inspector_ParentIgnoredHint => IsChinese 
            ? "父节点已被标记为忽略，此节点将不会被导出" 
            : "Parent node is marked as ignored, this node will not be exported";

        /// <summary>
        /// 来自嵌套Prefab的忽略状态提示
        /// </summary>
        public static string Inspector_NestedPrefabIgnoredHint => IsChinese 
            ? "此节点在嵌套的Prefab中被标记为忽略，将不会被导出" 
            : "This node is marked as ignored in the nested Prefab, will not be exported";

        // ============== 导出功能相关 ==============

        /// <summary>
        /// 导出提示对话框标题
        /// </summary>
        public static string Export_DialogTitle => IsChinese ? "导出提示" : "Export Info";

        /// <summary>
        /// 无法导出该节点
        /// </summary>
        public static string Export_CannotExportNode => IsChinese ? "无法导出该节点。" : "Cannot export this node.";

        /// <summary>
        /// 无法导出该Prefab
        /// </summary>
        public static string Export_CannotExportPrefab => IsChinese ? "无法导出该Prefab。" : "Cannot export this Prefab.";

        /// <summary>
        /// 确定按钮
        /// </summary>
        public static string Export_OK => IsChinese ? "确定" : "OK";

        /// <summary>
        /// 嵌套Prefab标记
        /// </summary>
        public static string Export_NestedPrefabMark => IsChinese ? "嵌套Prefab" : "Nested Prefab";

        /// <summary>
        /// 描述前缀
        /// </summary>
        public static string Export_DescriptionPrefix => IsChinese ? "描述：" : "Desc: ";

        /// <summary>
        /// 已复制到剪贴板日志
        /// </summary>
        public static string Export_CopiedToClipboard => IsChinese ? "已复制到剪贴板" : "Copied to clipboard";

        // ============== 警告和错误信息 ==============

        /// <summary>
        /// 无法获取 Hierarchy 窗口
        /// </summary>
        public static string Warning_CannotGetHierarchyWindow => IsChinese 
            ? "无法获取 Hierarchy 窗口，使用备用方案" 
            : "Cannot get Hierarchy window, using fallback";

        /// <summary>
        /// 无法获取 TreeView 控制器
        /// </summary>
        public static string Warning_CannotGetTreeView => IsChinese 
            ? "无法获取 TreeView 控制器，使用备用方案" 
            : "Cannot get TreeView controller, using fallback";

        /// <summary>
        /// 无法找到字段
        /// </summary>
        public static string Warning_CannotFindField(string fieldName) => IsChinese 
            ? $"无法找到 {fieldName} 字段" 
            : $"Cannot find {fieldName} field";

        /// <summary>
        /// 字段值为 null
        /// </summary>
        public static string Warning_FieldIsNull(string fieldName) => IsChinese 
            ? $"{fieldName} 值为 null" 
            : $"{fieldName} is null";

        /// <summary>
        /// TreeView 可能尚未初始化
        /// </summary>
        public static string Warning_TreeViewNotInitialized => IsChinese 
            ? "m_TreeView 值为 null，TreeView 可能尚未初始化" 
            : "m_TreeView is null, TreeView may not be initialized yet";

        /// <summary>
        /// 初始化失败
        /// </summary>
        public static string Warning_InitializationFailed(string message) => IsChinese 
            ? $"初始化 TreeView 失败: {message}" 
            : $"Failed to initialize TreeView: {message}";

        #endregion
    }
}
