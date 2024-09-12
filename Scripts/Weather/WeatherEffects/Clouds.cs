using UnityEngine;

namespace HotUpdate.Scripts.Weather.WeatherEffects
{
    public class Clouds : WeatherEffects
    {
        [SerializeField]
        private Material lowCloud;
        [SerializeField]
        private Material highCloud;

        public override void PlayEffect(WeatherEffectData weatherData)
        {
            lowCloud.SetColor(Shader.PropertyToID("_CloudColor"), weatherData.cloudColor);
            lowCloud.SetFloat(Shader.PropertyToID("_Density"), weatherData.cloudDensity);
            lowCloud.SetFloat(Shader.PropertyToID("_Size"), weatherData.cloudSpeed);
            
            highCloud.SetColor(Shader.PropertyToID("_CloudColor"), weatherData.cloudColor);
            highCloud.SetFloat(Shader.PropertyToID("_Density"), weatherData.cloudDensity);
            highCloud.SetFloat(Shader.PropertyToID("_Size"), weatherData.cloudSpeed);
        }
    }
}