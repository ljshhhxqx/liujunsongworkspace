using HotUpdate.Scripts.Config;
using UnityEngine;

namespace HotUpdate.Scripts.Weather.WeatherEffects
{
    public class LightAndFogEffect : WeatherEffects
    {
        [SerializeField]
        private Light mainLight;
        private float _sunInitialIntensity;

        public override void PlayEffect(WeatherEffectData weatherData)
        {
            mainLight.intensity = weatherData.lightDensity;
            _sunInitialIntensity = weatherData.lightDensity;
            RenderSettings.fogDensity = weatherData.fogDensity;
            RenderSettings.fog = weatherData.enableFog;
        }

        public void UpdateSun(float currentTimeOfDay)
        {
            // 旋转太阳的位置，模拟太阳从日出到日落的运动
            mainLight.transform.localRotation = Quaternion.Euler((currentTimeOfDay * 360f) - 90, 170, 0);

            // 根据太阳高度调整光强度
            float intensityMultiplier = 1;
            if (currentTimeOfDay is <= 0.23f or >= 0.75f)
            {
                // 夜晚光强度减到0.1倍，表示黑夜仍有微弱光线
                intensityMultiplier = 0.1f;
            }
            else if (currentTimeOfDay <= 0.25f)
            {
                // 日出阶段，逐渐从0.1倍光强度增加到最大光强度
                intensityMultiplier = Mathf.Lerp(0.1f, 1f, (currentTimeOfDay - 0.23f) / 0.02f);
            }
            else if (currentTimeOfDay >= 0.73f)
            {
                // 日落阶段，逐渐从最大光强度减少到0.1倍光强度
                intensityMultiplier = Mathf.Lerp(1f, 0.1f, (currentTimeOfDay - 0.73f) / 0.02f);
            }

            // 根据光强度调整太阳光的亮度
            mainLight.intensity = intensityMultiplier * _sunInitialIntensity;
        }
    }

    public struct DayNightCycleData
    {
        
    }
}