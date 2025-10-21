using AOTScripts.Data;
using HotUpdate.Scripts.Config;
using HotUpdate.Scripts.UI.UIs.Panel.Item;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HotUpdate.Scripts.UI.UIs.Overlay
{
    public class AnimationItem : ItemBase
    {
        [SerializeField]
        private TextMeshProUGUI timeText;
        [SerializeField]
        private Image iconImage;
        [SerializeField]
        private Image frameImage;
        [SerializeField]
        private TextMeshProUGUI keyText;
        [SerializeField]
        private TextMeshProUGUI indexText;
        [SerializeField]
        private TextMeshProUGUI costText;
        [SerializeField]
        private Image countdownImage;
        
        public override void SetData<T>(T data)
        {
            if (data is AnimationStateData animationStateData)
            {
                frameImage.sprite = !animationStateData.Frame ? frameImage.sprite : animationStateData.Frame;
                iconImage.sprite = !animationStateData.Icon ? iconImage.sprite : animationStateData.Icon;
                countdownImage.fillAmount = animationStateData.Timer / animationStateData.Duration;
                indexText.text = animationStateData.Index == 0 ? string.Empty : animationStateData.Index.ToString();
                keyText.text = EnumHeaderParser.GetHeader(animationStateData.State);
                costText.text = animationStateData.Cost.ToString("0");
                var isReady = animationStateData.Timer == 0f;
                timeText.enabled = !isReady;
                timeText.text = animationStateData.Timer.ToString("00");
                //Debug.Log($"AnimationItem SetData : {animationStateData}");
                return;
            }
            Debug.LogError($"{nameof(T)} is not of type {nameof(AnimationStateData)}.");
        }

        public override void Clear()
        {
            
        }
    }
}