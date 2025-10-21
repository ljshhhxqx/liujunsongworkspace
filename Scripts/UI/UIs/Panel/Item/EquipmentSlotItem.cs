using System;
using AOTScripts.Data;
using HotUpdate.Scripts.Audio;
using HotUpdate.Scripts.Config;
using HotUpdate.Scripts.Config.ArrayConfig;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HotUpdate.Scripts.UI.UIs.Panel.Item
{
    public class EquipmentSlotItem : ItemBase, IPointerClickHandler
    {
        [SerializeField]
        private Image itemImage;        // 显示物品图标的Image组件
        [SerializeField]
        private GameObject lockIcon;    // 锁定图标的GameObject组件
        [SerializeField]
        private Image qualityImage;        // 显示物品图标的Image组件

        [SerializeField] private TextMeshProUGUI equipPart;
        private BagItemData _currentItem;              
        private EquipmentPart _equipmentPart;
        private readonly Subject<PointerEventData> _pointerClickObservable = new Subject<PointerEventData>();
        public IObservable<PointerEventData> OnPointerClickObservable => _pointerClickObservable;
        public EquipmentPart EquipmentPart => _equipmentPart;
        public BagItemData CurrentItem => _currentItem;
        
        public override void SetData<T>(T data)
        {
            if (data is BagItemData bagItem)
            {
                _currentItem = bagItem;
                _equipmentPart = bagItem.EquipmentPart;
                UpdateSlotUI();
            }
        }

        public override void Clear()
        {
            _currentItem = default;
            _equipmentPart = default;
            UpdateSlotUI();
        }

        private void UpdateSlotUI()
        {
            var itemIsNull = _currentItem.Equals(default);
            itemImage.sprite = itemIsNull ? null : _currentItem.Icon;
            if (!itemIsNull)
            {
                qualityImage.sprite = _currentItem.QualityIcon;
            }
            itemImage.enabled = !itemIsNull;
            equipPart.text = EnumHeaderParser.GetHeader(_currentItem.EquipmentPart);
            equipPart.enabled = itemIsNull;
            lockIcon.SetActive(_currentItem.IsLock);
        }

        public bool IsEmpty()
        {
            return _currentItem.Equals(default);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            UIAudioManager.Instance.PlayUIEffect(UIAudioEffectType.Click);
            _pointerClickObservable.OnNext(eventData);
        }
    }
}