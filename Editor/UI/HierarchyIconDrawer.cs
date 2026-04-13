using UnityEngine;
using UnityEditor;
using PrefabAnnotator.Core;

namespace PrefabAnnotator.UI
{
    /// <summary>
    /// Hierarchy窗口图标绘制器 - 为有描述的节点显示小图标
    /// 使用EditorApplication.hierarchyWindowItemOnGUI回调实现
    /// </summary>
    [InitializeOnLoad]
    public static class HierarchyIconDrawer
    {
        private static Texture2D _descriptionIcon;
        private static Texture2D _ignoredIcon;
        private static Texture2D _warningIcon;
        private static GUIStyle _tooltipStyle;
        private static readonly Color IconColor = new Color(0.4f, 0.7f, 1f, 0.9f);
        private static readonly Color IgnoredIconColor = new Color(0.7f, 0.7f, 0.7f, 0.7f);
        private static readonly Color WarningIconColor = new Color(1f, 0.85f, 0.2f, 0.95f);

        static HierarchyIconDrawer()
        {
            EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyWindowItemOnGUI;
        }

        private static void OnHierarchyWindowItemOnGUI(int instanceID, Rect selectionRect)
        {
            // 检查注释功能是否启用
            if (!DescriptionSettings.IsEnabled)
            {
                return;
            }

            // 仅在Prefab编辑模式下生效（使用缓存，每帧只检查一次）
            if (!DescriptionFileManager.IsInPrefabEditMode())
            {
                return;
            }

            // 获取对应的GameObject
            GameObject gameObject = EditorUtility.InstanceIDToObject(instanceID) as GameObject;
            if (gameObject == null)
            {
                return;
            }

            // 计算图标位置（在行的右侧）
            float iconSize = 16f;
            Rect iconRect = new Rect(
                selectionRect.xMax - iconSize - 2,
                selectionRect.y + (selectionRect.height - iconSize) / 2,
                iconSize,
                iconSize
            );

            // 碰撞状态的节点显示黄色警告图标
            if (DescriptionFileManager.HasDuplicateGlobalId(gameObject))
            {
                DrawWarningIcon(iconRect, Localization.Inspector_DuplicateIdHint);
                return;
            }

            // 检查是否被忽略（支持嵌套Prefab，当前节点直接被标记）
            if (DescriptionFileManager.IsIgnoredWithNestedSupport(gameObject))
            {
                DrawIgnoredIcon(iconRect, Localization.Inspector_IgnoredHint);
                return;
            }

            // 检查父节点是否被忽略（支持嵌套Prefab）
            if (gameObject.transform.parent != null && 
                DescriptionFileManager.IsInIgnoredSubtreeWithNestedSupport(gameObject.transform.parent.gameObject))
            {
                // 父节点被忽略，不显示任何图标
                return;
            }

            // 使用合并的方法一次完成检查和获取（避免重复调用）
            if (!DescriptionFileManager.TryGetDescriptionWithNestedSupport(gameObject, out string description))
            {
                return;
            }

            // 绘制描述图标
            DrawDescriptionIcon(iconRect, description);
        }

        private static void DrawDescriptionIcon(Rect rect, string tooltip)
        {
            // 创建或获取图标
            if (_descriptionIcon == null)
            {
                _descriptionIcon = CreateDescriptionIcon();
            }

            // 设置tooltip
            GUI.color = IconColor;
            GUIContent content = new GUIContent(_descriptionIcon, tooltip);
            GUI.Label(rect, content);
            GUI.color = Color.white;

            // 如果鼠标悬停在图标上，显示更详细的tooltip
            if (rect.Contains(Event.current.mousePosition))
            {
                // 使用自定义tooltip样式显示描述
                ShowTooltip(rect, tooltip);
            }
        }

        private static void DrawWarningIcon(Rect rect, string tooltip)
        {
            if (_warningIcon == null)
            {
                _warningIcon = CreateWarningIcon();
            }

            GUI.color = WarningIconColor;
            GUIContent content = new GUIContent(_warningIcon, tooltip);
            GUI.Label(rect, content);
            GUI.color = Color.white;
        }

        private static void DrawIgnoredIcon(Rect rect, string tooltip)
        {
            // 创建或获取忽略图标
            if (_ignoredIcon == null)
            {
                _ignoredIcon = CreateIgnoredIcon();
            }

            // 设置tooltip
            GUI.color = IgnoredIconColor;
            GUIContent content = new GUIContent(_ignoredIcon, tooltip);
            GUI.Label(rect, content);
            GUI.color = Color.white;
        }

        private static void ShowTooltip(Rect iconRect, string description)
        {
            if (string.IsNullOrEmpty(description))
            {
                return;
            }

            if (_tooltipStyle == null)
            {
                _tooltipStyle = new GUIStyle("box")
                {
                    wordWrap = true,
                    alignment = TextAnchor.UpperLeft,
                    padding = new RectOffset(8, 8, 6, 6),
                    fontSize = 11,
                    normal = { textColor = Color.white }
                };
            }

            // 计算tooltip大小
            GUIContent content = new GUIContent(description);
            float maxWidth = 300f;
            float height = _tooltipStyle.CalcHeight(content, maxWidth);
            height = Mathf.Min(height, 200f); // 限制最大高度

            // 计算tooltip位置
            Rect tooltipRect = new Rect(
                iconRect.x - maxWidth - 5,
                iconRect.y,
                maxWidth,
                height
            );

            // 确保tooltip不会超出屏幕
            if (tooltipRect.x < 0)
            {
                tooltipRect.x = iconRect.xMax + 5;
            }

            // 绘制背景
            EditorGUI.DrawRect(tooltipRect, new Color(0.2f, 0.2f, 0.2f, 0.95f));
            
            // 绘制边框
            DrawRectBorder(tooltipRect, new Color(0.4f, 0.7f, 1f, 0.5f));

            // 绘制文本
            GUI.Label(tooltipRect, description, _tooltipStyle);
        }

        private static void DrawRectBorder(Rect rect, Color color)
        {
            // 上边
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1), color);
            // 下边
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1, rect.width, 1), color);
            // 左边
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1, rect.height), color);
            // 右边
            EditorGUI.DrawRect(new Rect(rect.xMax - 1, rect.y, 1, rect.height), color);
        }

        /// <summary>
        /// 创建描述图标（一个简单的文档/注释图标）
        /// </summary>
        private static Texture2D CreateDescriptionIcon()
        {
            int size = 16;
            Texture2D texture = new Texture2D(size, size, TextureFormat.ARGB32, false);
            texture.filterMode = FilterMode.Bilinear;

            // 清除为透明
            Color[] pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Color.clear;
            }

            // 绘制一个简单的文档图标
            Color iconColor = Color.white;
            Color shadowColor = new Color(0, 0, 0, 0.3f);

            // 文档主体（圆角矩形近似）
            for (int y = 2; y < 14; y++)
            {
                for (int x = 3; x < 13; x++)
                {
                    // 简单的圆角处理
                    bool isCorner = (x == 3 && y == 2) || (x == 12 && y == 2) ||
                                   (x == 3 && y == 13) || (x == 12 && y == 13);
                    
                    if (!isCorner)
                    {
                        // 绘制阴影
                        if (y < 13 && x < 12)
                        {
                            SetPixelSafe(pixels, size, x + 1, y - 1, shadowColor);
                        }
                        SetPixelSafe(pixels, size, x, y, iconColor);
                    }
                }
            }

            // 绘制文字线条
            Color lineColor = new Color(0.3f, 0.6f, 0.9f, 1f);
            for (int lineY = 4; lineY < 12; lineY += 2)
            {
                for (int x = 5; x < 11; x++)
                {
                    if (lineY < 10 || x < 9) // 最后一行短一些
                    {
                        SetPixelSafe(pixels, size, x, lineY, lineColor);
                    }
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();

            return texture;
        }

        /// <summary>
        /// 创建忽略图标（一个禁止/斜线图标）
        /// </summary>
        private static Texture2D CreateIgnoredIcon()
        {
            int size = 16;
            Texture2D texture = new Texture2D(size, size, TextureFormat.ARGB32, false);
            texture.filterMode = FilterMode.Bilinear;

            // 清除为透明
            Color[] pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Color.clear;
            }

            Color iconColor = Color.white;
            
            // 绘制一个圆形边框
            int centerX = size / 2;
            int centerY = size / 2;
            int radius = 6;
            
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Mathf.Sqrt((x - centerX) * (x - centerX) + (y - centerY) * (y - centerY));
                    // 圆形边框
                    if (dist >= radius - 1 && dist <= radius + 1)
                    {
                        SetPixelSafe(pixels, size, x, y, iconColor);
                    }
                }
            }
            
            // 绘制斜线（从左上到右下）
            for (int i = 3; i < 13; i++)
            {
                SetPixelSafe(pixels, size, i, i, iconColor);
                SetPixelSafe(pixels, size, i + 1, i, iconColor);
            }

            texture.SetPixels(pixels);
            texture.Apply();

            return texture;
        }

        /// <summary>
        /// 创建警告图标（三角形 + 感叹号）
        /// </summary>
        private static Texture2D CreateWarningIcon()
        {
            int size = 16;
            Texture2D texture = new Texture2D(size, size, TextureFormat.ARGB32, false);
            texture.filterMode = FilterMode.Bilinear;

            Color[] pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Color.clear;

            Color iconColor = Color.white;

            // 三角形轮廓（顶点在上，底边在下）
            // 顶点: (8, 2), 左下: (2, 13), 右下: (13, 13)
            for (int y = 2; y <= 13; y++)
            {
                float progress = (float)(y - 2) / 11f;
                int left = (int)(8 - progress * 6);
                int right = (int)(8 + progress * 6);
                
                // 左边和右边
                SetPixelSafe(pixels, size, left, y, iconColor);
                SetPixelSafe(pixels, size, left + 1, y, iconColor);
                SetPixelSafe(pixels, size, right, y, iconColor);
                SetPixelSafe(pixels, size, right - 1, y, iconColor);
            }
            // 底边
            for (int x = 2; x <= 13; x++)
            {
                SetPixelSafe(pixels, size, x, 13, iconColor);
            }

            // 感叹号竖线
            for (int y = 5; y <= 9; y++)
            {
                SetPixelSafe(pixels, size, 8, y, iconColor);
                SetPixelSafe(pixels, size, 7, y, iconColor);
            }
            // 感叹号点
            SetPixelSafe(pixels, size, 7, 11, iconColor);
            SetPixelSafe(pixels, size, 8, 11, iconColor);

            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        private static void SetPixelSafe(Color[] pixels, int size, int x, int y, Color color)
        {
            // Unity纹理的Y轴是从下到上的
            int flippedY = size - 1 - y;
            if (x >= 0 && x < size && flippedY >= 0 && flippedY < size)
            {
                int index = flippedY * size + x;
                // Alpha混合
                if (pixels[index].a > 0 && color.a < 1)
                {
                    pixels[index] = Color.Lerp(pixels[index], color, color.a);
                }
                else
                {
                    pixels[index] = color;
                }
            }
        }

        /// <summary>
        /// 强制刷新Hierarchy窗口
        /// </summary>
        public static void RepaintHierarchy()
        {
            EditorApplication.RepaintHierarchyWindow();
        }
    }
}
