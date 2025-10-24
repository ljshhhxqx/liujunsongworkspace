using AOTScripts.Data;
using HotUpdate.Scripts.UI.UIs.Panel.Item;
using TMPro;
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
        private PropertyItemData _propertyData;
        
        public override void SetData<T>(T data)
        {
            if (data is PropertyItemData propertyData)
            {
                _propertyData = propertyData;
                nameText.text = propertyData.Name;
                SetValue(propertyData.ConsumeType, propertyData.CurrentProperty, propertyData.MaxProperty);
            }
        }

        public override void Clear()
        {
            
        }

        private void SetValue(PropertyConsumeType consumeType, float currentValue, float maxValue)
        {
            switch (consumeType)
            {
                case PropertyConsumeType.Number:
                    var currentValueInt = Mathf.RoundToInt(currentValue);
                    valueText.text = _propertyData.IsPercentage ? $"{currentValue * 100:0}%" : currentValueInt.ToString("0");
                    iconImage.transform.parent.gameObject.SetActive(false);
                    break;
                case PropertyConsumeType.Consume:
                    var ratio = currentValue / maxValue;
                    currentValueInt = Mathf.RoundToInt(currentValue);
                    var maxValueInt = Mathf.RoundToInt(maxValue);
                    valueText.text = $"{currentValueInt}/{maxValueInt}";
                    iconImage.transform.parent.gameObject.SetActive(true);
                    iconImage.fillAmount = ratio;
                    break;
            }
        }
    }
}