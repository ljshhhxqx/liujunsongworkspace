using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using Random = UnityEngine.Random;

namespace HotUpdate.Scripts.Config.ArrayConfig
{
    [CreateAssetMenu(fileName = "WeatherConfig", menuName = "ScriptableObjects/WeatherConfig")]
    public class WeatherConfig : ConfigBase
    {
        [ReadOnly]
        [SerializeField] private List<WeatherData> weatherData = new List<WeatherData>();

        public List<WeatherData> WeatherData => weatherData;

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

        protected override void ReadFromCsv(List<string[]> textAsset)
        {
            weatherData.Clear();
            for (int i = 2; i < textAsset.Count; i++)
            {
                var row = textAsset[i];
                var weatherData = new WeatherData
                {
                    weatherType = (WeatherType) Enum.Parse(typeof(WeatherType), row[0]),
                    weatherRatio = float.Parse(row[1]),
                    cloudDensityRange = JsonUtility.FromJson<Range>(row[2]),
                    cloudSpeedRange = JsonUtility.FromJson<Range>(row[3]),
                    lightIntensity = JsonUtility.FromJson<Range>(row[4]),
                    fogRatio = float.Parse(row[5]),
                    fogDensity = JsonUtility.FromJson<Range>(row[6]),
                    thunderRatio = float.Parse(row[7]),
                    rainDensity = JsonUtility.FromJson<Range>(row[8]),
                    snowDensity = JsonUtility.FromJson<Range>(row[9])
                };
                this.weatherData.Add(weatherData);
            }
        }
    }

    [Serializable]
    public struct WeatherData
    {
        public WeatherType weatherType;
        public float weatherRatio;
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
    public struct WeatherLoadData
    {
        public WeatherType weatherType;
        public float weatherRatio;
        public Color cloudColor;
        public float cloudDensity;
        public float cloudSpeed;
        public float lightIntensity;
        public float fogRatio;
        public float fogDensity;
        public float thunderRatio;
        public float rainDensity;
        public float snowDensity;
    }

    [Serializable]
    public struct DayNightCycleData
    {
        public float timeMultiplier;
        public float weatherChangeTime;
        public float sunriseTime;
        public float sunsetTime;
        public Gradient dayLightColor;
        public Gradient nightColor;
        public float oneDayDuration;
        public float minLightIntensity; 
        public List<CloudColorData> cloudColorGradients;
    }

    [Serializable]
    public struct CloudColorData
    {
        public WeatherType weatherType;
        public Gradient cloudColor;
    }

    [Serializable]
    public enum WeatherType
    {
        None,
        Sunny,
        Cloudy,
        Rainy,
        Snowy,
    }

    [Serializable]
    public struct WeatherInfo
    {
        public WeatherType weatherType;
        public float density;
    }

    public static class WeatherTypeExtension
    {
        public static string ToDescription(this WeatherInfo weatherInfo)
        {
            var density = Mathf.Clamp01(weatherInfo.density);
            switch (weatherInfo.weatherType)
            {
                case WeatherType.Sunny:
                    return "晴天";
                case WeatherType.Cloudy:
                    return "多云";
                case WeatherType.Rainy:
                    if (density is > 0f and <= 0.1f)
                    {
                        return "小雨";
                    }
                    if (density is > 0.1f and <= 0.25f) 
                    {
                        return "中雨";
                    }
                    if (density is > 0.25f and <= 0.5f) 
                    {
                        return "大雨";
                    }
                    if (density is > 0.5f and <= 0.75f) 
                    {
                        return "暴雨";
                    }
                    return "大暴雨";
                case WeatherType.Snowy:
                    if (density is > 0f and <= 0.1f)
                    {
                        return "小雪";
                    }
                    if (density is > 0.1f and <= 0.25f) 
                    {
                        return "中雪";
                    }
                    if (density is > 0.25f and <= 0.5f) 
                    {
                        return "大雪";
                    }
                    if (density is > 0.5f and <= 0.75f) 
                    {
                        return "暴雪";
                    }
                    return "大暴雪";
                default:
                    return "未知";
            }
        }
    }
}
