using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using VContainer;

namespace Config
{
    public class ConfigManager
    {
        private Dictionary<Type, ScriptableObject> configs = new Dictionary<Type, ScriptableObject>();
        private readonly string configFolderPath = Application.dataPath + "/Data/Configurations/";
        
        [Inject]
        private ConfigManager()
        {
        }

        public void InitConfigs(params ScriptableObject[] configObjects)
        {
            if (!Directory.Exists(configFolderPath))
            {
                Directory.CreateDirectory(configFolderPath);
            }

            foreach (var configObject in configObjects)
            {
                configs.Add(configObject.GetType(), configObject);
                if (configObject is ConfigBase config)
                {
                    config.Init();
                }
            }

            LoadAllSettings();
        }

        public T GetConfig<T>() where T : ConfigBase, new()
        {
            if (configs.TryGetValue(typeof(T), out var foundConfig))
            {
                if (foundConfig is ConfigBase config)
                {
                    return config as T;
                }
                // var serialized = JsonUtility.ToJson(foundConfig);
                // var t = ScriptableObject.CreateInstance<T>();
                // JsonUtility.FromJsonOverwrite(serialized, t);
                // return t;
            }
            return null;
        }

        public void SaveSettings<T>(T settings) where T : ConfigBase
        {
            if (configs.ContainsKey(typeof(T)))
            {
                var file = configFolderPath + typeof(T).Name + ".json";
                var json = JsonUtility.ToJson(settings);
                File.WriteAllText(file, json);
            }
        }

        public void SaveAllSettings()
        {
            foreach (var configPair in configs)
            {
                var file = configFolderPath + configPair.Key.Name + ".json";
                var json = JsonUtility.ToJson(configPair.Value);
                File.WriteAllText(file, json);
                Debug.Log($"SaveAllSettings:setting-{file} save succeed");
            }
        }

        public void LoadAllSettings()
        {
            foreach (var configPair in configs)
            {
                var file = configFolderPath + configPair.Key.Name + ".json";
                var json = JsonUtility.ToJson(configPair.Value);
                File.WriteAllText(file, json);
                Debug.Log($"LoadAllSettings:setting-{file} load succeed");
            }
        }
    }
}