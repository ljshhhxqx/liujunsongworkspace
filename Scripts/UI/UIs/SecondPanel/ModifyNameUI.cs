using AOTScripts.Data;
using AOTScripts.Tool;
using Data;
using HotUpdate.Scripts.Data;
using HotUpdate.Scripts.Network.Data;
using HotUpdate.Scripts.UI.UIBase;
using Network.Data;
using TMPro;
using UI.UIBase;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace UI.UIs.SecondPanel
{
    public class ModifyNameUI : ScreenUIBase
    {
        private UIManager _uiManager;
        private PlayFabAccountManager _playFabAccountManager;
        [SerializeField] 
        private TMP_InputField nameInputField;
        [SerializeField] 
        private TextMeshProUGUI modifyNameCountText;
        [SerializeField]
        private Button modifyNameButton;
        private string _originalText;
        public override UIType Type => UIType.ModifyName;
        public override UICanvasType CanvasType => UICanvasType.Popup;

        [Inject]
        private void Init(UIManager uiManager, PlayFabAccountManager playFabAccountManager)
        {
            _uiManager = uiManager;
            _playFabAccountManager = playFabAccountManager;
            _originalText = modifyNameCountText.text;
            PlayFabData.PlayerReadOnlyData
                .Subscribe(OnPlayerModifyName)
                .AddTo(this);
            modifyNameButton.BindDebouncedListener(OnClickModifyNameButton);
        }

        private void OnPlayerModifyName(PlayerReadOnlyData playerReadOnlyData)
        {
            modifyNameCountText.text = $"{_originalText}{playerReadOnlyData.ModifyNameCount}次";
        }

        private void OnClickModifyNameButton()
        {
            if (!CheckNameInput())
            {
                return;
            }
            _playFabAccountManager.ModifyNickName(nameInputField.text);
        }

        private bool CheckNameInput()
        {
            if (nameInputField.text.Length > 15)
            {
                nameInputField.text = nameInputField.text.Substring(0, 15);
            }
            else if (string.IsNullOrEmpty(nameInputField.text) || string.IsNullOrWhiteSpace(nameInputField.text))
            {
                _uiManager.ShowTips("昵称不能为空！");
                return false;
            }
            return true;
        }
    }
}
