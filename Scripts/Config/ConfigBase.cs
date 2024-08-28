using Sirenix.OdinInspector;
using System;
using System.IO;
using UnityEngine;

namespace Config
{
    [Serializable]
    public class ConfigBase : ScriptableObject
    {
        [Button("保存在资源系统里面")]
        private void SaveToResources()
        {
            // 当数据变化时，自动调用保存逻辑
#if UNITY_EDITOR
            SaveToJson();
#endif
        }

        private void SaveToJson()
        {
            // 将ScriptableObject转换为JSON字符串
            string json = JsonUtility.ToJson(this, true);

            // 创建保存路径
            string streamingFile = Path.Combine(Application.streamingAssetsPath, "Config");
            string path = streamingFile;

            // 如果文件夹不存在，则创建文件夹
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            // 定义文件的保存路径和文件名
            string filePath = Path.Combine(path, $"{name}.json");

            // 将JSON字符串写入文件
            File.WriteAllText(filePath, json);

            // 输出日志确认保存成功
            Debug.Log($"ScriptableObject saved to {filePath}");
        }
    }
}