using System;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.UI.UIBase;
using HotUpdate.Scripts.UI.UIs.Panel.Item;
using HotUpdate.Scripts.UI.UIs.ThirdPanel;
using TMPro;
using UI.UIBase;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace HotUpdate.Scripts.UI.UIs.SecondPanel
{
    public class ItemDetailsScreenUI : ScreenUIBase
    {
        [Header("UI Components")]
        [SerializeField] private Image itemIcon;
        [SerializeField] private Image qualityBorder;
        [SerializeField] private TextMeshProUGUI itemNameText;
        [SerializeField] private TextMeshProUGUI stackText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private TextMeshProUGUI propertyText;
        [SerializeField] private TextMeshProUGUI passiveEffectText;
        [SerializeField] private TextMeshProUGUI priceText;
        
        [Header("Interaction Buttons")]
        [SerializeField] private Button useButton;
        [SerializeField] private Button equipButton;
        [SerializeField] private Button lockButton;
        [SerializeField] private Button dropButton;
        [SerializeField] private Button sellButton;
        
        private UIManager _uiManager;
        private BagItemData _currentItemData;
        private ItemDetailsType _currentItemDetailsType;

        [Inject]
        private void Init(UIManager uiManager)
        {
            _uiManager = uiManager;
            useButton.OnClickAsObservable().Subscribe(_ => OnUseClicked()).AddTo(this);
            equipButton.OnClickAsObservable().Subscribe(_ => OnEquipClicked()).AddTo(this);
            lockButton.OnClickAsObservable().Subscribe(_ => OnLockClicked()).AddTo(this);
            dropButton.OnClickAsObservable().Subscribe(_ => OnDropClicked()).AddTo(this);
        }

        public void Open(BagItemData itemData, ItemDetailsType itemDetailsType = ItemDetailsType.Bag)
        {
            _currentItemData = itemData;
            _currentItemDetailsType = itemDetailsType;
            UpdateUI();
        }

        private void UpdateUI()
        {
            // 基础信息
            itemIcon.sprite = _currentItemData.Icon;
            qualityBorder.sprite = _currentItemData.QualityIcon;
            itemNameText.text = _currentItemData.ItemName;
            
            // 堆叠显示
            stackText.text = $"{_currentItemData.Stack}/{_currentItemData.MaxStack}";
            stackText.gameObject.SetActive(_currentItemData.MaxStack > 1);

            // 描述信息
            descriptionText.text = _currentItemData.Description;
            var showProperty = _currentItemData.PlayerItemType.ShowProperty();
            propertyText.gameObject.SetActive(showProperty);
            propertyText.text = showProperty ? _currentItemData.PropertyDescription : "";
            propertyText.text = _currentItemData.PropertyDescription;
            
            // 被动效果（仅装备类显示）
            var showPassive = _currentItemData.PlayerItemType.IsEquipment();
            passiveEffectText.gameObject.SetActive(showPassive);
            passiveEffectText.text = showPassive ? _currentItemData.EquipPassiveDescription : "";

            // 价格信息
            priceText.text = $"价格: {_currentItemData.Price * _currentItemData.SellRatio}G";

            // 按钮状态
            UpdateButtonStates();
        }

        private void UpdateButtonStates()
        {
            var isLocked = _currentItemData.IsLock;
            switch (_currentItemDetailsType)
            {
                case ItemDetailsType.Bag:
                    useButton.gameObject.SetActive(!isLocked && _currentItemData.PlayerItemType == PlayerItemType.Consume);
                    dropButton.gameObject.SetActive(!isLocked);
                    equipButton.gameObject.SetActive(!isLocked && _currentItemData.PlayerItemType.IsEquipment());
                    lockButton.gameObject.SetActive(true);
                    sellButton.gameObject.SetActive(false);
                    break;
                case ItemDetailsType.Equipment:
                    useButton.gameObject.SetActive(false);
                    dropButton.gameObject.SetActive(false);
                    equipButton.gameObject.SetActive(!isLocked && _currentItemData.PlayerItemType.IsEquipment());
                    lockButton.gameObject.SetActive(true);
                    sellButton.gameObject.SetActive(false);
                    break;
                case ItemDetailsType.Shop:
                    useButton.gameObject.SetActive(false);
                    dropButton.gameObject.SetActive(false);
                    equipButton.gameObject.SetActive(false);
                    lockButton.gameObject.SetActive(false);
                    sellButton.gameObject.SetActive(true);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            
            lockButton.GetComponentInChildren<TextMeshProUGUI>().text = 
                _currentItemData.IsLock ? "解锁" : "锁定";
            equipButton.GetComponentInChildren<TextMeshProUGUI>().text = 
                _currentItemData.IsEquip ? "卸下" : "装备";
        }

        #region Button Handlers
        private void OnUseClicked()
        {
            if(_currentItemData.Stack > 1)
            {
                _uiManager.SwitchUI<QuantitySelectionPanel>(ui =>
                {
                    ui.Show(max: _currentItemData.Stack, onConfirm: (amount) =>
                    {
                        _currentItemData.OnUseItem?.Invoke(_currentItemData.Index, amount);
                        Close();
                    });
                });
            }
            else
            {
                _currentItemData.OnUseItem?.Invoke(_currentItemData.Index, 1);
                Close();
            }
        }

        private void OnEquipClicked()
        {
            bool newEquipState = !_currentItemData.IsEquip;
            _currentItemData.OnEquipItem?.Invoke(_currentItemData.Index, newEquipState);
            _currentItemData.IsEquip = newEquipState;
            UpdateButtonStates();
        }

        private void OnLockClicked()
        {
            bool newLockState = !_currentItemData.IsLock;
            _currentItemData.OnLockItem?.Invoke(_currentItemData.Index, newLockState);
            _currentItemData.IsLock = newLockState;
            UpdateButtonStates();
        }

        private void OnSellClicked()
        {
            var quantityPanel = _uiManager.SwitchUI<QuantitySelectionPanel>();
            quantityPanel.Show(max: _currentItemData.Stack, onConfirm: (amount) => 
            {
                _currentItemData.OnSellItem?.Invoke(_currentItemData.Index, amount);
                Close();
            });
        }

        private void OnDropClicked()
        {
            var quantityPanel = _uiManager.SwitchUI<QuantitySelectionPanel>();
            quantityPanel.Show(max: _currentItemData.Stack, onConfirm: (amount) => 
            {
                _currentItemData.OnDropItem?.Invoke(_currentItemData.Index, amount);
                Close();
            });
        }
        #endregion

        private void Close()
        {
            ;
            _currentItemData = default;
        }
        public override UIType Type => UIType.ItemDetails;
        public override UICanvasType CanvasType => UICanvasType.SecondPanel;
    }

    public enum ItemDetailsType
    {
        None,
        Bag,
        Equipment,
        Shop,
    }
}
