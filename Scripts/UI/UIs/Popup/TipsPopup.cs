using System;
using HotUpdate.Scripts.UI.UIBase;
using UI.UIBase;
using UnityEngine;
using VContainer;

namespace HotUpdate.Scripts.UI.UIs.Popup
{
    [RequireComponent(typeof(CommonTipsPopup))]
    public class TipsPopup : ScreenUIBase
    {
        private UIManager _uiManager;
        private CommonTipsPopup _commonTipsPopup;

        [Inject]
        private void Init(UIManager uiManager)
        {
            _commonTipsPopup = GetComponent<CommonTipsPopup>();
            _uiManager = uiManager;
            _commonTipsPopup.onClose = () => _uiManager.CloseUI(UIType.TipsPopup);
            print("TipsPopup Init");
        }

        public void ShowTips(string tips, Action onConfirm = null, Action onCancel = null)
        {
            _commonTipsPopup.ShowTips(tips, onConfirm, onCancel);
        }

        public override UIType Type => UIType.TipsPopup;
        public override UICanvasType CanvasType => UICanvasType.Popup;
    }
}