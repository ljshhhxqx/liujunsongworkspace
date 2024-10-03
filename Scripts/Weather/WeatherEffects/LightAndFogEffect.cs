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

        private void Start()
        {
            WeatherDataModel.time.Subscribe(UpdateSun).AddTo(this);
        }

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

        private void UpdateSun(float currentTime)
        {
            float lightIntensity;
            Color lightColor;

            if (currentTime >= DayNightCycleData.sunriseTime && currentTime < DayNightCycleData.sunsetTime)
            {
                // 白天
                var t = (currentTime - DayNightCycleData.sunriseTime) / (DayNightCycleData.sunsetTime - DayNightCycleData.sunriseTime); // [0,1]
                var dayMinIntensity = 2f / 3f * _sunInitialIntensity;
                lightIntensity = dayMinIntensity + (_sunInitialIntensity - dayMinIntensity) * Mathf.Sin(t * Mathf.PI / 2);

                // 颜色渐变从日出到正午
                lightColor = DayNightCycleData.dayLightColor.Evaluate(t);
            }
            else
            {
                // 夜晚
                float t;
                if (currentTime >= DayNightCycleData.sunsetTime)
                {
                    t = (currentTime - DayNightCycleData.sunsetTime) / (DayNightCycleData.oneDayDuration - DayNightCycleData.sunsetTime);
                }
                else
                {
                    t = currentTime /  DayNightCycleData.sunriseTime;
                }


                // 使用三角函数在minIntensity和currentMaxIntensity之间取值
                lightIntensity = DayNightCycleData.minLightIntensity + (_sunInitialIntensity - DayNightCycleData.minLightIntensity ) * (1 - Mathf.Cos(t * Mathf.PI)) / 2;

                // 颜色渐变
                lightColor = DayNightCycleData.nightColor.Evaluate(t);
            }

            // 设置光照强度和平滑过渡颜色
            mainLight.DOIntensity(lightIntensity, 1f);
            mainLight.DOColor(lightColor, 1f);
            // 调整光照角度
            var angle = (currentTime / DayNightCycleData.oneDayDuration) * 360f - 90f; // 将时间转换为角度
            var targetRotation = new Vector3(angle, 170f, 0f);
            mainLight.transform.DORotate(targetRotation, 1f); 
        }

        private void OnDestroy()
        {
            _enableLightning?.Dispose();
        }
    }
}