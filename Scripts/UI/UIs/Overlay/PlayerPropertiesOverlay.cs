using System.Collections.Generic;
using System.Linq;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Network.Client.Player;
using HotUpdate.Scripts.Network.Server.InGame;
using HotUpdate.Scripts.UI.UIs.Panel.Item;
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
        private PlayerInGameManager _playerInGameManager;
        private PlayerPropertyComponent _playerPropertyComponent;
        private PropertyConfig _propertyConfig;

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
        
        private Dictionary<int, PropertyItemData> _propertyItemDatas;
        
        [Inject]
        private void Init(IConfigProvider configProvider)
        {
            _playerInGameManager = PlayerInGameManager.Instance;
            _propertyConfig = configProvider.GetConfig<PropertyConfig>();
        }

        public void BindPlayerProperty(ReactiveDictionary<int, PropertyItemData> playerPropertyData)
        {
            _propertyItemDatas = new Dictionary<int, PropertyItemData>();
            foreach (var key in playerPropertyData.Keys)
            {
                var slot = playerPropertyData[key];
                _propertyItemDatas.Add(key, slot);
            }

            contentItemList.SetItemList(playerPropertyData);
            playerPropertyData.ObserveReplace()
                .Subscribe(x =>
                {
                    if (!x.NewValue.Equals(x.OldValue))
                    {
                        _propertyItemDatas[x.Key] = x.NewValue;
                        contentItemList.ReplaceItem(x.Key, x.NewValue);
                        //Debug.Log($"Replace property {x.Key} {x.NewValue}");
                    }
                })
                .AddTo(this);
            playerPropertyData.ObserveAdd()
                .Subscribe(x =>
                {
                    if (!_propertyItemDatas.ContainsKey(x.Key))
                    {
                        _propertyItemDatas.Add(x.Key, x.Value);
                        contentItemList.AddItem(x.Key, x.Value);
                    }
                })
                .AddTo(this);
            playerPropertyData.ObserveRemove()
                .Subscribe(x =>
                {
                    if (_propertyItemDatas.ContainsKey(x.Key))
                    {
                        _propertyItemDatas.Remove(x.Key);
                        contentItemList.RemoveItem(x.Key);
                    }
                })
                .AddTo(this);
            playerPropertyData.ObserveReset()
                .Subscribe(x =>
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
    }
}