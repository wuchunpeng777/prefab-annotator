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

        // 缓存已加载的嵌套Prefab描述数据，避免重复加载
        private static Dictionary<string, DescriptionFileData> _nestedPrefabCache = new Dictionary<string, DescriptionFileData>();

        #region 右键菜单

        /// <summary>
        /// 导出选中GameObject的完整结构和描述到剪贴板（Prefab编辑模式中使用）
        /// </summary>
        [MenuItem("GameObject/Export Prefab Descriptions", false, 49)]
        private static void ExportToClipboard()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null)
            {
                return;
            }

            string result = ExportGameObjectToString(selected);
            if (string.IsNullOrEmpty(result))
            {
                EditorUtility.DisplayDialog(L.Export_DialogTitle, L.Export_CannotExportNode, L.Export_OK);
                return;
            }

            GUIUtility.systemCopyBuffer = result;
            Debug.Log($"[PrefabAnnotator] {L.Export_CopiedToClipboard}:\n{result}");
        }

        [MenuItem("GameObject/Export Prefab Descriptions", true)]
        private static bool ExportToClipboardValidate()
        {
            // 仅在Prefab编辑模式下启用
            return Selection.activeGameObject != null && 
                   PrefabStageUtility.GetCurrentPrefabStage() != null;
        }

        /// <summary>
        /// 从Project窗口导出选中Prefab的完整结构和描述到剪贴板
        /// </summary>
        [MenuItem("Assets/Export Prefab Descriptions", false, 1000)]
        private static void ExportPrefabAssetToClipboard()
        {
            string assetPath = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (string.IsNullOrEmpty(assetPath))
            {
                return;
            }

            // 加载 prefab 内容
            GameObject prefabContents = PrefabUtility.LoadPrefabContents(assetPath);
            if (prefabContents == null)
            {
                return;
            }

            try
            {
                string result = ExportPrefabAssetToString(assetPath, prefabContents);
                if (string.IsNullOrEmpty(result))
                {
                    EditorUtility.DisplayDialog(L.Export_DialogTitle, L.Export_CannotExportPrefab, L.Export_OK);
                    return;
                }

                GUIUtility.systemCopyBuffer = result;
                Debug.Log($"[PrefabAnnotator] {L.Export_CopiedToClipboard}:\n{result}");
            }
            finally
            {
                // 必须卸载 prefab 内容
                PrefabUtility.UnloadPrefabContents(prefabContents);
            }
        }

        [MenuItem("Assets/Export Prefab Descriptions", true)]
        private static bool ExportPrefabAssetToClipboardValidate()
        {
            // 检查选中的是否是Prefab资产
            if (Selection.activeObject == null)
            {
                return false;
            }

            string assetPath = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (string.IsNullOrEmpty(assetPath) || !assetPath.EndsWith(".prefab"))
            {
                return false;
            }

            return true;
        }

        #endregion

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

            // 清理缓存
            _nestedPrefabCache.Clear();

            StringBuilder sb = new StringBuilder();
            
            // 获取当前编辑的Prefab路径
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            string currentPrefabPath = prefabStage?.assetPath;
            
            BuildTreeTextWithNestedSupport(root, sb, "", true, true, currentPrefabPath);
            
            // 清理缓存
            _nestedPrefabCache.Clear();
            
            string result = sb.ToString().TrimEnd();
            return string.IsNullOrEmpty(result) ? null : result;
        }

        /// <summary>
        /// 导出Prefab资产为树形文本（用于Project窗口）
        /// 支持嵌套Prefab的注释导出
        /// 导出完整的Prefab结构
        /// </summary>
        public static string ExportPrefabAssetToString(string prefabAssetPath, GameObject root)
        {
            if (root == null || string.IsNullOrEmpty(prefabAssetPath))
            {
                return null;
            }

            // 清理缓存
            _nestedPrefabCache.Clear();

            StringBuilder sb = new StringBuilder();
            BuildTreeTextWithNestedSupportForAsset(root, sb, "", true, true, prefabAssetPath);
            
            // 清理缓存
            _nestedPrefabCache.Clear();
            
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
        /// 递归构建树形文本（支持嵌套Prefab，用于Project窗口的Prefab资产导出）
        /// 导出完整的Prefab结构
        /// </summary>
        private static bool BuildTreeTextWithNestedSupportForAsset(GameObject gameObject, StringBuilder sb, string prefix, 
            bool isLast, bool isRoot, string currentPrefabPath)
        {
            if (gameObject == null)
            {
                return false;
            }

            // 检查是否被忽略（支持嵌套Prefab）
            if (DescriptionFileManager.IsIgnoredFromAssetWithNestedSupport(currentPrefabPath, gameObject))
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
            string description = DescriptionFileManager.GetDescriptionFromAssetWithNestedSupport(currentPrefabPath, gameObject);

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
                if (!DescriptionFileManager.IsIgnoredFromAssetWithNestedSupport(currentPrefabPath, child.gameObject))
                {
                    validChildren.Add(child);
                }
            }

            // 递归处理未被忽略的子节点
            for (int i = 0; i < validChildren.Count; i++)
            {
                Transform child = validChildren[i];
                bool isChildLast = (i == validChildren.Count - 1);
                
                string childPrefabPath = isNestedPrefabRoot ? nestedPrefabPath : currentPrefabPath;
                // parentPrefabPath 初始为 currentPrefabPath（根 Prefab）
                BuildTreeTextWithNestedSupportChildForAsset(child.gameObject, sb, childPrefix, isChildLast, childPrefabPath, currentPrefabPath, currentPrefabPath);
            }

            return true;
        }

        /// <summary>
        /// 递归构建子节点树形文本（支持嵌套Prefab，用于Project窗口）
        /// 导出完整的Prefab结构
        /// </summary>
        /// <param name="inheritedPrefabPath">当前节点应该从哪个 Prefab 获取原始注释</param>
        /// <param name="rootPrefabPath">最顶层的 Prefab 路径（导出起点）</param>
        /// <param name="parentPrefabPath">直接父级 Prefab 的路径（用于查找覆盖注释）</param>
        private static bool BuildTreeTextWithNestedSupportChildForAsset(GameObject gameObject, StringBuilder sb, string prefix, 
            bool isLast, string inheritedPrefabPath, string rootPrefabPath, string parentPrefabPath)
        {
            if (gameObject == null)
            {
                return false;
            }

            // 检查是否被忽略（支持嵌套Prefab）
            if (DescriptionFileManager.IsIgnoredFromAssetWithNestedSupport(rootPrefabPath, gameObject))
            {
                return false;
            }

            // 检查是否是嵌套Prefab的根节点
            string nestedPrefabPath = null;
            bool isNestedPrefabRoot = false;
            
            if (PrefabUtility.IsAnyPrefabInstanceRoot(gameObject))
            {
                // 使用 GetCorrespondingObjectFromOriginalSource 获取最原始的源对象
                // 这对于多层嵌套很重要，因为 GetCorrespondingObjectFromSource 只返回直接父级
                GameObject originalSourcePrefab = PrefabUtility.GetCorrespondingObjectFromOriginalSource(gameObject);
                if (originalSourcePrefab != null)
                {
                    nestedPrefabPath = AssetDatabase.GetAssetPath(originalSourcePrefab);
                    Debug.Log($"[BuildChild] {gameObject.name}: IsAnyPrefabInstanceRoot=true, nestedPrefabPath={nestedPrefabPath}, rootPrefabPath={rootPrefabPath}");
                    if (!string.IsNullOrEmpty(nestedPrefabPath) && nestedPrefabPath != rootPrefabPath)
                    {
                        isNestedPrefabRoot = true;
                    }
                }
            }

            // 确定当前节点使用哪个Prefab路径获取描述
            string activePrefabPath = isNestedPrefabRoot ? nestedPrefabPath : inheritedPrefabPath;
            
            // 获取描述（使用统一的嵌套支持查找逻辑）
            string description = DescriptionFileManager.GetDescriptionFromAssetWithNestedSupport(rootPrefabPath, gameObject);

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
                if (!DescriptionFileManager.IsIgnoredFromAssetWithNestedSupport(rootPrefabPath, child.gameObject))
                {
                    validChildren.Add(child);
                }
            }

            // 递归处理未被忽略的子节点
            for (int i = 0; i < validChildren.Count; i++)
            {
                Transform child = validChildren[i];
                bool isChildLast = (i == validChildren.Count - 1);
                
                // 如果当前节点是嵌套 Prefab 根，子节点的 parentPrefabPath 更新为当前的 inheritedPrefabPath
                // 这样子节点会在正确的父级 Prefab 中查找覆盖注释
                string childPrefabPath = isNestedPrefabRoot ? nestedPrefabPath : activePrefabPath;
                string childParentPrefabPath = isNestedPrefabRoot ? inheritedPrefabPath : parentPrefabPath;
                BuildTreeTextWithNestedSupportChildForAsset(child.gameObject, sb, childPrefix, isChildLast, childPrefabPath, rootPrefabPath, childParentPrefabPath);
            }

            return true;
        }

        /// <summary>
        /// 检查GameObject及其子节点是否有任何描述（支持嵌套Prefab，用于Prefab编辑模式）
        /// </summary>
        private static bool HasDescriptionInTreeWithNestedSupport(GameObject gameObject, string rootPrefabPath)
        {
            if (gameObject == null)
            {
                return false;
            }

            // 使用统一的嵌套支持查找逻辑
            if (DescriptionFileManager.HasDescriptionWithNestedSupport(gameObject))
            {
                return true;
            }

            // 递归检查子节点
            for (int i = 0; i < gameObject.transform.childCount; i++)
            {
                if (HasDescriptionInTreeWithNestedSupport(gameObject.transform.GetChild(i).gameObject, rootPrefabPath))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 检查GameObject及其子节点是否有任何描述（支持嵌套Prefab，用于Project窗口）
        /// </summary>
        private static bool HasDescriptionInTreeWithNestedSupportForAsset(GameObject gameObject, string rootPrefabPath)
        {
            if (gameObject == null)
            {
                return false;
            }

            // 使用统一的嵌套支持查找逻辑
            if (DescriptionFileManager.HasDescriptionFromAssetWithNestedSupport(rootPrefabPath, gameObject))
            {
                return true;
            }

            // 递归检查子节点
            for (int i = 0; i < gameObject.transform.childCount; i++)
            {
                if (HasDescriptionInTreeWithNestedSupportForAsset(gameObject.transform.GetChild(i).gameObject, rootPrefabPath))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 获取嵌套Prefab中对象的描述（带缓存）
        /// 支持多层嵌套：会自动检测源对象实际所属的 Prefab
        /// </summary>
        private static string GetNestedPrefabDescription(string prefabPath, GameObject sourceObject)
        {
            if (string.IsNullOrEmpty(prefabPath) || sourceObject == null)
            {
                return string.Empty;
            }

            // 获取源对象实际所属的 Prefab 路径（对于多层嵌套很重要）
            string actualPrefabPath = AssetDatabase.GetAssetPath(sourceObject);
            if (string.IsNullOrEmpty(actualPrefabPath))
            {
                actualPrefabPath = prefabPath;
            }

            Debug.Log($"[GetNestedPrefabDescription] prefabPath={prefabPath}, actualPrefabPath={actualPrefabPath}, sourceObject={sourceObject.name}");

            // 使用缓存避免重复加载描述文件
            if (!_nestedPrefabCache.TryGetValue(actualPrefabPath, out DescriptionFileData data))
            {
                string descFilePath = DescriptionFileManager.GetDescriptionFilePathByAssetPath(actualPrefabPath);
                if (!string.IsNullOrEmpty(descFilePath))
                {
                    data = DescriptionFileManager.LoadDescriptionFile(descFilePath);
                    _nestedPrefabCache[actualPrefabPath] = data;
                }
                else
                {
                    return string.Empty;
                }
            }

            if (data == null)
            {
                return string.Empty;
            }

            // 获取源对象的GlobalId
            // 需要使用源对象的 localFileId 和实际所属 Prefab 的 GUID 来构建正确的 GlobalObjectId
            string globalIdString = GetGlobalIdForSourceObject(actualPrefabPath, sourceObject);
            
            Debug.Log($"[GetNestedPrefabDescription] globalIdString={globalIdString}");
            
            if (string.IsNullOrEmpty(globalIdString))
            {
                return string.Empty;
            }

            string desc = data.GetDescription(globalIdString);
            Debug.Log($"[GetNestedPrefabDescription] Found description: '{desc}'");
            return desc;
        }

        /// <summary>
        /// 获取嵌套Prefab节点的描述（优先级查找）
        /// 优先级：1. 当前Prefab的覆盖注释 2. 嵌套Prefab的原始注释
        /// 用于 Prefab 编辑模式
        /// </summary>
        /// <param name="gameObject">当前实例节点</param>
        /// <param name="nestedPrefabPath">嵌套Prefab的资产路径</param>
        /// <returns>描述文本</returns>
        private static string GetDescriptionWithOverride(GameObject gameObject, string nestedPrefabPath)
        {
            // 优先级1：检查当前Prefab的注释文件（覆盖注释）
            // 注意：这里使用 DescriptionFileManager.GetDescription，它会在当前 Prefab Stage 的文件中查找
            string overrideDescription = DescriptionFileManager.GetDescription(gameObject);
            if (!string.IsNullOrEmpty(overrideDescription))
            {
                Debug.Log($"[GetDescriptionWithOverride] Found override: {gameObject.name} = '{overrideDescription}'");
                return overrideDescription;
            }

            // 优先级2：检查嵌套Prefab的原始注释文件
            if (!string.IsNullOrEmpty(nestedPrefabPath))
            {
                // 使用 GetCorrespondingObjectFromOriginalSource 获取最原始的源对象
                GameObject sourceObject = PrefabUtility.GetCorrespondingObjectFromOriginalSource(gameObject);
                if (sourceObject != null)
                {
                    string desc = GetNestedPrefabDescription(nestedPrefabPath, sourceObject);
                    Debug.Log($"[GetDescriptionWithOverride] From nested: {gameObject.name}, sourceObject={sourceObject.name}, desc='{desc}'");
                    return desc;
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// 获取嵌套Prefab节点的描述（多层级优先级查找）
        /// 优先级：1. 父级Prefab中的覆盖注释 2. 嵌套Prefab的原始注释
        /// 用于 Prefab 编辑模式（多层嵌套场景）
        /// </summary>
        /// <param name="gameObject">当前实例节点</param>
        /// <param name="parentPrefabPath">父级Prefab路径（用于查找覆盖注释）</param>
        /// <param name="nestedPrefabPath">嵌套Prefab的资产路径（原始注释来源）</param>
        /// <returns>描述文本</returns>
        private static string GetDescriptionWithOverrideMultiLevel(GameObject gameObject, string parentPrefabPath, string nestedPrefabPath)
        {
            Debug.Log($"[GetDescriptionWithOverrideMultiLevel] {gameObject.name}: parentPrefabPath={parentPrefabPath}, nestedPrefabPath={nestedPrefabPath}");
            
            // 优先级1：检查当前 Prefab Stage 的注释文件（最高优先级覆盖）
            string currentOverride = DescriptionFileManager.GetDescription(gameObject);
            if (!string.IsNullOrEmpty(currentOverride))
            {
                Debug.Log($"[GetDescriptionWithOverrideMultiLevel] Found in current stage: '{currentOverride}'");
                return currentOverride;
            }

            // 优先级2：检查父级Prefab的注释文件（中间层覆盖注释）
            if (!string.IsNullOrEmpty(parentPrefabPath))
            {
                // 获取父级 Prefab 中此节点的覆盖注释
                // 需要找到此节点在父级 Prefab 中对应的对象
                GameObject parentSourceObject = PrefabUtility.GetCorrespondingObjectFromSource(gameObject);
                if (parentSourceObject != null)
                {
                    string parentDesc = DescriptionFileManager.GetDescriptionFromAsset(parentPrefabPath, parentSourceObject);
                    if (!string.IsNullOrEmpty(parentDesc))
                    {
                        Debug.Log($"[GetDescriptionWithOverrideMultiLevel] Found in parent prefab: '{parentDesc}'");
                        return parentDesc;
                    }
                }
            }

            // 优先级3：检查嵌套Prefab的原始注释文件
            if (!string.IsNullOrEmpty(nestedPrefabPath))
            {
                GameObject originalSource = PrefabUtility.GetCorrespondingObjectFromOriginalSource(gameObject);
                if (originalSource != null)
                {
                    string desc = GetNestedPrefabDescription(nestedPrefabPath, originalSource);
                    Debug.Log($"[GetDescriptionWithOverrideMultiLevel] From original nested: '{desc}'");
                    return desc;
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// 获取最原始的源对象（处理多层嵌套）
        /// 使用 GetCorrespondingObjectFromOriginalSource 获取最原始的源对象
        /// </summary>
        private static GameObject GetOriginalSourceObject(GameObject gameObject, string targetPrefabPath)
        {
            if (gameObject == null)
            {
                Debug.Log($"[GetOriginalSourceObject] gameObject is null");
                return null;
            }

            Debug.Log($"[GetOriginalSourceObject] Start: gameObject={gameObject.name}, targetPrefabPath={targetPrefabPath}");

            // 使用 GetCorrespondingObjectFromOriginalSource 直接获取最原始的源对象
            GameObject originalSource = PrefabUtility.GetCorrespondingObjectFromOriginalSource(gameObject);
            
            if (originalSource != null)
            {
                string sourcePrefabPath = AssetDatabase.GetAssetPath(originalSource);
                Debug.Log($"[GetOriginalSourceObject] originalSource={originalSource.name}, sourcePrefabPath={sourcePrefabPath}");
                return originalSource;
            }

            // 回退：尝试使用 GetCorrespondingObjectFromSource
            GameObject fallback = PrefabUtility.GetCorrespondingObjectFromSource(gameObject);
            Debug.Log($"[GetOriginalSourceObject] Using fallback: {fallback?.name}");
            return fallback;
        }

        /// <summary>
        /// 获取嵌套Prefab节点的描述（优先级查找）
        /// 优先级：1. 当前Prefab的覆盖注释 2. 嵌套Prefab的原始注释
        /// 用于 Project 窗口（通过 LoadPrefabContents 加载）
        /// </summary>
        /// <param name="gameObject">当前实例节点</param>
        /// <param name="rootPrefabPath">根Prefab的资产路径</param>
        /// <param name="nestedPrefabPath">嵌套Prefab的资产路径</param>
        /// <returns>描述文本</returns>
        private static string GetDescriptionWithOverrideForAsset(GameObject gameObject, string rootPrefabPath, string nestedPrefabPath)
        {
            // 优先级1：检查当前Prefab的注释文件（覆盖注释）
            // 需要使用实例对象的 GlobalObjectId 在根 Prefab 的注释文件中查找
            string overrideDescription = GetOverrideDescriptionForAsset(gameObject, rootPrefabPath);
            if (!string.IsNullOrEmpty(overrideDescription))
            {
                return overrideDescription;
            }

            // 优先级2：检查嵌套Prefab的原始注释文件
            if (!string.IsNullOrEmpty(nestedPrefabPath))
            {
                // 获取最原始的源对象（处理多层嵌套的情况）
                GameObject sourceObject = GetOriginalSourceObject(gameObject, nestedPrefabPath);
                if (sourceObject != null)
                {
                    return GetNestedPrefabDescription(nestedPrefabPath, sourceObject);
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// 从根Prefab的注释文件中获取覆盖注释（用于 Project 窗口）
        /// 会尝试多种可能的 GlobalObjectId 格式来查找
        /// </summary>
        private static string GetOverrideDescriptionForAsset(GameObject gameObject, string rootPrefabPath)
        {
            if (gameObject == null || string.IsNullOrEmpty(rootPrefabPath))
            {
                return string.Empty;
            }

            string descFilePath = DescriptionFileManager.GetDescriptionFilePathByAssetPath(rootPrefabPath);
            if (string.IsNullOrEmpty(descFilePath))
            {
                return string.Empty;
            }

            // 使用缓存
            if (!_nestedPrefabCache.TryGetValue(rootPrefabPath, out DescriptionFileData data))
            {
                data = DescriptionFileManager.LoadDescriptionFile(descFilePath);
                _nestedPrefabCache[rootPrefabPath] = data;
            }

            if (data == null)
            {
                return string.Empty;
            }

            // 获取实例对象的 GlobalObjectId
            GlobalObjectId globalId = GlobalObjectId.GetGlobalObjectIdSlow(gameObject);
            string globalIdString = globalId.ToString();
            
            // GlobalObjectId格式: GlobalObjectId_V1-{identifierType}-{assetGUID}-{targetObjectId}-{targetPrefabId}
            string[] parts = globalIdString.Split('-');
            if (parts.Length < 5)
            {
                return string.Empty;
            }

            string rootPrefabGuid = AssetDatabase.AssetPathToGUID(rootPrefabPath);
            if (string.IsNullOrEmpty(rootPrefabGuid))
            {
                return string.Empty;
            }

            // 尝试多种可能的 GlobalObjectId 格式来查找覆盖注释
            // 因为保存时可能使用不同的格式
            
            // 尝试1: 使用根 Prefab GUID + identifierType=2
            parts[1] = "2";
            parts[2] = rootPrefabGuid;
            string tryId1 = string.Join("-", parts);
            string desc = data.GetDescription(tryId1);
            if (!string.IsNullOrEmpty(desc))
            {
                return desc;
            }

            // 尝试2: 保持原始 GUID + identifierType=2
            GlobalObjectId originalId = GlobalObjectId.GetGlobalObjectIdSlow(gameObject);
            string originalIdStr = originalId.ToString();
            string[] originalParts = originalIdStr.Split('-');
            if (originalParts.Length >= 5)
            {
                originalParts[1] = "2";
                string tryId2 = string.Join("-", originalParts);
                desc = data.GetDescription(tryId2);
                if (!string.IsNullOrEmpty(desc))
                {
                    return desc;
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// 获取源对象的GlobalObjectId字符串
        /// 注意：描述文件中存储的是 identifierType=2（Asset对象）的格式
        /// 但通过 LoadPrefabContents 加载的对象会返回 identifierType=1（Scene对象）
        /// 需要进行转换
        /// </summary>
        private static string GetGlobalIdForSourceObject(string prefabPath, GameObject sourceObject)
        {
            if (sourceObject == null || string.IsNullOrEmpty(prefabPath))
            {
                return string.Empty;
            }

            // 获取源对象实际所属的 Prefab 的 GUID（不是传入的 prefabPath）
            // 这对于多层嵌套很重要，因为源对象可能属于更深层的 Prefab
            string actualPrefabPath = AssetDatabase.GetAssetPath(sourceObject);
            string prefabGuid;
            
            if (!string.IsNullOrEmpty(actualPrefabPath))
            {
                // 使用源对象实际所属的 Prefab 的 GUID
                prefabGuid = AssetDatabase.AssetPathToGUID(actualPrefabPath);
            }
            else
            {
                // 回退到传入的 prefabPath
                prefabGuid = AssetDatabase.AssetPathToGUID(prefabPath);
            }
            
            if (string.IsNullOrEmpty(prefabGuid))
            {
                return string.Empty;
            }

            // 使用 GetGlobalObjectIdSlow 获取源对象的 GlobalObjectId
            GlobalObjectId globalId = GlobalObjectId.GetGlobalObjectIdSlow(sourceObject);
            string globalIdString = globalId.ToString();
            
            // GlobalObjectId格式: GlobalObjectId_V1-{identifierType}-{assetGUID}-{targetObjectId}-{targetPrefabId}
            string[] parts = globalIdString.Split('-');
            if (parts.Length >= 5)
            {
                // 确保 identifierType 为 2（Asset对象），因为描述是在 Prefab 编辑模式下保存的
                // 通过 LoadPrefabContents 加载的对象会返回 identifierType=1，需要替换为 2
                parts[1] = "2";
                
                // 确保 GUID 正确（使用源对象实际所属的 Prefab 的 GUID）
                parts[2] = prefabGuid;
                
                string normalizedGlobalId = string.Join("-", parts);
                Debug.Log($"[GetGlobalIdForSourceObject] sourceObject={sourceObject.name}, actualPrefabPath={actualPrefabPath}, prefabGuid={prefabGuid}, normalizedGlobalId={normalizedGlobalId}");
                return normalizedGlobalId;
            }

            // 如果无法解析，尝试使用 AssetDatabase 获取 localFileIdentifier
            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sourceObject, out string guid, out long localId))
            {
                // 构建 GlobalObjectId 字符串
                // identifierType = 2 表示这是一个资产对象
                return $"GlobalObjectId_V1-2-{prefabGuid}-{localId}-0";
            }

            return string.Empty;
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
