﻿using System;
using System.Collections.Generic;
using UnityEngine;

namespace HotUpdate.Scripts.Config
{
    [CreateAssetMenu(fileName = "MapConfig", menuName = "ScriptableObjects/MapConfig")]
    public class MapConfig : ConfigBase
    {
        [SerializeField]
        private List<MapConfigData> mapConfigData = new List<MapConfigData>();
        [SerializeField]
        private GameModeData gameModeData;
        
        public GameModeData GameModeData => gameModeData;

        public MapConfigData GetMapConfigData(MapType mapType)
        {
            foreach (var data in mapConfigData)
            {
                if (data.mapType == mapType)
                {
                    return data;
                }
            }

            return null;
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

        protected override void ReadFromExcel(string filePath)
        {
        }

        protected override void ReadFromCsv(string filePath)
        {
        }
    }

    [Serializable]
    public class MapConfigData
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

    [Serializable]
    public class GameModeData
    {
        public List<int> times;
        public List<int> scores;
    }
}