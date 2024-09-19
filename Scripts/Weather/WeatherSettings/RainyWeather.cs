using DG.Tweening;
using HotUpdate.Scripts.Config;
using UniRx;
using UnityEngine;

namespace HotUpdate.Scripts.Weather.WeatherSettings
{
    public class RainyWeather : WeatherSetting
    {
        [SerializeField]
        private ParticleSystem rainParticles;
        [SerializeField]
        private Material rainMaterial;
        [SerializeField]
        private Material rainCoverMaterial;
        private float _snowDensity;
        private readonly ReactiveProperty<float> _currentRainDensity = new ReactiveProperty<float>();
        private RainSnowSetting _originalRainSnowSetting;

        private void Start()
        {
            var mainModule = rainParticles.main;
            var emission =  rainParticles.emission;
            _originalRainSnowSetting = new RainSnowSetting
            {
                emissionRate = emission.rateOverTime,
                size = mainModule.startSize,
                speed = mainModule.startSpeed,
            };
            _currentRainDensity.Subscribe(x =>
            {
                rainCoverMaterial.SetFloat(Shader.PropertyToID("_Wetness"), x);
                mainModule.startSize = _originalRainSnowSetting.size.constant * x;
                mainModule.startSpeed = _originalRainSnowSetting.speed.constant * x;
                emission.rateOverTime = _originalRainSnowSetting.emissionRate.constant * x;
            }).AddTo(this);
        }

        public override void LoadWeather(WeatherData weatherData)
        {
            _snowDensity = weatherData.rainDensity.GetRandomValue();
            var duration = Mathf.Lerp(WeatherConstData.maxTransitionDuration, WeatherConstData.minTransitionDuration, _snowDensity);
            DOTween.To(() => _currentRainDensity.Value, x => _currentRainDensity.Value = x, _snowDensity, duration);
            
            rainParticles.Play();
        }

        public override void ClearWeather()
        {
            _snowDensity = 0;
            var mainModule = rainParticles.main;
            var emission =  rainParticles.emission;
            _currentRainDensity.Value = 0;
            DOTween.To(() => _currentRainDensity.Value, 
                x => _currentRainDensity.Value = x, 
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
