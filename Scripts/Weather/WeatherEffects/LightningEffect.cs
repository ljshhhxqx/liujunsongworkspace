using UnityEngine;

namespace HotUpdate.Scripts.Weather.WeatherEffects
{
    public class LightningEffect : WeatherEffects
    {
        [SerializeField]
        private TrailRenderer trailRenderer;

        public override void PlayEffect(WeatherEffectData weatherData)
        {
            trailRenderer.gameObject.SetActive(weatherData.enableThunder);
            trailRenderer.AddPosition(weatherData.thunderStartPos);
            trailRenderer.AddPosition(weatherData.thunderEndPos);
        }
    }
}