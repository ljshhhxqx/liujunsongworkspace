using System.Collections.Generic;
using AOTScripts.Data;
using Mirror;

namespace HotUpdate.Scripts.Network.Item
{
    public static class GameItemManager
    {
        private static Dictionary<int, GameItemData> _gameItemDatas = new Dictionary<int, GameItemData>();
        private static Dictionary<int, GameChestData> _chestDatas = new Dictionary<int, GameChestData>();

        public static bool HasGameItemData(int gameItemId)
        {
            return _gameItemDatas.ContainsKey(gameItemId);
        }
        
        public static GameItemData GetGameItemData(int itemId)
        {
            return _gameItemDatas.GetValueOrDefault(itemId);
            
        }

        public static void RemoveGameItemData(int itemId, NetworkIdentity networkIdentity)
        {
            if (networkIdentity.isServer)
                _gameItemDatas.Remove(itemId);
        }

        public static GameChestData GetChestData(int chestId)
        {
            return _chestDatas.GetValueOrDefault(chestId);
        }

        public static void AddChestData(GameChestData chestData, NetworkIdentity networkIdentity)
        {
            if (networkIdentity.isServer)
            {
                _chestDatas.TryAdd(chestData.ChestId, chestData);
            }
        }

        public static void RemoveChestData(int chestId, NetworkIdentity networkIdentity)
        {
            if (networkIdentity.isServer)
                _chestDatas.Remove(chestId);
        }

        public static void AddItemData(GameItemData gameItemConfigData, NetworkIdentity networkIdentity)
        {
            if (networkIdentity.isServer)
            {
                _gameItemDatas.TryAdd(gameItemConfigData.ItemId, gameItemConfigData);
            }
        }
    }
}