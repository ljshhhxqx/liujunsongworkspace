using System.Collections.Generic;
using System.Linq;
using HotUpdate.Scripts.Network.PredictSystem.Data;
using HotUpdate.Scripts.Network.PredictSystem.PredictableState;
using HotUpdate.Scripts.Network.PredictSystem.State;
using HotUpdate.Scripts.Tool.ObjectPool;
using MemoryPack;
using Mirror;

namespace HotUpdate.Scripts.Network.PredictSystem.SyncSystem
{
    public class PlayerItemSyncSystem : BaseSyncSystem
    {
        private readonly Dictionary<int, PlayerItemSyncState> _playerItemSyncStates = new Dictionary<int, PlayerItemSyncState>();
        
        protected override void OnClientProcessStateUpdate(byte[] state)
        {
            var playerStates = MemoryPackSerializer.Deserialize<Dictionary<int, PlayerItemState>>(state);
            foreach (var playerState in playerStates)
            {
                if (!PropertyStates.ContainsKey(playerState.Key))
                {
                    continue;
                }
                PropertyStates[playerState.Key] = playerState.Value;
            }
        }

        protected override void RegisterState(int connectionId, NetworkIdentity player)
        {
            var playerPredictableState = player.GetComponent<PlayerItemSyncState>();
            var playerItemState = new PlayerItemState();
            var items = ObjectPool<List<PlayerItem>>.Get();
            ModifyPlayerItems(items);
            playerItemState.PlayerItems = items.ToDictionary(x => x.ItemId, x => x);
            PropertyStates.Add(connectionId, playerItemState);
            _playerItemSyncStates.Add(connectionId, playerPredictableState);
        }

        private void ModifyPlayerItems(List<PlayerItem> playerItems)
        {
            
        }

        public override CommandType HandledCommandType => CommandType.Item;
        public override ISyncPropertyState ProcessCommand(INetworkCommand command)
        {
            switch (command)
            {
                case ItemUseCommand itemUseCommand:
                    break;
                case ItemEquipCommand itemEquipCommand:
                    break;
                case ItemLockCommand itemLockCommand:
                    break;
                case ItemDropCommand itemDropCommand:
                    break;
            }

            return null;
        }

        public override void SetState<T>(int connectionId, T state)
        {
            var playerPredictableState = _playerItemSyncStates[connectionId];
            playerPredictableState.ApplyServerState(state);
        }

        public override bool HasStateChanged(ISyncPropertyState oldState, ISyncPropertyState newState)
        {
            return false;
        }

        public override void Clear()
        {
            base.Clear();
            _playerItemSyncStates.Clear();
        }
    }
}