using System;
using HotUpdate.Scripts.Config;
using UnityEngine;

namespace HotUpdate.Scripts.Weather.WeatherSettings
{
    public interface IIWeather
    {
        WeatherData WeatherData { get; }
        void LoadWeather(WeatherData weatherData);
        void ClearWeather();
    }
    
    public abstract class WeatherSetting : MonoBehaviour, IIWeather 
    {
        public WeatherData WeatherData { get; private set; }

        public virtual void LoadWeather(WeatherData weatherData)
        {
            WeatherData = weatherData;
            if (WeatherData.weatherType == WeatherType.None)
            {
                throw new ArgumentException("WeatherType is not set.");
            }
        }

        public virtual void ClearWeather()
        {
            if (gameObject!= null)
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
