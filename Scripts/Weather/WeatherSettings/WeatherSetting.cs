using System;
using HotUpdate.Scripts.Config;
using UnityEngine;

namespace HotUpdate.Scripts.Weather.WeatherSettings
{
    public interface IIWeather
    {
        WeatherData WeatherData { get; }
        void SetWeatherData(WeatherData weatherData);
        void LoadWeather();
    }
    
    public abstract class WeatherSetting : MonoBehaviour, IIWeather 
    {
        [SerializeField]
        private WeatherType weatherType;
        public WeatherData WeatherData { get; private set; }

        public void SetWeatherData(WeatherData weatherData)
        {
            WeatherData = weatherData;
        }

        public virtual void LoadWeather()
        {
            if (WeatherData.weatherType == WeatherType.None)
            {
                throw new ArgumentException("WeatherType is not set.");
            }
        }
    }
}
