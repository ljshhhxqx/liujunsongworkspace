using DG.Tweening;
using UnityEngine;

namespace HotUpdate.Scripts.Weather.WeatherEffects
{
    public class Clouds : WeatherEffects
    {
        [SerializeField]
        private Material lowCloud;
        [SerializeField]
        private Material highCloud;

        public override void PlayEffect(WeatherEffectData weatherData)
        {
            lowCloud.DOFloat(weatherData.cloudDensity, "_Density", WeatherConstData.weatherChangeTime);
            highCloud.DOFloat(weatherData.cloudDensity, "_Density", WeatherConstData.weatherChangeTime);
            lowCloud.DOColor(weatherData.cloudColor, "_CloudColor", WeatherConstData.weatherChangeTime);
            highCloud.DOColor(weatherData.cloudColor, "_CloudColor", WeatherConstData.weatherChangeTime);
            lowCloud.DOFloat(weatherData.cloudSpeed, "_Size", WeatherConstData.weatherChangeTime);
            highCloud.DOFloat(weatherData.cloudSpeed, "_Size", WeatherConstData.weatherChangeTime);
        }
    }
}