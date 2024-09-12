using System;
using System.Collections.Generic;
using AOTScripts.Tool.ECS;
using HotUpdate.Scripts.Collector;
using HotUpdate.Scripts.Config;
using HotUpdate.Scripts.Weather.WeatherEffects;
using HotUpdate.Scripts.Weather.WeatherSettings;
using UnityEngine;
using UnityEngine.AddressableAssets;
using VContainer;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace HotUpdate.Scripts.Weather
{
    public class WeatherManager : NetworkMonoComponent
    {
        private float _currentTime;
        private float _timeMultiplier;  
        private bool _isDayNightCycle;
        
        private WeatherConfig _weatherConfig;
        private readonly List<WeatherSetting> _weatherPrefabs = new List<WeatherSetting>();
        private List<GameObject> _weatherEffectPrefabs = new List<GameObject>();
        private WeatherSetting _currentWeatherSetting;
        private MapBoundDefiner _mapBoundDefiner;
        private LightAndFogEffect _lightAndFogEffect;
        private readonly Dictionary<Type, WeatherEffects.WeatherEffects> _weatherEffectsDict = new Dictionary<Type, WeatherEffects.WeatherEffects>();
        private readonly Dictionary<WeatherType, WeatherSetting> _weatherSettingDict = new Dictionary<WeatherType, WeatherSetting>();

        [Inject]
        private async void Init(IConfigProvider configProvider, MapBoundDefiner mapBoundDefiner)
        {
            _weatherConfig = configProvider.GetConfig<WeatherConfig>();
            _mapBoundDefiner = mapBoundDefiner;
            _isDayNightCycle = false;
            _timeMultiplier = _weatherConfig.DayNightCycleData.timeMultiplier;
            _weatherEffectPrefabs = await ResourceManager.Instance.LoadResourcesAsync<GameObject>($"Weather/WeatherEffects");
            var weatherSettings = await ResourceManager.Instance.LoadResourcesAsync<GameObject>($"Weather/WeatherSettings");
            foreach (var weather in weatherSettings)
            {
                if (weather.TryGetComponent<WeatherSetting>(out var setting))
                {
                    _weatherPrefabs.Add(setting);
                }
            }
            _currentTime = Random.Range(0f, 1f);
        }

        public void SetWeather(WeatherType weatherType)
        {
            _isDayNightCycle = true;
            var data = _weatherConfig.GetWeatherData(weatherType);
            if (data.weatherType == WeatherType.None)
            {
                throw new Exception($"Weather Data not found for {weatherType}");
            }
            ChangeWeatherEffects(data);
            
            if (_weatherSettingDict.TryGetValue(weatherType, out var setting))
            {
                _currentWeatherSetting = setting;
            }
            else
            {
                var prefab = _weatherPrefabs.Find(w => w.WeatherData.weatherType == weatherType);

                if (prefab == null)
                {
                    throw new Exception($"Weather Setting not found for {weatherType}");
                }
            
                var instance = Object.Instantiate(prefab.gameObject);
                var settingComponent = instance.GetComponent<WeatherSetting>();
                _weatherSettingDict.Add(weatherType, settingComponent);
                _currentWeatherSetting = settingComponent;
            }
            _currentWeatherSetting.SetWeatherData(data);
            _currentWeatherSetting.gameObject.SetActive(true);
            _currentWeatherSetting.LoadWeather();
        }

        private void ChangeWeatherEffects(WeatherData data)
        {
            var enableFog = Random.Range(0, 1) <= data.fogRatio && data.fogRatio > 0;
            var fogDensity = data.fogDensity.GetRandomValue();
            var cloudSpeed = data.cloudSpeedRange.GetRandomValue();
            var cloudDensity = data.cloudDensityRange.GetRandomValue();
            var cloudColor = data.cloudColor;
            var lightDensity = data.lightIntensity.GetRandomValue();
            var enableThunder = Random.Range(0, 1) <= data.thunderRatio && data.thunderRatio > 0;
            var thunderEndPos = _mapBoundDefiner.GetRandomPoint();
            var thunderStartPos = new Vector3(thunderEndPos.x, thunderEndPos.y + 100, thunderEndPos.z);
            var weatherEffectData = new WeatherEffectData
            {
                enableFog = enableFog,
                fogDensity = fogDensity,
                lightDensity = lightDensity,
                cloudSpeed = cloudSpeed,
                cloudDensity = cloudDensity,
                cloudColor = cloudColor,
                thunderStartPos = thunderStartPos,
                thunderEndPos = thunderEndPos,
                enableThunder = enableThunder,
            };
            foreach (var effect in _weatherEffectPrefabs)
            {
                var go = Object.Instantiate(effect);
                var component = go.GetComponent<WeatherEffects.WeatherEffects>();
                if (component != null)
                {
                    if (component.GetType() == typeof(LightAndFogEffect) && _lightAndFogEffect == null)
                    {
                        _lightAndFogEffect = component.GetComponent<LightAndFogEffect>();
                    }

                    _weatherEffectsDict.Add(component.GetType(), component);
                    component.PlayEffect(weatherEffectData);
                }
            }
        }

        private void Update()
        {
            if (!_isDayNightCycle)
            {
                return;
            }
            _lightAndFogEffect.UpdateSun(_currentTime);
            _currentTime += Time.deltaTime * _timeMultiplier;
            if (_currentTime >= 1f)
            {
                _currentTime = 0f;
            }
        }

        private void OnDestroy()
        {
            foreach (var effect in _weatherEffectsDict.Values)
            {
                Object.Destroy(effect.gameObject);
            }

            foreach (var setting in _weatherSettingDict.Values)
            {
                Object.Destroy(setting.gameObject);
            }
 
            foreach (var effect in _weatherEffectPrefabs)
            {
                Addressables.Release(effect);
            }

            foreach (var weatherSetting in _weatherPrefabs)
            {
                Addressables.Release(weatherSetting.gameObject);
            }
        }
    }

    public class WeatherEffectData
    {
        public bool enableFog;
        public bool enableThunder;
        public float fogDensity;
        public float lightDensity;
        public float cloudSpeed;
        public float cloudDensity;
        public Vector3 thunderStartPos;
        public Vector3 thunderEndPos;
        public Color cloudColor;
    }
}
