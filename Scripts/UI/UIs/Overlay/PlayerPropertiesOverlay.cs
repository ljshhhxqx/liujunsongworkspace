using System;
using System.Collections.Generic;
using System.Linq;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Network.Client.Player;
using HotUpdate.Scripts.Network.PredictSystem.UI;
using HotUpdate.Scripts.Network.Server.InGame;
using HotUpdate.Scripts.UI.UIs.Panel.Item;
using HotUpdate.Scripts.UI.UIs.Panel.ItemList;
using Mirror.BouncyCastle.Math.EC.Rfc7748;
using UI.UIBase;
using UI.UIs.Common;
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
        
        private PropertyItemData[] _propertyItemDatas;
        
        [Inject]
        private void Init(PlayerInGameManager playerInGameManager, IConfigProvider configProvider)
        {
            _playerInGameManager = playerInGameManager;
            _propertyConfig = configProvider.GetConfig<PropertyConfig>();
        }

        public void BindPlayerProperty(ReactiveDictionary<int, PropertyItemData> playerPropertyData)
        {
            _propertyItemDatas ??= playerPropertyData.Values.ToArray();
            contentItemList.SetItemList(_propertyItemDatas);
            playerPropertyData.ObserveReplace()
                .Subscribe(x =>
                {
                    for (int i = 0; i < _propertyItemDatas.Length; i++)
                    {
                        if (x.Key == (int)_propertyItemDatas[i].PropertyType)
                        {
                            _propertyItemDatas[i] = x.NewValue;
                            break;
                        }
                    }
                    contentItemList.SetItemList(_propertyItemDatas);
                })
                .AddTo(this);
            playerPropertyData.ObserveAdd()
                .Subscribe(x =>
                {
                    var newPropertyItemDatas = new PropertyItemData[_propertyItemDatas.Length + 1];
                    for (var i = 0; i < newPropertyItemDatas.Length; i++)
                    {
                        if (i < _propertyItemDatas.Length)
                        {
                            newPropertyItemDatas[i] = _propertyItemDatas[i];
                        }
                        else
                        {
                            newPropertyItemDatas[i] = x.Value;
                        }
                    }

                    _propertyItemDatas = newPropertyItemDatas;

                    contentItemList.SetItemList(_propertyItemDatas);
                })
                .AddTo(this);
            playerPropertyData.ObserveRemove()
                .Subscribe(x =>
                {
                    var originalArray = _propertyItemDatas.ToList();
                    originalArray.RemoveAll(y => (int)y.PropertyType == x.Key);
                    _propertyItemDatas = originalArray.ToArray();
                    contentItemList.SetItemList(_propertyItemDatas);
                })
                .AddTo(this);
            playerPropertyData.ObserveReset()
                .Subscribe(x =>
                {
                    _propertyItemDatas = Array.Empty<PropertyItemData>();
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