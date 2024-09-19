using System;
using DG.Tweening;
using HotUpdate.Scripts.Config;
using UniRx;
using UnityEngine;
using Random = UnityEngine.Random;

namespace HotUpdate.Scripts.Weather.WeatherEffects
{
    public class LightAndFogEffect : WeatherEffects, IDayNightCycle
    {
        [SerializeField]
        private Light mainLight;
        private float _sunInitialIntensity;
        private Tween _lightTween;
        private IDisposable _enableLightning;

        public DayNightCycleData DayNightCycleData { get; set; }

        public override void PlayEffect(WeatherEffectData weatherData)
        {
            mainLight.intensity = Mathf.Clamp01( weatherData.lightDensity);
            _sunInitialIntensity = Mathf.Clamp01( weatherData.lightDensity);
            RenderSettings.fogDensity = weatherData.fogDensity;
            RenderSettings.fog = weatherData.enableFog;
            UpdateLight(weatherData.enableThunder);
        }

        private void UpdateLight(bool enableLightning = false)
        {
            if (enableLightning)
            {
                _enableLightning = Observable.Defer(() => 
                    {
                        // 每次生成一个 5 到 10 秒之间的随机时间 
                        var randomTime = Random.Range(5f, 10f);
        
                        // 打印随机时间，以确认随机间隔是否正确
                        Debug.Log($"Next action in {randomTime} seconds.");

                        // 使用 Observable.Timer 来等待随机时间
                        return Observable.Timer(TimeSpan.FromSeconds(randomTime))
                            .Do(_ => 
                            {
                                _lightTween?.Kill();
                                mainLight.intensity = _sunInitialIntensity * 0.1f;
                                _lightTween = DOTween.To(() => mainLight.intensity, 
                                        x => mainLight.intensity = x, 
                                        _sunInitialIntensity, 0.5f) 
                                    .SetLoops(Random.Range(2, 4), LoopType.Yoyo)
                                    .OnComplete(() => 
                                    {
                                        // 完成后可以触发下一个随机的光照变化或其他效果
                                        mainLight.intensity = _sunInitialIntensity;
                                        Debug.Log("Lightning effect completed.");
                                    });
                            });
                    })
                    // 递归订阅以重复这一过程
                    .Repeat()
                    .Subscribe();
            }
            else
            {
                _enableLightning?.Dispose();
            }
        }

        public void UpdateSun(float currentTime)
        {
            var lightIntensity = 0f;

            var lightColor = Color.gray;

            if (currentTime >= DayNightCycleData.sunriseTime && currentTime < DayNightCycleData.sunsetTime)
            {
                // 白天
                var t = (currentTime - DayNightCycleData.sunriseTime) / (DayNightCycleData.sunsetTime - DayNightCycleData.sunriseTime); // [0,1]
                lightIntensity = Mathf.Sin(t * Mathf.PI); // 从0到1再到0
                lightIntensity *= _sunInitialIntensity;

                // 颜色渐变从日出到正午
                lightColor = DayNightCycleData.dayLightColor.Evaluate(t);
            }
            else
            {
                // 夜晚
                float t;
                if (currentTime >= DayNightCycleData.sunsetTime)
                {
                    t = (currentTime - DayNightCycleData.sunsetTime) / (24f - DayNightCycleData.sunsetTime);
                }
                else
                {
                    t = currentTime /  DayNightCycleData.sunriseTime;
                }

                lightIntensity = Mathf.Sin(t * Mathf.PI); // 从0到1再到0
                lightIntensity *= _sunInitialIntensity * 0.5f; // 夜晚光照强度较低

                // 颜色渐变
                lightColor = DayNightCycleData.nightColor.Evaluate(t);
            }

            // 设置光照强度和平滑过渡颜色
            mainLight.intensity = lightIntensity;
            mainLight.color = lightColor;

            // 调整光照角度
            var angle = (currentTime / 24f) * 360f - 90f; // 将时间转换为角度
            mainLight.transform.rotation = Quaternion.Euler(angle, 170f, 0f); // 调整太阳的角度
        }
    }
}