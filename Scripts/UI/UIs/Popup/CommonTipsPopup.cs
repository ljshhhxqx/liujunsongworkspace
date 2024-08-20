using System;
using Cysharp.Threading.Tasks;
using TMPro;
using UI.UIBase;
using UnityEngine;
using UnityEngine.UI;

namespace HotUpdate.Scripts.UI.UIs.Popup
{
    public class CommonTipsPopup : ScreenUIBase
    {
        [SerializeField]
        protected TextMeshProUGUI text;
        [SerializeField]
        protected Button confirmBtn;
        [SerializeField]
        protected Button cancelBtn;
        private Action _onConfirm;
        private Action _onCancel;
        protected Action onClose;

        private void Awake()
        {
            onClose = () =>
            {
                gameObject.SetActive(false);
            };
        }

        public void ShowTips(string tips, Action confirm = null, Action cancel = null)
        {
            cancelBtn.gameObject.SetActive(cancel != null);
            text.text = tips;
            _onConfirm = confirm;
            _onCancel = cancel;
            confirmBtn.BindDebouncedListener(OnConfirm);
            cancelBtn.BindDebouncedListener(OnCancel);
            if (confirm == null && cancel == null)
            {
                AutoDestroy().Forget();
            }
        }
        
        private async UniTaskVoid AutoDestroy()
        {
            await UniTask.Delay(TimeSpan.FromSeconds(2));
            if (!this)
            {
                return;
            }
            onClose?.Invoke();
        }
        
        private void OnConfirm()
        {
            Debug.Log("Confirm");
            _onConfirm?.Invoke();
            onClose?.Invoke();
        }
        
        private void OnCancel()
        {
            Debug.Log("Cancel");
            _onCancel?.Invoke();
            onClose?.Invoke();
        }

        private void OnDestroy()
        {
            confirmBtn.onClick.RemoveAllListeners();
            cancelBtn.onClick.RemoveAllListeners();
        }

        public override UIType Type => UIType.TipsPopup;
        public override UICanvasType CanvasType => UICanvasType.Popup;
    }
}
