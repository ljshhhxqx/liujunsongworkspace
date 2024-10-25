using System;
using TMPro;
using UI.UIs.Common;
using UnityEngine;
using UnityEngine.UI;

namespace HotUpdate.Scripts.UI.UIs.Overlay
{
    public class PropertyItems : ItemBase
    {
        [SerializeField]
        private TextMeshProUGUI nameText;
        [SerializeField]
        private TextMeshProUGUI valueText;
        [SerializeField]
        private Image iconImage;
        
        public override void SetData<T>(T data)
        {
            if (data is PropertyItemData propertyData)
            {
                nameText.text = propertyData.Name;
                var currentValue = Mathf.Round(propertyData.CurrentValue);
                var maxValue = Mathf.Round(propertyData.MaxValue);
                SetValue(propertyData.ConsumeType, currentValue, maxValue);
            }
        }

        private void SetValue(PropertyConsumeType consumeType, float currentValue, float maxValue)
        {
            switch (consumeType)
            {
                case PropertyConsumeType.Number:
                    valueText.text = currentValue.ToString("0");
                    iconImage.gameObject.SetActive(false);
                    break;
                case PropertyConsumeType.Consume:
                    var ratio = currentValue / maxValue;
                    valueText.text = $"{currentValue}/{maxValue}";
                    iconImage.gameObject.SetActive(true);
                    iconImage.fillAmount = ratio;
                    break;
                default:
                    throw new Exception($"Invalid consume type {consumeType}");
            }
        }
    }
}