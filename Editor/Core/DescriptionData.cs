using System;
using System.Collections.Generic;
using UnityEngine;

namespace PrefabAnnotator.Core
{
    /// <summary>
    /// 单个节点的描述数据
    /// </summary>
    [Serializable]
    public class NodeDescription
    {
        public string description;
        
        /// <summary>
        /// 是否忽略此节点及其所有子节点（不导出、不显示注释编辑框）
        /// </summary>
        public bool ignored;

        public NodeDescription()
        {
            description = string.Empty;
            ignored = false;
        }

        public NodeDescription(string desc)
        {
            description = desc;
            ignored = false;
        }
    }

    /// <summary>
    /// 描述文件的根数据结构
    /// 存储格式: { "version": 1, "nodes": { "globalObjectId": { "description": "..." } } }
    /// </summary>
    [Serializable]
    public class DescriptionFileData
    {
        public int version = 1;
        public Dictionary<string, NodeDescription> nodes = new Dictionary<string, NodeDescription>();

        public DescriptionFileData()
        {
            version = 1;
            nodes = new Dictionary<string, NodeDescription>();
        }

        /// <summary>
        /// 获取指定节点的描述
        /// </summary>
        public string GetDescription(string globalObjectId)
        {
            if (nodes.TryGetValue(globalObjectId, out var node))
            {
                return node.description;
            }
            return string.Empty;
        }

        /// <summary>
        /// 设置指定节点的描述
        /// </summary>
        public void SetDescription(string globalObjectId, string description)
        {
            if (!nodes.ContainsKey(globalObjectId))
            {
                if (string.IsNullOrEmpty(description))
                {
                    return; // 没有节点且描述为空，无需创建
                }
                nodes[globalObjectId] = new NodeDescription();
            }
            
            nodes[globalObjectId].description = description ?? string.Empty;
            
            // 如果描述为空且没有忽略标记，移除该节点
            CleanupNodeIfEmpty(globalObjectId);
        }
        
        /// <summary>
        /// 获取指定节点的忽略状态
        /// </summary>
        public bool IsIgnored(string globalObjectId)
        {
            if (nodes.TryGetValue(globalObjectId, out var node))
            {
                return node.ignored;
            }
            return false;
        }
        
        /// <summary>
        /// 设置指定节点的忽略状态
        /// </summary>
        public void SetIgnored(string globalObjectId, bool ignored)
        {
            if (!nodes.ContainsKey(globalObjectId))
            {
                if (!ignored)
                {
                    return; // 没有节点且不需要忽略，无需创建
                }
                nodes[globalObjectId] = new NodeDescription();
            }
            
            nodes[globalObjectId].ignored = ignored;
            
            // 如果描述为空且没有忽略标记，移除该节点
            CleanupNodeIfEmpty(globalObjectId);
        }
        
        /// <summary>
        /// 如果节点没有有效数据，则清理
        /// </summary>
        private void CleanupNodeIfEmpty(string globalObjectId)
        {
            if (nodes.TryGetValue(globalObjectId, out var node))
            {
                if (string.IsNullOrEmpty(node.description) && !node.ignored)
                {
                    nodes.Remove(globalObjectId);
                }
            }
        }

        /// <summary>
        /// 检查是否有任何描述数据
        /// </summary>
        public bool HasAnyDescription()
        {
            return nodes.Count > 0;
        }

        /// <summary>
        /// 检查指定节点是否有描述
        /// </summary>
        public bool HasDescription(string globalObjectId)
        {
            return nodes.ContainsKey(globalObjectId) && 
                   !string.IsNullOrEmpty(nodes[globalObjectId].description);
        }
    }

}
