using System.Linq;
using DG.Tweening;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Mat;
using UniRx;
using UnityEngine;
using VContainer;

namespace HotUpdate.Scripts.Weather.WeatherEffects
{
    public class Clouds : WeatherEffects
    {
        [SerializeField]
        private Renderer lowCloud;
        [SerializeField]
        private Renderer highCloud;
        private MaterialPropertyBlock _lowCloudBlock;
        private MaterialPropertyBlock _highCloudBlock;
        private Gradient _colorGradient;
        private DayNightCycleData _dayNightCycleData;
        private WeatherConstantData _weatherConstantData;
        private static readonly int ColorProperty = Shader.PropertyToID("_CloudColor");
        private static readonly int SpeedProperty = Shader.PropertyToID("_Speed");
        private static readonly int DensityProperty = Shader.PropertyToID("_Density");

        [Inject]
        private void Init(IConfigProvider configProvider)
        {
            var config = configProvider.GetConfig<JsonDataConfig>();
            _dayNightCycleData = config.DayNightCycleData;
            _weatherConstantData = config.WeatherConstantData;
            _lowCloudBlock = MatStaticExtension.GetPropertyBlock(lowCloud);
            _highCloudBlock = MatStaticExtension.GetPropertyBlock(highCloud);
            WeatherDataModel.time.Subscribe(UpdateCloudsColor).AddTo(this);
        }

        public override void PlayEffect(WeatherEffectData weatherData)
        {
            _colorGradient = _dayNightCycleData.cloudColorGradients.FirstOrDefault(x => x.weatherType == weatherData.weatherType).cloudColor;
            Debug.Log($"Play Clouds Effect {weatherData.cloudDensity} {weatherData.cloudSpeed}");
            DOTween.To(() => _lowCloudBlock.GetFloat(DensityProperty), x =>
            {
                _lowCloudBlock.SetFloat(DensityProperty, x);
                lowCloud.SetPropertyBlock(_lowCloudBlock);
            },
                weatherData.cloudDensity, _weatherConstantData.weatherChangeTime);
            DOTween.To(() => _highCloudBlock.GetFloat(DensityProperty), x =>
            {
                _highCloudBlock.SetFloat(DensityProperty, x);
                highCloud.SetPropertyBlock(_highCloudBlock);
            },
                weatherData.cloudDensity, _weatherConstantData.weatherChangeTime);
            _lowCloudBlock.SetFloat(SpeedProperty, weatherData.cloudSpeed);
            _highCloudBlock.SetFloat(SpeedProperty, weatherData.cloudSpeed);
            lowCloud.SetPropertyBlock(_lowCloudBlock);
            highCloud.SetPropertyBlock(_highCloudBlock);
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
            _lowCloudBlock.SetColor(ColorProperty, cloudColor);
            _highCloudBlock.SetColor(ColorProperty, cloudColor);
            lowCloud.SetPropertyBlock(_lowCloudBlock);
            highCloud.SetPropertyBlock(_highCloudBlock);
        }

        private void OnDestroy()
        {
            MatStaticExtension.ClearCache(lowCloud);
            MatStaticExtension.ClearCache(highCloud);
        }
    }
}