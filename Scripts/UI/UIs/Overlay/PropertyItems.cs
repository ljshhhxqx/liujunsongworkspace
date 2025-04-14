using System;
using HotUpdate.Scripts.UI.UIs.Panel.Item;
using TMPro;
using UI.UIs.Common;
using UniRx;
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
                // _currentProperty = propertyData.CurrentProperty;
                // _maxProperty = propertyData.MaxProperty;
                // SetValue(propertyData.ConsumeType, _currentProperty.Value.Value, _maxProperty.Value.Value);
                // _currentProperty.Subscribe(x =>
                // {
                //     SetValue(propertyData.ConsumeType, x.Value, _maxProperty.Value.Value);
                // }).AddTo(this);
                // _maxProperty.Subscribe(x =>
                // {
                //     var currentValue = Mathf.RoundToInt(_currentProperty.Value.Value);
                //     var maxValue = Mathf.RoundToInt(_maxProperty.Value.Value);
                //     SetValue(propertyData.ConsumeType, _currentProperty.Value.Value, x.Value);
                // }).AddTo(this);
            }
        }

        private void SetValue(PropertyConsumeType consumeType, float currentValue, float maxValue)
        {
            switch (consumeType)
            {
                case PropertyConsumeType.Number:
                    var currentValueInt = Mathf.RoundToInt(currentValue);
                    valueText.text = currentValueInt.ToString("0");
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
                default:
                    throw new Exception($"Invalid consume type {consumeType}");
            }
        }
    }
}