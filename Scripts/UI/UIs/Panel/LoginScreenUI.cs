using AOTScripts.Tool;
using Cysharp.Threading.Tasks;
using Data;
using Network.Data;
using TMPro;
using UI.UIBase;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace UI.UIs
{
    public class LoginScreenUI : ScreenUIBase
    {
        [SerializeField]
        private TMP_InputField accountInputField;
        [SerializeField]
        private TMP_InputField passwordInputField;
        [SerializeField]    
        private Button loginButton;
        [SerializeField]    
        private Button registerButton;
        [SerializeField]
        private TextMeshProUGUI errorMessageText;
        [Inject]
        private PlayFabAccountManager _playFabAccountManager;
        [Inject]
        private UIManager _uiManager;
        [SerializeField]
        private bool _isValidInput;

        private bool _isClickedLogin;

        public override UIType Type => UIType.Login;
        public override UICanvasType CanvasType => UICanvasType.Panel;

        [Inject]
        private void Init()
        {
            accountInputField.onValueChanged.AddListener(CheckInputField);
            loginButton.BindDebouncedListener(OnLoginButtonClick);
            registerButton.BindDebouncedListener(OnRegisterButtonClick);
            PlayFabData.IsDevelopMode
                .Subscribe(isDevelopMode =>
                {
                    if (!isDevelopMode)
                    {
                        passwordInputField.onValueChanged.AddListener(CheckInputField);
                    }
                    else
                    {
                        passwordInputField.onValueChanged.RemoveListener(CheckInputField);
                    }
                })
                .AddTo(this);
            CheckLastLogin();
        }
        
        private void CheckLastLogin()
        {
            var accountData = _playFabAccountManager.GetLatestAccount();
            if (accountData != null)
            {
                accountInputField.text = accountData.AccountName;
                passwordInputField.text = accountData.Password;
            }
        }

        private void OnRegisterButtonClick()
        {
            _uiManager.SwitchUI<RegisterScreenUI>();
        }

        public void WriteAccount(RegisterData data)
        {
            accountInputField.text = data.AccountName;
            passwordInputField.text = data.Password;
        }

        private void CheckInputField(string value)
        {
            if (string.IsNullOrWhiteSpace(value) && _isClickedLogin)
            {                
                errorMessageText.text = "请输入账号密码";
                _isValidInput = false;
                return;
            }
            errorMessageText.text = "";
            _isValidInput = true;
        }

        private void OnLoginButtonClick()
        {
            _isClickedLogin = true;
            if (PlayFabData.IsDevelopMode.Value)
            {
                if (string.IsNullOrWhiteSpace(accountInputField.text))
                {
                    CheckInputField(accountInputField.text);
                    return;
                }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(accountInputField.text) || string.IsNullOrWhiteSpace(passwordInputField.text))
                {
                    CheckInputField(accountInputField.text);
                    CheckInputField(passwordInputField.text);
                    return;
                }
            }
            _playFabAccountManager.Login(new AccountData { AccountName = accountInputField.text, Password = passwordInputField.text });
        }

        private void OnDestroy()
        {
            accountInputField.onValueChanged.RemoveListener(CheckInputField);
            passwordInputField.onValueChanged.RemoveListener(CheckInputField);
            loginButton.onClick.RemoveAllListeners();
            registerButton.onClick.RemoveAllListeners();
        }
    }
}
