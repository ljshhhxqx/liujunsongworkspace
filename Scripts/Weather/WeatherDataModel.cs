using AOTScripts.Data;
using HotUpdate.Scripts.Tool.ReactiveProperty;
using UniRx;

namespace HotUpdate.Scripts.Weather
{
    public static class WeatherDataModel
    {
        public static HReactiveProperty<float> time;
        public static HReactiveProperty<WeatherInfo> weatherInfo;
        
        public static void Init()
        {
            time = new HReactiveProperty<float>();
            weatherInfo = new HReactiveProperty<WeatherInfo>();
        }

        public static void Dispose()
        {
            time.Dispose();
            weatherInfo.Dispose();
        }
    }
}