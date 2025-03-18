using System.Collections.Generic;
using AOTScripts.Tool.Resource;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

namespace HotUpdate.Scripts.Config.ArrayConfig
{
    #if UNITY_EDITOR
    [CreateAssetMenu(fileName = "ConfigManagerEditor", menuName = "ConfigManager/ConfigManagerEditor")]
    public class ConfigManager : ScriptableObject, IConfigProvider
    {
        [SerializeField] 
        private FolderReference configReference;
        [ReadOnly]
        [SerializeField]
        private List<ConfigBase> configs = new List<ConfigBase>();
        
        [Button]
        public void LoadConfig()
        {
            configs.Clear();
            // 1. 获取路径下所有资源的 GUID
            var guids = AssetDatabase.FindAssets("", new[] { configReference.Path });

            // 2. 遍历 GUID 加载每个资源
            foreach (var guid in guids)
            {
                // 将 GUID 转换为资源路径
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
    
                // 加载该路径的资源
                var asset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
    
                // 根据需要进行类型检查和使用
                if (asset is ConfigBase configBase)
                {
                    configs.Add(configBase);
                }
            }
        }

        [Button]
        public void LoadResources()
        {
            foreach (var config in configs)
            {
                config.LoadFromResources();
            }
        }
        
        public T GetConfig<T>() where T : ConfigBase, new()
        {
            return (T)configs.Find(obj => obj.GetType() == typeof(T));
        }
    }
    #endif
}