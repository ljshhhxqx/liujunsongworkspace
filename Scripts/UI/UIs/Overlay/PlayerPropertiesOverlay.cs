using System;
using System.Collections.Generic;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Network.Client.Player;
using HotUpdate.Scripts.Network.Server.InGame;
using HotUpdate.Scripts.Network.UI;
using HotUpdate.Scripts.Tool.ReactiveProperty;
using HotUpdate.Scripts.UI.UIs.Panel.ItemList;
using UI.UIBase;
using UniRx;
using UnityEngine;
using VContainer;

namespace HotUpdate.Scripts.UI.UIs.Overlay
{
    public class PlayerPropertiesOverlay : ScreenUIBase
    {
        [SerializeField]
        private ContentItemList contentItemList;
        [SerializeField]
        private FieldItem animationState;
        [SerializeField]
        private FieldItem currentChestType;
        [SerializeField]
        private FieldItem playerStateProperty;
        [SerializeField]
        private FieldItem hasMovementInput;
        [SerializeField]
        private FieldItem frameCount;
        [SerializeField] 
        private ProgressItem progressItem;
        
        private PlayerInGameManager _playerInGameManager;
        private PlayerPropertyComponent _playerPropertyComponent;
        private PropertyConfig _propertyConfig;

        private Dictionary<int, PropertyItemData> _propertyItemDatas;
        
        [Inject]
        private void Init(IConfigProvider configProvider)
        {
            _playerInGameManager = PlayerInGameManager.Instance;
            _propertyConfig = configProvider.GetConfig<PropertyConfig>();
        }

        public void BindPlayerProperty(HReactiveDictionary<int, PropertyItemData> playerPropertyData)
        {
            _propertyItemDatas = new Dictionary<int, PropertyItemData>();
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

        public override UIType Type => UIType.PlayerPropertiesOverlay;
        public override UICanvasType CanvasType => UICanvasType.Overlay;

        private float _seconds;
        
        private void Update()
        {
            _seconds += Time.deltaTime;
            if (_seconds>=0.5f)
            {
                _seconds = 0;
                frameCount.SetField("帧数：", 1/Time.deltaTime);
            }
        }

        public void StartProgress(string description, float countdown, Action onComplete = null, Func<bool> condition = null)
        {
            if (countdown <= 0)
            {
                onComplete?.Invoke();
                return;
            }
            progressItem.gameObject.SetActive(true);
            progressItem.SetProgress(description, countdown, onComplete, condition);
        }
    }
}