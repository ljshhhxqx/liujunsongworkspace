using System;
using AOTScripts.Data;
using AOTScripts.Data.NetworkMes;
using HotUpdate.Scripts.Audio;
using HotUpdate.Scripts.Collector;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Game.Map;
using HotUpdate.Scripts.Network.PredictSystem.Interact;
using HotUpdate.Scripts.Network.PredictSystem.SyncSystem;
using HotUpdate.Scripts.Network.Server;
using HotUpdate.Scripts.Network.Server.InGame;
using HotUpdate.Scripts.Network.Server.Sync;
using HotUpdate.Scripts.Tool.ObjectPool;
using HotUpdate.Scripts.Weather;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace HotUpdate.Scripts.Game.Inject
{
    public class GameMapLifetimeScope : LifetimeScope, IMapLifeScope
    {
        [SerializeField]
        private MapType _mapType;
        
        protected override void Configure(IContainerBuilder builder)
        {
            RegisterComponent<MirrorNetworkMessageHandler>(builder);
            RegisterComponent<NetworkManagerCustom>(builder);
            RegisterComponent<GameLoopController>(builder);
            RegisterComponent<GameAudioManager>(builder);
            RegisterComponent<ItemsSpawnerManager>(builder);
            RegisterComponent<WeatherManager>(builder);
            RegisterComponent<GameMapInit>(builder);
            RegisterComponent<PlayerNotifyManager>(builder);
            RegisterComponent<GameSyncManager>(builder);
            RegisterComponent<InteractSystem>(builder);
            RegisterComponent<NetworkEndHandler>(builder);
            RegisterComponent<PlayerInGameManager>(builder);
            RegisterComponent<NetworkGameObjectPoolManager>(builder);
            builder.Register<GameMapInjector>(Lifetime.Singleton).WithParameter(typeof(LifetimeScope), this);
            Debug.Log("GameMapLifetimeScope Configured!!!");
        }

        private void RegisterComponent<T>(IContainerBuilder builder) where T : Component
        {
            builder.RegisterComponentInHierarchy<T>()
                .AsSelf()
                .AsImplementedInterfaces();
        }

        public MapType GetMapType()
        {
            if (_mapType == MapType.Town)
            {
                var type = Enum.Parse(typeof(MapType), gameObject.scene.name);
                _mapType = (MapType)type;
            }

            return _mapType;
        }
    }

    public interface IMapLifeScope
    {
        public MapType GetMapType();
    }
}