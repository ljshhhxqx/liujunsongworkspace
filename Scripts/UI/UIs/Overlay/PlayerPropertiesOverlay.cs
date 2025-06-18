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
        
        private List<PropertyItemData> _propertyItemDatas;
        
        [Inject]
        private void Init(IConfigProvider configProvider)
        {
            _playerInGameManager = PlayerInGameManager.Instance;
            _propertyConfig = configProvider.GetConfig<PropertyConfig>();
        }

        public void BindPlayerProperty(ReactiveDictionary<int, PropertyItemData> playerPropertyData)
        {
            _propertyItemDatas ??= playerPropertyData.Values.ToList();
            contentItemList.SetItemList(_propertyItemDatas.ToArray());
            playerPropertyData.ObserveReplace()
                .Subscribe(x =>
                {
                    var index = _propertyItemDatas.FindIndex(y => (int)y.PropertyType == x.Key);
                    _propertyItemDatas[index] = x.NewValue;
                    contentItemList.SetItemList(_propertyItemDatas.ToArray());
                })
                .AddTo(this);
            playerPropertyData.ObserveAdd()
                .Subscribe(x =>
                {
                    _propertyItemDatas.Add(x.Value);
                    contentItemList.SetItemList(_propertyItemDatas.ToArray());
                })
                .AddTo(this);
            playerPropertyData.ObserveRemove()
                .Subscribe(x =>
                {
                    _propertyItemDatas.RemoveAll(y => (int)y.PropertyType == x.Key);
                    contentItemList.SetItemList(_propertyItemDatas.ToArray());
                })
                .AddTo(this);
            playerPropertyData.ObserveReset()
                .Subscribe(x =>
                {
                    _propertyItemDatas.Clear();
                    contentItemList.SetItemList(_propertyItemDatas.ToArray());
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