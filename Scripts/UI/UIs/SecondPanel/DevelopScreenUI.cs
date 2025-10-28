using AOTScripts.Tool;
using Data;
using HotUpdate.Scripts.Data;
using TMPro;
using UI.UIBase;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace UI.UIs.SecondPanel
{
    public class DevelopScreenUI: ScreenUIBase
    {
        [SerializeField]
        private Button confirmButton;
        [SerializeField]
        private TMP_InputField keyInputField;
        private PlayFabAccountManager _playFabAccountManager;
        
        [Inject]
        private void Init(PlayFabAccountManager playFabAccountManager)
        {
            confirmButton.BindDebouncedListener(OnConfirmButtonClick);
            _playFabAccountManager = playFabAccountManager;
        }

        private void OnConfirmButtonClick()
        {
            _playFabAccountManager.CheckDevelopers(keyInputField.text);
        }

        public override UIType Type => UIType.Develop;
        public override UICanvasType CanvasType => UICanvasType.SecondPanel;
    }
}