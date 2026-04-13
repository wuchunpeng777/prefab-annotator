using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
#if UNITY_2021_2_OR_NEWER
using UnityEditor.SceneManagement;
#else
using UnityEditor.Experimental.SceneManagement;
#endif
using Newtonsoft.Json;

namespace PrefabAnnotator.Core
{
    /// <summary>
    /// 描述文件管理器 - 负责读写.desc.json文件
    /// 文件命名规则: Assets/Prefabs/MyUI.prefab → Assets/Editor/Descriptions/Prefabs/MyUI.prefab.desc.json
    /// 仅支持Prefab编辑模式
    /// </summary>
    [InitializeOnLoad]
    public static class DescriptionFileManager
    {
        private const string DESC_FILE_EXTENSION = ".desc.json";
        private const string DESCRIPTIONS_FOLDER = "Assets/Editor/Descriptions";

        // ========== 缓存系统 ==========
        
        // 多文件缓存（文件路径 -> 描述数据）
        private static readonly Dictionary<string, DescriptionFileData> _fileCache = new Dictionary<string, DescriptionFileData>();
        
        // PrefabStage 缓存
        private static PrefabStage _cachedPrefabStage;
        private static string _cachedPrefabPath;
        private static string _cachedPrefabGuid;
        private static string _cachedDescFilePath;
        private static int _prefabStageCheckFrame = -1;
        
        // 依赖关系缓存（prefab路径 -> 排序后的依赖列表）
        private static readonly Dictionary<string, List<(string path, int depth)>> _dependencyCache = new Dictionary<string, List<(string, int)>>();
        
        // 节点描述缓存（用于 Hierarchy 绘制优化）
        // Key: instanceID, Value: (hasDescription, description)
        private static readonly Dictionary<int, (bool hasDesc, string desc)> _nodeDescriptionCache = new Dictionary<int, (bool, string)>();
        private static string _nodeDescriptionCachePrefabPath;
        
        // GlobalObjectId 碰撞检测（复制节点在未保存时 ID 相同）
        private static readonly HashSet<int> _duplicateIdInstances = new HashSet<int>();
        
        // 节点忽略状态缓存（用于 Hierarchy 绘制优化）
        // Key: instanceID, Value: isIgnored
        private static readonly Dictionary<int, bool> _nodeIgnoredCache = new Dictionary<int, bool>();
        // 节点是否在忽略子树中的缓存
        // Key: instanceID, Value: isInIgnoredSubtree
        private static readonly Dictionary<int, bool> _nodeInIgnoredSubtreeCache = new Dictionary<int, bool>();
        private static string _nodeIgnoredCachePrefabPath;
        
        // 兼容旧代码的缓存
        private static DescriptionFileData _cachedData;
        private static string _cachedFilePath;
        
        static DescriptionFileManager()
        {
            // 监听 PrefabStage 变化
            PrefabStage.prefabStageOpened += OnPrefabStageChanged;
            PrefabStage.prefabStageClosing += OnPrefabStageChanged;
#if UNITY_2020_1_OR_NEWER
            PrefabStage.prefabSaved += OnPrefabSaved;
#endif
            
            // 监听编辑器更新以检测帧变化
            EditorApplication.update += OnEditorUpdate;
            
            // 监听 Hierarchy 变化以检测节点复制/删除
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
        }
        
        private static void OnPrefabStageChanged(PrefabStage stage)
        {
            ClearAllCaches();
        }
        
        private static void OnPrefabSaved(GameObject savedPrefab)
        {
            _duplicateIdInstances.Clear();
            ClearNodeDescriptionCache();
        }
        
        private static void OnHierarchyChanged()
        {
            if (!DescriptionSettings.IsEnabled) return;
            
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage == null) return;
            
            ClearNodeDescriptionCache();
            DetectDuplicateGlobalIds(prefabStage);
        }
        
        private static void DetectDuplicateGlobalIds(PrefabStage prefabStage)
        {
            _duplicateIdInstances.Clear();
            var root = prefabStage.prefabContentsRoot;
            if (root == null) return;
            
            var allTransforms = root.GetComponentsInChildren<Transform>(true);
            var objects = new Object[allTransforms.Length];
            for (int i = 0; i < allTransforms.Length; i++)
                objects[i] = allTransforms[i].gameObject;
            
            var globalIds = new GlobalObjectId[objects.Length];
            GlobalObjectId.GetGlobalObjectIdsSlow(objects, globalIds);
            
            string cachedGuid = GetCachedPrefabGuid();
            
            // globalId string → index of first occurrence
            var seen = new Dictionary<string, int>();
            
            for (int i = 0; i < globalIds.Length; i++)
            {
                string idString = globalIds[i].ToString();
                
                if (!string.IsNullOrEmpty(cachedGuid))
                {
                    string[] parts = idString.Split('-');
                    if (parts.Length >= 5)
                    {
                        parts[2] = cachedGuid;
                        idString = string.Join("-", parts);
                    }
                }
                
                int instanceId = objects[i].GetInstanceID();
                
                if (seen.TryGetValue(idString, out int firstIndex))
                {
                    _duplicateIdInstances.Add(objects[firstIndex].GetInstanceID());
                    _duplicateIdInstances.Add(instanceId);
                }
                else
                {
                    seen[idString] = i;
                }
            }
        }
        
        /// <summary>
        /// 检查节点是否处于 GlobalObjectId 碰撞状态（复制后未保存）
        /// </summary>
        public static bool HasDuplicateGlobalId(GameObject gameObject)
        {
            if (gameObject == null) return false;
            return _duplicateIdInstances.Contains(gameObject.GetInstanceID());
        }
        
        private static void OnEditorUpdate()
        {
            // 每帧重置 PrefabStage 检查标记（只在帧开始时检查一次）
            if (_prefabStageCheckFrame != Time.frameCount)
            {
                _prefabStageCheckFrame = -1;
            }
        }

        /// <summary>
        /// 检查当前是否在Prefab编辑模式（带缓存）
        /// </summary>
        public static bool IsInPrefabEditMode()
        {
            UpdatePrefabStageCache();
            return _cachedPrefabStage != null;
        }
        
        /// <summary>
        /// 获取缓存的当前 PrefabStage
        /// </summary>
        public static PrefabStage GetCachedPrefabStage()
        {
            UpdatePrefabStageCache();
            return _cachedPrefabStage;
        }
        
        /// <summary>
        /// 获取缓存的当前 Prefab 路径
        /// </summary>
        public static string GetCachedPrefabPath()
        {
            UpdatePrefabStageCache();
            return _cachedPrefabPath;
        }
        
        /// <summary>
        /// 获取缓存的当前 Prefab GUID
        /// </summary>
        public static string GetCachedPrefabGuid()
        {
            UpdatePrefabStageCache();
            return _cachedPrefabGuid;
        }
        
        /// <summary>
        /// 更新 PrefabStage 缓存（每帧只执行一次）
        /// </summary>
        private static void UpdatePrefabStageCache()
        {
            int currentFrame = Time.frameCount;
            if (_prefabStageCheckFrame == currentFrame)
            {
                return; // 本帧已检查过
            }
            
            _prefabStageCheckFrame = currentFrame;
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            
            // 检查是否需要更新缓存
            if (prefabStage != _cachedPrefabStage)
            {
                _cachedPrefabStage = prefabStage;
                
                if (prefabStage != null)
                {
                    _cachedPrefabPath = prefabStage.assetPath;
                    _cachedPrefabGuid = AssetDatabase.AssetPathToGUID(_cachedPrefabPath);
                    _cachedDescFilePath = DESCRIPTIONS_FOLDER + "/" + _cachedPrefabGuid + DESC_FILE_EXTENSION;
                }
                else
                {
                    _cachedPrefabPath = null;
                    _cachedPrefabGuid = null;
                    _cachedDescFilePath = null;
                }
                
                // PrefabStage 变化时清除节点描述缓存
                _nodeDescriptionCache.Clear();
                _nodeDescriptionCachePrefabPath = null;
            }
        }

        /// <summary>
        /// 获取当前Prefab的描述文件路径（使用缓存）
        /// 使用Prefab的GUID作为文件名，确保移动/重命名Prefab时不影响已保存的数据
        /// </summary>
        /// <returns>描述文件路径，如果不在Prefab编辑模式则返回null</returns>
        public static string GetDescriptionFilePath()
        {
            UpdatePrefabStageCache();
            return _cachedDescFilePath;
        }

        /// <summary>
        /// 通过Prefab资产路径获取描述文件路径
        /// </summary>
        /// <param name="prefabAssetPath">Prefab资产路径</param>
        /// <returns>描述文件路径</returns>
        public static string GetDescriptionFilePathByAssetPath(string prefabAssetPath)
        {
            if (string.IsNullOrEmpty(prefabAssetPath))
            {
                return null;
            }

            string guid = AssetDatabase.AssetPathToGUID(prefabAssetPath);
            if (!string.IsNullOrEmpty(guid))
            {
                return DESCRIPTIONS_FOLDER + "/" + guid + DESC_FILE_EXTENSION;
            }
            return null;
        }

        /// <summary>
        /// 获取Prefab资产中指定GameObject的描述（不需要进入Prefab编辑模式）
        /// </summary>
        /// <param name="prefabAssetPath">Prefab资产路径</param>
        /// <param name="gameObject">Prefab中的GameObject</param>
        /// <returns>描述文本</returns>
        public static string GetDescriptionFromAsset(string prefabAssetPath, GameObject gameObject)
        {
            string filePath = GetDescriptionFilePathByAssetPath(prefabAssetPath);
            if (string.IsNullOrEmpty(filePath))
            {
                return string.Empty;
            }

            DescriptionFileData data = LoadDescriptionFile(filePath);
            foreach (string globalId in GetPossibleAssetGlobalIds(prefabAssetPath, gameObject))
            {
                string desc = data.GetDescription(globalId);
                if (!string.IsNullOrEmpty(desc))
                {
                    return desc;
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// 检查Prefab资产中指定GameObject是否有描述（不需要进入Prefab编辑模式）
        /// </summary>
        /// <param name="prefabAssetPath">Prefab资产路径</param>
        /// <param name="gameObject">Prefab中的GameObject</param>
        /// <returns>是否有描述</returns>
        public static bool HasDescriptionFromAsset(string prefabAssetPath, GameObject gameObject)
        {
            string filePath = GetDescriptionFilePathByAssetPath(prefabAssetPath);
            if (string.IsNullOrEmpty(filePath))
            {
                return false;
            }

            DescriptionFileData data = LoadDescriptionFile(filePath);
            foreach (string globalId in GetPossibleAssetGlobalIds(prefabAssetPath, gameObject))
            {
                if (data.HasDescription(globalId))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 获取描述（支持嵌套Prefab，用于从资产路径获取，不需要进入Prefab编辑模式）
        /// 优先级：覆盖描述（有 targetPrefabId）> 原始描述（无 targetPrefabId）
        /// </summary>
        /// <param name="rootPrefabPath">根Prefab的资产路径</param>
        /// <param name="gameObject">Prefab中的GameObject</param>
        /// <returns>描述文本</returns>
        public static string GetDescriptionFromAssetWithNestedSupport(string rootPrefabPath, GameObject gameObject)
        {
            if (gameObject == null || string.IsNullOrEmpty(rootPrefabPath))
            {
                return string.Empty;
            }

            // 优先检查根 Prefab 的描述文件中的覆盖描述
            string overrideDesc = GetDescriptionFromAsset(rootPrefabPath, gameObject);
            if (!string.IsNullOrEmpty(overrideDesc))
            {
                return overrideDesc;
            }

            // 获取对象的 localFileId
            GameObject originalSource = PrefabUtility.GetCorrespondingObjectFromOriginalSource(gameObject);
            if (originalSource == null)
            {
                originalSource = gameObject;
            }

            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(originalSource, out _, out long targetLocalId))
            {
                return string.Empty;
            }

            // 使用 prefab 依赖关系查找描述
            return FindDescriptionInDependencies(rootPrefabPath, targetLocalId);
        }

        /// <summary>
        /// 检查是否有描述（支持嵌套Prefab，用于从资产路径检查，不需要进入Prefab编辑模式）
        /// </summary>
        public static bool HasDescriptionFromAssetWithNestedSupport(string rootPrefabPath, GameObject gameObject)
        {
            return !string.IsNullOrEmpty(GetDescriptionFromAssetWithNestedSupport(rootPrefabPath, gameObject));
        }

        /// <summary>
        /// 获取GameObject所属的描述文件路径（仅Prefab编辑模式）
        /// </summary>
        /// <param name="gameObject">目标GameObject</param>
        /// <returns>描述文件路径，如果不在Prefab编辑模式则返回null</returns>
        public static string GetDescriptionFilePath(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return null;
            }

            return GetDescriptionFilePath();
        }

        /// <summary>
        /// 加载描述文件数据（使用多文件缓存）
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>描述数据，如果文件不存在则返回新的空数据</returns>
        public static DescriptionFileData LoadDescriptionFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return new DescriptionFileData();
            }

            // 使用多文件缓存
            if (_fileCache.TryGetValue(filePath, out var cachedData))
            {
                return cachedData;
            }

            DescriptionFileData data;
            
            if (!File.Exists(filePath))
            {
                data = new DescriptionFileData();
            }
            else
            {
                try
                {
                    string json = File.ReadAllText(filePath);
                    data = JsonConvert.DeserializeObject<DescriptionFileData>(json);
                    
                    // 确保反序列化后的数据不为null
                    if (data == null)
                    {
                        data = new DescriptionFileData();
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[PrefabAnnotator] 加载描述文件失败: {filePath}\n{e.Message}");
                    data = new DescriptionFileData();
                }
            }
            
            // 存入缓存
            _fileCache[filePath] = data;
            
            // 兼容旧代码
            _cachedData = data;
            _cachedFilePath = filePath;
            
            return data;
        }

        /// <summary>
        /// 保存描述文件数据
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="data">描述数据</param>
        public static void SaveDescriptionFile(string filePath, DescriptionFileData data)
        {
            if (string.IsNullOrEmpty(filePath) || data == null)
            {
                return;
            }

            try
            {
                // 如果没有任何描述，删除文件
                if (!data.HasAnyDescription())
                {
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                        
                        // 删除对应的meta文件
                        string metaPath = filePath + ".meta";
                        if (File.Exists(metaPath))
                        {
                            File.Delete(metaPath);
                        }
                        
                        AssetDatabase.Refresh();
                    }
                    // 清除该文件的缓存
                    _fileCache.Remove(filePath);
                    ClearNodeDescriptionCache();
                    return;
                }

                // 使用 Newtonsoft.Json 序列化，格式化输出
                string json = JsonConvert.SerializeObject(data, Formatting.Indented);
                
                // 确保目录存在
                string directory = Path.GetDirectoryName(filePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(filePath, json);
                
                // 更新缓存
                _fileCache[filePath] = data;
                _cachedData = data;
                _cachedFilePath = filePath;
                
                // 清除节点描述缓存（描述已更改）
                ClearNodeDescriptionCache();

                AssetDatabase.Refresh();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[PrefabAnnotator] 保存描述文件失败: {filePath}\n{e.Message}");
            }
        }

        /// <summary>
        /// 获取指定GameObject的描述
        /// </summary>
        /// <param name="gameObject">目标GameObject</param>
        /// <returns>描述文本</returns>
        public static string GetDescription(GameObject gameObject)
        {
            string filePath = GetDescriptionFilePath(gameObject);
            if (string.IsNullOrEmpty(filePath))
            {
                return string.Empty;
            }

            string globalId = NodeIdentifier.GetGlobalId(gameObject);
            if (string.IsNullOrEmpty(globalId))
            {
                return string.Empty;
            }

            DescriptionFileData data = LoadDescriptionFile(filePath);
            return data.GetDescription(globalId);
        }

        /// <summary>
        /// 设置指定GameObject的描述
        /// </summary>
        /// <param name="gameObject">目标GameObject</param>
        /// <param name="description">描述文本</param>
        public static void SetDescription(GameObject gameObject, string description)
        {
            string filePath = GetDescriptionFilePath(gameObject);
            if (string.IsNullOrEmpty(filePath))
            {
                Debug.LogWarning("[PrefabAnnotator] 无法确定描述文件路径，请确保在Prefab编辑模式中");
                return;
            }

            string globalId = NodeIdentifier.GetGlobalId(gameObject);
            if (string.IsNullOrEmpty(globalId))
            {
                Debug.LogWarning("[PrefabAnnotator] 无法获取GameObject的GlobalObjectId");
                return;
            }

            DescriptionFileData data = LoadDescriptionFile(filePath);
            data.SetDescription(globalId, description);
            SaveDescriptionFile(filePath, data);
        }

        /// <summary>
        /// 检查指定GameObject是否有描述
        /// </summary>
        /// <param name="gameObject">目标GameObject</param>
        /// <returns>是否有描述</returns>
        public static bool HasDescription(GameObject gameObject)
        {
            string filePath = GetDescriptionFilePath(gameObject);
            if (string.IsNullOrEmpty(filePath))
            {
                return false;
            }

            string globalId = NodeIdentifier.GetGlobalId(gameObject);
            if (string.IsNullOrEmpty(globalId))
            {
                return false;
            }

            DescriptionFileData data = LoadDescriptionFile(filePath);
            return data.HasDescription(globalId);
        }

        /// <summary>
        /// 检查指定GameObject是否被标记为忽略
        /// </summary>
        /// <param name="gameObject">目标GameObject</param>
        /// <returns>是否被忽略</returns>
        public static bool IsIgnored(GameObject gameObject)
        {
            string filePath = GetDescriptionFilePath(gameObject);
            if (string.IsNullOrEmpty(filePath))
            {
                return false;
            }

            string globalId = NodeIdentifier.GetGlobalId(gameObject);
            if (string.IsNullOrEmpty(globalId))
            {
                return false;
            }

            DescriptionFileData data = LoadDescriptionFile(filePath);
            return data.IsIgnored(globalId);
        }

        /// <summary>
        /// 设置指定GameObject的忽略状态
        /// </summary>
        /// <param name="gameObject">目标GameObject</param>
        /// <param name="ignored">是否忽略</param>
        public static void SetIgnored(GameObject gameObject, bool ignored)
        {
            string filePath = GetDescriptionFilePath(gameObject);
            if (string.IsNullOrEmpty(filePath))
            {
                Debug.LogWarning("[PrefabAnnotator] 无法确定描述文件路径，请确保在Prefab编辑模式中");
                return;
            }

            string globalId = NodeIdentifier.GetGlobalId(gameObject);
            if (string.IsNullOrEmpty(globalId))
            {
                Debug.LogWarning("[PrefabAnnotator] 无法获取GameObject的GlobalObjectId");
                return;
            }

            DescriptionFileData data = LoadDescriptionFile(filePath);
            data.SetIgnored(globalId, ignored);
            SaveDescriptionFile(filePath, data);
        }

        /// <summary>
        /// 检查节点或其任意父节点是否被标记为忽略
        /// </summary>
        /// <param name="gameObject">目标GameObject</param>
        /// <returns>是否在忽略的子树中</returns>
        public static bool IsInIgnoredSubtree(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return false;
            }

            Transform current = gameObject.transform;
            while (current != null)
            {
                if (IsIgnored(current.gameObject))
                {
                    return true;
                }
                current = current.parent;
            }

            return false;
        }

        /// <summary>
        /// 检查Prefab资产中指定GameObject是否被标记为忽略（不需要进入Prefab编辑模式）
        /// </summary>
        /// <param name="prefabAssetPath">Prefab资产路径</param>
        /// <param name="gameObject">Prefab中的GameObject</param>
        /// <returns>是否被忽略</returns>
        public static bool IsIgnoredFromAsset(string prefabAssetPath, GameObject gameObject)
        {
            string filePath = GetDescriptionFilePathByAssetPath(prefabAssetPath);
            if (string.IsNullOrEmpty(filePath))
            {
                return false;
            }

            DescriptionFileData data = LoadDescriptionFile(filePath);
            foreach (string globalId in GetPossibleAssetGlobalIds(prefabAssetPath, gameObject))
            {
                if (data.IsIgnored(globalId))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 检查是否被忽略（支持嵌套Prefab）
        /// 仅用于Prefab编辑模式
        /// 支持外部覆盖：外层Prefab可以覆盖嵌套Prefab的忽略状态
        /// </summary>
        public static bool IsIgnoredWithNestedSupport(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return false;
            }

            // 检查缓存
            int instanceId = gameObject.GetInstanceID();
            UpdatePrefabStageCache();
            
            // 验证缓存是否有效（同一个 prefab）
            if (_nodeIgnoredCachePrefabPath == _cachedPrefabPath && 
                _nodeIgnoredCache.TryGetValue(instanceId, out bool cachedResult))
            {
                return cachedResult;
            }
            
            // 缓存未命中，执行完整查询
            bool result = IsIgnoredWithNestedSupportInternal(gameObject);
            
            // 存入缓存
            if (_cachedPrefabPath != null)
            {
                if (_nodeIgnoredCachePrefabPath != _cachedPrefabPath)
                {
                    _nodeIgnoredCache.Clear();
                    _nodeInIgnoredSubtreeCache.Clear();
                    _nodeIgnoredCachePrefabPath = _cachedPrefabPath;
                }
                _nodeIgnoredCache[instanceId] = result;
            }
            
            return result;
        }
        
        /// <summary>
        /// 内部方法：检查是否被忽略（支持嵌套Prefab）
        /// </summary>
        private static bool IsIgnoredWithNestedSupportInternal(GameObject gameObject)
        {
            // 检查当前Prefab是否对此节点有覆盖设置（通过完整 GlobalId 匹配）
            bool? overrideIgnored = TryGetIgnoredOverride(gameObject);
            if (overrideIgnored.HasValue)
            {
                // 当前Prefab有覆盖设置，优先使用
                return overrideIgnored.Value;
            }

            // 获取原始源对象（在嵌套Prefab中的对应对象）
            GameObject originalSource = PrefabUtility.GetCorrespondingObjectFromOriginalSource(gameObject);
            if (originalSource == null || originalSource == gameObject)
            {
                // 不是来自嵌套Prefab的对象
                return false;
            }

            // 获取原始源对象所属的Prefab路径
            string sourcePrefabPath = AssetDatabase.GetAssetPath(originalSource);
            if (string.IsNullOrEmpty(sourcePrefabPath) || sourcePrefabPath == _cachedPrefabPath)
            {
                // 源对象属于当前Prefab，不需要额外检查
                return false;
            }

            // 获取原始对象的 localId，用于在依赖链中匹配覆盖设置
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(originalSource, out string originalGuid, out long originalLocalId))
            {
                return false;
            }

            // 检查依赖链中的覆盖设置（使用缓存）
            if (!string.IsNullOrEmpty(_cachedPrefabPath))
            {
                bool? chainOverride = FindOverrideInDependencyChain(_cachedPrefabPath, originalGuid, originalLocalId);
                if (chainOverride.HasValue)
                {
                    return chainOverride.Value;
                }
            }

            // 最后检查原始Prefab中的直接设置
            return IsIgnoredInOriginalPrefab(sourcePrefabPath, originalSource, originalLocalId);
        }

        /// <summary>
        /// 在依赖链中查找对目标节点的覆盖设置（targetPrefabId != 0）
        /// </summary>
        private static bool? FindOverrideInDependencyChain(string rootPrefabPath, string targetPrefabGuid, long targetLocalId)
        {
            string localIdStr = targetLocalId.ToString();
            var prefabsWithDepth = GetSortedDependencies(rootPrefabPath);
            
            foreach (var (prefabPath, _) in prefabsWithDepth)
            {
                // 跳过目标对象所属的原始 Prefab（那里的设置不是覆盖）
                string currentGuid = AssetDatabase.AssetPathToGUID(prefabPath);
                if (currentGuid == targetPrefabGuid)
                {
                    continue;
                }

                string filePath = GetDescriptionFilePathByAssetPath(prefabPath);
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                {
                    continue;
                }

                DescriptionFileData data = LoadDescriptionFile(filePath);
                
                foreach (var kvp in data.nodes)
                {
                    string[] parts = kvp.Key.Split('-');
                    // 查找覆盖设置：localId 匹配 且 targetPrefabId 非零
                    if (parts.Length >= 5 && parts[3] == localIdStr && parts[4] != "0")
                    {
                        // 找到覆盖设置
                        return kvp.Value.ignored;
                    }
                }
            }
            
            return null;  // 没有找到覆盖设置
        }

        /// <summary>
        /// 检查节点在其原始Prefab中是否被忽略（递归支持多层嵌套）
        /// 只匹配 targetPrefabId = 0 的直接设置
        /// </summary>
        private static bool IsIgnoredInOriginalPrefab(string prefabPath, GameObject sourceObject, long knownLocalId)
        {
            // 检查这个Prefab的描述文件中是否对此节点有设置
            string filePath = GetDescriptionFilePathByAssetPath(prefabPath);
            if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
            {
                DescriptionFileData data = LoadDescriptionFile(filePath);
                string prefabGuid = AssetDatabase.AssetPathToGUID(prefabPath);
                string localIdStr = knownLocalId.ToString();
                
                foreach (var kvp in data.nodes)
                {
                    string[] parts = kvp.Key.Split('-');
                    // 检查 GUID、localId 都匹配，且 targetPrefabId 为 "0"（表示是这个 Prefab 的直接节点）
                    if (parts.Length >= 5 && parts[2] == prefabGuid && parts[3] == localIdStr && parts[4] == "0")
                    {
                        return kvp.Value.ignored;
                    }
                }
            }

            // 如果这个Prefab中没有设置，检查是否有更深层的嵌套
            GameObject deeperSource = PrefabUtility.GetCorrespondingObjectFromOriginalSource(sourceObject);
            if (deeperSource != null && deeperSource != sourceObject)
            {
                string deeperPrefabPath = AssetDatabase.GetAssetPath(deeperSource);
                if (!string.IsNullOrEmpty(deeperPrefabPath) && deeperPrefabPath != prefabPath)
                {
                    if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(deeperSource, out _, out long deeperLocalId))
                    {
                        return IsIgnoredInOriginalPrefab(deeperPrefabPath, deeperSource, deeperLocalId);
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// 尝试获取当前Prefab中对节点的覆盖忽略设置
        /// 返回 null 表示没有覆盖设置
        /// </summary>
        private static bool? TryGetIgnoredOverride(GameObject gameObject)
        {
            string filePath = GetDescriptionFilePath(gameObject);
            if (string.IsNullOrEmpty(filePath))
            {
                return null;
            }

            string globalId = NodeIdentifier.GetGlobalId(gameObject);
            if (string.IsNullOrEmpty(globalId))
            {
                return null;
            }

            DescriptionFileData data = LoadDescriptionFile(filePath);
            
            // 检查节点是否存在于描述文件中
            if (data.nodes.TryGetValue(globalId, out var node))
            {
                // 节点存在，返回其忽略状态（无论 true 还是 false 都是覆盖）
                return node.ignored;
            }

            return null;  // 节点不存在，没有覆盖设置
        }

        /// <summary>
        /// 检查节点或其任意父节点是否被标记为忽略（支持嵌套Prefab）
        /// </summary>
        /// <param name="gameObject">目标GameObject</param>
        /// <returns>是否在忽略的子树中</returns>
        public static bool IsInIgnoredSubtreeWithNestedSupport(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return false;
            }

            // 检查缓存
            int instanceId = gameObject.GetInstanceID();
            UpdatePrefabStageCache();
            
            // 验证缓存是否有效（同一个 prefab）
            if (_nodeIgnoredCachePrefabPath == _cachedPrefabPath && 
                _nodeInIgnoredSubtreeCache.TryGetValue(instanceId, out bool cachedResult))
            {
                return cachedResult;
            }
            
            // 缓存未命中，执行完整查询
            bool result = false;
            Transform current = gameObject.transform;
            while (current != null)
            {
                if (IsIgnoredWithNestedSupport(current.gameObject))
                {
                    result = true;
                    break;
                }
                current = current.parent;
            }
            
            // 存入缓存
            if (_cachedPrefabPath != null)
            {
                if (_nodeIgnoredCachePrefabPath != _cachedPrefabPath)
                {
                    _nodeIgnoredCache.Clear();
                    _nodeInIgnoredSubtreeCache.Clear();
                    _nodeIgnoredCachePrefabPath = _cachedPrefabPath;
                }
                _nodeInIgnoredSubtreeCache[instanceId] = result;
            }

            return result;
        }

        /// <summary>
        /// 检查是否被忽略（支持嵌套Prefab，用于从资产路径检查，不需要进入Prefab编辑模式）
        /// 支持外部覆盖：外层Prefab可以覆盖嵌套Prefab的忽略状态
        /// </summary>
        public static bool IsIgnoredFromAssetWithNestedSupport(string rootPrefabPath, GameObject gameObject)
        {
            if (gameObject == null || string.IsNullOrEmpty(rootPrefabPath))
            {
                return false;
            }

            // 检查根 Prefab 的描述文件中是否对此节点有覆盖设置
            bool? overrideIgnored = TryGetIgnoredOverrideFromAsset(rootPrefabPath, gameObject);
            if (overrideIgnored.HasValue)
            {
                // 根Prefab有覆盖设置，优先使用
                return overrideIgnored.Value;
            }

            // 获取原始源对象（在嵌套Prefab中的对应对象）
            GameObject originalSource = PrefabUtility.GetCorrespondingObjectFromOriginalSource(gameObject);
            if (originalSource == null || originalSource == gameObject)
            {
                // 不是来自嵌套Prefab的对象
                return false;
            }

            // 获取原始源对象所属的Prefab路径
            string sourcePrefabPath = AssetDatabase.GetAssetPath(originalSource);
            if (string.IsNullOrEmpty(sourcePrefabPath) || sourcePrefabPath == rootPrefabPath)
            {
                // 源对象属于当前Prefab，不需要额外检查
                return false;
            }

            // 获取原始对象的 localId，用于在依赖链中匹配覆盖设置
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(originalSource, out string originalGuid, out long originalLocalId))
            {
                return false;
            }

            // 检查依赖链中的覆盖设置
            bool? chainOverride = FindOverrideInDependencyChain(rootPrefabPath, originalGuid, originalLocalId);
            if (chainOverride.HasValue)
            {
                return chainOverride.Value;
            }

            // 最后检查原始Prefab中的直接设置
            return IsIgnoredInOriginalPrefab(sourcePrefabPath, originalSource, originalLocalId);
        }

        /// <summary>
        /// 尝试从资产路径获取对节点的覆盖忽略设置
        /// 返回 null 表示没有覆盖设置
        /// </summary>
        private static bool? TryGetIgnoredOverrideFromAsset(string prefabAssetPath, GameObject gameObject)
        {
            string filePath = GetDescriptionFilePathByAssetPath(prefabAssetPath);
            if (string.IsNullOrEmpty(filePath))
            {
                return null;
            }

            DescriptionFileData data = LoadDescriptionFile(filePath);
            
            // 尝试多种可能的 GlobalId 格式
            foreach (string globalId in GetPossibleAssetGlobalIds(prefabAssetPath, gameObject))
            {
                if (data.nodes.TryGetValue(globalId, out var node))
                {
                    // 节点存在，返回其忽略状态
                    return node.ignored;
                }
            }

            return null;  // 节点不存在，没有覆盖设置
        }

        /// <summary>
        /// 获取描述（支持嵌套Prefab：优先覆盖注释，其次嵌套Prefab原始注释）
        /// 仅用于Prefab编辑模式
        /// </summary>
        public static string GetDescriptionWithNestedSupport(GameObject gameObject)
        {
            TryGetDescriptionWithNestedSupport(gameObject, out string description);
            return description;
        }

        /// <summary>
        /// 检查是否有描述（支持嵌套Prefab）
        /// </summary>
        public static bool HasDescriptionWithNestedSupport(GameObject gameObject)
        {
            return TryGetDescriptionWithNestedSupport(gameObject, out _);
        }
        
        /// <summary>
        /// 尝试获取描述（支持嵌套Prefab，一次调用完成检查和获取）
        /// 用于 Hierarchy 绘制优化，避免重复调用
        /// </summary>
        /// <param name="gameObject">目标GameObject</param>
        /// <param name="description">输出的描述文本</param>
        /// <returns>是否有描述</returns>
        public static bool TryGetDescriptionWithNestedSupport(GameObject gameObject, out string description)
        {
            description = string.Empty;
            
            if (gameObject == null)
            {
                return false;
            }
            
            // 检查节点描述缓存
            int instanceId = gameObject.GetInstanceID();
            UpdatePrefabStageCache();
            
            // 验证缓存是否有效（同一个 prefab）
            if (_nodeDescriptionCachePrefabPath == _cachedPrefabPath && 
                _nodeDescriptionCache.TryGetValue(instanceId, out var cached))
            {
                description = cached.desc;
                return cached.hasDesc;
            }
            
            // 缓存未命中，执行完整查询
            description = GetDescriptionWithNestedSupportInternal(gameObject);
            bool hasDesc = !string.IsNullOrEmpty(description);
            
            // 存入缓存
            if (_cachedPrefabPath != null)
            {
                if (_nodeDescriptionCachePrefabPath != _cachedPrefabPath)
                {
                    _nodeDescriptionCache.Clear();
                    _nodeDescriptionCachePrefabPath = _cachedPrefabPath;
                }
                _nodeDescriptionCache[instanceId] = (hasDesc, description);
            }
            
            return hasDesc;
        }
        
        /// <summary>
        /// 内部方法：获取描述（支持嵌套Prefab）
        /// </summary>
        private static string GetDescriptionWithNestedSupportInternal(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return string.Empty;
            }

            // 优先读取当前Prefab的覆盖注释
            string overrideDescription = GetDescription(gameObject);
            if (!string.IsNullOrEmpty(overrideDescription))
            {
                return overrideDescription;
            }

            // 获取对象的 localFileId，用于在嵌套 prefab 中匹配
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    PrefabUtility.GetCorrespondingObjectFromOriginalSource(gameObject) ?? gameObject,
                    out _, out long targetLocalId))
            {
                return string.Empty;
            }

            // 使用缓存的 prefab 路径
            if (string.IsNullOrEmpty(_cachedPrefabPath))
            {
                return string.Empty;
            }

            return FindDescriptionInDependencies(_cachedPrefabPath, targetLocalId);
        }

        /// <summary>
        /// 在 prefab 依赖链中查找描述（使用缓存）
        /// 优先返回覆盖描述（有 targetPrefabId），其次返回原始描述
        /// </summary>
        private static string FindDescriptionInDependencies(string currentPrefabPath, long targetLocalId)
        {
            string localIdStr = targetLocalId.ToString();
            string originalDescription = null;
            
            // 获取排序后的依赖列表（使用缓存）
            var prefabsWithDepth = GetSortedDependencies(currentPrefabPath);
            
            // 遍历所有依赖的 prefab，查找描述
            foreach (var (prefabPath, _) in prefabsWithDepth)
            {
                string filePath = GetDescriptionFilePathByAssetPath(prefabPath);
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                {
                    continue;
                }

                DescriptionFileData data = LoadDescriptionFile(filePath);
                
                foreach (var kvp in data.nodes)
                {
                    // GlobalObjectId 格式: GlobalObjectId_V1-{type}-{guid}-{localId}-{prefabId}
                    string[] parts = kvp.Key.Split('-');
                    if (parts.Length >= 5 && parts[3] == localIdStr)
                    {
                        if (string.IsNullOrEmpty(kvp.Value.description))
                        {
                            continue;
                        }

                        // parts[4] 是 targetPrefabId
                        // 如果不是 "0"，说明这是对嵌套 prefab 中节点的覆盖描述
                        bool isOverride = parts[4] != "0";
                        
                        if (isOverride)
                        {
                            // 覆盖描述，立即返回（优先级最高）
                            return kvp.Value.description;
                        }
                        else if (originalDescription == null)
                        {
                            // 保存原始描述作为备选
                            originalDescription = kvp.Value.description;
                        }
                    }
                }
            }
            
            return originalDescription ?? string.Empty;
        }
        
        /// <summary>
        /// 获取排序后的依赖列表（使用缓存）
        /// </summary>
        private static List<(string path, int depth)> GetSortedDependencies(string prefabPath)
        {
            // 检查缓存
            if (_dependencyCache.TryGetValue(prefabPath, out var cached))
            {
                return cached;
            }
            
            // 获取当前 prefab 的所有 prefab 依赖
            string[] allDependencies = AssetDatabase.GetDependencies(prefabPath, true);
            
            // 按依赖深度排序
            var prefabsWithDepth = new List<(string path, int depth)>();
            
            foreach (string dep in allDependencies)
            {
                if (dep.EndsWith(".prefab") && dep != prefabPath)
                {
                    int depth = CalculateDependencyDepth(prefabPath, dep);
                    prefabsWithDepth.Add((dep, depth));
                }
            }
            
            // 按深度排序（深度小的优先，即直接依赖优先于间接依赖）
            prefabsWithDepth.Sort((a, b) => a.depth.CompareTo(b.depth));
            
            // 存入缓存
            _dependencyCache[prefabPath] = prefabsWithDepth;
            
            return prefabsWithDepth;
        }

        /// <summary>
        /// 计算从 rootPrefab 到 targetPrefab 的依赖深度
        /// </summary>
        private static int CalculateDependencyDepth(string rootPrefab, string targetPrefab)
        {
            // 检查是否是直接依赖
            string[] directDeps = AssetDatabase.GetDependencies(rootPrefab, false);
            foreach (string dep in directDeps)
            {
                if (dep == targetPrefab)
                {
                    return 1;
                }
            }
            
            // 递归检查间接依赖
            foreach (string dep in directDeps)
            {
                if (dep.EndsWith(".prefab") && dep != rootPrefab)
                {
                    string[] subDeps = AssetDatabase.GetDependencies(dep, true);
                    foreach (string subDep in subDeps)
                    {
                        if (subDep == targetPrefab)
                        {
                            return 2 + CalculateDependencyDepth(dep, targetPrefab);
                        }
                    }
                }
            }
            
            return int.MaxValue;
        }

        private static bool TryGetNestedPrefabSource(GameObject gameObject, out GameObject sourceObject, out string nestedPrefabPath)
        {
            sourceObject = null;
            nestedPrefabPath = null;

            if (gameObject == null || !IsInPrefabEditMode())
            {
                return false;
            }

            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage == null)
            {
                return false;
            }

            // 使用最原始源对象，确保多层嵌套也能定位到正确Prefab
            sourceObject = PrefabUtility.GetCorrespondingObjectFromOriginalSource(gameObject);
            if (sourceObject == null)
            {
                return false;
            }

            nestedPrefabPath = AssetDatabase.GetAssetPath(sourceObject);
            if (string.IsNullOrEmpty(nestedPrefabPath))
            {
                return false;
            }

            // 如果源对象来自当前Prefab自身，不视为嵌套
            if (nestedPrefabPath == prefabStage.assetPath)
            {
                return false;
            }

            return true;
        }

        private static List<string> GetPossibleAssetGlobalIds(string prefabAssetPath, GameObject gameObject)
        {
            List<string> result = new List<string>();
            if (gameObject == null || string.IsNullOrEmpty(prefabAssetPath))
            {
                return result;
            }

            string prefabGuid = AssetDatabase.AssetPathToGUID(prefabAssetPath);
            if (string.IsNullOrEmpty(prefabGuid))
            {
                return result;
            }

            // 优先使用 localFileId 构建稳定的资产 GlobalObjectId
            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(gameObject, out _, out long localId))
            {
                result.Add($"GlobalObjectId_V1-2-{prefabGuid}-{localId}-0");
            }

            // 回退：使用 GlobalObjectId，但强制 identifierType=2 且 GUID=Prefab GUID
            GlobalObjectId globalId = GlobalObjectId.GetGlobalObjectIdSlow(gameObject);
            string globalIdString = globalId.ToString();
            string[] parts = globalIdString.Split('-');
            if (parts.Length >= 5)
            {
                parts[1] = "2";
                parts[2] = prefabGuid;
                string normalized = string.Join("-", parts);
                if (!result.Contains(normalized))
                {
                    result.Add(normalized);
                }
            }

            return result;
        }

        /// <summary>
        /// 清除缓存（兼容旧代码）
        /// </summary>
        public static void ClearCache()
        {
            _cachedData = null;
            _cachedFilePath = null;
            _fileCache.Clear();
            ClearNodeDescriptionCache();
        }
        
        /// <summary>
        /// 清除所有缓存
        /// </summary>
        public static void ClearAllCaches()
        {
            _fileCache.Clear();
            _dependencyCache.Clear();
            _nodeDescriptionCache.Clear();
            _nodeDescriptionCachePrefabPath = null;
            _nodeIgnoredCache.Clear();
            _nodeInIgnoredSubtreeCache.Clear();
            _nodeIgnoredCachePrefabPath = null;
            _duplicateIdInstances.Clear();
            _cachedData = null;
            _cachedFilePath = null;
            _cachedPrefabStage = null;
            _cachedPrefabPath = null;
            _cachedPrefabGuid = null;
            _cachedDescFilePath = null;
            _prefabStageCheckFrame = -1;
        }
        
        /// <summary>
        /// 清除节点描述缓存
        /// </summary>
        public static void ClearNodeDescriptionCache()
        {
            _nodeDescriptionCache.Clear();
            _nodeDescriptionCachePrefabPath = null;
            _nodeIgnoredCache.Clear();
            _nodeInIgnoredSubtreeCache.Clear();
            _nodeIgnoredCachePrefabPath = null;
        }
        
        /// <summary>
        /// 使指定节点的缓存失效
        /// </summary>
        public static void InvalidateNodeCache(GameObject gameObject)
        {
            if (gameObject != null)
            {
                int instanceId = gameObject.GetInstanceID();
                _nodeDescriptionCache.Remove(instanceId);
                _nodeIgnoredCache.Remove(instanceId);
                _nodeInIgnoredSubtreeCache.Remove(instanceId);
            }
        }

        /// <summary>
        /// 强制重新加载当前文件
        /// </summary>
        public static void ReloadCurrentFile()
        {
            string currentPath = _cachedFilePath;
            ClearCache();
            if (!string.IsNullOrEmpty(currentPath))
            {
                LoadDescriptionFile(currentPath);
            }
        }

    }
}
