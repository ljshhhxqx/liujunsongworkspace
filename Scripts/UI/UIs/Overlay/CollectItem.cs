using AOTScripts.Tool;
using DG.Tweening;
using HotUpdate.Scripts.Network.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HotUpdate.Scripts.UI.UIs.Overlay
{
    public class CollectItem : ItemBase
    {
        [SerializeField] private Slider hp;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI hpText;
        [SerializeField] private CanvasGroup canvasGroup;
        public RectTransform rectTransform;
        private Sequence _sequence;
        private CollectItemData _collectItemData;
        
        public override void SetData<T>(T data)
        {
            if (data is CollectItemData collectItemData)
            {
                hpText.text = $"{collectItemData.CurrentHp}/{collectItemData.MaxHp}";
                hp.value = collectItemData.CurrentHp / collectItemData.MaxHp;
                nameText.text = collectItemData.ItemId.ToString();
                canvasGroup.alpha = 1;
                _sequence?.Kill();
                _sequence = DOTween.Sequence();
                _sequence.AppendInterval(1.5f);
                _sequence.Append(canvasGroup.DOFade(0, 0.5f));
            }
        }

        public override void Clear()
        {
            
        }

        public void Show(FollowTargetParams followTargetParams)
        {
            followTargetParams.Target = _collectItemData.Position;
            //使用扩展方法跟随目标
            GameStaticExtensions.FollowTarget(followTargetParams);
        }
    }
}