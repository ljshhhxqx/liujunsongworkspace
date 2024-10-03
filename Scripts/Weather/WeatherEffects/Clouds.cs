using DG.Tweening;
using HotUpdate.Scripts.Config;
using UniRx;
using UnityEngine;
using VContainer;

namespace HotUpdate.Scripts.Weather.WeatherEffects
{
    public class Clouds : WeatherEffects
    {
        [SerializeField]
        private Material lowCloud;
        [SerializeField]
        private Material highCloud;
        [SerializeField]
        private Gradient dawnDuskColorGradient; // 日出和日落的渐变颜色
        private Color _fixedColor;
        private DayNightCycleData _dayNightCycleData;
        private static readonly int Color = Shader.PropertyToID("_CloudColor");

        [Inject]
        private void Init(IConfigProvider configProvider)
        {
            var config = configProvider.GetConfig<WeatherConfig>();
            _dayNightCycleData = config.DayNightCycleData;
            WeatherDataModel.time.Subscribe(UpdateCloudsColor).AddTo(this);
        }

        public override void PlayEffect(WeatherEffectData weatherData)
        {
            _fixedColor = weatherData.cloudColor;
            Debug.Log($"Play Clouds Effect {weatherData.cloudDensity} {weatherData.cloudColor} {weatherData.cloudSpeed}");
            lowCloud.DOFloat(weatherData.cloudDensity, "_Density", WeatherConstData.weatherChangeTime);
            highCloud.DOFloat(weatherData.cloudDensity, "_Density", WeatherConstData.weatherChangeTime);
            lowCloud.DOColor(weatherData.cloudColor, Color, WeatherConstData.weatherChangeTime);
            highCloud.DOColor(weatherData.cloudColor, Color, WeatherConstData.weatherChangeTime);
            lowCloud.DOFloat(weatherData.cloudSpeed, "_Speed", WeatherConstData.weatherChangeTime);
            highCloud.DOFloat(weatherData.cloudSpeed, "_Speed", WeatherConstData.weatherChangeTime);
        }

        private void UpdateCloudsColor(float currentTime)
        {
            Color cloudColor;

            if (currentTime >= _dayNightCycleData.sunriseTime && currentTime < _dayNightCycleData.sunsetTime)
            {
                // 夜晚
                float t;
                if (currentTime >= _dayNightCycleData.sunsetTime)
                {
                    t = (currentTime - _dayNightCycleData.sunsetTime) / (_dayNightCycleData.oneDayDuration - _dayNightCycleData.sunsetTime);
                }
                else
                {
                    t = currentTime /  _dayNightCycleData.sunriseTime;
                }

                // 颜色渐变
                cloudColor = UnityEngine.Color.Lerp(_fixedColor, dawnDuskColorGradient.Evaluate(t), t);
            }
            else
            {
                // 夜晚
                float t;
                if (currentTime >= _dayNightCycleData.sunsetTime)
                {
                    t = (currentTime - _dayNightCycleData.sunsetTime) / (_dayNightCycleData.oneDayDuration - _dayNightCycleData.sunsetTime);
                }
                else
                {
                    t = currentTime /  _dayNightCycleData.sunriseTime;
                }

                // 颜色渐变
                cloudColor = UnityEngine.Color.Lerp(_fixedColor, dawnDuskColorGradient.Evaluate(t), t);
            }
            highCloud.SetColor(Color, cloudColor);
            lowCloud.SetColor(Color, cloudColor);
        }
    }
}