using AOTScripts.Data;
using HotUpdate.Scripts.Tool.ReactiveProperty;
using UniRx;

namespace HotUpdate.Scripts.Weather
{
    public static class WeatherDataModel
    {
        public static HReactiveProperty<float> GameTime;
        public static HReactiveProperty<WeatherInfo> WeatherInfo;
        public static HReactiveProperty<bool> IsDayTime;
        
        public static void Init()
        {
            GameTime = new HReactiveProperty<float>();
            WeatherInfo = new HReactiveProperty<WeatherInfo>();
        }

        public static void Dispose()
        {
            GameTime.Dispose();
            WeatherInfo.Dispose();
        }
    }
}