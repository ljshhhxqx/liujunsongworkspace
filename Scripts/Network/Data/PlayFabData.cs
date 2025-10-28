using AOTScripts.Data;
using HotUpdate.Scripts.Tool.ReactiveProperty;
using PlayFab.CloudScriptModels;
using UniRx;

namespace HotUpdate.Scripts.Network.Data
{
    public static class PlayFabData
    {
        public static HReactiveProperty<string> PlayFabId { get; private set; }  = new HReactiveProperty<string>();
        public static HReactiveProperty<bool> IsLoggedIn { get; private set; } = new HReactiveProperty<bool>();
        public static HReactiveProperty<PlayerInternalData> PlayerInternalData { get; private set; } = new HReactiveProperty<PlayerInternalData>();
        public static HReactiveProperty<PlayerReadOnlyData> PlayerReadOnlyData { get; private set; } = new HReactiveProperty<PlayerReadOnlyData>();
        public static HReactiveProperty<bool> IsDevelopMode { get; private set; }    = new HReactiveProperty<bool>();
        public static HReactiveProperty<EntityKey> EntityKey { get; private set; } = new HReactiveProperty<EntityKey>();
        public static HReactiveCollection<GamePlayerInfo> PlayerList { get; private set; } = new HReactiveCollection<GamePlayerInfo>(); 
        public static HReactiveProperty<string> ConnectionAddress { get; private set; } = new HReactiveProperty<string>();
        public static HReactiveProperty<int> ConnectionPort { get; private set; } = new HReactiveProperty<int>();
        public static HReactiveProperty<string> CurrentGameId { get; private set; } = new HReactiveProperty<string>();

        public static void Initialize()
        {
            PlayFabId ??= new HReactiveProperty<string>();
            IsLoggedIn ??= new HReactiveProperty<bool>();
            PlayerInternalData ??= new HReactiveProperty<PlayerInternalData>();
            PlayerReadOnlyData ??= new HReactiveProperty<PlayerReadOnlyData>();
            IsDevelopMode ??= new HReactiveProperty<bool>();
            EntityKey ??= new HReactiveProperty<EntityKey>();
            ConnectionAddress ??= new HReactiveProperty<string>();
            ConnectionPort ??= new HReactiveProperty<int>();
            PlayerList ??= new HReactiveCollection<GamePlayerInfo>();
        }
        
        public static void Dispose()
        {
            PlayFabId.Dispose();
            IsLoggedIn.Dispose();
            PlayerInternalData.Dispose();
            PlayerReadOnlyData.Dispose();
            IsDevelopMode.Dispose();
            EntityKey.Dispose();
            PlayerList.Clear();
            ConnectionAddress.Dispose();
            ConnectionPort.Dispose();
        }
    }
}