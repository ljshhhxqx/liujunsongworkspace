using System;
using HotUpdate.Scripts.Config;
using UnityEngine;

namespace HotUpdate.Scripts.Weather.WeatherEffects
{
    public class LightAndFogEffect : WeatherEffects, IDayNightCycle
    {
        [SerializeField]
        private Light mainLight;

        public DayNightCycleData DayNightCycleData { get; set; }

        public override void PlayEffect(WeatherEffectData weatherData)
        {
            mainLight.intensity = weatherData.lightDensity;
            RenderSettings.fogDensity = weatherData.fogDensity;
            RenderSettings.fog = weatherData.enableFog;
        }

        private void Update()
        {
            
        }
    }
}