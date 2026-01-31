using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEditor;
#if UNITY_2021_2_OR_NEWER
using UnityEditor.SceneManagement;
#else
using UnityEditor.Experimental.SceneManagement;
#endif
using PrefabAnnotator.Core;
using L = PrefabAnnotator.Core.Localization;

namespace PrefabAnnotator.Export
{
    /// <summary>
    /// 描述导出器 - 将有描述的节点导出为树形文本结构
    /// 仅支持Prefab编辑模式
    /// 支持嵌套Prefab的注释导出
    /// </summary>
    public static class DescriptionExporter
    {
        // 需要过滤的组件类型（Unity内置的基础组件，通常不需要显示）
        private static readonly HashSet<string> FilteredComponents = new HashSet<string>
        {
            "Transform",
            "RectTransform",
            "CanvasRenderer"
        };

        #region 导出逻辑

        /// <summary>
        /// 导出指定GameObject为树形文本（用于Prefab编辑模式）
        /// 支持嵌套Prefab的注释导出
        /// 导出完整的Prefab结构
        /// </summary>
        public static string ExportGameObjectToString(GameObject root)
        {
            if (root == null)
            {
                return null;
            }

            StringBuilder sb = new StringBuilder();
            
            // 获取当前编辑的Prefab路径
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            string currentPrefabPath = prefabStage?.assetPath;
            
            BuildTreeTextWithNestedSupport(root, sb, "", true, true, currentPrefabPath);
            
            string result = sb.ToString().TrimEnd();
            return string.IsNullOrEmpty(result) ? null : result;
        }

        /// <summary>
        /// 递归构建树形文本（支持嵌套Prefab，用于Prefab编辑模式）
        /// 导出完整的Prefab结构
        /// </summary>
        /// <param name="gameObject">当前GameObject</param>
        /// <param name="sb">StringBuilder</param>
        /// <param name="prefix">当前行的前缀（用于缩进）</param>
        /// <param name="isLast">是否是父节点的最后一个子节点</param>
        /// <param name="isRoot">是否是根节点</param>
        /// <param name="currentPrefabPath">当前正在处理的Prefab路径</param>
        /// <returns>是否有任何内容</returns>
        private static bool BuildTreeTextWithNestedSupport(GameObject gameObject, StringBuilder sb, string prefix, 
            bool isLast, bool isRoot, string currentPrefabPath)
        {
            if (gameObject == null)
            {
                return false;
            }

            // 检查是否被忽略（支持嵌套Prefab）
            if (DescriptionFileManager.IsIgnoredWithNestedSupport(gameObject))
            {
                return false;
            }

            // 检查是否是嵌套Prefab的根节点
            string nestedPrefabPath = null;
            bool isNestedPrefabRoot = false;
            
            if (!isRoot && PrefabUtility.IsAnyPrefabInstanceRoot(gameObject))
            {
                // 使用 GetCorrespondingObjectFromOriginalSource 获取最原始的源对象
                GameObject sourcePrefab = PrefabUtility.GetCorrespondingObjectFromOriginalSource(gameObject);
                if (sourcePrefab != null)
                {
                    nestedPrefabPath = AssetDatabase.GetAssetPath(sourcePrefab);
                    if (!string.IsNullOrEmpty(nestedPrefabPath) && nestedPrefabPath != currentPrefabPath)
                    {
                        isNestedPrefabRoot = true;
                    }
                }
            }

            // 获取描述（使用统一的嵌套支持查找逻辑）
            string description = DescriptionFileManager.GetDescriptionWithNestedSupport(gameObject);

            // 构建当前节点的行
            string nodeInfo = BuildNodeInfo(gameObject, description, isNestedPrefabRoot, nestedPrefabPath);
            
            if (isRoot)
            {
                sb.AppendLine(nodeInfo);
            }
            else
            {
                string connector = isLast ? "└─" : "├─";
                sb.AppendLine($"{prefix}{connector} {nodeInfo}");
            }

            // 构建子节点的前缀
            string childPrefix;
            if (isRoot)
            {
                childPrefix = "";
            }
            else
            {
                childPrefix = prefix + (isLast ? "   " : "│  ");
            }

            // 收集未被忽略的子节点（支持嵌套Prefab）
            List<Transform> validChildren = new List<Transform>();
            int childCount = gameObject.transform.childCount;
            for (int i = 0; i < childCount; i++)
            {
                Transform child = gameObject.transform.GetChild(i);
                if (!DescriptionFileManager.IsIgnoredWithNestedSupport(child.gameObject))
                {
                    validChildren.Add(child);
                }
            }

            // 递归处理未被忽略的子节点
            for (int i = 0; i < validChildren.Count; i++)
            {
                Transform child = validChildren[i];
                bool isChildLast = (i == validChildren.Count - 1);
                
                // 如果当前节点是嵌套Prefab根，子节点继续使用嵌套Prefab的路径
                string childPrefabPath = isNestedPrefabRoot ? nestedPrefabPath : currentPrefabPath;
                // parentPrefabPath 初始为 currentPrefabPath（根 Prefab）
                BuildTreeTextWithNestedSupportChild(child.gameObject, sb, childPrefix, isChildLast, childPrefabPath, currentPrefabPath, currentPrefabPath);
            }

            return true;
        }

        /// <summary>
        /// 递归构建子节点树形文本（支持嵌套Prefab）
        /// 导出完整的Prefab结构
        /// </summary>
        /// <param name="inheritedPrefabPath">当前节点应该从哪个 Prefab 获取原始注释</param>
        /// <param name="rootPrefabPath">最顶层的 Prefab 路径（导出起点）</param>
        /// <param name="parentPrefabPath">直接父级 Prefab 的路径（用于查找覆盖注释）</param>
        private static bool BuildTreeTextWithNestedSupportChild(GameObject gameObject, StringBuilder sb, string prefix, 
            bool isLast, string inheritedPrefabPath, string rootPrefabPath, string parentPrefabPath)
        {
            if (gameObject == null)
            {
                return false;
            }

            // 检查是否被忽略（支持嵌套Prefab）
            if (DescriptionFileManager.IsIgnoredWithNestedSupport(gameObject))
            {
                return false;
            }

            // 检查是否是嵌套Prefab的根节点
            string nestedPrefabPath = null;
            bool isNestedPrefabRoot = false;
            
            if (PrefabUtility.IsAnyPrefabInstanceRoot(gameObject))
            {
                // 使用 GetCorrespondingObjectFromOriginalSource 获取最原始的源对象
                GameObject sourcePrefab = PrefabUtility.GetCorrespondingObjectFromOriginalSource(gameObject);
                if (sourcePrefab != null)
                {
                    nestedPrefabPath = AssetDatabase.GetAssetPath(sourcePrefab);
                    if (!string.IsNullOrEmpty(nestedPrefabPath) && nestedPrefabPath != rootPrefabPath)
                    {
                        isNestedPrefabRoot = true;
                    }
                }
            }

            // 确定当前节点使用哪个Prefab路径获取描述
            string activePrefabPath = isNestedPrefabRoot ? nestedPrefabPath : inheritedPrefabPath;
            
            // 获取描述（使用统一的嵌套支持查找逻辑）
            string description = DescriptionFileManager.GetDescriptionWithNestedSupport(gameObject);

            // 构建当前节点的行
            string nodeInfo = BuildNodeInfo(gameObject, description, isNestedPrefabRoot, nestedPrefabPath);
            
            string connector = isLast ? "└─" : "├─";
            sb.AppendLine($"{prefix}{connector} {nodeInfo}");

            // 构建子节点的前缀
            string childPrefix = prefix + (isLast ? "   " : "│  ");

            // 收集未被忽略的子节点（支持嵌套Prefab）
            List<Transform> validChildren = new List<Transform>();
            int childCount = gameObject.transform.childCount;
            for (int i = 0; i < childCount; i++)
            {
                Transform child = gameObject.transform.GetChild(i);
                if (!DescriptionFileManager.IsIgnoredWithNestedSupport(child.gameObject))
                {
                    validChildren.Add(child);
                }
            }

            // 递归处理未被忽略的子节点
            for (int i = 0; i < validChildren.Count; i++)
            {
                Transform child = validChildren[i];
                bool isChildLast = (i == validChildren.Count - 1);
                
                // 如果当前节点是嵌套Prefab根，子节点的 parentPrefabPath 更新为当前的 inheritedPrefabPath
                string childPrefabPath = isNestedPrefabRoot ? nestedPrefabPath : activePrefabPath;
                string childParentPrefabPath = isNestedPrefabRoot ? inheritedPrefabPath : parentPrefabPath;
                BuildTreeTextWithNestedSupportChild(child.gameObject, sb, childPrefix, isChildLast, childPrefabPath, rootPrefabPath, childParentPrefabPath);
            }

            return true;
        }

        /// <summary>
        /// 递归构建树形文本（原始版本，保留兼容性）
        /// </summary>
        private static bool BuildTreeText(GameObject gameObject, StringBuilder sb, string prefix, bool isLast, bool isRoot,
            System.Func<GameObject, string> getDescription)
        {
            if (gameObject == null)
            {
                return false;
            }

            // 获取当前节点的描述
            string description = getDescription(gameObject);
            bool hasDescription = !string.IsNullOrEmpty(description);

            // 先递归检查子节点是否有描述
            List<int> childrenWithContent = new List<int>();
            for (int i = 0; i < gameObject.transform.childCount; i++)
            {
                Transform child = gameObject.transform.GetChild(i);
                if (HasDescriptionInTree(child.gameObject, getDescription))
                {
                    childrenWithContent.Add(i);
                }
            }

            // 如果当前节点没有描述，且没有需要导出的子节点，则跳过
            if (!hasDescription && childrenWithContent.Count == 0)
            {
                return false;
            }

            // 构建当前节点的行
            string nodeInfo = BuildNodeInfo(gameObject, description);
            
            if (isRoot)
            {
                // 根节点不需要连接符
                sb.AppendLine(nodeInfo);
            }
            else
            {
                string connector = isLast ? "└─" : "├─";
                sb.AppendLine($"{prefix}{connector} {nodeInfo}");
            }

            // 构建子节点的前缀
            string childPrefix;
            if (isRoot)
            {
                childPrefix = "";
            }
            else
            {
                childPrefix = prefix + (isLast ? "   " : "│  ");
            }

            // 递归处理有内容的子节点
            for (int i = 0; i < childrenWithContent.Count; i++)
            {
                int childIndex = childrenWithContent[i];
                Transform child = gameObject.transform.GetChild(childIndex);
                bool isChildLast = (i == childrenWithContent.Count - 1);
                BuildTreeText(child.gameObject, sb, childPrefix, isChildLast, false, getDescription);
            }

            return true;
        }

        /// <summary>
        /// 检查GameObject及其子节点是否有任何描述
        /// </summary>
        private static bool HasDescriptionInTree(GameObject gameObject, System.Func<GameObject, string> getDescription)
        {
            if (gameObject == null)
            {
                return false;
            }

            string description = getDescription(gameObject);
            if (!string.IsNullOrEmpty(description))
            {
                return true;
            }

            for (int i = 0; i < gameObject.transform.childCount; i++)
            {
                if (HasDescriptionInTree(gameObject.transform.GetChild(i).gameObject, getDescription))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 构建节点信息字符串
        /// 格式: NodeName (Component1, Component2) - 描述内容
        /// </summary>
        private static string BuildNodeInfo(GameObject gameObject, string description)
        {
            return BuildNodeInfo(gameObject, description, false, null);
        }

        /// <summary>
        /// 构建节点信息字符串（支持嵌套Prefab标记）
        /// 格式: NodeName (Component1, Component2) [嵌套Prefab: xxx.prefab] - 描述内容
        /// </summary>
        private static string BuildNodeInfo(GameObject gameObject, string description, bool isNestedPrefabRoot, string nestedPrefabPath)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(gameObject.name);

            // 获取组件列表（过滤掉基础组件）
            List<string> componentNames = new List<string>();
            Component[] components = gameObject.GetComponents<Component>();
            foreach (var comp in components)
            {
                if (comp == null) continue;
                string typeName = comp.GetType().Name;
                if (!FilteredComponents.Contains(typeName))
                {
                    componentNames.Add(typeName);
                }
            }

            // 添加组件信息
            if (componentNames.Count > 0)
            {
                sb.Append(" (");
                sb.Append(string.Join(", ", componentNames));
                sb.Append(")");
            }

            // 添加嵌套Prefab标记
            if (isNestedPrefabRoot && !string.IsNullOrEmpty(nestedPrefabPath))
            {
                string prefabName = System.IO.Path.GetFileName(nestedPrefabPath);
                sb.Append($" [{L.Export_NestedPrefabMark}: {prefabName}]");
            }

            // 添加描述
            if (!string.IsNullOrEmpty(description))
            {
                // 将多行描述合并为单行
                string singleLineDesc = description.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ");
                sb.Append($" - {L.Export_DescriptionPrefix}");
                sb.Append(singleLineDesc);
            }

            return sb.ToString();
        }

        #endregion
    }
}
