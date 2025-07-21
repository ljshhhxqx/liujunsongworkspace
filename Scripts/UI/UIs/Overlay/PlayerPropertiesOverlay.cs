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
        
        private IDictionary<int, PropertyItemData> _propertyItemDatas;
        
        [Inject]
        private void Init(IConfigProvider configProvider)
        {
            _playerInGameManager = PlayerInGameManager.Instance;
            _propertyConfig = configProvider.GetConfig<PropertyConfig>();
        }

        public void BindPlayerProperty(ReactiveDictionary<int, PropertyItemData> playerPropertyData)
        {
            _propertyItemDatas ??= playerPropertyData;
            contentItemList.SetItemList(playerPropertyData);
            playerPropertyData.ObserveReplace()
                .Subscribe(x =>
                {
                    _propertyItemDatas[x.Key] = x.NewValue;
                    contentItemList.SetItemList(_propertyItemDatas);
                })
                .AddTo(this);
            playerPropertyData.ObserveAdd()
                .Subscribe(x =>
                {
                    _propertyItemDatas.Add(x.Key, x.Value);
                    contentItemList.SetItemList(_propertyItemDatas);
                })
                .AddTo(this);
            playerPropertyData.ObserveRemove()
                .Subscribe(x =>
                {
                    _propertyItemDatas.Remove(x.Key);
                    contentItemList.SetItemList(_propertyItemDatas);
                })
                .AddTo(this);
            playerPropertyData.ObserveReset()
                .Subscribe(x =>
                {
                    _propertyItemDatas.Clear();
                    contentItemList.SetItemList(_propertyItemDatas);
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