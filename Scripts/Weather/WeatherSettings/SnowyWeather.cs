using System;
using DG.Tweening;
using HotUpdate.Scripts.Config;
using UniRx;
using UnityEngine;

namespace HotUpdate.Scripts.Weather.WeatherSettings
{
    public class SnowyWeather : WeatherSetting
    {
        [SerializeField]
        private ParticleSystem snowParticles;
        [SerializeField]
        private Material snowMaterial;
        [SerializeField]
        private Material snowCoverMaterial;
        // 强度参数
        private float _snowDensity;
        private readonly ReactiveProperty<float> _currentSnowDensity = new ReactiveProperty<float>();
        private RainSnowSetting _originalRainSnowSetting;

        private void Start()
        {
            var mainModule = snowParticles.main;
            var emission =  snowParticles.emission;
            _originalRainSnowSetting = new RainSnowSetting
            {
                emissionRate = emission.rateOverTime,
                size = mainModule.startSize,
                speed = mainModule.startSpeed,
            };
            _currentSnowDensity.Subscribe(x =>
            {
                snowCoverMaterial.SetFloat(Shader.PropertyToID("_SnowCoverage"), x);
                mainModule.startSize = _originalRainSnowSetting.size.constant * x;
                mainModule.startSpeed = _originalRainSnowSetting.speed.constant * x;
                emission.rateOverTime = _originalRainSnowSetting.emissionRate.constant * x;
            }).AddTo(this);
        }

        public override void LoadWeather(WeatherData weatherData)
        {
            _snowDensity = weatherData.rainDensity.GetRandomValue();
            var duration = Mathf.Lerp(WeatherConstData.maxTransitionDuration, WeatherConstData.minTransitionDuration, _snowDensity);
            DOTween.To(() => _currentSnowDensity.Value, x => _currentSnowDensity.Value = x, _snowDensity, duration);
            
            snowParticles.Play();
        }

        public override void ClearWeather()
        {
            _snowDensity = 0;
            var mainModule = snowParticles.main;
            var emission =  snowParticles.emission;
            _currentSnowDensity.Value = 0;
            DOTween.To(() => _currentSnowDensity.Value, 
                x => _currentSnowDensity.Value = x, 
                _snowDensity, 
                WeatherConstData.minTransitionDuration).OnComplete(() =>
            {
                base.ClearWeather();
                mainModule.startSize = _originalRainSnowSetting.size;
                mainModule.startSpeed = _originalRainSnowSetting.speed;
                emission.rateOverTime = _originalRainSnowSetting.emissionRate;
            });
        }
    }
}