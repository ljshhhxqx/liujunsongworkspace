using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AOTScripts.Data;
using AOTScripts.Tool;
using Cysharp.Threading.Tasks;
using Data;
using DG.Tweening;
using Game;
using HotUpdate.Scripts.Data;
using HotUpdate.Scripts.Network.Data;
using HotUpdate.Scripts.Static;
using HotUpdate.Scripts.UI.UIBase;
using HotUpdate.Scripts.UI.UIs.Panel;
using HotUpdate.Scripts.UI.UIs.Panel.Item;
using HotUpdate.Scripts.UI.UIs.Panel.ItemList;
using TMPro;
using UI.UIBase;
using UniRx;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using VContainer;

namespace HotUpdate.Scripts.UI.UIs.Overlay
{
    public class GameFlowOverlay : ScreenUIBase
    {
        [Header("热身倒计时界面")]
        [SerializeField]
        private CanvasGroup warmupPanel;
        [SerializeField]
        private TextMeshProUGUI warmupCountdownText;
        [SerializeField]
        private TextMeshProUGUI warmupTitleText;
        [SerializeField]
        private Image warmupProgressCircle;
        
        [Header("游戏总倒计时界面")]
        [SerializeField]
        private CanvasGroup gameTimerPanel;
        [SerializeField]
        private TextMeshProUGUI gameTimerText;
        [SerializeField]
        private TextMeshProUGUI gameTimerTitleText;
        [SerializeField]
        private Image gameTimerProgressBar;
        
        [Header("游戏结束界面")]
        [SerializeField]
        private CanvasGroup gameOverPanel;
        [SerializeField]
        private TextMeshProUGUI messageText;
        [SerializeField]
        private ContentItemList gameOverItemList;
        [SerializeField]
        private Button goOnButton;
        
        [Header("动画设置")]
        [SerializeField]
        private float panelFadeDuration = 0.5f;
        [SerializeField]
        private float numberScaleDuration = 0.8f;
        [SerializeField]
        private float warningThreshold = 5f; // 最后5秒警告
        
        [Header("颜色设置")]
        public Color warmupColor = Color.yellow;
        public Color normalTimeColor = Color.white;
        public Color warningTimeColor = Color.red;
        public Color victoryColor = Color.green;
        public Color defeatColor = Color.red;
        
        private Sequence _warmupSequence;
        private Sequence _gameTimerSequence;
        private Tween _gameTimerTween;
        
        private float _totalGameTime;
        private float _currentGameTime;
        private bool _isGameRunning;
        private GameResult _gameResult;

        [Inject]
        private void Init(GameSceneManager gameSceneManager, UIManager uiManager)
        {
            InitializeUI();
            DOTween.Init();
            GameLoopDataModel.WarmupRemainingTime
                .Where(time => time <= 3)
                .Take(1)
                .Subscribe(_ => StartWarmupCountdown())
                .AddTo(this);
            
            GameLoopDataModel.GameRemainingTime
                .Where(time => time <= 5)
                .Take(1)
                .Subscribe(_ => StartGameTimer(warningThreshold))
                .AddTo(this);
            GameLoopDataModel.GameResult
                .Subscribe(ShowGameOver)
                .AddTo(this);
            
            goOnButton.BindDebouncedListener(() =>
            {
                UISpriteContainer.Clear(ResourceManager.Instance.CurrentLoadingSceneName);
                var op = ResourceManager.Instance.UnloadCurrentScene();
                op.Completed += _ =>
                {
                    uiManager.SwitchUI<MainScreenUI>();
                    uiManager.CloseUI(Type);
                };
            });
        }
        
        private void InitializeUI()
        {
            // 初始化所有面板状态
            warmupPanel.gameObject.SetActive(false);
            
            gameTimerPanel.gameObject.SetActive(false);
            
            gameOverPanel.gameObject.SetActive(false);
        }
        
        private void OnDestroy()
        {
            _warmupSequence?.Kill();
            _gameTimerSequence?.Kill();
            _gameTimerTween?.Kill();
        }
        
        // 开始热身倒计时
        private void StartWarmupCountdown(int warmupSeconds = 3)
        {
            _warmupSequence?.Kill();
            WarmupCountdownCoroutine(warmupSeconds);
        }
        
        // 开始游戏总倒计时
        private void StartGameTimer(float gameTimeSeconds)
        {
            _totalGameTime = gameTimeSeconds;
            _currentGameTime = gameTimeSeconds;
            _isGameRunning = true;
            
            _gameTimerSequence?.Kill();
            GameTimerCoroutine();
        }
        
        // 停止游戏计时器（提前结束游戏时调用）
        private void StopGameTimer()
        {
            _isGameRunning = false;
            _gameTimerTween?.Kill();
            
            if (gameTimerPanel.gameObject.activeInHierarchy)
            {
                gameTimerPanel.DOFade(0, panelFadeDuration)
                    .OnComplete(() => gameTimerPanel.gameObject.SetActive(false));
            }
        }
        
        // 显示游戏结束界面
        private void ShowGameOver(GameResultData data)
        {
            StopGameTimer();
            GameOverCoroutine(data);
        }
        
        private async void WarmupCountdownCoroutine(int seconds)
        {
            // 设置热身界面
            warmupPanel.gameObject.SetActive(true);
            warmupTitleText.text = "准备开始";
            warmupProgressCircle.fillAmount = 1f;
            warmupProgressCircle.color = warmupColor;
            
            // 淡入热身面板
            warmupPanel.alpha = 0;
            warmupPanel.DOFade(1, panelFadeDuration);
            
            await UniTask.Delay(TimeSpan.FromSeconds(panelFadeDuration));
            
            // 创建热身倒计时序列
            _warmupSequence = DOTween.Sequence();
            
            for (int i = seconds; i > 0; i--)
            {
                warmupCountdownText.text = i.ToString();
                
                // 数字缩放动画 - 更醒目的效果
                _warmupSequence.Append(warmupCountdownText.transform.DOScale(2f, 0.2f).SetEase(Ease.OutBack));
                _warmupSequence.Append(warmupCountdownText.transform.DOScale(1f, 0.6f).SetEase(Ease.InOutSine));
                
                // 进度圈动画
                _warmupSequence.Join(warmupProgressCircle.DOFillAmount(0, 1f).SetEase(Ease.Linear));
                
                // 颜色闪烁
                _warmupSequence.Join(warmupCountdownText.DOColor(Color.red, 0.1f).SetLoops(2, LoopType.Yoyo));
                
                _warmupSequence.AppendInterval(0.1f);
            }
            
            // 显示"开始！"的醒目效果
            warmupCountdownText.text = "开始！";
            _warmupSequence.Append(warmupCountdownText.transform.DOScale(3f, 0.3f).SetEase(Ease.OutBack));
            _warmupSequence.Join(warmupCountdownText.DOColor(Color.green, 0.3f));
            _warmupSequence.Append(warmupCountdownText.transform.DOScale(0f, 0.2f).SetEase(Ease.InBack));
            
            // 淡出面板
            _warmupSequence.Join(warmupPanel.DOFade(0, panelFadeDuration));
            
            _warmupSequence.OnComplete(() =>
            {
                warmupPanel.gameObject.SetActive(false);
            });
            
            _warmupSequence.Play();
            
            await UniTask.Yield();
        }
        
        private async void GameTimerCoroutine()
        {
            // 设置游戏计时器界面
            gameTimerPanel.gameObject.SetActive(true);
            gameTimerTitleText.text = "剩余时间";
            gameTimerProgressBar.fillAmount = 1f;
            gameTimerText.color = normalTimeColor;
            
            // 淡入游戏计时器面板
            gameTimerPanel.alpha = 0;
            gameTimerPanel.DOFade(1, panelFadeDuration);
            
            await UniTask.Delay(TimeSpan.FromSeconds(panelFadeDuration));
            
            // 更新计时器显示
            UpdateGameTimerDisplay();
            
            // 创建游戏计时器补间
            _gameTimerTween = DOTween.To(() => _currentGameTime, x => _currentGameTime = x, 0, _totalGameTime)
                .SetEase(Ease.Linear)
                .OnUpdate(UpdateGameTimerDisplay)
                .OnComplete(() =>
                {
                    _isGameRunning = false;
                });
        }
        
        private void UpdateGameTimerDisplay()
        {
            // 更新时间文本
            int minutes = Mathf.FloorToInt(_currentGameTime / 60f);
            int seconds = Mathf.FloorToInt(_currentGameTime % 60f);
            gameTimerText.text = $"{minutes:00}:{seconds:00}";
            
            // 更新进度条
            gameTimerProgressBar.fillAmount = _currentGameTime / _totalGameTime;
            
            // 检查警告阈值
            if (_currentGameTime <= warningThreshold)
            {
                // 警告效果：红色闪烁
                if (!gameTimerText.color.Equals(warningTimeColor))
                {
                    gameTimerText.color = warningTimeColor;
                    
                    // 警告动画
                    gameTimerText.transform.DOScale(1.2f, 0.2f).SetLoops(-1, LoopType.Yoyo);
                    gameTimerPanel.transform.DOShakePosition(0.5f, 5f, 10, 90f, false, true)
                        .SetLoops(-1, LoopType.Restart);
                }
            }
            else
            {
                // 恢复正常颜色和大小
                gameTimerText.color = normalTimeColor;
                gameTimerText.transform.localScale = Vector3.one;
            }
        }

        private Dictionary<int, PlayerGameResultItemData> GetPlayerGameResults(GameResultData data)
        {
            var results = new Dictionary<int, PlayerGameResultItemData>();
            foreach (var playerData in data.playersResultData)
            {
                results.Add(playerData.rank, new PlayerGameResultItemData
                {
                    PlayerName = playerData.playerName,
                    Score = playerData.score,
                    Rank = playerData.rank,
                    IsWin = playerData.isWinner,
                });
            }

            return results;
        }

        private async void GameOverCoroutine(GameResultData data)
        {
            var playerGameResults = GetPlayerGameResults(data);
            gameOverItemList.SetItemList(playerGameResults);
            var playerData = data.playersResultData.FirstOrDefault(x => x.isWinner && x.playerName == PlayFabData.PlayerReadOnlyData.Value.Nickname);
            messageText.text = playerData.playerName != null ? "胜利" : "失败";
            // 设置颜色
            var titleColor = playerData.playerName != null ? victoryColor : defeatColor;
            messageText.color = titleColor;
            
            gameOverPanel.gameObject.SetActive(true);
            
            // 重置元素状态
            gameOverPanel.alpha = 0;
            goOnButton.transform.localScale = Vector3.zero;
            gameOverItemList.gameObject.SetActive(false);
            
            // 创建游戏结束序列
            var gameOverSeq = DOTween.Sequence();
            
            // 面板淡入
            gameOverSeq.Append(gameOverPanel.DOFade(1, panelFadeDuration));
            
            // 标题动画 - 弹跳进入
            gameOverSeq.Append(messageText.transform.DOScale(1.2f, 0.5f).SetEase(Ease.OutBack));
            gameOverSeq.Append(messageText.transform.transform.DOScale(1f, 0.2f));
            
            // 标题闪烁效果
            gameOverSeq.Append(messageText.DOColor(new Color(titleColor.r, titleColor.g, titleColor.b, 0.7f), 0.3f));
            gameOverSeq.Append(messageText.DOColor(titleColor, 0.3f));
            gameOverSeq.SetLoops(2, LoopType.Yoyo);
            
            // 得分显示
            gameOverSeq.AppendCallback(() => gameOverItemList.gameObject.SetActive(true));
            gameOverSeq.Append(gameOverItemList.transform.DOScale(1f, 0.4f).SetEase(Ease.OutBack));
            
            // 重新开始按钮
            gameOverSeq.AppendCallback(() => goOnButton.gameObject.SetActive(true));
            gameOverSeq.Append(goOnButton.transform.DOScale(1.1f, 0.3f).SetEase(Ease.OutBack));
            gameOverSeq.Append(goOnButton.transform.DOScale(1f, 0.2f));
            
            // 按钮持续脉冲效果
            gameOverSeq.AppendCallback(() => 
            {
                goOnButton.transform.DOScale(1.1f, 0.5f)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetEase(Ease.InOutSine);
            });
            
            gameOverSeq.Play();
            
            await UniTask.Yield();
        }
        
        // 获取当前游戏时间（用于其他系统）
        public float GetCurrentGameTime()
        {
            return _currentGameTime;
        }
        
        // 检查游戏是否在进行中
        public bool IsGameRunning()
        {
            return _isGameRunning;
        }

        public override UIType Type => UIType.GameFlow;
        public override UICanvasType CanvasType => UICanvasType.Overlay;
    }
}
