using System;
using System.Collections.Generic;
using AOTScripts.Data;
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
        [SerializeField] private TextMeshProUGUI conditionText;
        [SerializeField] private TextMeshProUGUI passiveEffectText;
        [SerializeField] private TextMeshProUGUI priceText;
        [SerializeField] private TextMeshProUGUI skillText;
        [SerializeField] private Transform propertyContent;
        [SerializeField] private Transform groupContent;
        [SerializeField] private VerticalLayoutGroup propertyVerticalLayoutGroup;
        private List<TextMeshProUGUI> _texts = new List<TextMeshProUGUI>();
        
        [Header("Interaction Buttons")]
        [SerializeField] private Button equipButton;
        [SerializeField] private Button lockButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private Button enableButton;
        
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
        
        private CompositeDisposable _disposables = new CompositeDisposable();

        [Inject]
        private void Init(UIManager uiManager)
        {
            _uiManager = uiManager;
            _itemDetailsBagBindingKey = new BindingKey(UIPropertyDefine.BagItem);
            _itemDetailsShopBindingKey = new BindingKey(UIPropertyDefine.ShopItem);
            _disposables?.Dispose();
            _disposables = new CompositeDisposable();
            equipButton.OnClickAsObservable()
                .ThrottleFirst(TimeSpan.FromSeconds(0.5f))
                .Subscribe(_ => OnEquipClicked())
                .AddTo(_disposables);
            lockButton.OnClickAsObservable()
                .ThrottleFirst(TimeSpan.FromSeconds(0.5f))
                .Subscribe(_ => OnLockClicked())
                .AddTo(_disposables);
            quitButton.OnClickAsObservable()
                .ThrottleFirst(TimeSpan.FromSeconds(0.5f))
                .Subscribe(_ => _uiManager.CloseUI(Type))
                .AddTo(_disposables);
            enableButton.OnClickAsObservable()
                .ThrottleFirst(TimeSpan.FromSeconds(0.5f))
                .Subscribe(_ => OnEnableClicked())
                .AddTo(_disposables);
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
                .AddTo(_disposables);
            shopItem.ObserveReplace()
                .Subscribe(x =>
                {
                    if (_currentItemData is RandomShopItemData randomShopItemData && randomShopItemData.ShopId == x.NewValue.ShopId && !randomShopItemData.Equals(x.NewValue))
                    {
                        OpenShop(x.NewValue);
                    }
                })
                .AddTo(_disposables);
            shopItem.ObserveReset()
                .Subscribe(_ =>
                {
                    if (_currentItemData is RandomShopItemData randomShopItemData)
                    {
                        _uiManager.CloseUI(Type);
                    }
                })
                .AddTo(_disposables);
            bagItem.ObserveRemove()
                .Subscribe(x =>
                {
                    if (_currentItemData is BagItemData bagItemData && x.Value.ConfigId == bagItemData.ConfigId)
                    {
                        _uiManager.CloseUI(Type);
                    }
                })
                .AddTo(_disposables);
            bagItem.ObserveReplace()
                .Subscribe(x =>
                {
                    if (_currentItemData is BagItemData bagItemData && x.NewValue.ConfigId == bagItemData.ConfigId && !bagItemData.Equals(x.NewValue))
                    {
                        OpenBag(x.NewValue);
                    }
                })
                .AddTo(_disposables);
            bagItem.ObserveReset()
                .Subscribe(_ =>
                {
                    if (_currentItemData is BagItemData bagItemData)
                        _uiManager.CloseUI(Type);
                })
                .AddTo(_disposables);
            var texts = propertyContent.GetComponentsInChildren<TextMeshProUGUI>();
            foreach (var text in texts)
            {
                _texts.Add(text);
            }
        }

        public void BindPlayerGold(IObservable<ValuePropertyData> playerGold)
        {
            playerGold.Subscribe(data =>
            {
                if (Mathf.Approximately(_currentValuePropertyData.Gold, data.Gold))
                {
                    return;
                }
                _currentValuePropertyData = data;
                useCountSlider.SetPlayerGold(data.Gold);
                dropCountSlider.SetPlayerGold(data.Gold);
                sellCountSlider.SetPlayerGold(data.Gold);
                buyCountSlider.SetPlayerGold(data.Gold);
            }).AddTo(_disposables);
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
            _currentItemData = itemData;
            _currentItemDetailsType = ItemDetailsType.Shop;
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
            skillText.text = randomShopItemData.SkillDescription;
            itemIcon.sprite = randomShopItemData.Icon;
            qualityBorder.sprite = randomShopItemData.QualityIcon;
            itemNameText.text = randomShopItemData.Name;
            itemNameText.enabled = !string.IsNullOrWhiteSpace(itemNameText.text);
            stackText.text = randomShopItemData.RemainingCount > 1 ? $"剩余：{randomShopItemData.RemainingCount}个，总量：{randomShopItemData.MaxCount}个" : "";
            stackText.color = randomShopItemData.RemainingCount > 0 ? Color.green : Color.red;
            
            // 描述信息
            descriptionText.text = randomShopItemData.Description;
            descriptionText.enabled = (!string.IsNullOrWhiteSpace(descriptionText.text));
            var showProperty = randomShopItemData.ItemType.ShowProperty();
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
            passiveEffectText.text = showPassive ? randomShopItemData.PassiveDescription : "";
            // 价格信息
            priceText.text = $"价格: {randomShopItemData.Price}G 当前金币: {_currentValuePropertyData.Gold}G";
            priceText.color = randomShopItemData.Price <= _currentValuePropertyData.Gold ? Color.green : Color.red;
            
            var countSliderButtonGroupData = new CountSliderButtonGroupData
            {
                MinCount = Mathf.Max(1, randomShopItemData.RemainingCount),
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
                ButtonType = ButtonType.Buy,
                PlayerItemType = randomShopItemData.ItemType
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
            skillText.text = bagItemData.SkillDescription;
            // 堆叠显示
            stackText.text = $"当前{bagItemData.Stack}个/最大{bagItemData.MaxStack}个";
            stackText.gameObject.SetActive(bagItemData.MaxStack > 1);

            // 描述信息
            descriptionText.text = bagItemData.Description;
            var showProperty = bagItemData.PlayerItemType.ShowProperty();
            if (!string.IsNullOrEmpty(bagItemData.PropertyDescription))
            {
                propertyText.text = bagItemData.PropertyDescription;
            }
            else if (!string.IsNullOrEmpty(bagItemData.RandomDescription))
            {
                propertyText.text = bagItemData.RandomDescription;
            }

            if (conditionText.gameObject.activeSelf)
            {
                conditionText.text = bagItemData.ConditionDescription;
            }
            
            // 被动效果（仅装备类显示）
            var showPassive = bagItemData.PlayerItemType.IsEquipment();
            passiveEffectText.gameObject.SetActive(showPassive && !conditionText.gameObject.activeSelf);
            passiveEffectText.text = showPassive ? bagItemData.PassiveDescription : "";

            // 价格信息
            priceText.text = $"价格: {bagItemData.Price * bagItemData.SellRatio}G";
            var countSliderButtonGroupData = new CountSliderButtonGroupData
            {
                MinCount = Mathf.Max(1, bagItemData.Stack),
                MaxCount = bagItemData.Stack,
                Callback = x =>
                {
                    _uiManager.ShowTips($"是否使用{x}个{bagItemData.ItemName}？", () =>
                    {
                        CountHandler(x);
                        bagItemData.OnUseItem?.Invoke(bagItemData.Index, x);
                        if (x == bagItemData.Stack)
                        {
                            _uiManager.CloseUI(Type);
                        }
                    });
                },
                PricePerItem = bagItemData.Price * bagItemData.SellRatio,
                ShowPrice = false,
                CurrentGold = _currentValuePropertyData.Gold,
                ButtonType = ButtonType.Use,
                PlayerItemType = bagItemData.PlayerItemType
            };
            useCountSlider.Init(countSliderButtonGroupData);
            countSliderButtonGroupData.Callback = x =>
            {
                _uiManager.ShowTips($"是否丢弃{x}个{bagItemData.ItemName}？", () =>
                {
                    CountHandler(x);
                    bagItemData.OnDropItem?.Invoke(bagItemData.Index, x);
                    if (x == bagItemData.Stack)
                    {
                        _uiManager.CloseUI(Type);
                    }
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
                    if (x == bagItemData.Stack)
                    {
                        _uiManager.CloseUI(Type);
                    }
                });
            };
            countSliderButtonGroupData.ButtonType = ButtonType.Sell;
            countSliderButtonGroupData.PlayerItemType = bagItemData.PlayerItemType;
            sellCountSlider.Init(countSliderButtonGroupData);

            return;
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
                    lockButton.GetComponentInChildren<TextMeshProUGUI>().text = 
                        bagItemData.IsLock ? "解锁" : "锁定";
                    equipButton.GetComponentInChildren<TextMeshProUGUI>().text = 
                        bagItemData.IsEquip ? "卸下" : "装备";
                    enableButton.GetComponentInChildren<TextMeshProUGUI>().text =
                        bagItemData.IsEnable ? "技能关" : "技能开";
                    switch (_currentItemDetailsType)
                    {
                        case ItemDetailsType.Bag:
                            useCountSlider.gameObject.SetActive(!isLocked && bagItemData.PlayerItemType == PlayerItemType.Consume);
                            dropCountSlider.gameObject.SetActive(!isLocked);
                            equipButton.gameObject.SetActive(!isLocked && bagItemData.PlayerItemType.IsEquipment());
                            lockButton.gameObject.SetActive(true);
                            sellCountSlider.gameObject.SetActive(false);
                            enableButton.gameObject.SetActive(bagItemData.IsEquip && bagItemData.SkillId != 0);
                            break;
                        case ItemDetailsType.Equipment:
                            useCountSlider.gameObject.SetActive(false);
                            dropCountSlider.gameObject.SetActive(false);
                            equipButton.gameObject.SetActive(!isLocked && bagItemData.PlayerItemType.IsEquipment());
                            lockButton.gameObject.SetActive(true);
                            sellCountSlider.gameObject.SetActive(false);
                            enableButton.gameObject.SetActive(bagItemData.PlayerItemType.IsEquipment());
                            groupContent.gameObject.SetActive(true);
                            break;
                        case ItemDetailsType.Shop:
                            useCountSlider.gameObject.SetActive(false);
                            dropCountSlider.gameObject.SetActive(false);
                            equipButton.gameObject.SetActive(false);
                            lockButton.gameObject.SetActive(false);
                            sellCountSlider.gameObject.SetActive(true);
                            enableButton.gameObject.SetActive(false);
                            groupContent.gameObject.SetActive(false);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                    break;
                case RandomShopItemData randomShopItemData:
                    buyCountSlider.gameObject.SetActive(true);
                    useCountSlider.gameObject.SetActive(false);
                    dropCountSlider.gameObject.SetActive(false);
                    equipButton.gameObject.SetActive(false);
                    lockButton.gameObject.SetActive(false);
                    sellCountSlider.gameObject.SetActive(false);
                    enableButton.gameObject.SetActive(false);
                    groupContent.gameObject.SetActive(false);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(_currentItemData));
            }
            skillText.gameObject.SetActive(!string.IsNullOrEmpty(skillText.text));

            for (int i = 0; i < _texts.Count; i++)
            {
                var text = _texts[i];
                text.gameObject.SetActive(!string.IsNullOrEmpty(_texts[i].text) && !string.IsNullOrWhiteSpace(_texts[i].text));
            }
        }

        #region Button Handlers

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

        private void OnEnableClicked()
        {
            if (_currentItemData is BagItemData bagItemData && bagItemData.SkillId > 0)
            {
                bool newState = !bagItemData.IsEnable;
                bagItemData.OnEnableSkill?.Invoke(bagItemData.Index, bagItemData.SkillId, newState);
                bagItemData.IsEnable = newState;
                _currentItemData = bagItemData;
                UpdateButtonStates();
            }
        }
        #endregion

        private void OnDestroy()
        {
            _disposables?.Dispose();
        }

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
