using System;
using System.Collections.Generic;
using Config;
using UnityEngine;
using Random = UnityEngine.Random;

namespace HotUpdate.Scripts.Config
{
    [CreateAssetMenu(fileName = "WeatherConfig", menuName = "ScriptableObjects/WeatherConfig")]
    public class WeatherConfig : ConfigBase
    {
        [SerializeField] private List<WeatherData> weatherData = new List<WeatherData>();
        [SerializeField] private DayNightCycleData dayNightCycleData = new DayNightCycleData();

        public List<WeatherData> WeatherData => weatherData;
        public DayNightCycleData DayNightCycleData => dayNightCycleData;

        public WeatherData GetWeatherData(WeatherType weatherType)
        {
            foreach (var weather in WeatherData)
            {
                if (weather.weatherType == weatherType)
                {
                    return weather;
                }
            }

            return default;
        }

        public WeatherData GetRandomWeatherData()
        {
            if (weatherData.Count == 0)
            {
                Debug.LogError("WeatherConfig未设置或没有配置任何天气类型。");
                return GetWeatherData(WeatherType.Sunny); // 默认返回晴天
            }

            // 计算总概率
            var totalProbability = 0f;
            foreach (var wp in weatherData)
            {
                totalProbability += wp.weatherRatio;
            }

            if (totalProbability <= 0f)
            {
                Debug.LogError("总概率必须大于0。");
                return GetWeatherData(WeatherType.Sunny); // 默认返回晴天
            }

            // 生成一个0到totalProbability之间的随机数
            var randomValue = Random.Range(0f, totalProbability);

            // 遍历天气类型，找到随机数落入的概率区间
            var cumulative = 0f;
            foreach (var wp in weatherData)
            {
                cumulative += wp.weatherRatio;
                if (randomValue <= cumulative)
                {
                    return GetWeatherData(wp.weatherType);
                }
            }

            // 由于浮点数精度问题，返回最后一个天气类型
            return weatherData[^1];
        }
    }

    [Serializable]
    public struct WeatherData
    {
        public WeatherType weatherType;
        public float weatherRatio;
        public Color cloudColor;
        public Range cloudDensityRange;
        public Range cloudSpeedRange;
        public Range lightIntensity;
        public float fogRatio;
        public Range fogDensity;
        public float thunderRatio;
        public Range rainDensity;
        public Range snowDensity;
    }

    [Serializable]
    public struct DayNightCycleData
    {
        public float timeMultiplier;
        public float weatherChangeTime;
        public float sunriseTime;
        public float sunsetTime;
        public Gradient  dayLightColor;
        public Gradient  nightColor;
    }

    public enum WeatherType
    {
        None,
        Sunny,
        Cloudy,
        Rainy,
        Snowy,
    }
}
