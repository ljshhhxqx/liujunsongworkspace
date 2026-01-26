using System.Linq;
using AOTScripts.Data;
using DG.Tweening;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Data;
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
        private float _rainDensity;
        private Vector3 _size;
        private Vector3 _pos;
        private readonly ReactiveProperty<float> _currentRainDensity = new ReactiveProperty<float>();
        private RainSnowSetting _originalRainSnowSetting;

        private void Start()
        {
            var mainModule = rainParticles.main;
            var emission =  rainParticles.emission;
            var shape = rainParticles.shape;
            _pos = shape.position;
            _size = shape.scale;
            
            GameLoopDataModel.MapConfig
                .Subscribe(weather =>
            {
                if (weather.weatherMapData == null) return;
                var weathers = weather.weatherMapData.First(x => x.weatherType == WeatherType.Rainy);
                shape.scale = weathers.size;
                shape.position = weathers.position;
            }).AddTo(this);
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

        public override void LoadWeather(WeatherLoadData weatherData)
        {
            _rainDensity = weatherData.rainDensity;
            var duration = Mathf.Lerp(WeatherConstantData.maxTransitionDuration, WeatherConstantData.minTransitionDuration, _rainDensity);
            DOTween.To(() => _currentRainDensity.Value, x => _currentRainDensity.Value = x, _rainDensity, duration);
            
            rainParticles.Play();
        }

        public override void ClearWeather()
        {
            _rainDensity = 0;
            var mainModule = rainParticles.main;
            var emission =  rainParticles.emission;
            _currentRainDensity.Value = 0;
            DOTween.To(() => _currentRainDensity.Value, 
                x => _currentRainDensity.Value = x, 
                _rainDensity, 
                WeatherConstantData.minTransitionDuration).OnComplete(() =>
            {
                base.ClearWeather();
                mainModule.startSize = _originalRainSnowSetting.size;
                mainModule.startSpeed = _originalRainSnowSetting.speed;
                emission.rateOverTime = _originalRainSnowSetting.emissionRate;
            });
        }

        private void OnDestroy()
        {
            rainCoverMaterial.SetFloat(Shader.PropertyToID("_Wetness"), 0);
            var shape = rainParticles.shape;
            shape.position = _pos;
            shape.scale = _size;
        }
    }
}
