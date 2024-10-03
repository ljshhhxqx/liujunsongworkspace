using AOTScripts.Tool;
using Cysharp.Threading.Tasks;
using Data;
using TMPro;
using UI.UIBase;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace UI.UIs
{
    public class RegisterScreenUI : ScreenUIBase
    {
        [SerializeField]
        private TMP_InputField accountInputField;
        [SerializeField]
        private TMP_InputField passwordInputField;
        [SerializeField]
        private TMP_InputField emailInputField;
        [SerializeField]    
        private Button registerButton;
        [SerializeField]    
        private Button cancelButton;
        [SerializeField]
        private TextMeshProUGUI errorMessageText;
        [Inject]
        private PlayFabAccountManager _playFabAccountManager;
        [Inject]
        private UIManager _uiManager;
        [SerializeField]
        private bool _isValidInput;
        
        public override UIType Type => UIType.Register;
        public override UICanvasType CanvasType => UICanvasType.Panel;

        [Inject]
        private void Init()
        {
            accountInputField.onValueChanged.AddListener(CheckInputField);
            passwordInputField.onValueChanged.AddListener(CheckInputField);
            emailInputField.onValueChanged.AddListener(CheckInputField);
            registerButton.BindDebouncedListener(OnRegisterButtonClick);
            cancelButton.BindDebouncedListener(OnCancelButtonClick);
        }

        private void OnCancelButtonClick()
        {
            _uiManager.SwitchUI<LoginScreenUI>();
        }

        private void OnRegisterButtonClick()
        {
            if (_isValidInput)
            {
                if (!IsValidEmail(emailInputField.text))
                {
                    errorMessageText.text = "请输入正确的邮箱";
                    return;
                }

                _playFabAccountManager.Register(new RegisterData { Email = emailInputField.text, Password = passwordInputField.text, AccountName = accountInputField.text });
                return;
            }
            errorMessageText.gameObject.SetActive(true);
            errorMessageText.text = "输入正确的账户名、密码和邮箱";
        }

        private void OnDestroy()
        {
            accountInputField.onValueChanged.RemoveAllListeners();
            passwordInputField.onValueChanged.RemoveAllListeners();
            emailInputField.onValueChanged.RemoveAllListeners();
            registerButton.onClick.RemoveAllListeners();
        }

        private void CheckInputField(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {                
                errorMessageText.text = "输入正确的账户名、密码和邮箱";
                _isValidInput = false;
                return;
            }
            errorMessageText.text = "";
            _isValidInput = true;
        }
        
        private bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }
    }
}
