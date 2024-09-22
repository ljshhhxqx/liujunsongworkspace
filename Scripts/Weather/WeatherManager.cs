using System;
using System.Collections.Generic;
using AOTScripts.Tool.ECS;
using HotUpdate.Scripts.Collector;
using HotUpdate.Scripts.Config;
using HotUpdate.Scripts.Weather.WeatherEffects;
using HotUpdate.Scripts.Weather.WeatherSettings;
using Mirror;
using Tool.GameEvent;
using Tool.Message;
using UnityEngine;
using UnityEngine.AddressableAssets;
using VContainer;
using Random = UnityEngine.Random;

namespace HotUpdate.Scripts.Weather
{
    public class WeatherManager : ServerNetworkComponent
    {
        [SyncVar]
        private float _dayNightCycleTime;
        [SyncVar]
        private bool _isDayNightCycle;
        private float _timeMultiplier;  
        private float _weatherCycleTimer;
        private float _weatherCycleDuration;
        private GameEventManager _gameEventManager;
        private WeatherConfig _weatherConfig;
        private readonly List<WeatherSetting> _weatherPrefabs = new List<WeatherSetting>();
        private List<GameObject> _weatherEffectPrefabs = new List<GameObject>();
        private WeatherSetting _currentWeatherSetting;
        private MapBoundDefiner _mapBoundDefiner;
        private LightAndFogEffect _lightAndFogEffect;
        private List<Material> _weatherMaterials = new List<Material>();
        private readonly Dictionary<Type, WeatherEffects.WeatherEffects> _weatherEffectsDict = new Dictionary<Type, WeatherEffects.WeatherEffects>();
        private readonly Dictionary<WeatherType, WeatherSetting> _weatherSettingDict = new Dictionary<WeatherType, WeatherSetting>();

        [Inject]
        private async void Init(MessageCenter messageCenter, GameEventManager gameEventManager,IConfigProvider configProvider, MapBoundDefiner mapBoundDefiner)
        {
            _weatherConfig = configProvider.GetConfig<WeatherConfig>();
            _mapBoundDefiner = mapBoundDefiner;
            _isDayNightCycle = false;
            _timeMultiplier = _weatherConfig.DayNightCycleData.timeMultiplier;
            _gameEventManager.Subscribe<GameReadyEvent>(OnGameReady);
            _weatherCycleDuration = _weatherConfig.DayNightCycleData.weatherChangeTime;
            _weatherMaterials = await ResourceManager.Instance.LoadResourcesAsync<Material>($"Weather/Materials");
            _weatherEffectPrefabs = await ResourceManager.Instance.LoadResourcesAsync<GameObject>($"Weather/WeatherEffects");
            var weatherSettings = await ResourceManager.Instance.LoadResourcesAsync<GameObject>($"Weather/WeatherSettings");
            foreach (var weather in weatherSettings)
            {
                if (weather.TryGetComponent<WeatherSetting>(out var setting))
                {
                    _weatherPrefabs.Add(setting);
                }
            }
        }

        private void OnGameReady(GameReadyEvent gameReadyEvent)
        {
            CmdStartWeatherLoop(true);
        }

        [Command]
        private void CmdStartWeatherLoop(bool isStart)
        {
            _isDayNightCycle = isStart;
            if (isStart)
            {
                _dayNightCycleTime = Random.Range(0f, 1f);
                RpcSetWeather();
            }
        }

        [ClientRpc]
        private void RpcSetWeather()
        {
            RandomWeather();
        }

        public void RandomWeather()
        {
            var randomWeather = _weatherConfig.GetRandomWeatherData();
            SetWeather(randomWeather.weatherType);
        }

        public void SetWeather(WeatherType weatherType)
        {
            if (_currentWeatherSetting)
                _currentWeatherSetting.ClearWeather();
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
            
                var instance = Instantiate(prefab.gameObject);
                var settingComponent = instance.GetComponent<WeatherSetting>();
                _weatherSettingDict.Add(weatherType, settingComponent);
                _currentWeatherSetting = settingComponent;
            }
            _currentWeatherSetting.gameObject.SetActive(true);
            _currentWeatherSetting.LoadWeather(data);
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
            if (_weatherEffectsDict.Count != 0 && _weatherEffectsDict.Count == _weatherEffectPrefabs.Count)
            {
                foreach (var effect in _weatherEffectsDict.Values)
                {
                    effect.PlayEffect(weatherEffectData);
                }
                return;
            }
            foreach (var effect in _weatherEffectPrefabs)
            {
                var go = Instantiate(effect);
                var component = go.GetComponent<WeatherEffects.WeatherEffects>();
                if (component != null)
                {
                    if (component.GetType() == typeof(LightAndFogEffect) && _lightAndFogEffect == null)
                    {
                        _lightAndFogEffect = component.GetComponent<LightAndFogEffect>();
                    }

                    _weatherEffectsDict.Add(component.GetType(), component);
                    if (component.TryGetComponent<IDayNightCycle>(out var dayNightCycle))
                    {
                        dayNightCycle.DayNightCycleData = _weatherConfig.DayNightCycleData;
                    }

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
            _weatherCycleTimer += Time.deltaTime;
            if (_weatherCycleTimer >= _weatherCycleDuration)
            {
                _weatherCycleTimer = 0f;
                RandomWeather();
            }

            _lightAndFogEffect.UpdateSun(_dayNightCycleTime);
            _dayNightCycleTime += Time.deltaTime * _timeMultiplier;
            if (_dayNightCycleTime >= _timeMultiplier * 60 * 24)
            {
                _dayNightCycleTime = 0f;
            }
        }

        private void OnDestroy()
        {
            foreach (var effect in _weatherEffectsDict.Values)
            {
                Destroy(effect.gameObject);
            }

            foreach (var setting in _weatherSettingDict.Values)
            {
                Destroy(setting.gameObject);
            }
 
            foreach (var effect in _weatherEffectPrefabs)
            {
                Addressables.Release(effect);
            }

            foreach (var weatherSetting in _weatherPrefabs)
            {
                Addressables.Release(weatherSetting.gameObject);
            }
            
            foreach (var material in _weatherMaterials)
            {
                Addressables.Release(material);
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
