using System;
using AOTScripts.Tool;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Network.PredictSystem.UI;
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
        [SerializeField] private Button equipButton;
        [SerializeField] private Button lockButton;
        
        [Header("Count Slider")]
        [SerializeField] private CountSliderButtonGroup useCountSlider;
        [SerializeField] private CountSliderButtonGroup dropCountSlider;
        [SerializeField] private CountSliderButtonGroup sellCountSlider;
        [SerializeField] private CountSliderButtonGroup buyCountSlider;
        
        private UIManager _uiManager;
        private IItemBaseData _currentItemData;
        private ItemDetailsType _currentItemDetailsType;
        private GoldData _currentGoldData;

        [Inject]
        private void Init(UIManager uiManager)
        {
            _uiManager = uiManager;

            equipButton.onClick.RemoveAllListeners();
            lockButton.onClick.RemoveAllListeners();
            equipButton.BindDebouncedListener(OnEquipClicked);
            lockButton.BindDebouncedListener(OnLockClicked);
        }

        public void BindPlayerGold(IObservable<GoldData> playerGold)
        {
            playerGold.Subscribe(data =>
            {
                _currentGoldData = data;
                useCountSlider.SetPlayerGold(data.Gold);
                dropCountSlider.SetPlayerGold(data.Gold);
                sellCountSlider.SetPlayerGold(data.Gold);
                buyCountSlider.SetPlayerGold(data.Gold);
                UpdateUI();
            }).AddTo(this);
        }

        // private void OnBuyClicked()
        // {
        //     switch (_currentItemData)
        //     {
        //         case RandomShopItemData randomShopItemData:
        //             randomShopItemData.OnBuyItem?.Invoke(randomShopItemData.ShopId, buyCountSlider.Value);
        //             UpdateButtonStates();
        //             break;
        //         default:
        //             throw new ArgumentOutOfRangeException(nameof(_currentItemData));
        //     }
        // }

        public void OpenBag(BagItemData itemData, ItemDetailsType itemDetailsType = ItemDetailsType.Bag)
        {
            _currentItemData = itemData;
            _currentItemDetailsType = itemDetailsType;
            UpdateUI();
            UpdateButtonStates();
        }

        public void OpenShop(RandomShopItemData itemData)
        {
            UpdateShopItemUI(itemData);
            UpdateButtonStates();
        }

        private void UpdateUI()
        {
            switch (_currentItemData)
            {
                case BagItemData bagItemData:
                    UpdateBagItemUI(bagItemData);
                    break;
                case RandomShopItemData randomShopItemData:
                    UpdateShopItemUI(randomShopItemData);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void UpdateShopItemUI(RandomShopItemData randomShopItemData)
        {
            itemIcon.sprite = randomShopItemData.Icon;
            qualityBorder.sprite = randomShopItemData.QualityIcon;
            itemNameText.text = randomShopItemData.Name;
            stackText.text = $"剩余：{randomShopItemData.RemainingCount}个，总量：{randomShopItemData.MaxCount}个";
            stackText.color = randomShopItemData.RemainingCount > 0 ? Color.green : Color.red;
            
            // 描述信息
            descriptionText.text = randomShopItemData.Description;
            var showProperty = randomShopItemData.ItemType.ShowProperty();
            propertyText.gameObject.SetActive(showProperty);
            if (!string.IsNullOrEmpty(randomShopItemData.MainProperty))
            {
                propertyText.text = randomShopItemData.MainProperty;
            }
            else if (!string.IsNullOrEmpty(randomShopItemData.RandomProperty))
            {
                propertyText.text = randomShopItemData.RandomProperty;
            }
            
            // 被动效果（仅装备类显示）
            var showPassive = randomShopItemData.ItemType.IsEquipment();
            passiveEffectText.gameObject.SetActive(showPassive);
            passiveEffectText.text = showPassive ? randomShopItemData.PassiveDescription : "";
            // 价格信息
            priceText.text = $"价格: {randomShopItemData.Price}G 当前金币: {_currentGoldData.Gold}G";
            priceText.color = randomShopItemData.Price <= _currentGoldData.Gold ? Color.green : Color.red;
            
            var countSliderButtonGroupData = new CountSliderButtonGroupData
            {
                MinCount = Mathf.Min(1, randomShopItemData.RemainingCount),
                MaxCount = randomShopItemData.MaxCount,
                Callback = x => randomShopItemData.OnBuyItem?.Invoke(randomShopItemData.ShopId, x),
                PricePerItem = randomShopItemData.Price,
                ShowPrice = true,
                CurrentGold = _currentGoldData.Gold
            };
            buyCountSlider.Init(countSliderButtonGroupData);
            buyCountSlider.OnSliderChanged.Subscribe(x =>
            {
                var newPrice = randomShopItemData.Price * x;
                priceText.text = $"价格: {newPrice}G 当前金币: {_currentGoldData.Gold}G";
                priceText.color = newPrice <= _currentGoldData.Gold ? Color.green : Color.red;
            }).AddTo(this);
        }

        private void UpdateBagItemUI(BagItemData bagItemData)
        {
            // 基础信息
            itemIcon.sprite = bagItemData.Icon;
            qualityBorder.sprite = bagItemData.QualityIcon;
            itemNameText.text = bagItemData.ItemName;
            
            // 堆叠显示
            stackText.text = $"{bagItemData.Stack}/{bagItemData.MaxStack}";
            stackText.gameObject.SetActive(bagItemData.MaxStack > 1);

            // 描述信息
            descriptionText.text = bagItemData.Description;
            var showProperty = bagItemData.PlayerItemType.ShowProperty();
            propertyText.gameObject.SetActive(showProperty);
            if (!string.IsNullOrEmpty(bagItemData.PropertyDescription))
            {
                propertyText.text = bagItemData.PropertyDescription;
            }
            else if (!string.IsNullOrEmpty(bagItemData.RandomDescription))
            {
                propertyText.text = bagItemData.RandomDescription;
            }
            
            // 被动效果（仅装备类显示）
            var showPassive = bagItemData.PlayerItemType.IsEquipment();
            passiveEffectText.gameObject.SetActive(showPassive);
            passiveEffectText.text = showPassive ? bagItemData.PassiveDescription : "";

            // 价格信息
            priceText.text = $"价格: {bagItemData.Price * bagItemData.SellRatio}G";
            var countSliderButtonGroupData = new CountSliderButtonGroupData
            {
                MinCount = Mathf.Min(1, bagItemData.Stack),
                MaxCount = bagItemData.Stack,
                Callback = x => bagItemData.OnUseItem?.Invoke(bagItemData.Index, x),
                PricePerItem = bagItemData.Price * bagItemData.SellRatio,
                ShowPrice = false,
                CurrentGold = _currentGoldData.Gold
            };
            useCountSlider.Init(countSliderButtonGroupData);
            countSliderButtonGroupData.Callback = x => bagItemData.OnDropItem?.Invoke(bagItemData.Index, x);
            dropCountSlider.Init(countSliderButtonGroupData);
            countSliderButtonGroupData.ShowPrice = true;
            countSliderButtonGroupData.Callback = x => bagItemData.OnSellItem?.Invoke(bagItemData.Index, x);
            sellCountSlider.Init(countSliderButtonGroupData);
        }

        private void UpdateButtonStates()
        {
            switch (_currentItemData)
            {
                case BagItemData bagItemData:
                    var isLocked = bagItemData.IsLock;
                    buyCountSlider.gameObject.SetActive(false);
                    switch (_currentItemDetailsType)
                    {
                        case ItemDetailsType.Bag:
                            useCountSlider.gameObject.SetActive(!isLocked && bagItemData.PlayerItemType == PlayerItemType.Consume);
                            dropCountSlider.gameObject.SetActive(!isLocked);
                            equipButton.gameObject.SetActive(!isLocked && bagItemData.PlayerItemType.IsEquipment());
                            lockButton.gameObject.SetActive(true);
                            sellCountSlider.gameObject.SetActive(false);
                            break;
                        case ItemDetailsType.Equipment:
                            useCountSlider.gameObject.SetActive(false);
                            dropCountSlider.gameObject.SetActive(false);
                            equipButton.gameObject.SetActive(!isLocked && bagItemData.PlayerItemType.IsEquipment());
                            lockButton.gameObject.SetActive(true);
                            sellCountSlider.gameObject.SetActive(false);
                            break;
                        case ItemDetailsType.Shop:
                            useCountSlider.gameObject.SetActive(false);
                            dropCountSlider.gameObject.SetActive(false);
                            equipButton.gameObject.SetActive(false);
                            lockButton.gameObject.SetActive(false);
                            sellCountSlider.gameObject.SetActive(true);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }

            
                    lockButton.GetComponentInChildren<TextMeshProUGUI>().text = 
                        bagItemData.IsLock ? "解锁" : "锁定";
                    equipButton.GetComponentInChildren<TextMeshProUGUI>().text = 
                        bagItemData.IsEquip ? "卸下" : "装备";
                    break;
                case RandomShopItemData randomShopItemData:
                    buyCountSlider.gameObject.SetActive(true);
                    useCountSlider.gameObject.SetActive(false);
                    dropCountSlider.gameObject.SetActive(false);
                    equipButton.gameObject.SetActive(false);
                    lockButton.gameObject.SetActive(false);
                    sellCountSlider.gameObject.SetActive(false);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(_currentItemData));
            }
            
        }

        #region Button Handlers
        // private void OnUseClicked()
        // {
        //     switch (_currentItemData)
        //     {
        //         case BagItemData bagItemData:
        //
        //             if(bagItemData.Stack > 1)
        //             {
        //                 _uiManager.SwitchUI<QuantitySelectionPanel>(ui =>
        //                 {
        //                     ui.Show(max: bagItemData.Stack, onConfirm: (amount) =>
        //                     {
        //                         bagItemData.OnUseItem?.Invoke(bagItemData.Index, amount);
        //                         Close();
        //                     });
        //                 });
        //             }
        //             else
        //             {
        //                 bagItemData.OnUseItem?.Invoke(bagItemData.Index, 1);
        //                 Close();
        //             }
        //             break;
        //         case RandomShopItemData randomShopItemData:
        //             break;
        //         default:
        //             throw new ArgumentOutOfRangeException();
        //     }
        // }

        private void OnEquipClicked()
        {
            switch (_currentItemData)
            {
                case BagItemData bagItemData:
                    var newEquipState = !bagItemData.IsEquip;
                    bagItemData.OnEquipItem?.Invoke(bagItemData.Index, newEquipState);
                    bagItemData.IsEquip = newEquipState;
                    UpdateButtonStates();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void OnLockClicked()
        {
            switch (_currentItemData)
            {
                case BagItemData bagItemData:
                    bool newLockState = !bagItemData.IsLock;
                    bagItemData.OnLockItem?.Invoke(bagItemData.Index, newLockState);
                    bagItemData.IsLock = newLockState;
                    UpdateButtonStates();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(_currentItemData));
            }
        }

        // private void OnSellClicked()
        // {
        //     switch (_currentItemData)
        //     {
        //         case BagItemData bagItemData:
        //             var quantityPanel = _uiManager.SwitchUI<QuantitySelectionPanel>();
        //             quantityPanel.Show(max: bagItemData.Stack, onConfirm: (amount) => 
        //             {
        //                 bagItemData.OnSellItem?.Invoke(bagItemData.Index, amount);
        //                 Close();
        //             });
        //             break;
        //         case RandomShopItemData randomShopItemData:
        //             break;
        //         default:
        //             throw new ArgumentOutOfRangeException(nameof(_currentItemData));
        //     }
        // }
        //
        // private void OnDropClicked()
        // {
        //     switch (_currentItemData)
        //     {
        //         case BagItemData bagItemData:
        //             var quantityPanel = _uiManager.SwitchUI<QuantitySelectionPanel>();
        //             quantityPanel.Show(max: bagItemData.Stack, onConfirm: (amount) => 
        //             {
        //                 bagItemData.OnDropItem?.Invoke(bagItemData.Index, amount);
        //                 Close();
        //             });
        //             break;
        //         case RandomShopItemData randomShopItemData:
        //             break;
        //         default:
        //             throw new ArgumentOutOfRangeException(nameof(_currentItemData));
        //     }
        // }
        #endregion

        private void Close()
        {
            _currentItemData = default;
            _currentItemDetailsType = ItemDetailsType.None;
        }
        public override UIType Type => UIType.ItemDetails;
        public override UICanvasType CanvasType => UICanvasType.SecondPanel;
    }

    public struct CountSliderButtonGroupData
    {
        public int MinCount;
        public int MaxCount;
        public Action<int> Callback;
        public float PricePerItem;
        public bool ShowPrice;
        public float CurrentGold;
    }

    public enum ItemDetailsType
    {
        None,
        Bag,
        Equipment,
        Shop,
    }
}
