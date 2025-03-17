using AOTScripts.Tool;
using Cysharp.Threading.Tasks;
using HotUpdate.Scripts.Tool.Coroutine;
using Sirenix.OdinInspector;
using Sirenix.Utilities;
using TMPro;
using Tool.Coroutine;
using UI.UIBase;
using UnityEngine;

namespace HotUpdate.Scripts.UI.UIs.Overlay
{
    public class TipsOverlay : ScreenUIBase
    {
        [SerializeField]
        private TextMeshProUGUI tipsText;
        private string _tips;
        
        public override UIType Type => UIType.TipsOverlay;
        public override UICanvasType CanvasType => UICanvasType.Overlay;
        
        public void ShowTips(string tips)
        {
            DelayInvoker.CancelInvoke(DelayDisable);
            if (tips.IsNullOrWhitespace() || tips.Equals(_tips))
                return;
            _tips = tips;
            var currentColor = tipsText.color;
            tipsText.color = new Color(currentColor.r, currentColor.g, currentColor.b, 1f); 
            tipsText.text = _tips;
            DelayInvoker.DelayInvoke(2f, DelayDisable);
        }

        private void DelayDisable()
        {
            tipsText.FadeOutAsync(1f).Forget();
        }
        
        [Button]
        private void TestShowTips(string tips)
        {
            ShowTips(tips);
        }
    }
}