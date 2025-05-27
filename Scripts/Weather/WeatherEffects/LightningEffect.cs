using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using HotUpdate.Scripts.Tool.Coroutine;
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
        }

        public void UpdateLightning(Vector3 startPos, Vector3 endPos)
        {
            lightningPrefab.SetActive(_weatherData.enableThunder);
            lightningStartPos.position = startPos;
            lightningEndPos.position = endPos;
            DelayInvoker.DelayInvoke(0.75f, DisableLightning);
        }

        public void UpdateLight()
        {
            lightningLight.gameObject.SetActive(true);
            lightningLight.intensity = 0.1f;
            _lightTween?.Kill();
            _lightTween = DOTween.To(() => lightningLight.intensity, 
                    x => lightningLight.intensity = x, 
                    2f, 0.5f) 
                .SetLoops(4, LoopType.Yoyo)
                .SetEase(Ease.Linear)
                .OnComplete(() => 
                {
                    // 完成后可以触发下一个随机的光照变化或其他效果
                    lightningLight.gameObject.SetActive(false);
                    Debug.Log("Lightning effect completed.");
                });
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