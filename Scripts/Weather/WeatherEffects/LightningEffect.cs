using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Tool.Coroutine;
using UniRx;
using UnityEngine;
using VContainer;
using Random = UnityEngine.Random;

namespace HotUpdate.Scripts.Weather.WeatherEffects
{
    public class LightningEffect : WeatherEffects
    {
        [SerializeField]
        private GameObject lightningPrefab;
        [SerializeField]
        private Transform lightningStartPos;
        [SerializeField]
        private Transform lightningEndPos;
        [SerializeField]
        private Light lightningLight;
        private IDisposable _enableLightning;
        private Tween _lightTween;
        private WeatherEffectData _weatherData;

        [Inject]
        private void Init()
        {
            lightningPrefab.gameObject.SetActive(false);
            lightningLight.gameObject.SetActive(false);
        }

        public override void PlayEffect(WeatherEffectData weatherData)
        {
            _weatherData = weatherData;
            UpdateLight(weatherData.enableThunder);
        }
        
        private void UpdateLight(bool enableLightning = false)
        {
            if (enableLightning)
            {
                Debug.Log("<WeatherManager> Lightning effect started.");
                _enableLightning = Observable.Defer(() => 
                    {
                        // 每次生成一个 5 到 10 秒之间的随机时间 
                        var randomTime = Random.Range(5f, 15f);
            
                        // 打印随机时间，以确认随机间隔是否正确
                        Debug.Log($"Next action in {randomTime} seconds.");
            
                        // 使用 Observable.Timer 来等待随机时间
                        return Observable.Timer(TimeSpan.FromSeconds(randomTime))
                            .Do(_ => 
                            {
                                lightningLight.gameObject.SetActive(true);
                                lightningLight.intensity = 0.1f;
                                _lightTween?.Kill();
                                _lightTween = DOTween.To(() => lightningLight.intensity, 
                                        x => lightningLight.intensity = x, 
                                        2f, 0.5f) 
                                    .SetLoops(Random.Range(2, 4), LoopType.Yoyo)
                                    .OnComplete(() => 
                                    {
                                        // 完成后可以触发下一个随机的光照变化或其他效果
                                        lightningLight.gameObject.SetActive(false);
                                        
                                        lightningPrefab.SetActive(_weatherData.enableThunder);
                                        var startPos = _weatherData.thunderStartPosGetter();
                                        lightningStartPos.position = startPos + Vector3.up * 100f;
                                        lightningEndPos.position = startPos;
                                        DelayInvoker.DelayInvoke(0.75f, DisableLightning);
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
        
        private void DisableLightning()
        {
            _enableLightning?.Dispose();
            lightningLight.gameObject.SetActive(false);
            lightningPrefab.SetActive(false);
        }

        private void OnDestroy()
        {
            _lightTween?.Kill();
            DelayInvoker.CancelInvoke(DisableLightning);
            _enableLightning?.Dispose();
        }
    }
}