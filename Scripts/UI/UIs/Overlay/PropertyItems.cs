using System;
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
        private ReactiveProperty<PropertyType> _currentProperty;
        private ReactiveProperty<PropertyType> _maxProperty;
        
        public override void SetData<T>(T data)
        {
            if (data is PropertyItemData propertyData)
            {
                nameText.text = propertyData.Name;
                _currentProperty = propertyData.CurrentProperty;
                _maxProperty = propertyData.MaxProperty;
                var currentValue = Mathf.Round(_currentProperty.Value.Value);
                var maxValue = Mathf.Round(_maxProperty.Value.Value);
                SetValue(propertyData.ConsumeType, currentValue, maxValue);
                _currentProperty.Subscribe(x =>
                {
                    SetValue(propertyData.ConsumeType, x.Value, _maxProperty.Value.Value);
                }).AddTo(this);
                _maxProperty.Subscribe(x =>
                {
                    SetValue(propertyData.ConsumeType, _currentProperty.Value.Value, x.Value);
                }).AddTo(this);
            }
        }

        private void SetValue(PropertyConsumeType consumeType, float currentValue, float maxValue)
        {
            switch (consumeType)
            {
                case PropertyConsumeType.Number:
                    valueText.text = currentValue.ToString("0");
                    iconImage.transform.parent.gameObject.SetActive(false);
                    break;
                case PropertyConsumeType.Consume:
                    var ratio = currentValue / maxValue;
                    valueText.text = $"{currentValue}/{maxValue}";
                    iconImage.transform.parent.gameObject.SetActive(true);
                    iconImage.fillAmount = ratio;
                    break;
                default:
                    throw new Exception($"Invalid consume type {consumeType}");
            }
        }
    }
}