using System.Collections.Generic;
using HotUpdate.Scripts.Network.Client.Player;
using HotUpdate.Scripts.Network.Server.InGame;
using HotUpdate.Scripts.UI.UIs.Panel.ItemList;
using UI.UIBase;
using UI.UIs.Common;
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
        
        [Inject]
        private void Init(PlayerInGameManager playerInGameManager)
        {
            _playerInGameManager = playerInGameManager;
        }

        public void SetPlayerProperties(PlayerPropertyComponent playerPropertyComponent)
        {
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
    }
}