using System;
using AOTScripts.Data;
using HotUpdate.Scripts.Audio;
using HotUpdate.Scripts.Config;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Config.JsonConfig;
using UnityEngine;
using UnityEngine.Serialization;

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
        [FormerlySerializedAs("weatherType")] [SerializeField]
        private WeatherType wType;
        [SerializeField]
        private AudioMusicType musicType;
        [SerializeField]
        private AudioEffectType subMusicType;
        public WeatherType WType => wType;

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

            if (subMusicType != AudioEffectType.None)
            {
                GameAudioManager.Instance.PlayLoopingMusic(subMusicType, transform.position, transform);
            }
            GameAudioManager.Instance.PlayMusic(musicType);
        }

        public virtual void ClearWeather()
        {
            if (gameObject)
                gameObject.SetActive(false);
            GameAudioManager.Instance.StopMusic();
            GameAudioManager.Instance.StopLoopingMusic(subMusicType);
        }
    }
    
    public struct RainSnowSetting
    {
        public ParticleSystem.MinMaxCurve emissionRate; 
        public ParticleSystem.MinMaxCurve size;
        public ParticleSystem.MinMaxCurve speed;
    }
}
