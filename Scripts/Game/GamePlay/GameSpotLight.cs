using HotUpdate.Scripts.Weather;
using UniRx;
using UnityEngine;

namespace HotUpdate.Scripts.Game.GamePlay
{
    public class GameSpotLight : MonoBehaviour
    {
        private Light _spotLight;

        private void Start()
        {
            _spotLight = GetComponentInChildren<Light>();
            WeatherDataModel.IsDayTime
                .Subscribe(dayTime =>
                {
                    _spotLight.enabled = !dayTime;
                })
                .AddTo(this);
        }
    }
}
