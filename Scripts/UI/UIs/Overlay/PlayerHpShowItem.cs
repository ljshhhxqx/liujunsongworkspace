using HotUpdate.Scripts.Tool.Static;
using HotUpdate.Scripts.UI.UIs.Panel.Item;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HotUpdate.Scripts.UI.UIs.Overlay
{
    public class PlayerHpItem : ItemBase
    {
        [SerializeField]
        private RectTransform indicatorTransform;
        [SerializeField]
        private Slider hpSlider;
        [SerializeField]
        private Slider mpSlider;
        [SerializeField]
        private TextMeshProUGUI nameText;

        private PlayerHpItemData _data;
        public int PlayerId { get; private set; }
        
        public override void SetData<T>(T data)
        {
            if (data is PlayerHpItemData playerHpItemData)
            {
                _data = playerHpItemData;
                PlayerId = playerHpItemData.PlayerId;
                nameText.text = playerHpItemData.Name;
                hpSlider.value = playerHpItemData.CurrentHp / playerHpItemData.MaxHp;
                mpSlider.value = playerHpItemData.CurrentMp / playerHpItemData.MaxMp;
            }
        }

        public override void Clear()
        {
        }

        public void Show(FollowTargetParams followTargetParams)
        {
            followTargetParams.IndicatorUI = indicatorTransform;
            followTargetParams.Target = _data.TargetPosition;
            followTargetParams.Player = _data.PlayerPosition;
            GameStaticExtensions.FollowTarget(followTargetParams);
        }
    }
}