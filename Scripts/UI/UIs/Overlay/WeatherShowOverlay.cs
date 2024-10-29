using AOTScripts.Tool;
using Data;
using HotUpdate.Scripts.Config;
using HotUpdate.Scripts.Data;
using HotUpdate.Scripts.Weather;
using TMPro;
using UI.UIBase;
using UniRx;
using UnityEngine;
using VContainer;

namespace HotUpdate.Scripts.UI.UIs.Overlay
{
    public class WeatherShowOverlay : ScreenUIBase
    {
        [SerializeField]
        private TextMeshProUGUI weatherText;
        [SerializeField]
        private TextMeshProUGUI showTimeText;
        [SerializeField]
        private TextMeshProUGUI countDownText;
        [SerializeField]
        private TextMeshProUGUI warmupText;
        [SerializeField]
        private TextMeshProUGUI scoreText;
        [SerializeField]
        private GameObject countDownGameObject;
        [SerializeField]
        private GameObject targetScore;
        private GameLoopData _gameLoopData;
        
        public override UIType Type => UIType.Weather;
        public override UICanvasType CanvasType => UICanvasType.Overlay;

        [Inject]
        private void Init()
        {
            //NetworkClient.RegisterHandler<>();
            GameLoopDataModel.WarmupRemainingTime.Subscribe(x => SetWarmupRemainingTime(x.ToHMSStr())).AddTo(this);
            GameLoopDataModel.GameLoopData.Subscribe(x =>
            {
                _gameLoopData = x;
                warmupText.transform.parent.gameObject.SetActive(false);
                targetScore.gameObject.SetActive(_gameLoopData.GameMode == GameMode.Score);
                countDownGameObject.SetActive(_gameLoopData.GameMode == GameMode.Time);
                scoreText.text = _gameLoopData.TargetScore.ToString();
            }).AddTo(this);
            GameLoopDataModel.GameRemainingTime.Subscribe(x => SetCountDown(x.ToHMSStr())).AddTo(this);
            WeatherDataModel.time.Subscribe(x => SetShowTime(x.ToHMSStr(false))).AddTo(this);
            WeatherDataModel.weatherInfo.Subscribe(x => SetWeather(x.ToDescription())).AddTo(this);
        }

        private void SetWarmupRemainingTime(string warmup)
        {
            warmupText.text = warmup;
        }


        private void SetCountDown(string countDown)
        {
            countDownText.text = countDown;
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
