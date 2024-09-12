using System;
using System.Collections.Generic;
using Config;
using UnityEngine;
using UnityEngine.Serialization;

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
    }

    [Serializable]
    public struct WeatherData
    {
        public WeatherType weatherType;
        public Color cloudColor;
        public Range cloudDensityRange;
        public Range cloudSpeedRange;
        public Range lightIntensity;
        public float fogRatio;
        public Range fogDensity;
        public float thunderRatio;
    }

    [Serializable]
    public struct DayNightCycleData
    {
        public float dayDurationInMinutes;
        public float timeMultiplier;
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
