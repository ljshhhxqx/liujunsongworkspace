using System;
using System.Collections.Generic;
using AOTScripts.Data;
using AOTScripts.Tool;
using AOTScripts.Tool.ECS;
using Cysharp.Threading.Tasks;
using HotUpdate.Scripts.Collector;
using HotUpdate.Scripts.Config;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Data;
using HotUpdate.Scripts.Tool.GameEvent;
using HotUpdate.Scripts.Tool.Message;
using HotUpdate.Scripts.Tool.ObjectPool;
using HotUpdate.Scripts.UI.UIBase;
using HotUpdate.Scripts.UI.UIs.Overlay;
using HotUpdate.Scripts.Weather.WeatherEffects;
using HotUpdate.Scripts.Weather.WeatherSettings;
using Mirror;
using Tool.Message;
using UI.UIBase;
using UniRx;
using UnityEngine;
using UnityEngine.AddressableAssets;
using VContainer;
using Random = UnityEngine.Random;

namespace HotUpdate.Scripts.Weather
{
    public class WeatherManager : ServerNetworkComponent
    {
        [SyncVar(hook = nameof(OnDayNightCycleTimeChanged))]
        private float _dayNightCycleTime;
        [SyncVar]
        private bool _isDayNightCycle;
        [SyncVar(hook = nameof(OnWeatherChanged))]
        private WeatherInfo _weatherInfo;
        private float _updateTimer;
        private float _timeMultiplier;  
        private float _weatherCycleTimer;
        private float _weatherCycleDuration;
        private GameEventManager _gameEventManager;
        private JsonDataConfig _jsonDataConfig;
        private WeatherConfig _weatherConfig;
        private MapConfig _mapConfig;
        private readonly List<WeatherSetting> _weatherPrefabs = new List<WeatherSetting>();
        private List<GameObject> _weatherEffectPrefabs = new List<GameObject>();
        private WeatherSetting _currentWeatherSetting;
        private LightAndFogEffect _lightAndFogEffect;
        private Clouds _clouds;
        private List<Material> _weatherMaterials = new List<Material>();
        private IObjectResolver _objectResolver;
        private UIManager _uiManager;
        private IDisposable _enableLightning;
        private bool _serverHandled;
        private readonly Dictionary<Type, WeatherEffects.WeatherEffects> _weatherEffectsDict = new Dictionary<Type, WeatherEffects.WeatherEffects>();
        private readonly Dictionary<WeatherType, WeatherSetting> _weatherSettingDict = new Dictionary<WeatherType, WeatherSetting>();

        private float DayNightCycleTime
        {
            get => _dayNightCycleTime;
            set
            {
                if (_serverHandled)
                {
                    _dayNightCycleTime = value;
                }
                else
                {
                    Debug.LogError("Client cannot change DayNightCycleTime");
                }
            }
        }
        
        private void OnDayNightCycleTimeChanged(float oldTime, float newTime)
        {
            WeatherDataModel.GameTime.Value = newTime;
//            Debug.Log($"OnDayNightCycleTimeChanged: {oldTime}, {newTime}");
        }
        
        private void OnWeatherChanged(WeatherInfo oldWeather, WeatherInfo newWeather)
        {
            WeatherDataModel.WeatherInfo.Value = newWeather;
            //Debug.Log($"OnWeatherChanged: {oldWeather}, {newWeather}");
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _serverHandled=true;
        }

        [Inject]
        private async void Init(MessageCenter messageCenter, IObjectResolver objectResolver,GameEventManager gameEventManager,IConfigProvider configProvider, 
            UIManager uiManager)
        {
            WeatherReaderWriter.RegisterReaderWriter(); 
            WeatherDataModel.Init();
            _uiManager = uiManager;
            _jsonDataConfig = configProvider.GetConfig<JsonDataConfig>();
            _weatherConfig = configProvider.GetConfig<WeatherConfig>();
            _mapConfig = configProvider.GetConfig<MapConfig>();
            _isDayNightCycle = false;
            _gameEventManager = gameEventManager;
            _objectResolver = objectResolver;
            _timeMultiplier = _jsonDataConfig.DayNightCycleData.timeMultiplier;
            _weatherCycleDuration = _jsonDataConfig.DayNightCycleData.weatherChangeTime;
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
                    setting.WeatherData = _weatherConfig.GetWeatherData(setting.WType);
                    setting.WeatherConstantData = _jsonDataConfig.WeatherConstantData;
                    _weatherPrefabs.Add(setting);
                }
            }
        }
        
        [Server]
        public void StartWeatherLoop(bool isStart)
        {
            if (!_serverHandled)
                return;
            _isDayNightCycle = isStart;
            if (isStart)
            {
                DayNightCycleTime = Random.Range(0f, _jsonDataConfig.DayNightCycleData.oneDayDuration);
                RandomWeather();
            }
        }

        private void RandomWeather()
        {
            _enableLightning?.Dispose();
            var mapData =_mapConfig.GetMapConfigData(GameLoopDataModel.GameSceneName.Value);
            var randomWeather = _weatherConfig.GetRandomWeatherData(mapData.availableWeather);
            var weatherEffectData = GetWeatherEffectData(randomWeather);

            if (weatherEffectData.enableThunder)
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
                            .Where(_ => this)
                            .Do(_ => 
                            {
                                var start = GetRandomPos();
                                var end = start + Vector3.up * 50f;
                                RpcEnableLightning(start, end);
                            });
                    })
                    // 递归订阅以重复这一过程
                    .Repeat()
                    .Subscribe();
            }

            var weatherLoadData = GetWeatherLoadData(randomWeather);
            _weatherInfo = new WeatherInfo
            {   
                weatherType = weatherLoadData.weatherType,
                density = weatherLoadData.weatherType switch
                {
                    WeatherType.Rainy => weatherLoadData.rainDensity,
                    WeatherType.Snowy => weatherLoadData.snowDensity,
                    _ => 0f
                }
            };
            //SetWeather(weatherLoadData);
            RpcSetWeather(weatherLoadData);
            RpcSetWeatherEffect(weatherEffectData);
            //ChangeWeatherEffects(weatherEffectData);
            Debug.Log("<WeatherManager>---- Random Weather: " + randomWeather.weatherType);
            Debug.Log("<WeatherManager>---- Random time: " + DayNightCycleTime.ToHMSStr());
        }

        [ClientRpc]
        private void RpcEnableLightning(Vector3 start, Vector3 end)
        {
            var effect = _weatherEffectsDict[typeof(LightningEffect)] as LightningEffect;
            if (effect)
            {
                effect.UpdateLightning(start, end);
                effect.UpdateLight();
            }
        }

        [ClientRpc]
        private void RpcSetWeather(WeatherLoadData loadData)
        {
            SetWeather(loadData);
        }

        [ClientRpc]
        private void RpcSetWeatherEffect(WeatherEffectData effectData)
        {
            ChangeWeatherEffects(effectData);
        }

        private void SetWeather(WeatherLoadData loadData)
        {
            if (_currentWeatherSetting)
                _currentWeatherSetting.ClearWeather();
            if (_weatherSettingDict.TryGetValue(loadData.weatherType, out var setting))
            {
                _currentWeatherSetting = setting;
            }
            else
            {
                var prefab = _weatherPrefabs.Find(w => w.WeatherData.weatherType == loadData.weatherType);

                if (!prefab)
                {
                    throw new Exception($"Weather Setting not found for {loadData.weatherType}");
                }
            
                var instance = GameObjectPoolManger.Instance.GetObject(prefab.gameObject, prefab.gameObject.transform.position, prefab.gameObject.transform.rotation, parent: transform, capacity: 1);
                var settingComponent = instance.GetComponent<WeatherSetting>();
                //instance.gameObject.SetActive(false);
                //_objectResolver.Inject(settingComponent);
                _weatherSettingDict.Add(loadData.weatherType, settingComponent);
                _currentWeatherSetting = settingComponent;
            }
            _currentWeatherSetting.LoadWeather(loadData);
            _currentWeatherSetting.gameObject.SetActive(true);
            
            if (!_uiManager.IsUIOpen(UIType.GameFlow))
            {
                _uiManager.SwitchUI<GameFlowOverlay>();
            }
        }
        
        private Vector3 GetRandomPos() => MapBoundDefiner.Instance.GetRandomPoint();

        private WeatherLoadData GetWeatherLoadData(WeatherData data)
        {
            return new WeatherLoadData
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
        }

        private WeatherEffectData GetWeatherEffectData(WeatherData data)
        {
            return new WeatherEffectData
            {
                weatherType = data.weatherType,
                enableFog = Random.Range(0, 1) <= data.fogRatio && data.fogRatio > 0,
                fogDensity = data.fogDensity.GetRandomValue(),
                lightDensity = data.lightIntensity.GetRandomValue(),
                cloudSpeed = data.cloudSpeedRange.GetRandomValue(),
                cloudDensity = data.cloudDensityRange.GetRandomValue(),
                enableThunder = Random.Range(0, 1) <= data.thunderRatio && data.thunderRatio > 0,
            };
        }

        private void ChangeWeatherEffects(WeatherEffectData data)
        {
            if (_weatherEffectsDict.Count != 0 && _weatherEffectsDict.Count == _weatherEffectPrefabs.Count)
            {
                foreach (var effect in _weatherEffectsDict.Values)
                {
                    effect.PlayEffect(data);
                }
                return;
            }
            foreach (var effect in _weatherEffectPrefabs)
            {
                var go = GameObjectPoolManger.Instance.GetObject(effect, effect.transform.position, effect.transform.rotation, parent: transform, capacity: 1);
                var component = go.GetComponent<WeatherEffects.WeatherEffects>();
                if (component)
                {
                    if (component.GetType() == typeof(LightAndFogEffect) && !_lightAndFogEffect)
                    {
                        _lightAndFogEffect = component.GetComponent<LightAndFogEffect>();
                    }
                    else if (component.GetType() == typeof(Clouds) && !_clouds)
                    {
                        _clouds = component.GetComponent<Clouds>();
                    }
                    _objectResolver.Inject(component);

                    _weatherEffectsDict.TryAdd(component.GetType(), component);
                    if (component.TryGetComponent<IDayNightCycle>(out var dayNightCycle))
                    {
                        dayNightCycle.DayNightCycleData = _jsonDataConfig.DayNightCycleData;
                    }

                    component.PlayEffect(data);
                }
            }
        }

        private void Update()
        {
            if (!_isDayNightCycle || !_serverHandled)
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
            }
            DayNightCycleTime += Time.deltaTime * _timeMultiplier;
            if (DayNightCycleTime >= _jsonDataConfig.DayNightCycleData.oneDayDuration)
            {
                DayNightCycleTime = 0f;
            }
        }

        private void OnDestroy()
        {
            _enableLightning?.Dispose();
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
            WeatherReaderWriter.UnregisterReaderWriter();
            if (isLocalPlayer)
            {
                _uiManager.CloseUI(UIType.Weather);
            }
        }
    }

    [Serializable]
    public class WeatherEffectData
    {
        public WeatherType weatherType;
        public bool enableFog;
        public bool enableThunder;
        public float fogDensity;
        public float lightDensity;
        public float cloudSpeed;
        public float cloudDensity;
    }
    
    public static class WeatherReaderWriter
    {
        public static void RegisterReaderWriter()
        {
            Reader<WeatherEffectData>.read = ReadWeatherEffectData;
            Writer<WeatherEffectData>.write = WriteWeatherEffectData;
            Reader<WeatherLoadData>.read = ReadWeatherLoadData;
            Writer<WeatherLoadData>.write = WriteWeatherLoadData;
            Reader<WeatherInfo>.read = ReadWeatherInfo;
            Writer<WeatherInfo>.write = WriteWeatherInfo;
            Reader<WeatherData>.read = ReadWeatherData;
            Writer<WeatherData>.write = WriteWeatherData;
        }

        private static WeatherEffectData ReadWeatherEffectData(NetworkReader reader)
        {
            return new WeatherEffectData
            {
                weatherType = (WeatherType)reader.ReadInt(),
                enableFog = reader.ReadBool(),
                enableThunder = reader.ReadBool(),
                fogDensity = reader.ReadFloat(),
                lightDensity = reader.ReadFloat(),
                cloudSpeed = reader.ReadFloat(),
                cloudDensity = reader.ReadFloat(),
            };
        }

        private static void WriteWeatherEffectData(NetworkWriter writer, WeatherEffectData value)
        {
            writer.WriteInt((int)value.weatherType);
            writer.WriteBool(value.enableFog);
            writer.WriteBool(value.enableThunder);
            writer.WriteFloat(value.fogDensity);
            writer.WriteFloat(value.lightDensity);
            writer.WriteFloat(value.cloudSpeed);
            writer.WriteFloat(value.cloudDensity);  
        }

        private static WeatherLoadData ReadWeatherLoadData(NetworkReader reader)
        {
            return new WeatherLoadData
            {
                weatherType = (WeatherType)reader.ReadInt(),
                weatherRatio = reader.ReadFloat(),
                rainDensity = reader.ReadFloat(),
                cloudDensity = reader.ReadFloat(),
                cloudSpeed = reader.ReadFloat(),
                lightIntensity = reader.ReadFloat(),
                fogDensity = reader.ReadFloat(),
                fogRatio = reader.ReadFloat(),
                thunderRatio = reader.ReadFloat(),
                snowDensity = reader.ReadFloat(),
            };
        }
        private static void WriteWeatherLoadData(NetworkWriter writer, WeatherLoadData value)
        {
            writer.WriteInt((int)value.weatherType);
            writer.WriteFloat(value.weatherRatio);
            writer.WriteFloat(value.rainDensity);
            writer.WriteFloat(value.cloudDensity);
            writer.WriteFloat(value.cloudSpeed);
            writer.WriteFloat(value.lightIntensity);
            writer.WriteFloat(value.fogDensity);
            writer.WriteFloat(value.fogRatio);
            writer.WriteFloat(value.thunderRatio);
            writer.WriteFloat(value.snowDensity);
        }
        
        private static WeatherInfo ReadWeatherInfo(NetworkReader reader)
        {
            return new WeatherInfo
            {
                weatherType = (WeatherType)reader.ReadInt(),
                density = reader.ReadFloat(),
            };
        }
        private static void WriteWeatherInfo(NetworkWriter writer, WeatherInfo value)
        {
            writer.WriteInt((int)value.weatherType);
            writer.WriteFloat(value.density);
        }
        
        private static WeatherData ReadWeatherData(NetworkReader reader)
        {
            return new WeatherData
            {   
                weatherType = (WeatherType)reader.ReadInt(),
                weatherRatio = reader.ReadFloat(),
                rainDensity = new Range(reader.ReadFloat(), reader.ReadFloat()),
                cloudDensityRange = new Range(reader.ReadFloat(), reader.ReadFloat()),
                cloudSpeedRange = new Range(reader.ReadFloat(), reader.ReadFloat()),
                lightIntensity = new Range(reader.ReadFloat(), reader.ReadFloat()),
                fogDensity = new Range(reader.ReadFloat(), reader.ReadFloat()),
                fogRatio = reader.ReadFloat(),
                thunderRatio = reader.ReadFloat(),
                snowDensity = new Range(reader.ReadFloat(), reader.ReadFloat()),
            };
        }
        private static void WriteWeatherData(NetworkWriter writer, WeatherData value)
        {
            writer.WriteInt((int)value.weatherType);
            writer.WriteFloat(value.weatherRatio);
            writer.WriteFloat(value.rainDensity.min);
            writer.WriteFloat(value.rainDensity.max);
            writer.WriteFloat(value.cloudDensityRange.min);
            writer.WriteFloat(value.cloudDensityRange.max);
            writer.WriteFloat(value.cloudSpeedRange.min);
            writer.WriteFloat(value.cloudSpeedRange.max);
            writer.WriteFloat(value.lightIntensity.min);
            writer.WriteFloat(value.lightIntensity.max);
            writer.WriteFloat(value.fogDensity.min);
            writer.WriteFloat(value.fogDensity.max);
            writer.WriteFloat(value.fogRatio);
            writer.WriteFloat(value.thunderRatio);
            writer.WriteFloat(value.snowDensity.min);
            writer.WriteFloat(value.snowDensity.max);
        }
        
        public static void UnregisterReaderWriter()
        {
            Reader<WeatherEffectData>.read = null;
            Writer<WeatherEffectData>.write = null;
            Reader<WeatherLoadData>.read = null;
            Writer<WeatherLoadData>.write = null;
            Reader<WeatherInfo>.read = null;
            Writer<WeatherInfo>.write = null;
            Reader<WeatherData>.read = null;
            Writer<WeatherData>.write = null;
        }
    }
}
