using System;
using Cysharp.Threading.Tasks;
using TMPro;
using UI.UIBase;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace UI.UIs.Popup
{
    public class TipsPopup : ScreenUIBase
    {
        [SerializeField]
        private TextMeshProUGUI text;
        [SerializeField]
        private Button confirmBtn;
        [SerializeField]
        private Button cancelBtn;
        private UIManager _uiManager;
        private Action _onConfirm;
        private Action _onCancel;
        public override UIType Type => UIType.TipsPopup;
        public override UICanvasType CanvasType => UICanvasType.Popup;

        public void ShowTips(string tips, Action onConfirm = null, Action onCancel = null)
        {
            cancelBtn.gameObject.SetActive(onCancel != null);
            text.text = tips;
            _onConfirm = onConfirm;
            _onCancel = onCancel;
            confirmBtn.BindDebouncedListener(OnConfirm);
            cancelBtn.BindDebouncedListener(OnCancel);
            if (onConfirm == null && onCancel == null)
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
            _uiManager.CloseUI(UIType.TipsPopup);
        }
        
        private void OnConfirm()
        {
            Debug.Log("Confirm");
            _onConfirm?.Invoke();
            _uiManager.CloseUI(UIType.TipsPopup);
        }
        
        private void OnCancel()
        {
            Debug.Log("Cancel");
            _onCancel?.Invoke();
            _uiManager.CloseUI(UIType.TipsPopup);
        }

        [Inject]
        private void Init(UIManager uiManager)
        {
            _uiManager = uiManager;
            print("TipsPopup Init");
        }

        private void OnDestroy()
        {
            confirmBtn.onClick.RemoveAllListeners();
        }
    }
}