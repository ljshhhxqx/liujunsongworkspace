using DG.Tweening;
using HotUpdate.Scripts.Network.UI;
using HotUpdate.Scripts.UI.UIs.Common;
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
        private TextMeshProUGUI changedText;
        [SerializeField]
        private Image iconImage;
        private PropertyItemData _propertyData;
        private Sequence _sq;
        private Vector2 _startPosition;

        private void Start()
        {
            _startPosition = changedText.transform.localPosition;
        }

        public override void SetData<T>(T data)
        {
            if (data is PropertyItemData propertyData)
            {
                var changedValue = propertyData.CurrentProperty - _propertyData.CurrentProperty;
                _propertyData = propertyData;
                nameText.text = propertyData.Name;
                SetValue(propertyData.ConsumeType, propertyData.CurrentProperty, propertyData.MaxProperty, changedValue);
            }
        }

        public override void Clear()
        {
            
        }

        private void SetValue(PropertyConsumeType consumeType, float currentValue, float maxValue, float changeValue = 0)
        {
            switch (consumeType)
            {
                case PropertyConsumeType.Number:
                    var currentValueInt = Mathf.RoundToInt(currentValue);
                    valueText.text = _propertyData.IsPercentage ? $"{currentValue * 100:0}%" : currentValueInt.ToString("0");
                    iconImage.transform.parent.gameObject.SetActive(false);
                    changedText.gameObject.SetActive(changeValue != 0);
                    if (changeValue != 0)
                    {
                        DoAnimation(changeValue);
                    }
                    break;
                case PropertyConsumeType.Consume:
                    var ratio = currentValue / maxValue;
                    currentValueInt = Mathf.RoundToInt(currentValue);
                    var maxValueInt = Mathf.RoundToInt(maxValue);
                    valueText.text = $"{currentValueInt}/{maxValueInt}";
                    iconImage.transform.parent.gameObject.SetActive(true);
                    iconImage.fillAmount = ratio;
                    if (!_propertyData.IsAutoRecover && changeValue != 0)
                    {
                        DoAnimation(changeValue);
                    }
                    break;
            }
        }

        private void DoAnimation(float changeValue)
        {
            changedText.transform.localPosition = _startPosition;
            changedText.text = changeValue > 0 ? $"+{changeValue:0}" : $"-{changeValue:0}";
            changedText.color = changeValue > 0 ? Color.green : Color.red;
            _sq?.Kill(true);
            _sq.Append(changedText.transform.DOLocalMoveX(-50f, 0.5f).SetEase(Ease.Linear));
            _sq.AppendInterval(0.5f);
            _sq.AppendCallback(() =>
            {
                changedText.gameObject.SetActive(false);
            });
        }
    }
}