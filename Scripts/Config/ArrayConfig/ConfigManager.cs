using System.Collections.Generic;
using AOTScripts.Tool.Resource;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

namespace HotUpdate.Scripts.Config.ArrayConfig
{
    #if UNITY_EDITOR
    [CreateAssetMenu(fileName = "ConfigManager", menuName = "ConfigManager/ConfigManager")]
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
            EditorUtility.SetDirty(this);
        }

        [Button]
        public void LoadResources()
        {
            foreach (var config in configs)
            {
                config.LoadFromResources();
                if (config is ItemConfig itemConfig)
                {
                    itemConfig.CoverBuffExtraData();
                }
            }
        }
        
        private static ConfigManager instance;
        public static ConfigManager Instance
        {
            get
            {
                instance ??= AssetDatabase.LoadAssetAtPath<ConfigManager>("Assets/Editor/Config/ConfigManager.asset");
                return instance;
            }
        }
        
        [MenuItem("Tools/Load Config")]
        public static void LoadConfigMenu()
        {
            Instance.LoadConfig();
            Instance.LoadResources();
        }

        [MenuItem("Tools/Load Equip Config and Buff ExtraData Menu")]
        public static void LoadEquipConfigAndBuffExtraDataValidation()
        {
            var itemConfig = Instance.GetConfig<ItemConfig>();
            var constantBuffConfig = Instance.GetConfig<ConstantBuffConfig>();
            itemConfig.CoverBuffExtraData();
            itemConfig.WriteBuffExtraDataToExcel();
            constantBuffConfig.WriteBuffExtraDataToExcel();
        }

        public T GetConfig<T>() where T : ConfigBase, new()
        {
            return (T)configs.Find(obj => obj.GetType() == typeof(T));
        }
    }
    #endif
}