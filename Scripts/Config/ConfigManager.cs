using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer;

namespace HotUpdate.Scripts.Config
{
    public class ConfigManager
    {
        private readonly Dictionary<Type, ScriptableObject> _configs = new Dictionary<Type, ScriptableObject>();
        
        [Inject]
        private ConfigManager()
        {
        }

        public void InitConfigs(params ScriptableObject[] configObjects)
        {
            foreach (var configObject in configObjects)
            {
                _configs.Add(configObject.GetType(), configObject);
                if (configObject is ConfigBase config)
                {
                    #if !UNITY_EDITOR
                    config.Init();
                    #else
                    var resource = ResourceManager.Instance.GetResource<TextAsset>(config.ConfigName, config.IsArray ? ".csv" : ".json");
                    if (!resource)
                    {
                        Debug.LogError($"ConfigManager: {config.ConfigName} not found");
                        continue;
                    }
                    config.Init(resource);
                    //ResourceManager.Instance.UnloadResource(resource.name);
                    #endif
                }
            }
        }

        public T GetConfig<T>() where T : ConfigBase, new()
        {
            if (_configs.TryGetValue(typeof(T), out var foundConfig))
            {
                if (foundConfig is ConfigBase config)
                {
                    return config as T;
                }
            }
            return null;
        }
        
        
    }
}