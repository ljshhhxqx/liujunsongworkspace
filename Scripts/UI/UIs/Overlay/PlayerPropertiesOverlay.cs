using System;
using System.Collections.Generic;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Network.Client.Player;
using HotUpdate.Scripts.Network.PredictSystem.UI;
using HotUpdate.Scripts.Network.Server.InGame;
using HotUpdate.Scripts.UI.UIs.Panel.Item;
using HotUpdate.Scripts.UI.UIs.Panel.ItemList;
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
        
        [Inject]
        private void Init(PlayerInGameManager playerInGameManager, IConfigProvider configProvider)
        {
            _playerInGameManager = playerInGameManager;
            _propertyConfig = configProvider.GetConfig<PropertyConfig>();
        }

        public void SetPlayerProperties(PlayerPropertyComponent playerPropertyComponent)
        {
            //  _playerPropertyComponent = playerPropertyComponent;
            // //  playerPropertyComponent.CurrentAnimationStateProperty.Subscribe(state =>
            // //  {
            // //      animationState.SetField(animationState.name, state);
            // // });
            //  playerPropertyComponent.PlayerStateProperty.Subscribe(state =>
            //  {
            //      playerStateProperty.SetField(animationState.name, state);
            //  });
            //  // playerPropertyComponent.CurrentAnimationStateProperty.Subscribe(chestType =>
            //  // {
            //  //     currentChestType.SetField(currentChestType.name, chestType);
            //  // });
            //  playerPropertyComponent.HasMovementInputProperty.Subscribe(hasInput =>
            //  {
            //      hasMovementInput.SetField(hasMovementInput.name, hasInput);
            //  });
            var list = new List<PropertyItemData>();
            var enumValues = Enum.GetValues(typeof(PropertyTypeEnum));
            for (var i = 0; i < enumValues.Length; i++)
            {
                var propertyType = (PropertyTypeEnum)enumValues.GetValue(i);
                var propertyConfig = _propertyConfig.GetPropertyConfigData(propertyType);
                var displayName = propertyConfig.description;
                var consumeType = propertyConfig.consumeType;
                list.Add(new PropertyItemData
                {
                    Name = displayName,
                    CurrentProperty = 1,
                    MaxProperty = 1,
                    ConsumeType = consumeType
                });
            }

            contentItemList.SetItemList(list.ToArray());
        }

        private void OnPlayerPropertiesChanged(PropertyItemData[] value) 
        {
            
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