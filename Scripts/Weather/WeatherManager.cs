using System;
using System.Collections.Generic;
using AOTScripts.Tool;
using AOTScripts.Tool.ECS;
using Cysharp.Threading.Tasks;
using HotUpdate.Scripts.Collector;
using HotUpdate.Scripts.Config;
using HotUpdate.Scripts.UI.UIs.Overlay;
using HotUpdate.Scripts.Weather.WeatherEffects;
using HotUpdate.Scripts.Weather.WeatherSettings;
using Tool.GameEvent;
using Tool.Message;
using UI.UIBase;
using UnityEngine;
using UnityEngine.AddressableAssets;
using VContainer;
using Random = UnityEngine.Random;

namespace HotUpdate.Scripts.Weather
{
    public class WeatherManager : ServerNetworkComponent
    {
        //[SyncVar]
        private float _dayNightCycleTime;
        //[SyncVar]
        private bool _isDayNightCycle;
        private float _updateTimer;
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
        private Clouds _clouds;
        private List<Material> _weatherMaterials = new List<Material>();
        private IObjectResolver _objectResolver;
        private UIManager _uiManager;
        private readonly Dictionary<Type, WeatherEffects.WeatherEffects> _weatherEffectsDict = new Dictionary<Type, WeatherEffects.WeatherEffects>();
        private readonly Dictionary<WeatherType, WeatherSetting> _weatherSettingDict = new Dictionary<WeatherType, WeatherSetting>();

        [Inject]
        private async void Init(MessageCenter messageCenter, IObjectResolver objectResolver,GameEventManager gameEventManager,IConfigProvider configProvider, 
            MapBoundDefiner mapBoundDefiner, UIManager uiManager)
        {
            _uiManager = uiManager;
            _weatherConfig = configProvider.GetConfig<WeatherConfig>();
            _mapBoundDefiner = mapBoundDefiner;
            _isDayNightCycle = false;
            _gameEventManager = gameEventManager;
            _objectResolver = objectResolver;
            _timeMultiplier = _weatherConfig.DayNightCycleData.timeMultiplier;
            _gameEventManager.Subscribe<GameReadyEvent>(OnGameReady);
            _weatherCycleDuration = _weatherConfig.DayNightCycleData.weatherChangeTime;
            Debug.Log("WeatherManager init");
            await GetWeatherResourcesAsync();
        }

        private async UniTask GetWeatherResourcesAsync()
        {
            _weatherMaterials = await ResourceManager.Instance.LoadResourcesAsync<Material>($"Weather/Materials");
            _weatherEffectPrefabs = await ResourceManager.Instance.LoadResourcesAsync<GameObject>($"Weather/WeatherEffects");
            var weatherSettings = await ResourceManager.Instance.LoadResourcesAsync<GameObject>($"Weather/WeatherSettings");
            foreach (var weather in weatherSettings)
            {
                if (weather.TryGetComponent<WeatherSetting>(out var setting))
                {
                    setting.WeatherData = _weatherConfig.GetWeatherData(setting.WeatherType);
                    _weatherPrefabs.Add(setting);
                }
            }
        }

        private void OnGameReady(GameReadyEvent gameReadyEvent)
        {
            WeatherDataModel.Init();
            _uiManager.SwitchUI<WeatherShowOverlay>();
            CmdStartWeatherLoop(true);
        }

        //[Command]
        private void CmdStartWeatherLoop(bool isStart)
        {
            _isDayNightCycle = isStart;
            if (isStart)
            {
                _dayNightCycleTime = Random.Range(0f, _weatherConfig.DayNightCycleData.oneDayDuration);
                WeatherDataModel.time.Value = _dayNightCycleTime;
                RpcSetWeather();
            }
        }

        //[ClientRpc]
        private void RpcSetWeather()
        {
            RandomWeather();
        }

        public void RandomWeather()
        {
            var randomWeather = _weatherConfig.GetRandomWeatherData();
            SetWeather(randomWeather.weatherType);
            Debug.Log("<WeatherManager>---- Random Weather: " + randomWeather.weatherType);
            Debug.Log("<WeatherManager>---- Random time: " + _dayNightCycleTime.ToHMSStr());
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
                _objectResolver.Inject(settingComponent);
                _weatherSettingDict.Add(weatherType, settingComponent);
                _currentWeatherSetting = settingComponent;
            }
            _currentWeatherSetting.gameObject.SetActive(true);
            var loadData = new WeatherLoadData
            {
                weatherType = data.weatherType,
                weatherRatio = data.weatherRatio,
                rainDensity = data.rainDensity.GetRandomValue(),
                cloudDensity = data.cloudDensityRange.GetRandomValue(),
                cloudSpeed = data.cloudSpeedRange.GetRandomValue(),
                lightIntensity = data.lightIntensity.GetRandomValue(),
                fogDensity = data.fogDensity.GetRandomValue(),
                fogRatio = data.fogRatio,
                thunderRatio = data.thunderRatio,
                snowDensity = data.snowDensity.GetRandomValue(),
            };
            _currentWeatherSetting.LoadWeather(loadData);
            WeatherDataModel.weatherInfo.Value = new WeatherInfo
            {   
                weatherType = loadData.weatherType,
                density = loadData.weatherType switch
                {
                    WeatherType.Rainy => loadData.rainDensity,
                    WeatherType.Snowy => loadData.snowDensity,
                    _ => 0f
                },

            };
        }
        private Vector3 GetRandomPos() => _mapBoundDefiner.GetRandomPoint();

        private void ChangeWeatherEffects(WeatherData data)
        {
            var enableFog = Random.Range(0, 1) <= data.fogRatio && data.fogRatio > 0;
            var fogDensity = data.fogDensity.GetRandomValue();
            var cloudSpeed = data.cloudSpeedRange.GetRandomValue();
            var cloudDensity = data.cloudDensityRange.GetRandomValue();
            var lightDensity = data.lightIntensity.GetRandomValue();
            var enableThunder = Random.Range(0, 1) <= data.thunderRatio && data.thunderRatio > 0;
            var weatherEffectData = new WeatherEffectData
            {
                weatherType = data.weatherType,
                enableFog = enableFog,
                fogDensity = fogDensity,
                lightDensity = lightDensity,
                cloudSpeed = cloudSpeed,
                cloudDensity = cloudDensity,
                thunderStartPosGetter = GetRandomPos,
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
                    else if (component.GetType() == typeof(Clouds) && _clouds == null)
                    {
                        _clouds = component.GetComponent<Clouds>();
                    }
                    _objectResolver.Inject(component);

                    _weatherEffectsDict.TryAdd(component.GetType(), component);
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
            _updateTimer += Time.deltaTime;
            _weatherCycleTimer += Time.deltaTime;
            if (_weatherCycleTimer >= _weatherCycleDuration)
            {
                _weatherCycleTimer = 0f;
                RandomWeather();
            }

            if (_updateTimer >= 1f)
            {
                _updateTimer = 0f;
                WeatherDataModel.time.Value = _dayNightCycleTime;
            }
            _dayNightCycleTime += Time.deltaTime * _timeMultiplier;
            if (_dayNightCycleTime >= _weatherConfig.DayNightCycleData.oneDayDuration)
            {
                _dayNightCycleTime = 0f;
            }
        }

        private void OnDestroy()
        {
            foreach (var effect in _weatherEffectsDict.Values)
            {
                if (effect.gameObject != null)
                {
                    Destroy(effect.gameObject);
                }
            }

            foreach (var setting in _weatherSettingDict.Values)
            {
                if (setting.gameObject != null)
                {
                    Destroy(setting.gameObject);
                }
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
            WeatherDataModel.Dispose();
            _uiManager.CloseUI(UIType.Weather);
        }
    }

    public class WeatherEffectData
    {
        public WeatherType weatherType;
        public bool enableFog;
        public bool enableThunder;
        public float fogDensity;
        public float lightDensity;
        public float cloudSpeed;
        public float cloudDensity;
        public Func<Vector3> thunderStartPosGetter;
    }
}
