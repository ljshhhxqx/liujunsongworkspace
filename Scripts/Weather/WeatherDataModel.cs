﻿using HotUpdate.Scripts.Config;
using HotUpdate.Scripts.Config.ArrayConfig;
using UniRx;
using UnityEngine;

namespace HotUpdate.Scripts.Weather
{
    public static class WeatherDataModel
    {
        public static ReactiveProperty<float> time;
        public static ReactiveProperty<WeatherInfo> weatherInfo;
        
        public static void Init()
        {
            time = new ReactiveProperty<float>();
            weatherInfo = new ReactiveProperty<WeatherInfo>();
        }

        public static void Dispose()
        {
            time.Dispose();
            weatherInfo.Dispose();
        }
    }
}