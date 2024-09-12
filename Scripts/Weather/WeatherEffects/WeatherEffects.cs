using HotUpdate.Scripts.Config;
using UnityEngine;

namespace HotUpdate.Scripts.Weather.WeatherEffects
{
    public abstract class WeatherEffects : MonoBehaviour
    {
        public abstract void PlayEffect(WeatherEffectData weatherData);
    }

    public interface IDayNightCycle
    {
        public DayNightCycleData DayNightCycleData { get; set; }
    }
}