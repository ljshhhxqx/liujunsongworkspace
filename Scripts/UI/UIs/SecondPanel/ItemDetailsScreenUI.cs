using System;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Network.PredictSystem.UI;
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
        [SerializeField] private Button equipButton;
        [SerializeField] private Button lockButton;
        [SerializeField] private Button quitButton;
        
        [Header("Count Slider")]
        [SerializeField] private CountSliderButtonGroup useCountSlider;
        [SerializeField] private CountSliderButtonGroup dropCountSlider;
        [SerializeField] private CountSliderButtonGroup sellCountSlider;
        [SerializeField] private CountSliderButtonGroup buyCountSlider;
        
        private UIManager _uiManager;
        private IItemBaseData _currentItemData;
        private ItemDetailsType _currentItemDetailsType;
        private ValuePropertyData _currentValuePropertyData;
        
        private BindingKey _itemDetailsBagBindingKey;
        private BindingKey _itemDetailsShopBindingKey;

        [Inject]
        private void Init(UIManager uiManager)
        {
            _uiManager = uiManager;
            _itemDetailsBagBindingKey = new BindingKey(UIPropertyDefine.BagItem);
            _itemDetailsShopBindingKey = new BindingKey(UIPropertyDefine.ShopItem);
            equipButton.OnClickAsObservable()
                .ThrottleFirst(TimeSpan.FromSeconds(0.5f))
                .Subscribe(_ => OnEquipClicked())
                .AddTo(this);
            lockButton.OnClickAsObservable()
                .ThrottleFirst(TimeSpan.FromSeconds(0.5f))
                .Subscribe(_ => OnLockClicked())
                .AddTo(this);
            quitButton.OnClickAsObservable()
                .ThrottleFirst(TimeSpan.FromSeconds(0.5f))
                .Subscribe(_ => _uiManager.CloseUI(Type))
                .AddTo(this);
            var shopItem = UIPropertyBinder.GetReactiveDictionary<RandomShopItemData>(_itemDetailsShopBindingKey);
            var bagItem = UIPropertyBinder.GetReactiveDictionary<BagItemData>(_itemDetailsBagBindingKey);
            shopItem.ObserveRemove()
                .Subscribe(x =>
                {
                    if (_currentItemData is RandomShopItemData randomShopItemData && randomShopItemData.ShopId == x.Value.ShopId)
                    {
                        _uiManager.CloseUI(Type);
                    }
                })
                .AddTo(this);
            shopItem.ObserveReplace()
                .Subscribe(x =>
                {
                    if (_currentItemData is RandomShopItemData randomShopItemData && randomShopItemData.ShopId == x.NewValue.ShopId)
                    {
                        OpenShop(x.NewValue);
                    }
                })
                .AddTo(this);
            shopItem.ObserveReset()
                .Subscribe(_ =>
                {
                    if (_currentItemData is RandomShopItemData randomShopItemData)
                    {
                        _uiManager.CloseUI(Type);
                    }
                })
                .AddTo(this);
            bagItem.ObserveRemove()
                .Subscribe(x =>
                {
                    if (_currentItemData is BagItemData bagItemData && x.Value.ConfigId == bagItemData.ConfigId)
                    {
                        _uiManager.CloseUI(Type);
                    }
                })
                .AddTo(this);
            bagItem.ObserveReplace()
                .Subscribe(x =>
                {
                    if (_currentItemData is BagItemData bagItemData && x.NewValue.ConfigId == bagItemData.ConfigId)
                    {
                        OpenBag(x.NewValue);
                    }
                })
                .AddTo(this);
            bagItem.ObserveReset()
                .Subscribe(_ =>
                {
                    if (_currentItemData is BagItemData bagItemData)
                        _uiManager.CloseUI(Type);
                })
                .AddTo(this);
        }

        public void BindPlayerGold(IObservable<ValuePropertyData> playerGold)
        {
            playerGold.Subscribe(data =>
            {
                _currentValuePropertyData = data;
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
            if (randomShopItemData.RemainingCount <= 0)
            {
                _uiManager.CloseUI(Type);
                return;
            }
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
            priceText.text = $"价格: {randomShopItemData.Price}G 当前金币: {_currentValuePropertyData.Gold}G";
            priceText.color = randomShopItemData.Price <= _currentValuePropertyData.Gold ? Color.green : Color.red;
            
            var countSliderButtonGroupData = new CountSliderButtonGroupData
            {
                MinCount = Mathf.Min(1, randomShopItemData.RemainingCount),
                MaxCount = randomShopItemData.RemainingCount,
                Callback = x =>
                {
                    _uiManager.ShowTips($"是否购买{x}个{randomShopItemData.Name}？", () =>
                    {
                        CountHandler(x);
                        randomShopItemData.OnBuyItem?.Invoke(randomShopItemData.ShopId, x);
                    });
                },
                PricePerItem = randomShopItemData.Price,
                ShowPrice = true,
                CurrentGold = _currentValuePropertyData.Gold,
                ButtonType = ButtonType.Buy
            };
            buyCountSlider.Init(countSliderButtonGroupData);
            countSliderButtonGroupData.PlayerItemType = randomShopItemData.ItemType;
            buyCountSlider.OnSliderChanged.Subscribe(x =>
            {
                var newPrice = randomShopItemData.Price * x;
                priceText.text = $"价格: {newPrice}G 当前金币: {_currentValuePropertyData.Gold}G";
                priceText.color = newPrice <= _currentValuePropertyData.Gold ? Color.green : Color.red;
            }).AddTo(this);
            void CountHandler(int count)
            {
                if (count > randomShopItemData.RemainingCount)
                {
                    Debug.LogError($"Now Count is Over the max stack {count}-{randomShopItemData.RemainingCount}");
                }

                randomShopItemData.RemainingCount -= Mathf.Clamp(count, 0, randomShopItemData.RemainingCount);
                UpdateShopItemUI(randomShopItemData);
                UpdateButtonStates();
            }
        }

        private void UpdateBagItemUI(BagItemData bagItemData)
        {
            // 基础信息
            itemIcon.sprite = bagItemData.Icon;
            qualityBorder.sprite = bagItemData.QualityIcon;
            itemNameText.text = bagItemData.ItemName;
            
            // 堆叠显示
            stackText.text = $"当前{bagItemData.Stack}个/最大{bagItemData.MaxStack}个";
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
                Callback = x =>
                {
                    _uiManager.ShowTips($"是否使用{x}个{bagItemData.ItemName}？", () =>
                    {
                        CountHandler(x);
                        bagItemData.OnUseItem?.Invoke(bagItemData.Index, x);
                    });
                },
                PricePerItem = bagItemData.Price * bagItemData.SellRatio,
                ShowPrice = false,
                CurrentGold = _currentValuePropertyData.Gold,
                ButtonType = ButtonType.Use
            };
            useCountSlider.Init(countSliderButtonGroupData);
            countSliderButtonGroupData.Callback = x =>
            {
                _uiManager.ShowTips($"是否丢弃{x}个{bagItemData.ItemName}？", () =>
                {
                    CountHandler(x);
                    bagItemData.OnDropItem?.Invoke(bagItemData.Index, x);
                });
            };
            countSliderButtonGroupData.ButtonType = ButtonType.Drop;
            dropCountSlider.Init(countSliderButtonGroupData);
            countSliderButtonGroupData.ShowPrice = true;
            countSliderButtonGroupData.Callback = x =>
            {
                _uiManager.ShowTips($"是否售出{x}个{bagItemData.ItemName}？", () =>
                {
                    CountHandler(x);
                    bagItemData.OnSellItem?.Invoke(bagItemData.Index, x);
                });
            };
            countSliderButtonGroupData.ButtonType = ButtonType.Sell;
            countSliderButtonGroupData.PlayerItemType = bagItemData.PlayerItemType;
            sellCountSlider.Init(countSliderButtonGroupData);

            void CountHandler(int count)
            {
                if (count > bagItemData.Stack)
                {
                    Debug.LogError($"Now Count is Over the max stack {count}-{bagItemData.MaxStack}");
                }

                bagItemData.Stack -= Mathf.Clamp(count, 0, bagItemData.Stack);
                _currentItemData = bagItemData;
                UpdateBagItemUI(bagItemData);
                UpdateButtonStates();
            }
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
                    _currentItemData = bagItemData;
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
                    _currentItemData = bagItemData;
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
        public ButtonType ButtonType;
        public PlayerItemType PlayerItemType;
    }

    public enum ItemDetailsType
    {
        None,
        Bag,
        Equipment,
        Shop,
    }

    public enum ButtonType
    {
        [Header("使用")]
        Use,
        [Header("丢弃")]
        Drop,
        [Header("售出")]
        Sell,
        [Header("购买")]
        Buy,
    }
}
