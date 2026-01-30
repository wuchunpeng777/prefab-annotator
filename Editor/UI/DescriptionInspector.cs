using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEditor;
#if UNITY_2021_2_OR_NEWER
using UnityEditor.SceneManagement;
#else
using UnityEditor.Experimental.SceneManagement;
#endif
using PrefabAnnotator.Core;

namespace PrefabAnnotator.UI
{
    /// <summary>
    /// Inspector扩展 - 在GameObject的Inspector底部添加描述编辑区域
    /// 使用Editor.finishedDefaultHeaderGUI回调实现
    /// </summary>
    [InitializeOnLoad]
    public static class DescriptionInspector
    {
        private static string _currentDescription = string.Empty;
        private static GameObject _currentGameObject;
        private static bool _isEditing = false;
        private static GUIStyle _textAreaStyle;
        private static GUIStyle _labelStyle;
        private static GUIStyle _boxStyle;
        private static GUIStyle _buttonStyle;

        static DescriptionInspector()
        {
            // 注册Inspector GUI回调
            Editor.finishedDefaultHeaderGUI += OnPostHeaderGUI;
        }

        private static void OnPostHeaderGUI(Editor editor)
        {
            // 检查注释功能是否启用
            if (!DescriptionSettings.IsEnabled)
            {
                return;
            }

            // 只处理GameObject
            if (!(editor.target is GameObject gameObject))
            {
                return;
            }

            // 仅在Prefab编辑模式下生效（使用缓存）
            if (!DescriptionFileManager.IsInPrefabEditMode())
            {
                return;
            }

            // 检查选中的 GameObject 是否属于当前正在编辑的 Prefab Stage
            // 防止在 Project 面板中选中其他 Prefab 时也显示编辑 UI
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage == null || gameObject.scene != prefabStage.scene)
            {
                return;
            }

            // 检查是否选择了新的GameObject
            if (_currentGameObject != gameObject)
            {
                _currentGameObject = gameObject;
                // 使用缓存的方法获取描述
                DescriptionFileManager.TryGetDescriptionWithNestedSupport(gameObject, out _currentDescription);
                _isEditing = false;
            }

            InitStyles();

            EditorGUILayout.Space(5);

            // 展开按钮
            if (GUILayout.Button(Localization.Inspector_ExpandAnnotatedNodes, _buttonStyle))
            {
                ExpandAnnotatedHierarchy();
            }

            EditorGUILayout.Space(3);

            // 检查是否在忽略的子树中（支持嵌套Prefab，父节点被忽略）
            bool isCurrentNodeIgnored = DescriptionFileManager.IsIgnored(gameObject);
            bool isCurrentNodeIgnoredNested = DescriptionFileManager.IsIgnoredWithNestedSupport(gameObject);
            bool isParentIgnored = false;
            if (gameObject.transform.parent != null)
            {
                isParentIgnored = DescriptionFileManager.IsInIgnoredSubtreeWithNestedSupport(gameObject.transform.parent.gameObject);
            }
            // 如果嵌套Prefab中的节点被忽略但当前Prefab没有覆盖，显示为来自嵌套Prefab的忽略
            bool isIgnoredFromNestedPrefab = !isCurrentNodeIgnored && isCurrentNodeIgnoredNested;

            // 绘制描述区域
            EditorGUILayout.BeginVertical(_boxStyle);
            {
                // 忽略节点复选框（来自嵌套Prefab的忽略状态时禁用）
                EditorGUI.BeginDisabledGroup(isParentIgnored || isIgnoredFromNestedPrefab);
                {
                    EditorGUI.BeginChangeCheck();
                    bool newIgnored = EditorGUILayout.ToggleLeft(
                        Localization.Inspector_IgnoreNode, 
                        isCurrentNodeIgnored || isParentIgnored || isIgnoredFromNestedPrefab
                    );
                    if (EditorGUI.EndChangeCheck() && !isParentIgnored && !isIgnoredFromNestedPrefab)
                    {
                        DescriptionFileManager.SetIgnored(gameObject, newIgnored);
                        DescriptionFileManager.ClearNodeDescriptionCache();
                        EditorApplication.RepaintHierarchyWindow();
                    }
                }
                EditorGUI.EndDisabledGroup();

                // 如果父节点被忽略，显示提示
                if (isParentIgnored)
                {
                    EditorGUILayout.HelpBox(Localization.Inspector_ParentIgnoredHint, MessageType.Info);
                }
                // 如果来自嵌套Prefab的忽略状态，显示提示
                else if (isIgnoredFromNestedPrefab)
                {
                    EditorGUILayout.HelpBox(Localization.Inspector_NestedPrefabIgnoredHint, MessageType.Info);
                }
                // 如果当前节点被忽略，显示提示
                else if (isCurrentNodeIgnored)
                {
                    EditorGUILayout.HelpBox(Localization.Inspector_IgnoredHint, MessageType.Info);
                }
                // 正常显示描述编辑区域
                else
                {
                    EditorGUILayout.Space(5);
                    
                    // 标题行
                    EditorGUILayout.BeginHorizontal();
                    {
                        EditorGUILayout.LabelField(Localization.Inspector_DescriptionLabel, _labelStyle, GUILayout.Width(80));
                        
                        GUILayout.FlexibleSpace();

                        // 显示是否有描述的状态
                        bool hasDescription = !string.IsNullOrEmpty(_currentDescription);
                        if (hasDescription)
                        {
                            GUI.color = new Color(0.5f, 0.8f, 0.5f);
                            GUILayout.Label("●", GUILayout.Width(15));
                            GUI.color = Color.white;
                        }
                    }
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.Space(2);

                    // 描述文本框
                    EditorGUI.BeginChangeCheck();
                    
                    string newDescription = EditorGUILayout.TextArea(
                        _currentDescription, 
                        _textAreaStyle, 
                        GUILayout.MinHeight(60),
                        GUILayout.ExpandHeight(true)
                    );

                    if (EditorGUI.EndChangeCheck())
                    {
                        _currentDescription = newDescription;
                        _isEditing = true;
                    }

                    // 如果正在编辑，显示保存按钮
                    if (_isEditing)
                    {
                        EditorGUILayout.Space(2);
                        EditorGUILayout.BeginHorizontal();
                        {
                            GUILayout.FlexibleSpace();

                            if (GUILayout.Button(Localization.Inspector_Cancel, GUILayout.Width(60)))
                            {
                                DescriptionFileManager.TryGetDescriptionWithNestedSupport(gameObject, out _currentDescription);
                                _isEditing = false;
                                GUI.FocusControl(null);
                            }

                            if (GUILayout.Button(Localization.Inspector_Save, GUILayout.Width(60)))
                            {
                                SaveDescription(gameObject);
                            }
                        }
                        EditorGUILayout.EndHorizontal();
                    }

                    // 处理失去焦点时自动保存
                    if (_isEditing && Event.current.type == EventType.KeyDown)
                    {
                        if (Event.current.keyCode == KeyCode.Escape)
                        {
                            DescriptionFileManager.TryGetDescriptionWithNestedSupport(gameObject, out _currentDescription);
                            _isEditing = false;
                            GUI.FocusControl(null);
                            Event.current.Use();
                        }
                        else if (Event.current.keyCode == KeyCode.Return && Event.current.control)
                        {
                            SaveDescription(gameObject);
                            Event.current.Use();
                        }
                    }
                }
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(5);
        }

        private static void SaveDescription(GameObject gameObject)
        {
            DescriptionFileManager.SetDescription(gameObject, _currentDescription);
            _isEditing = false;
            GUI.FocusControl(null);

            // 刷新Hierarchy窗口以更新图标
            EditorApplication.RepaintHierarchyWindow();
        }

        private static void InitStyles()
        {
            if (_textAreaStyle == null)
            {
                _textAreaStyle = new GUIStyle(EditorStyles.textArea)
                {
                    wordWrap = true,
                    padding = new RectOffset(8, 8, 6, 6),
                    fontSize = 12
                };
            }

            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 12
                };
            }

            if (_boxStyle == null)
            {
                _boxStyle = new GUIStyle("box")
                {
                    padding = new RectOffset(10, 10, 8, 8),
                    margin = new RectOffset(0, 0, 0, 0)
                };
            }

            if (_buttonStyle == null)
            {
                _buttonStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 11,
                    padding = new RectOffset(10, 10, 5, 5)
                };
            }
        }

        /// <summary>
        /// 强制刷新当前显示的描述
        /// </summary>
        public static void RefreshCurrentDescription()
        {
            if (_currentGameObject != null)
            {
                // 先使节点缓存失效，再获取最新描述
                DescriptionFileManager.InvalidateNodeCache(_currentGameObject);
                DescriptionFileManager.TryGetDescriptionWithNestedSupport(_currentGameObject, out _currentDescription);
                _isEditing = false;
            }
        }

        #region 展开有注释的节点

        /// <summary>
        /// 展开所有有注释的节点层级，收缩没有注释的节点
        /// </summary>
        private static void ExpandAnnotatedHierarchy()
        {
            var prefabStage = DescriptionFileManager.GetCachedPrefabStage();
            if (prefabStage == null)
            {
                return;
            }

            GameObject root = prefabStage.prefabContentsRoot;
            if (root == null)
            {
                return;
            }

            // 收集需要展开的节点和有注释的节点
            HashSet<int> nodesToExpand = new HashSet<int>();
            List<GameObject> annotatedObjects = new List<GameObject>();
            CollectNodesToExpand(root.transform, nodesToExpand, annotatedObjects);

            // 获取 Hierarchy 窗口和 TreeView 控制器
            var hierarchyWindow = GetHierarchyWindow();
            if (hierarchyWindow == null)
            {
                Debug.LogWarning($"[PrefabAnnotator] {Localization.Warning_CannotGetHierarchyWindow}");
                ExpandAnnotatedHierarchyFallback(annotatedObjects);
                return;
            }

            var treeView = GetTreeViewController(hierarchyWindow);
            if (treeView == null)
            {
                Debug.LogWarning($"[PrefabAnnotator] {Localization.Warning_CannotGetTreeView}");
                ExpandAnnotatedHierarchyFallback(annotatedObjects);
                return;
            }

            // 收集 prefab 内容树中所有节点的 ID
            HashSet<int> prefabNodeIds = new HashSet<int>();
            CollectAllNodeIds(root.transform, prefabNodeIds);

            // 只收缩 prefab 内容中的节点（不影响环境节点如 Canvas (Environment)）
            CollapseNodes(treeView, prefabNodeIds);

            // 展开需要展开的节点
            foreach (int id in nodesToExpand)
            {
                SetExpanded(treeView, id, true);
            }

            // 刷新窗口
            EditorApplication.RepaintHierarchyWindow();
        }

        /// <summary>
        /// 备用方案：通过选择和 Ping 来展开有注释的节点
        /// </summary>
        private static void ExpandAnnotatedHierarchyFallback(List<GameObject> annotatedObjects)
        {
            if (annotatedObjects.Count == 0)
            {
                return;
            }

            // 保存当前选择
            var previousSelection = Selection.objects;

            // 依次 Ping 每个有注释的对象，这会自动展开其父节点
            foreach (var obj in annotatedObjects)
            {
                EditorGUIUtility.PingObject(obj);
            }

            // 恢复之前的选择
            Selection.objects = previousSelection;

            EditorApplication.RepaintHierarchyWindow();
        }

        /// <summary>
        /// 递归收集需要展开的节点（有注释的节点的所有父节点）
        /// </summary>
        private static bool CollectNodesToExpand(Transform transform, HashSet<int> nodesToExpand, List<GameObject> annotatedObjects = null)
        {
            bool hasAnnotationInSubtree = false;

            // 检查当前节点是否有注释
            if (DescriptionFileManager.HasDescriptionWithNestedSupport(transform.gameObject))
            {
                hasAnnotationInSubtree = true;
                annotatedObjects?.Add(transform.gameObject);
            }

            // 递归检查子节点
            for (int i = 0; i < transform.childCount; i++)
            {
                if (CollectNodesToExpand(transform.GetChild(i), nodesToExpand, annotatedObjects))
                {
                    hasAnnotationInSubtree = true;
                }
            }

            // 如果子树中有注释，当前节点需要展开（仅当有子节点时）
            if (hasAnnotationInSubtree && transform.childCount > 0)
            {
                nodesToExpand.Add(transform.gameObject.GetInstanceID());
            }

            return hasAnnotationInSubtree;
        }

        private static EditorWindow GetHierarchyWindow()
        {
            var hierarchyWindowType = typeof(Editor).Assembly.GetType("UnityEditor.SceneHierarchyWindow");
            if (hierarchyWindowType == null)
            {
                return null;
            }

            // 尝试获取最后交互的 Hierarchy 窗口
            var lastInteractedProperty = hierarchyWindowType.GetProperty(
                "lastInteractedHierarchyWindow", 
                BindingFlags.Static | BindingFlags.Public);
            
            if (lastInteractedProperty != null)
            {
                var lastInteracted = lastInteractedProperty.GetValue(null) as EditorWindow;
                if (lastInteracted != null)
                {
                    return lastInteracted;
                }
            }

            // 回退：获取所有窗口中的第一个
            var windows = Resources.FindObjectsOfTypeAll(hierarchyWindowType);
            return windows != null && windows.Length > 0 ? windows[0] as EditorWindow : null;
        }

        private static object GetTreeViewController(EditorWindow hierarchyWindow)
        {
            // 获取 sceneHierarchy 字段
            var windowType = hierarchyWindow.GetType();
            var sceneHierarchyField = windowType.GetField("m_SceneHierarchy", BindingFlags.Instance | BindingFlags.NonPublic);
            if (sceneHierarchyField == null)
            {
                Debug.LogWarning($"[PrefabAnnotator] {Localization.Warning_CannotFindField("m_SceneHierarchy")}");
                return null;
            }

            var sceneHierarchy = sceneHierarchyField.GetValue(hierarchyWindow);
            if (sceneHierarchy == null)
            {
                Debug.LogWarning($"[PrefabAnnotator] {Localization.Warning_FieldIsNull("m_SceneHierarchy")}");
                return null;
            }

            // 获取 treeView 字段
            var sceneHierarchyType = sceneHierarchy.GetType();
            var treeViewField = sceneHierarchyType.GetField("m_TreeView", BindingFlags.Instance | BindingFlags.NonPublic);
            if (treeViewField == null)
            {
                Debug.LogWarning($"[PrefabAnnotator] {Localization.Warning_CannotFindField("m_TreeView")}");
                return null;
            }

            var treeView = treeViewField.GetValue(sceneHierarchy);
            if (treeView == null)
            {
                // TreeView 可能尚未初始化，尝试调用 Init 方法
                var initMethod = sceneHierarchyType.GetMethod("Init", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (initMethod != null)
                {
                    try
                    {
                        initMethod.Invoke(sceneHierarchy, null);
                        treeView = treeViewField.GetValue(sceneHierarchy);
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"[PrefabAnnotator] {Localization.Warning_InitializationFailed(e.Message)}");
                    }
                }

                if (treeView == null)
                {
                    Debug.LogWarning($"[PrefabAnnotator] {Localization.Warning_TreeViewNotInitialized}");
                }
            }

            return treeView;
        }

        /// <summary>
        /// 递归收集所有节点的 InstanceID
        /// </summary>
        private static void CollectAllNodeIds(Transform transform, HashSet<int> nodeIds)
        {
            nodeIds.Add(transform.gameObject.GetInstanceID());
            
            for (int i = 0; i < transform.childCount; i++)
            {
                CollectAllNodeIds(transform.GetChild(i), nodeIds);
            }
        }

        /// <summary>
        /// 只收缩指定的节点集合
        /// </summary>
        private static void CollapseNodes(object treeView, HashSet<int> nodeIdsToCollapse)
        {
            if (treeView == null) return;

            var dataProperty = treeView.GetType().GetProperty("data", BindingFlags.Instance | BindingFlags.Public);
            if (dataProperty == null) return;

            var data = dataProperty.GetValue(treeView);
            if (data == null) return;

            // 获取所有展开的节点
            var getExpandedMethod = data.GetType().GetMethod("GetExpandedIDs", BindingFlags.Instance | BindingFlags.Public);
            if (getExpandedMethod == null) return;

            var expandedIds = getExpandedMethod.Invoke(data, null) as int[];
            if (expandedIds == null) return;

            // 只收缩属于 prefab 内容的节点
            var setExpandedMethod = data.GetType().GetMethod("SetExpanded", 
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(int), typeof(bool) },
                null);

            if (setExpandedMethod != null)
            {
                foreach (int id in expandedIds)
                {
                    // 只收缩 prefab 内容中的节点
                    if (nodeIdsToCollapse.Contains(id))
                    {
                        setExpandedMethod.Invoke(data, new object[] { id, false });
                    }
                }
            }
        }

        private static void SetExpanded(object treeView, int instanceID, bool expand)
        {
            if (treeView == null) return;

            var dataProperty = treeView.GetType().GetProperty("data", BindingFlags.Instance | BindingFlags.Public);
            if (dataProperty == null) return;

            var data = dataProperty.GetValue(treeView);
            if (data == null) return;

            var setExpandedMethod = data.GetType().GetMethod("SetExpanded",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(int), typeof(bool) },
                null);

            if (setExpandedMethod != null)
            {
                setExpandedMethod.Invoke(data, new object[] { instanceID, expand });
            }
        }

        #endregion
    }
}
