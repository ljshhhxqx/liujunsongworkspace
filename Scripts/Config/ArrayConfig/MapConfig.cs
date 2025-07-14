﻿using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Sirenix.OdinInspector;
using UnityEngine;

namespace HotUpdate.Scripts.Config.ArrayConfig
{
    [CreateAssetMenu(fileName = "MapConfig", menuName = "ScriptableObjects/MapConfig")]
    public class MapConfig : ConfigBase
    {
        [ReadOnly]
        [SerializeField]
        private List<MapConfigData> mapConfigData = new List<MapConfigData>();
        
        public Dictionary<MapType, MapConfigData> MapConfigDataDictionary { get; } = new Dictionary<MapType, MapConfigData>();

        public MapConfigData GetMapConfigData(MapType mapType)
        {
            if (MapConfigDataDictionary.TryGetValue(mapType, out var configData))
            {
                return configData;
            }
            foreach (var data in mapConfigData)
            {
                if (data.mapType == mapType)
                {
                    MapConfigDataDictionary.Add(mapType, data);
                    return data;
                }
            }

            Debug.LogError("MapConfigData not found for " + mapType);
            return new MapConfigData();
        }

        public IEnumerable<MapConfigData> GetMapConfigDatas(Func<MapConfigData, bool> predicate)
        {
            foreach (var data in mapConfigData)
            {
                if (predicate(data))
                {
                    yield return data;
                }
            }
        }

        protected override void ReadFromCsv(List<string[]> textAsset)
        {
            mapConfigData.Clear();
            for (int i = 2; i < textAsset.Count; i++)
            {
                var row = textAsset[i];
                var data = new MapConfigData();
                data.mapType = (MapType) Enum.Parse(typeof(MapType), row[0]);
                data.maxPlayer = int.Parse(row[1]);
                data.minPlayer = int.Parse(row[2]);
                data.availableWeather = JsonConvert.DeserializeObject<List<WeatherType>>(row[3]);
                mapConfigData.Add(data);
            }
        }
    }

    [Serializable]
    public struct MapConfigData
    {
        public MapType mapType;
        public int maxPlayer;
        public int minPlayer;
        public List<WeatherType> availableWeather;
    }
    
    
    public enum MapType
    {
        Town,
        Forest,
    }
}