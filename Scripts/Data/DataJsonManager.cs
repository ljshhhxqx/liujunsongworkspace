using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;

public class DataJsonManager : Singleton<DataJsonManager>
{
    private readonly Dictionary<DataType, string> _dataJsons = new Dictionary<DataType, string>();

    public ResourceData GetResourceData(string resName)
    {
        var json = GetJson(DataType.ResourcesData);
        if (string.IsNullOrEmpty(json))
        {
            throw new System.Exception("ResourcesData.json is empty or not exist.");
        }

        var data = JsonUtility.FromJson<ResourcesContainer>(json);
        return data.Resources.FirstOrDefault(item => item.Name == resName);
    }

    public List<ResourceData> GetResourcesDataByAddress(string address)
    {
        var json = GetJson(DataType.ResourcesData);
        if (string.IsNullOrEmpty(json))
        {
            throw new System.Exception("ResourcesData.json is empty or not exist.");
        }
        var data = JsonUtility.FromJson<ResourcesContainer>(json);
        if (address == null) throw new System.Exception("Address is null.");

        return data.Resources.Where(item => item.Address.StartsWith(address)).ToList();
    }

    public string GetJson(DataType type)
    {
        if (!_dataJsons.ContainsKey(type))
        {
            LoadJsonForType(type).Forget(); // 新的方法，根据类型加载JSON
        }

        return _dataJsons.GetValueOrDefault(type);
    }

    private async UniTask LoadJsonForType(DataType type)
    {
#if UNITY_EDITOR
        // 在编辑器模式下，使用AssetDatabase加载资源
        string filePath = $"Assets/Data/{type}.json"; // 假设文件名与DataType的名称匹配
        var textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(filePath);
        if (textAsset != null)
        {
            _dataJsons[type] = textAsset.text;
        }
#else
        // 在非编辑器模式下，从StreamingAssets目录加载资源Application.streamingAssetsPath
        string streamingPath = $"{Application.streamingAssetsPath}/{type}.json";
        using (var www = new WWW(streamingPath))
        {
            await www;
            
            if (string.IsNullOrEmpty(www.error))
            {
                _dataJsons[type] = www.text;
            }
            else
            {
                Debug.LogError($"Failed to load JSON for {type} from StreamingAssets. Error: {www.error}");
            }
        }
#endif
    }
}

/// <summary>
/// 用于将Data文件目录下的json源文件的文件名转化为枚举，这样可以与字符串区分开，便于管理和使用
/// </summary>
public enum DataType
{
    ResourcesData,
}