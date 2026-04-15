using System.IO;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json;

namespace PrefabAnnotator.Core
{
    /// <summary>
    /// 监听 Prefab 复制事件，自动为复制的 Prefab 复制注释文件
    /// 检测逻辑：文件名模式匹配 → 源注释文件存在 → localFileId 验证
    /// </summary>
    public class PrefabCopyHandler : AssetPostprocessor
    {
        private const string DESC_FILE_EXTENSION = ".desc.json";
        private const string DESCRIPTIONS_FOLDER = "Assets/Editor/Descriptions";

        // 匹配末尾的 " N" 模式（空格 + 数字）
        private static readonly Regex SuffixPattern = new Regex(@"^(.+)\s+(\d+)$", RegexOptions.Compiled);

        static void OnPostprocessAllAssets(
            string[] importedAssets, string[] deletedAssets,
            string[] movedAssets, string[] movedFromAssetPaths)
        {
            foreach (string assetPath in importedAssets)
            {
                if (!assetPath.EndsWith(".prefab"))
                    continue;

                TryCopyDescriptionForDuplicatedPrefab(assetPath);
            }
        }

        private static void TryCopyDescriptionForDuplicatedPrefab(string newPrefabPath)
        {
            string newGuid = AssetDatabase.AssetPathToGUID(newPrefabPath);
            if (string.IsNullOrEmpty(newGuid))
                return;

            string newDescPath = DESCRIPTIONS_FOLDER + "/" + newGuid + DESC_FILE_EXTENSION;
            if (File.Exists(newDescPath))
                return;

            string directory = Path.GetDirectoryName(newPrefabPath);
            string fileNameWithoutExt = Path.GetFileNameWithoutExtension(newPrefabPath);

            // 生成候选源文件名列表
            List<string> candidates = GetSourceCandidates(directory, fileNameWithoutExt);
            if (candidates.Count == 0)
                return;

            foreach (string candidatePath in candidates)
            {
                if (!File.Exists(candidatePath))
                    continue;

                string sourceGuid = AssetDatabase.AssetPathToGUID(candidatePath);
                if (string.IsNullOrEmpty(sourceGuid))
                    continue;

                string sourceDescPath = DESCRIPTIONS_FOLDER + "/" + sourceGuid + DESC_FILE_EXTENSION;
                if (!File.Exists(sourceDescPath))
                    continue;

                // localFileId 验证：对比根节点确认是复制关系
                if (!VerifyRootLocalFileId(candidatePath, newPrefabPath))
                    continue;

                // 复制注释文件并替换 GUID
                CopyDescriptionFile(sourceDescPath, newDescPath, sourceGuid, newGuid);
                Debug.Log($"[PrefabAnnotator] 已为复制的 Prefab 复制注释: {newPrefabPath}");
                return;
            }
        }

        /// <summary>
        /// 根据文件名模式生成候选源文件路径列表
        /// "MyUI 1" → ["MyUI.prefab"]
        /// "MyUI 3" → ["MyUI 2.prefab", "MyUI 1.prefab", "MyUI.prefab"]
        /// </summary>
        private static List<string> GetSourceCandidates(string directory, string fileNameWithoutExt)
        {
            var candidates = new List<string>();

            Match match = SuffixPattern.Match(fileNameWithoutExt);
            if (!match.Success)
                return candidates;

            string baseName = match.Groups[1].Value;
            int number = int.Parse(match.Groups[2].Value);

            // 从 N-1 递减到 0，优先匹配最近的
            for (int i = number - 1; i >= 1; i--)
            {
                candidates.Add(Path.Combine(directory, $"{baseName} {i}.prefab").Replace('\\', '/'));
            }
            // 无后缀的原始文件
            candidates.Add(Path.Combine(directory, $"{baseName}.prefab").Replace('\\', '/'));

            return candidates;
        }

        /// <summary>
        /// 对比两个 Prefab 根节点的 localFileId，验证复制关系
        /// </summary>
        private static bool VerifyRootLocalFileId(string sourcePrefabPath, string newPrefabPath)
        {
            var sourceRoot = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePrefabPath);
            var newRoot = AssetDatabase.LoadAssetAtPath<GameObject>(newPrefabPath);

            if (sourceRoot == null || newRoot == null)
                return false;

            bool gotSource = AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sourceRoot, out _, out long sourceLocalId);
            bool gotNew = AssetDatabase.TryGetGUIDAndLocalFileIdentifier(newRoot, out _, out long newLocalId);

            return gotSource && gotNew && sourceLocalId == newLocalId;
        }

        /// <summary>
        /// 复制注释文件，将 key 中属于源 Prefab 的 GUID 替换为新 Prefab 的 GUID
        /// </summary>
        private static void CopyDescriptionFile(string sourceDescPath, string newDescPath, string sourceGuid, string newGuid)
        {
            try
            {
                string json = File.ReadAllText(sourceDescPath);
                var sourceData = JsonConvert.DeserializeObject<DescriptionFileData>(json);
                if (sourceData == null || !sourceData.HasAnyDescription())
                    return;

                var newData = new DescriptionFileData { version = sourceData.version };

                foreach (var kvp in sourceData.nodes)
                {
                    string oldKey = kvp.Key;
                    string newKey = RemapGlobalObjectId(oldKey, sourceGuid, newGuid);
                    newData.nodes[newKey] = new NodeDescription
                    {
                        description = kvp.Value.description,
                        ignored = kvp.Value.ignored
                    };
                }

                if (!newData.HasAnyDescription())
                    return;

                string dir = Path.GetDirectoryName(newDescPath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                string newJson = JsonConvert.SerializeObject(newData, Formatting.Indented);
                File.WriteAllText(newDescPath, newJson);

                AssetDatabase.Refresh();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[PrefabAnnotator] 复制注释文件失败: {e.Message}");
            }
        }

        /// <summary>
        /// 替换 GlobalObjectId key 中的 GUID 段
        /// 只替换属于源 Prefab 自身节点的 key（GUID 段等于 sourceGuid 的），
        /// 嵌套 Prefab 引用的 key 保持不变
        /// </summary>
        private static string RemapGlobalObjectId(string globalObjectId, string sourceGuid, string newGuid)
        {
            string[] parts = globalObjectId.Split('-');
            if (parts.Length >= 5 && parts[2] == sourceGuid)
            {
                parts[2] = newGuid;
                return string.Join("-", parts);
            }
            return globalObjectId;
        }
    }
}
