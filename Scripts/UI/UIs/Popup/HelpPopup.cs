using AOTScripts.Tool;
using HotUpdate.Scripts.UI.UIBase;
using TMPro;
using UI.UIBase;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace UI.UIs.Popup
{
    public class HelpPopup : ScreenUIBase
    {
        [SerializeField]
        private TextMeshProUGUI text;
        [SerializeField]
        private Button closeButton;
        public override UIType Type => UIType.Help;
        public override UICanvasType CanvasType => UICanvasType.Popup;

        [Inject]
        private void Init(UIManager uiManager)
        {
            closeButton.BindDebouncedListener(() => uiManager.CloseUI(Type));
        }

        public void ShowHelp(string tips)
        {
            text.text = tips;
        }
    }
}