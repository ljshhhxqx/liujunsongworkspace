﻿using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Text;
using AOTScripts.Tool.Resource;
using ExcelDataReader;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

namespace HotUpdate.Scripts.Config
{
    [Serializable]
    public abstract class ConfigBase : ScriptableObject
    {
        [SerializeField] protected FolderReference excelAssetReference;
        [SerializeField] protected FolderReference csvAssetReference;
        [SerializeField] protected FolderReference jsonAssetReference;
        [SerializeField] protected string configName;
        [SerializeField] protected bool isArray;
        
        public string ConfigName => configName;
        public string SheetName => "Sheet1";
        public bool IsArray => isArray;

        [Button("保存为Json文件并将Excel转换为Csv文件")]
        public void SaveToJsonAndCsv()
        {
            SaveToJson();
            SaveToCsv();
        }
        
        [Button("保存为Json文件")]
        public void SaveJson()
        {
#if UNITY_EDITOR
            SaveToJson();
#endif
        }

        [Button("将Excel保存为Csv文件")]
        public void SaveToCsv()
        {
#if UNITY_EDITOR
            if (!isArray)
            {
                return;
            }
            var csvContent = ConvertExcelToCsv(Path.Combine(excelAssetReference.Path, $"{configName}.xlsx"));
            if (csvContent == null)
            {
                Debug.LogError($"Excel文件未找到：{configName}。请确保{configName}.xlsx 文件存在。");
            }
#endif
        }

        [Button("从资源系统加载配置数据")]
        public void LoadFromResources()
        {
#if UNITY_EDITOR
            LoadConfigData();
#endif
        }

        public virtual void Init(TextAsset textAsset = null)
        {
            if (textAsset)
            {
                LoadConfigData(textAsset);
            }
        }
        
        private void LoadConfigData(TextAsset textAsset = null)
        {
            if (textAsset)
            {
                if (!isArray)
                {
                    ReadFromJson(textAsset);
                    return;
                }
                var csvContent = ParseCsvContent(textAsset.text);
                ReadFromCsv(csvContent);
                return;
            }
            
            if (!isArray)
            {
                var filePath = Path.Combine(jsonAssetReference.Path, $"{configName}.json");
                var jsonContent = File.ReadAllText(filePath);
        
                ReadFromJson(new TextAsset(jsonContent));
                return;
            }

            var excelPath = Path.Combine(excelAssetReference.Path, $"{configName}.xlsx");

            if (File.Exists(excelPath))
            {
                var csv = ConvertExcelToCsv(excelPath);
                if (csv != null)
                {
                    var jsonContent = ParseCsvContent(csv);
                    ReadFromCsv(jsonContent);
                }
            }
            else
            {
                Debug.LogError($"配置文件未找到：{configName}。请确保存在 .xlsx 文件。");
            }
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
            #endif
        }

        private string ConvertExcelToCsv(string excelPath)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var csvContent = new StringBuilder();

            try
            {
                using (var stream = File.Open(excelPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    using (var reader = ExcelReaderFactory.CreateReader(stream))
                    {
                        var result = reader.AsDataSet(new ExcelDataSetConfiguration
                        {
                            ConfigureDataTable = _ => new ExcelDataTableConfiguration
                            {
                                UseHeaderRow = true
                            }
                        });

                        var sheetName = SheetName;
                        DataTable table;
                        if (string.IsNullOrEmpty(sheetName))
                        {
                            table = result.Tables[0];
                        }
                        else
                        {
                            table = result.Tables[sheetName];
                            if (table == null)
                            {
                                Debug.LogError($"Sheet '{sheetName}' not found in Excel file: {excelPath}");
                                return null;
                            }
                        }

                        // 写入表头
                        var headers = new string[table.Columns.Count];
                        for (var i = 0; i < table.Columns.Count; i++)
                        {
                            headers[i] = table.Columns[i].ColumnName;
                        }
                        csvContent.AppendLine(string.Join(",", headers));

                        // 写入数据行
                        foreach (DataRow row in table.Rows)
                        {
                            // 检查这一行是否全为空
                            bool isEmptyRow = true;
                            for (int i = 0; i < table.Columns.Count; i++)
                            {
                                string value = row[i]?.ToString()?.Trim();
                                if (!string.IsNullOrEmpty(value))
                                {
                                    isEmptyRow = false;
                                    break;
                                }
                            }

                            // 跳过空行
                            if (isEmptyRow)
                            {
                                continue;
                            }

                            string[] fields = new string[table.Columns.Count];
                            for (int i = 0; i < table.Columns.Count; i++)
                            {
                                string field = row[i]?.ToString()?.Trim() ?? string.Empty;
                                
                                // 检查是否为空或只包含空格
                                if (string.IsNullOrWhiteSpace(field))
                                {
                                    // 根据具体需求处理空值
                                    // 这里可以选择设置默认值或者抛出错误
                                    Debug.LogWarning($"Empty value found in row {table.Rows.IndexOf(row) + 1}, column {i + 1}");
                                    field = string.Empty; // 或设置为其他默认值
                                }
                                
                                // 处理包含逗号的字段
                                if (field.Contains(","))
                                {
                                    field = $"\"{field}\"";
                                }
                                fields[i] = field;
                            }
                            csvContent.AppendLine(string.Join(",", fields));
                        }
                    }
                }
            }
            
            catch (IOException ex)
            {
                Debug.LogError($"无法访问Excel文件 {excelPath}。可能是文件被其他程序锁定。错误信息: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                Debug.LogError($"处理Excel文件时发生错误: {ex.Message}");
                return null;
            }
            

            var csvPath = csvAssetReference.Path + $"/{configName}.csv";
            var content = csvContent.ToString();
            if (!string.IsNullOrEmpty(csvPath))
            {
                string directory = Path.GetDirectoryName(csvPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                File.WriteAllText(csvPath, content);
                Debug.Log($"Saved CSV file to: {csvPath}");
                return content;
            }
            Debug.LogError($"CSV file path not set. Please set it in the ConfigBase component.");

            return null;
        }


        private void SaveToJson()
        {
#if UNITY_EDITOR
            // 将ScriptableObject转换为JSON字符串
            var json = JsonUtility.ToJson(this, true);

            // 创建保存路径
            var path = jsonAssetReference;

            // 如果文件夹不存在，则创建文件夹
            if (!Directory.Exists(path.Path))
            {
                Directory.CreateDirectory(path.Path);
            }

            // 定义文件的保存路径和文件名
            var filePath = Path.Combine(path.Path, $"{configName}.json");

            // 将JSON字符串写入文件
            File.WriteAllText(filePath, json);

            // 输出日志确认保存成功
            Debug.Log($"ScriptableObject saved to {filePath}");
#endif
        }
        
        protected List<string[]> ParseCsvContent(string csvContent)
        {
            var result = new List<string[]>();
            using var reader = new StringReader(csvContent);
            while (reader.ReadLine() is { } line)
            {
                // 处理CSV行，考虑引号内的逗号
                var row = new List<string>();
                bool inQuotes = false;
                int start = 0;
                
                for (int i = 0; i < line.Length; i++)
                {
                    if (line[i] == '"')
                    {
                        inQuotes = !inQuotes;
                    }
                    else if (line[i] == ',' && !inQuotes)
                    {
                        row.Add(line.Substring(start, i - start).Trim('"'));
                        start = i + 1;
                    }
                }
                
                // 添加最后一个字段
                row.Add(line[start..].Trim('"'));
                
                result.Add(row.ToArray());
            }

            return result;
        }
        
        protected void ReadFromJson(TextAsset textAsset)
        {
            JsonUtility.FromJsonOverwrite(textAsset.text, this);
            #if UNITY_EDITOR
            EditorUtility.SetDirty(this);
            #endif
        }

        /// <summary>
        /// 从 Excel 文件读取数据
        /// </summary>
        /// <param name="textAsset">Csv 文件</param>
        protected abstract void ReadFromCsv(List<string[]> textAsset);
    }
}
    
