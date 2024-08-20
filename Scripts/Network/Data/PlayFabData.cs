using Data;
using PlayFab.CloudScriptModels;
using UniRx;

namespace Network.Data
{
    public static class PlayFabData
    {
        public static ReactiveProperty<string> PlayFabId { get; private set; }  = new ReactiveProperty<string>();
        public static ReactiveProperty<bool> IsLoggedIn { get; private set; } = new ReactiveProperty<bool>();
        public static ReactiveProperty<PlayerInternalData> PlayerInternalData { get; private set; } = new ReactiveProperty<PlayerInternalData>();
        public static ReactiveProperty<PlayerReadOnlyData> PlayerReadOnlyData { get; private set; } = new ReactiveProperty<PlayerReadOnlyData>();
        public static ReactiveProperty<bool> IsDevelopMode { get; private set; }    = new ReactiveProperty<bool>();
        public static ReactiveProperty<EntityKey> EntityKey { get; private set; } = new ReactiveProperty<EntityKey>();

        public static void Initialize()
        {
            PlayFabId ??= new ReactiveProperty<string>();
            IsLoggedIn ??= new ReactiveProperty<bool>();
            PlayerInternalData ??= new ReactiveProperty<PlayerInternalData>();
            PlayerReadOnlyData ??= new ReactiveProperty<PlayerReadOnlyData>();
            IsDevelopMode ??= new ReactiveProperty<bool>();
            EntityKey ??= new ReactiveProperty<EntityKey>();
        }
        
        public static void Dispose()
        {
            PlayFabId.Dispose();
            IsLoggedIn.Dispose();
            PlayerInternalData.Dispose();
            PlayerReadOnlyData.Dispose();
            IsDevelopMode.Dispose();
            EntityKey.Dispose();
        }
    }
}