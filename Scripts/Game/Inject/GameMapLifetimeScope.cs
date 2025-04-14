using HotUpdate.Scripts.Audio;
using HotUpdate.Scripts.Collector;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Game.Map;
using HotUpdate.Scripts.Network.NetworkMes;
using HotUpdate.Scripts.Network.PredictSystem.Interact;
using HotUpdate.Scripts.Network.PredictSystem.SyncSystem;
using HotUpdate.Scripts.Network.Server;
using HotUpdate.Scripts.Network.Server.Sync;
using HotUpdate.Scripts.Weather;
using Network.Server;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace HotUpdate.Scripts.Game.Inject
{
    public class GameMapLifetimeScope : LifetimeScope, IMapLifeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            RegisterComponent<MirrorNetworkMessageHandler>(builder);
            RegisterComponent<MapBoundDefiner>(builder);
            RegisterComponent<NetworkManagerCustom>(builder);
            RegisterComponent<GameLoopController>(builder);
            RegisterComponent<NetworkAudioManager>(builder);
            RegisterComponent<ItemsSpawnerManager>(builder);
            RegisterComponent<WeatherManager>(builder);
            RegisterComponent<GameMapInit>(builder);
            RegisterComponent<FrameSyncManager>(builder);
            RegisterComponent<PlayerNotifyManager>(builder);
            RegisterComponent<GameSyncManager>(builder);
            RegisterComponent<InteractSystem>(builder);
            builder.Register<GameMapInjector>(Lifetime.Singleton).WithParameter(typeof(LifetimeScope), this);
            Debug.Log("GameMapLifetimeScope Configured!!!");
        }

        private void RegisterComponent<T>(IContainerBuilder builder) where T : Component
        {
            builder.RegisterComponentInHierarchy<T>()
                .AsSelf()
                .AsImplementedInterfaces();
        }

        public MapType MapType => MapType.Town;
    }

    public interface IMapLifeScope
    {
        public MapType MapType { get; }
    }
}