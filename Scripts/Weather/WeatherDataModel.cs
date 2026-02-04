using AOTScripts.Data;
using HotUpdate.Scripts.Tool.ReactiveProperty;
using UniRx;

namespace HotUpdate.Scripts.Weather
{
    public static class WeatherDataModel
    {
        public static HReactiveProperty<float> GameTime = new HReactiveProperty<float>();
        public static HReactiveProperty<string> WeatherInfo = new HReactiveProperty<string>();
        public static HReactiveProperty<bool> IsDayTime = new HReactiveProperty<bool>();
        
        public static void Init()
        {
            GameTime = new HReactiveProperty<float>();
            WeatherInfo = new HReactiveProperty<string>();
            IsDayTime = new HReactiveProperty<bool>();
        }

        public static void Dispose()
        {
            GameTime.Dispose();
            WeatherInfo.Dispose();
            IsDayTime.Dispose();
        }
    }
}