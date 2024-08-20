using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace UI.UIs.Common
{
    public class ContentLayoutFitter : MonoBehaviour
    {
        public void RefreshLayout()
        {
            Canvas.ForceUpdateCanvases();
    
            ContentSizeFitter[] csfs = GetComponentsInChildren<ContentSizeFitter>();
            foreach (var csf in csfs)
            {
                csf.SetLayoutHorizontal();
                csf.SetLayoutVertical();
            }
    
            var hlg = GetComponent<HorizontalLayoutGroup>();

            if (hlg != null)
            {
                hlg.SetLayoutHorizontal();
                hlg.SetLayoutVertical();
            }
            
            var vlg = GetComponent<VerticalLayoutGroup>();

            if (vlg != null)
            {
                vlg.SetLayoutHorizontal();
                vlg.SetLayoutVertical();
            }
    
            LayoutRebuilder.ForceRebuildLayoutImmediate(GetComponent<RectTransform>());
    
            DelayedRefresh().Forget();
        }

        private async UniTaskVoid DelayedRefresh()
        {
            await UniTask.DelayFrame(1);
            LayoutRebuilder.ForceRebuildLayoutImmediate(GetComponent<RectTransform>());
        }
    }
}
