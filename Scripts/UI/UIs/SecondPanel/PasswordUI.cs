using System;
using Data;
using TMPro;
using UI.UIBase;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace UI.UIs.SecondPanel
{
    public class PasswordUI : ScreenUIBase
    {
        private Action<bool> _onConfirm;
        [SerializeField]
        private Button confirmButton;
        [SerializeField]
        private TMP_InputField keyInputField;
        private string _password;

        private void Start()
        {
            confirmButton.BindDebouncedListener(OnConfirmButtonClick, 0.5f);
        }

        public void ShowPasswordUI(string password, Action<bool> onConfirm)
        {
            _password = password;
            _onConfirm = onConfirm;
        }

        private void OnConfirmButtonClick()
        {
            _onConfirm?.Invoke(keyInputField.text == _password);
        }

        public override UIType Type => UIType.Password;
        public override UICanvasType CanvasType => UICanvasType.SecondPanel;
    }
}
