using System;
using System.Collections.Generic;
using AOTScripts.Tool.Resource;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Network.UI;
using HotUpdate.Scripts.Tool.ReactiveProperty;
using HotUpdate.Scripts.UI.UIBase;
using HotUpdate.Scripts.UI.UIs.Overlay;
using HotUpdate.Scripts.UI.UIs.Panel.ItemList;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace HotUpdate.Scripts.UI.UIs.SecondPanel
{
    public class PlayerInGameInfoScreenUI : ScreenUIBase
    {
        [SerializeField]
        private ContentItemList contentItemList;
        [SerializeField]
        private Button closeButton;
        private UIManager _uiManager;
        private PropertyConfig _propertyConfig;
        private Dictionary<int, PropertyItemData> _propertyItemDatas;

        [Inject]
        private void Init(UIManager uiManager,IConfigProvider configProvider)
        {
            _uiManager = uiManager;
            _propertyConfig = configProvider.GetConfig<PropertyConfig>();
            closeButton.onClick.AddListener(() =>
            {
                _uiManager.CloseUI(Type);
            });
        }

        public void BindPlayerProperty(HReactiveDictionary<int, PropertyItemData> playerPropertyData)
        {
            _propertyItemDatas = new Dictionary<int, PropertyItemData>();
            foreach (var item in playerPropertyData)
            {
                var propertyItem = _propertyConfig.GetPropertyConfigData((PropertyTypeEnum)item.Key);
                if (!propertyItem.showInUI)
                {
                    continue;
                }
                _propertyItemDatas.Add(item.Key, item.Value);
            }
            contentItemList.SetItemList(_propertyItemDatas);
            foreach (var key in playerPropertyData.Keys)
            {
                var slot = playerPropertyData[key];
                _propertyItemDatas.Add(key, slot);
            }

            contentItemList.SetItemList(_propertyItemDatas);
            playerPropertyData.ObserveUpdate((x, y, z) =>
                {
                    _propertyItemDatas[x] = z;
                    contentItemList.ReplaceItem<PropertyItemData, PropertyItems>(x, z);
                })
                .AddTo(this);
            playerPropertyData.ObserveAdd((x,y) =>
                {
                    if (!_propertyItemDatas.ContainsKey(x))
                    {
                        _propertyItemDatas.Add(x,y);
                        contentItemList.AddItem<PropertyItemData, PropertyItems>(x,y);
                    }
                })
                .AddTo(this);
            playerPropertyData.ObserveRemove((x,y) =>
                {
                    if (_propertyItemDatas.ContainsKey(x))
                    {
                        _propertyItemDatas.Remove(x);
                        contentItemList.RemoveItem(x);
                    }
                })
                .AddTo(this);
            playerPropertyData.ObserveClear(x =>
                {
                    _propertyItemDatas.Clear();
                    contentItemList.Clear();
                })
                .AddTo(this);
        }

        private void OnDestroy()
        {
            closeButton.onClick.RemoveAllListeners();
        }

        public override UIType Type => UIType.PlayerInGameInfo;
        public override UICanvasType CanvasType => UICanvasType.SecondPanel;
    }
}