using AOTScripts.Data;
using AOTScripts.Tool;
using Data;
using HotUpdate.Scripts.Data;
using HotUpdate.Scripts.Network.Data;
using HotUpdate.Scripts.UI.UIBase;
using Network.Data;
using TMPro;
using UI.UIBase;
using UI.UIs.Common;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace UI.UIs.SecondPanel
{
    public class PlayerInfoScreenUI : ScreenUIBase
    {
        private UIManager _uiManager;
        private PlayFabAccountManager _playFabAccountManager;
        
        [SerializeField]
        private TextMeshProUGUI playerNameText;
        [SerializeField]
        private TextMeshProUGUI playerScoreText;
        [SerializeField]
        private TextMeshProUGUI playerIDText;
        [SerializeField]
        private TextMeshProUGUI playerEmailText;
        [SerializeField]
        private ContentLayoutFitter emailContentFitter;
        [SerializeField]
        private ContentLayoutFitter nameContentFitter;
        [SerializeField]
        private ContentLayoutFitter idContentFitter;
        [SerializeField]
        private Button copyEmailButton;
        [SerializeField]
        private Button copyNameButton;
        [SerializeField]
        private Button copyIDButton;
        [SerializeField]
        private Button closeButton;
        [SerializeField]
        private Button modifyButton;
        
        public override UIType Type => UIType.PlayerInfo;
        public override UICanvasType CanvasType => UICanvasType.SecondPanel;

        [Inject]
        private void Init(UIManager uiManager, PlayFabAccountManager playFabAccountManager)
        {
            _uiManager = uiManager;
            _playFabAccountManager = playFabAccountManager;
            PlayFabData.PlayerReadOnlyData
                .Subscribe(OnRefreshPlayerInfo)
                .AddTo(this);
            closeButton.BindDebouncedListener(OnCloseButtonClick);
            modifyButton.BindDebouncedListener(OnModifyButtonClick);
            copyEmailButton.BindDebouncedListener(OnCopyEmailButtonClick);
            copyNameButton.BindDebouncedListener(OnCopyNameButtonClick);
            copyIDButton.BindDebouncedListener(OnCopyIDButtonClick);
        }

        private void OnRefreshPlayerInfo(PlayerReadOnlyData playerReadOnlyData)
        {
            playerNameText.text = playerReadOnlyData.Nickname;
            playerScoreText.text = playerReadOnlyData.Score.ToString();
            playerIDText.text = playerReadOnlyData.PlayerId;
            playerEmailText.text = string.IsNullOrEmpty(playerReadOnlyData.Email) ? "未绑定邮箱" : playerReadOnlyData.Email;          
            copyEmailButton.gameObject.SetActive(!string.IsNullOrEmpty(playerReadOnlyData.Email));// = !string.IsNullOrEmpty(playerReadOnlyData.Email);
            if (!string.IsNullOrEmpty(playerReadOnlyData.Email))
            {
                emailContentFitter.RefreshLayout();
            }
            nameContentFitter.RefreshLayout();
            idContentFitter.RefreshLayout();
        }

        private void OnModifyButtonClick()
        {
            _uiManager.SwitchUI<ModifyNameUI>();
        }

        private void OnCloseButtonClick()
        {
            _uiManager.CloseUI(Type);
        }

        private void OnCopyIDButtonClick()
        {
            GUIUtility.systemCopyBuffer = playerIDText.text;
            ShowCopySuccess();
        }

        private void OnCopyNameButtonClick()
        {
            GUIUtility.systemCopyBuffer = playerNameText.text;
            ShowCopySuccess();
        }

        private void OnCopyEmailButtonClick()
        {
            GUIUtility.systemCopyBuffer = playerEmailText.text;
            ShowCopySuccess();
        }

        private void ShowCopySuccess()
        {
            _uiManager.ShowTips("复制成功！");
        }
    }
}
