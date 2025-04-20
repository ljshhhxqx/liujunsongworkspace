using System;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.UI.UIBase;
using HotUpdate.Scripts.UI.UIs.Panel.Item;
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
        [SerializeField] private Button sellButton;
        [SerializeField] private Button dropButton;

        [Header("Sub Panels")]
        [SerializeField] private QuantitySelectionPanel quantityPanel;
        
        private UIManager _uiManager;
        private BagItemData _currentItemData;
        private bool _isEquippedState; // 当前装备状态缓存

        [Inject]
        private void Init(UIManager uiManager)
        {
            _uiManager = uiManager;
            useButton.OnClickAsObservable().Subscribe(_ => OnUseClicked()).AddTo(this);
            equipButton.OnClickAsObservable().Subscribe(_ => OnEquipClicked()).AddTo(this);
            lockButton.OnClickAsObservable().Subscribe(_ => OnLockClicked()).AddTo(this);
            sellButton.OnClickAsObservable().Subscribe(_ => OnSellClicked()).AddTo(this);
            dropButton.OnClickAsObservable().Subscribe(_ => OnDropClicked()).AddTo(this);
        }

        public void Open(BagItemData itemData)
        {
            _currentItemData = itemData;
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
            propertyText.text = _currentItemData.PropertyDescription;
            
            // 被动效果（仅装备类显示）
            var showPassive = _currentItemData.PlayerItemType.IsEquipment();
            passiveEffectText.gameObject.SetActive(showPassive);
            passiveEffectText.text = showPassive ? _currentItemData.EquipPassiveDescription : "";

            // 价格信息
            priceText.text = $"价格: {_currentItemData.Price}G\n售价: {_currentItemData.Price * _currentItemData.SellRatio}G";

            // 按钮状态
            UpdateButtonStates();
        }

        private void UpdateButtonStates()
        {
            var isLocked = _currentItemData.IsLock;
            
            useButton.interactable = !isLocked;
            equipButton.interactable = !isLocked && _currentItemData.PlayerItemType.IsEquipment();
            sellButton.interactable = !isLocked;
            dropButton.interactable = !isLocked;

            lockButton.GetComponentInChildren<TextMeshProUGUI>().text = 
                _currentItemData.IsLock ? "解锁" : "锁定";
            
            if(equipButton.interactable)
            {
                equipButton.GetComponentInChildren<TextMeshProUGUI>().text = 
                    _currentItemData.IsEquip ? "卸下" : "装备";
            }
        }

        #region Button Handlers
        private void OnUseClicked()
        {
            if(_currentItemData.Stack > 1)
            {
                quantityPanel.Show(max: _currentItemData.Stack, onConfirm: (amount) => 
                {
                    _currentItemData.OnUseItem?.Invoke(_currentItemData.Index, amount);
                    Close();
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
            quantityPanel.Show(max: _currentItemData.Stack, onConfirm: (amount) => 
            {
                _currentItemData.OnSellItem?.Invoke(_currentItemData.Index, amount);
                Close();
            });
        }

        private void OnDropClicked()
        {
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

    // 数量选择面板（简版实现）
    public class QuantitySelectionPanel : MonoBehaviour
    {
        [SerializeField] private TMP_InputField amountInput;
        [SerializeField] private Button confirmButton;

        public void Show(int max, Action<int> onConfirm)
        {
            gameObject.SetActive(true);
            amountInput.text = "1";
            amountInput.onValueChanged.AddListener(v => 
            {
                if(!int.TryParse(v, out int value)) return;
                value = Mathf.Clamp(value, 1, max);
                amountInput.text = value.ToString();
            });

            confirmButton.onClick.AddListener(() => 
            {
                if(int.TryParse(amountInput.text, out int result))
                {
                    onConfirm?.Invoke(Mathf.Clamp(result, 1, max));
                    Hide();
                }
            });
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
