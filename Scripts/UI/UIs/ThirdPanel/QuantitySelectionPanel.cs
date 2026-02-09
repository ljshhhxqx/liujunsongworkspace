using System;
using AOTScripts.Tool.Resource;
using TMPro;
using UI.UIBase;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

namespace HotUpdate.Scripts.UI.UIs.ThirdPanel
{
    // 数量选择面板（简版实现）
    public class QuantitySelectionPanel : ScreenUIBase
    {
        [SerializeField] private TMP_InputField amountInput;
        [SerializeField] private Button confirmButton;
        private CompositeDisposable _disposables = new CompositeDisposable();

        public void Show(int max, Action<int> onConfirm)
        {
            gameObject.SetActive(true);
            amountInput.text = "1";
            amountInput.onValueChanged.RemoveAllListeners();
            confirmButton.onClick.RemoveAllListeners();
            amountInput.onValueChanged.AddListener(v => 
            {
                if(!int.TryParse(v, out int value)) return;
                value = Mathf.Clamp(value, 1, max);
                amountInput.text = value.ToString();
            });

            confirmButton.onClick.AddListener(() => 
            {
                if(int.TryParse(amountInput.text, out int result))
                {
                    onConfirm?.Invoke(Mathf.Clamp(result, 1, max));
                    Hide();
                }
            });
        }

        public void Hide() => gameObject.SetActive(false);
        public override UIType Type => UIType.QuantitySelection;
        public override UICanvasType CanvasType => UICanvasType.ThirdPanel;
    }
}