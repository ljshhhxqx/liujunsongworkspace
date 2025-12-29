using DG.Tweening;
using HotUpdate.Scripts.Config;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Game.GamePlay;
using UniRx;
using UnityEngine;
using VContainer;

namespace HotUpdate.Scripts.Weather.WeatherEffects
{
    public class LightAndFogEffect : WeatherEffects
    {
        [SerializeField]
        private Light mainLight;
        private float _sunInitialIntensity;
        private DayNightCycleData _dayNightCycleData;
        
        [Inject]
        private void Init(IConfigProvider configProvider)
        {
            var config = configProvider.GetConfig<JsonDataConfig>();
            _dayNightCycleData = config.DayNightCycleData;
            WeatherDataModel.GameTime.Subscribe(UpdateSun).AddTo(this);
        }

        public override void PlayEffect(WeatherEffectData weatherData)
        {
            mainLight.intensity = Mathf.Clamp01( weatherData.lightDensity);
            _sunInitialIntensity = Mathf.Clamp01( weatherData.lightDensity);
            RenderSettings.fogDensity = weatherData.fogDensity;
            RenderSettings.fog = weatherData.enableFog;
        }

        private void UpdateSun(float currentTime)
        {
            float lightIntensity;
            Color lightColor;

            if (currentTime >= _dayNightCycleData.sunriseTime && currentTime < _dayNightCycleData.sunsetTime)
            {
                WeatherDataModel.IsDayTime.Value = true;
                // 白天
                var t = (currentTime - _dayNightCycleData.sunriseTime) / (_dayNightCycleData.sunsetTime - _dayNightCycleData.sunriseTime); // [0,1]

                lightIntensity = (2f / 3f * _sunInitialIntensity) + (1f / 3f * _sunInitialIntensity) * Mathf.Sin(t * Mathf.PI);
                // 颜色渐变从日出到正午
                lightColor = _dayNightCycleData.dayLightColor.Evaluate(t);
            }
            else
            {
                WeatherDataModel.IsDayTime.Value = false;
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


                // 使用三角函数在minIntensity和currentMaxIntensity之间取值
                lightIntensity = _dayNightCycleData.minLightIntensity + (2f / 3f * _sunInitialIntensity - _dayNightCycleData.minLightIntensity) * (1 + Mathf.Cos(t * Mathf.PI)) / 2;
                // 颜色渐变
                lightColor = _dayNightCycleData.nightColor.Evaluate(t);
            }

            // 设置光照强度和平滑过渡颜色
            mainLight.DOIntensity(lightIntensity, 0.9f).SetEase(Ease.Linear);
            mainLight.DOColor(lightColor, 0.9f).SetEase(Ease.Linear);
            // Debug.Log($"(WeatherManager)(-Target-) Light intensity: {lightIntensity} Color: {lightColor}");
            // Debug.Log($"(WeatherManager)(-Current-) Light intensity: {mainLight.intensity} Color: {mainLight.color}");
            // 调整光照角度
            var timeRatio = currentTime / _dayNightCycleData.oneDayDuration;
            var sunAngle = timeRatio * 360f - 90f; // 太阳角度，从-90度（东升）到270度（西落）
            var targetRotation = Quaternion.Euler(new Vector3(sunAngle, 0f, 0f));
            mainLight.transform.DORotateQuaternion(targetRotation, 0.99f); // 在1秒内平滑过渡到目标角度
        }
    }
}