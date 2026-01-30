using UnityEditor;
using UnityEngine;
using System;

namespace PrefabAnnotator.Core
{
    /// <summary>
    /// 依赖检查器 - 检查 Newtonsoft.Json 是否可用
    /// </summary>
    [InitializeOnLoad]
    public static class DependencyChecker
    {
        private const string DEPENDENCY_CHECK_KEY = "PrefabAnnotator_DependencyChecked";

        static DependencyChecker()
        {
            // 每次 Unity 启动时检查一次
            if (!SessionState.GetBool(DEPENDENCY_CHECK_KEY, false))
            {
                SessionState.SetBool(DEPENDENCY_CHECK_KEY, true);
                CheckDependencies();
            }
        }

        private static void CheckDependencies()
        {
            if (!IsNewtonsoftJsonAvailable())
            {
                Debug.LogWarning(
                    "[Prefab Annotator] 缺少依赖：Newtonsoft.Json\n" +
                    "请通过 Package Manager 安装：\n" +
                    "Window > Package Manager > + > Add package by name\n" +
                    "输入：com.unity.nuget.newtonsoft-json\n\n" +
                    "[Prefab Annotator] Missing dependency: Newtonsoft.Json\n" +
                    "Please install via Package Manager:\n" +
                    "Window > Package Manager > + > Add package by name\n" +
                    "Enter: com.unity.nuget.newtonsoft-json"
                );
            }
        }

        /// <summary>
        /// 检查 Newtonsoft.Json 是否可用
        /// </summary>
        public static bool IsNewtonsoftJsonAvailable()
        {
            try
            {
                var type = Type.GetType("Newtonsoft.Json.JsonConvert, Newtonsoft.Json");
                return type != null;
            }
            catch
            {
                return false;
            }
        }
    }
}
