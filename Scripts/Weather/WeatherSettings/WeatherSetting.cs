using System;
using HotUpdate.Scripts.Config;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Config.JsonConfig;
using UnityEngine;

namespace HotUpdate.Scripts.Weather.WeatherSettings
{
    public interface IIWeather
    {
        WeatherData WeatherData { get; }
        void LoadWeather(WeatherLoadData weatherData);
        void ClearWeather();
    }
    
    public abstract class WeatherSetting : MonoBehaviour, IIWeather 
    {
        [SerializeField]
        private WeatherType weatherType;
        public WeatherType WeatherType => weatherType;

        private WeatherLoadData _weatherLoadData;
        public WeatherData WeatherData { get; set; }
        public WeatherConstantData WeatherConstantData { get; set; }

        public virtual void LoadWeather(WeatherLoadData weatherData)
        {
            _weatherLoadData = weatherData;
            if (_weatherLoadData.weatherType == WeatherType.None)
            {
                throw new ArgumentException("WeatherType is not set.");
            }
        }

        public virtual void ClearWeather()
        {
            if (gameObject)
                gameObject.SetActive(false);
        }
    }
    
    public struct RainSnowSetting
    {
        public ParticleSystem.MinMaxCurve emissionRate; 
        public ParticleSystem.MinMaxCurve size;
        public ParticleSystem.MinMaxCurve speed;
    }
}
