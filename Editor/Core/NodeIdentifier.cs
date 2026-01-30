using UnityEngine;
using UnityEditor;
#if UNITY_2021_2_OR_NEWER
using UnityEditor.SceneManagement;
#else
using UnityEditor.Experimental.SceneManagement;
#endif

namespace PrefabAnnotator.Core
{
    /// <summary>
    /// 节点标识器 - 使用GlobalObjectId获取GameObject的稳定唯一标识
    /// GlobalObjectId格式: GlobalObjectId_V1-{identifierType}-{assetGUID}-{targetObjectId}-{targetPrefabId}
    /// 节点移动、重命名时ID不变，仅删除重建时变化
    /// </summary>
    public static class NodeIdentifier
    {
        private const string ZERO_GUID = "00000000000000000000000000000000";

        /// <summary>
        /// 获取GameObject的全局唯一标识符字符串
        /// 在Prefab编辑模式中，会自动将全零的assetGUID替换为实际的Prefab GUID
        /// </summary>
        /// <param name="gameObject">目标GameObject</param>
        /// <returns>GlobalObjectId的字符串表示</returns>
        public static string GetGlobalId(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return string.Empty;
            }

            GlobalObjectId globalId = GlobalObjectId.GetGlobalObjectIdSlow(gameObject);
            string idString = globalId.ToString();

            // 在Prefab编辑模式中，GlobalObjectId的assetGUID可能为全零
            // 需要替换为实际的Prefab资产GUID以确保一致性
            // 使用缓存的 PrefabGuid 避免重复查询
            string cachedGuid = DescriptionFileManager.GetCachedPrefabGuid();
            if (!string.IsNullOrEmpty(cachedGuid) && idString.Contains(ZERO_GUID))
            {
                idString = idString.Replace(ZERO_GUID, cachedGuid);
            }

            return idString;
        }

        /// <summary>
        /// 检查GlobalObjectId是否有效
        /// </summary>
        /// <param name="globalIdString">GlobalObjectId字符串</param>
        /// <returns>是否有效</returns>
        public static bool IsValidGlobalId(string globalIdString)
        {
            if (string.IsNullOrEmpty(globalIdString))
            {
                return false;
            }

            // 尝试解析GlobalObjectId
            return GlobalObjectId.TryParse(globalIdString, out _);
        }

        /// <summary>
        /// 根据GlobalObjectId字符串查找对应的GameObject
        /// </summary>
        /// <param name="globalIdString">GlobalObjectId字符串</param>
        /// <returns>对应的GameObject，如果找不到则返回null</returns>
        public static GameObject FindGameObject(string globalIdString)
        {
            if (!GlobalObjectId.TryParse(globalIdString, out GlobalObjectId globalId))
            {
                return null;
            }

            Object obj = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalId);
            return obj as GameObject;
        }

        /// <summary>
        /// 批量获取多个GameObject的GlobalObjectId
        /// </summary>
        /// <param name="gameObjects">GameObject数组</param>
        /// <returns>对应的GlobalObjectId字符串数组</returns>
        public static string[] GetGlobalIds(GameObject[] gameObjects)
        {
            if (gameObjects == null || gameObjects.Length == 0)
            {
                return new string[0];
            }

            string[] result = new string[gameObjects.Length];
            for (int i = 0; i < gameObjects.Length; i++)
            {
                result[i] = GetGlobalId(gameObjects[i]);
            }
            return result;
        }

        /// <summary>
        /// 检查GameObject是否属于Prefab
        /// </summary>
        /// <param name="gameObject">目标GameObject</param>
        /// <returns>是否属于Prefab</returns>
        public static bool IsPrefabInstance(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return false;
            }

            return PrefabUtility.IsPartOfPrefabInstance(gameObject);
        }

        /// <summary>
        /// 检查GameObject是否是Prefab资产的一部分（在Prefab编辑模式中）
        /// </summary>
        /// <param name="gameObject">目标GameObject</param>
        /// <returns>是否是Prefab资产的一部分</returns>
        public static bool IsPrefabAsset(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return false;
            }

            return PrefabUtility.IsPartOfPrefabAsset(gameObject);
        }
    }
}
