using System;
using AOTScripts.Tool;
using HotUpdate.Scripts.Config;
using HotUpdate.Scripts.Weather;
using TMPro;
using UI.UIBase;
using UniRx;
using UnityEngine;

namespace HotUpdate.Scripts.UI.UIs.Overlay
{
    public class WeatherShowOverlay : ScreenUIBase
    {
        [SerializeField]
        private TextMeshProUGUI weatherText;
        [SerializeField]
        private TextMeshProUGUI showTimeText;
        
        public override UIType Type => UIType.Weather;
        public override UICanvasType CanvasType => UICanvasType.Overlay;

        private void Start()
        {
            WeatherDataModel.time.Subscribe(x => SetShowTime(x.ToHMSStr(false))).AddTo(this);
            WeatherDataModel.weatherInfo.Subscribe(x => SetWeather(x.ToDescription())).AddTo(this);
        }

        private void SetWeather(string weather)
        {
            weatherText.text = weather;
        }
        
        private void SetShowTime(string showTime)
        {
            showTimeText.text = showTime;
        }
    }
}
