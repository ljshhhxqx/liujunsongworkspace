using System;
using System.Collections.Generic;
using System.IO;
using ExcelDataReader;
using Newtonsoft.Json;
using Sirenix.OdinInspector;
using UnityEngine;
#if UNITY_EDITOR
#endif

namespace HotUpdate.Scripts.Config
{
    [Serializable]
    public abstract class ConfigBase : ScriptableObject
    {
        public virtual ConfigType ConfigType { get; }
        protected virtual string ConfigFileName => name;
        protected const string EditorExcelFolder = "ConfigData/Excel";
        protected const string EditorCsvFolder = "ConfigData/Csv";
        protected const string CsvFolder = "HotUpdate/Res/Preload/Csv";
        
        [Button("保存在资源系统里面")]
        private void SaveToResources()
        {
#if UNITY_EDITOR
            SaveToJson();
            LoadConfigData();
#endif
        }

        public virtual void Init()
        {
            
#if UNITY_EDITOR
            LoadConfigData();
#else
            LoadConfigData();
#endif
        }
        
        private void LoadConfigData()
        {
#if UNITY_EDITOR
            string excelPath = Path.Combine(Application.streamingAssetsPath, EditorExcelFolder, $"{ConfigFileName}.xlsx");
            string csvPath = Path.Combine(Application.streamingAssetsPath, EditorCsvFolder, $"{ConfigFileName}.csv");

            if (File.Exists(excelPath))
            {
                ReadFromExcel(excelPath);
            }
            else if (File.Exists(csvPath))
            {
                ReadFromCsv(csvPath);
            }
            else
            {
                Debug.LogError($"配置文件未找到：{ConfigFileName}。请确保存在 .xlsx 或 .csv 文件。");
            }
#else
            string runtimeCsvPath = Path.Combine(CsvFolder, $"{ConfigFileName}.csv");
            if (File.Exists(runtimeCsvPath))
            {
                ReadFromCsv(runtimeCsvPath);
            }
            else
            {
                Debug.LogError($"运行时配置文件未找到：{runtimeCsvPath}");
            }
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
        

        /// <summary>
        /// 从 Excel 文件读取数据
        /// </summary>
        /// <param name="filePath">Excel 文件路径</param>
        protected abstract void ReadFromExcel(string filePath);

        /// <summary>
        /// 从 CSV 文件读取数据
        /// </summary>
        /// <param name="filePath">CSV 文件路径</param>
        protected abstract void ReadFromCsv(string filePath);
    }

    public static class ExcelDataReader<T> where T : new()
    {
        public static List<T> ReadExcelData(string filePath)
        {
            List<T> dataList = new List<T>();

            using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read))
            {
                using (var reader = ExcelReaderFactory.CreateReader(stream))
                {
                    // 跳过标题行和类型行
                    reader.Read(); // 标题行
                    reader.Read(); // 类型行

                    // 获取属性信息
                    var properties = typeof(T).GetProperties();

                    while (reader.Read())
                    {
                        T item = new T();

                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            var property = properties[i];
                            var value = reader.GetValue(i);

                            // 处理不同类型的数据转换
                            object convertedValue = ConvertValue(value, property.PropertyType);

                            property.SetValue(item, convertedValue);
                        }

                        dataList.Add(item);
                    }
                }
            }

            return dataList;
        }

        private static object ConvertValue(object value, Type targetType)
        {
            if (value == null)
                return null;

            // 处理数组类型
            if (targetType.IsArray)
            {
                string jsonString = value.ToString().Trim('"');
                Type elementType = targetType.GetElementType();
        
                // 反序列化为对应类型的数组
                if (elementType != null)
                {
                    var array = JsonConvert.DeserializeObject(jsonString, Array.CreateInstance(elementType, 0).GetType()) as Array;
                    return array;
                }
            }

            // 处理基本类型
            if (targetType.IsPrimitive || targetType == typeof(string) || targetType.IsEnum)
            {
                return Convert.ChangeType(value, targetType);
            }

            // 处理自定义类型（JSON反序列化）
            if (IsCustomType(targetType))
            {
                string jsonString = value.ToString();
                return JsonConvert.DeserializeObject(jsonString, targetType);
            }

            // 处理特殊类型（如Vector3）
            if (targetType == typeof(Vector3))
            {
                string jsonString = value.ToString();
                return JsonConvert.DeserializeObject<Vector3>(jsonString);
            }

            return value;
        }

        private static bool IsCustomType(Type type)
        {
            // 判断是否为自定义类型
            return type.IsClass && type != typeof(string);
        }
    }

    public enum ConfigType
    {
        Array,
        Json,
    }
}