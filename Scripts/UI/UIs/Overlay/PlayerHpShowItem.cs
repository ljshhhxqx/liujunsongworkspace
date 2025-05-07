using HotUpdate.Scripts.UI.UIs.Panel.Item;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HotUpdate.Scripts.UI.UIs.Overlay
{
    public class PlayerHpItem : ItemBase
    {
        [SerializeField]
        private Slider hpSlider;
        [SerializeField]
        private Slider mpSlider;
        [SerializeField]
        private TextMeshProUGUI nameText;

        private uint _playerId;
        
        public override void SetData<T>(T data)
        {
            if (data is PlayerHpItemData playerHpItemData)
            {
                _playerId = playerHpItemData.PlayerId;
                nameText.text = playerHpItemData.Name;
                hpSlider.value = playerHpItemData.CurrentHp / playerHpItemData.MaxHp;
                mpSlider.value = playerHpItemData.CurrentMp / playerHpItemData.MaxMp;
            }
        }

        public override void Clear()
        {
        }
    }
}