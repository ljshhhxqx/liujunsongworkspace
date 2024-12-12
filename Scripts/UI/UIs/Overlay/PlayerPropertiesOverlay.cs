using System;
using System.Collections.Generic;
using HotUpdate.Scripts.Network.Client.Player;
using HotUpdate.Scripts.Network.Server.InGame;
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
        private void Init(PlayerInGameManager playerInGameManager)
        {
            _playerInGameManager = playerInGameManager;
        }

        public void SetPlayerProperties(PlayerPropertyComponent playerPropertyComponent)
        {
            _playerPropertyComponent = playerPropertyComponent;
            playerPropertyComponent.CurrentAnimationStateProperty.Subscribe(state =>
            {
                animationState.SetField(animationState.name, state);
           });
            playerPropertyComponent.PlayerStateProperty.Subscribe(state =>
            {
                playerStateProperty.SetField(animationState.name, state);
            });
            playerPropertyComponent.CurrentAnimationStateProperty.Subscribe(chestType =>
            {
                currentChestType.SetField(currentChestType.name, chestType);
            });
            playerPropertyComponent.HasMovementInputProperty.Subscribe(hasInput =>
            {
                hasMovementInput.SetField(hasMovementInput.name, hasInput);
            });
            
            var list = new List<PropertyItemData>();
            for (var i = (int)PropertyTypeEnum.Speed; i <= (int)PropertyTypeEnum.Score; i++)
            {
                var propertyType = (PropertyTypeEnum)i;
                var currentProperty = playerPropertyComponent.GetProperty(propertyType);
                var maxProperty = playerPropertyComponent.GetMaxProperty(propertyType);
                var displayName = propertyType.ToDisplayName();
                var consumeType = propertyType.GetConsumeType();
                list.Add(new PropertyItemData
                {
                    Name = displayName,
                    CurrentProperty = currentProperty,
                    MaxProperty = maxProperty,
                    ConsumeType = consumeType
                });
            }
            contentItemList.SetItemList(list.ToArray());
        }

        public override UIType Type => UIType.PlayerPropertiesOverlay;
        public override UICanvasType CanvasType => UICanvasType.Overlay;

        private float _seconds;
        
        private void Update()
        {
            _seconds+=Time.deltaTime;
            if (_seconds>=0.5f)
            {
                _seconds = 0;
                frameCount.SetField("帧数：", 1/Time.deltaTime);
            }
        }
    }
}