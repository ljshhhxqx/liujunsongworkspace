using System.Linq;
using DG.Tweening;
using HotUpdate.Scripts.Config;
using HotUpdate.Scripts.Config.JsonConfig;
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
        private Gradient _colorGradient;
        private DayNightCycleData _dayNightCycleData;
        private static readonly int ColorProperty = Shader.PropertyToID("_CloudColor");
        private static readonly int SpeedProperty = Shader.PropertyToID("_Speed");
        private static readonly int DensityProperty = Shader.PropertyToID("_Density");

        [Inject]
        private void Init(IConfigProvider configProvider)
        {
            var config = configProvider.GetConfig<JsonDataConfig>();
            _dayNightCycleData = config.DayNightCycleData;
            WeatherDataModel.time.Subscribe(UpdateCloudsColor).AddTo(this);
        }

        public override void PlayEffect(WeatherEffectData weatherData)
        {
            _colorGradient = _dayNightCycleData.cloudColorGradients.FirstOrDefault(x => x.weatherType == weatherData.weatherType).cloudColor;
            Debug.Log($"Play Clouds Effect {weatherData.cloudDensity} {weatherData.cloudSpeed}");
            lowCloud.DOFloat(weatherData.cloudDensity, DensityProperty, WeatherConstData.weatherChangeTime);
            highCloud.DOFloat(weatherData.cloudDensity, DensityProperty, WeatherConstData.weatherChangeTime);
            lowCloud.SetFloat(SpeedProperty, weatherData.cloudSpeed);
            highCloud.SetFloat(SpeedProperty, weatherData.cloudSpeed);
        }

        private void UpdateCloudsColor(float currentTime)
        {
            if (_colorGradient == null)
            {
                return;
            }
            var t = currentTime / _dayNightCycleData.oneDayDuration;
            // 颜色渐变
            var cloudColor = _colorGradient.Evaluate(t);
            highCloud.DOColor(cloudColor, ColorProperty, 0.99f);
            lowCloud.DOColor(cloudColor, ColorProperty, 0.99f);
        }
    }
}