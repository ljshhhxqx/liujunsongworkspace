﻿using HotUpdate.Scripts.Audio;
using HotUpdate.Scripts.Buff;
using HotUpdate.Scripts.Collector;
using HotUpdate.Scripts.Game.Map;
using HotUpdate.Scripts.Network.NetworkMes;
using HotUpdate.Scripts.Network.Server.InGame;
using HotUpdate.Scripts.Network.Server.Sync;
using HotUpdate.Scripts.Weather;
using Mirror;
using Network.Server;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace HotUpdate.Scripts.Game.Inject
{
    public class GameMapLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            RegisterComponent<MirrorNetworkMessageHandler>(builder);
            RegisterComponent<MapBoundDefiner>(builder);
            RegisterComponent<NetworkManagerCustom>(builder);
            RegisterComponent<GameLoopController>(builder);
            RegisterComponent<NetworkAudioManager>(builder);
            RegisterComponent<BuffManager>(builder);
            RegisterComponent<ItemsSpawnerManager>(builder);
            RegisterComponent<WeatherManager>(builder);
            RegisterComponent<GameMapInit>(builder);
            RegisterComponent<FrameSyncManager>(builder);
            RegisterComponent<PlayerNotifyManager>(builder);
            builder.Register<GameMapInjector>(Lifetime.Singleton).WithParameter(typeof(LifetimeScope), this);
            Debug.Log("GameMapLifetimeScope Configured!!!");
        }

        private void RegisterComponent<T>(IContainerBuilder builder) where T : Component
        {
            // var t = gameSingletonParent.GetComponentInChildren<T>(true);
            // if (t == null)
            // {
            //     var go = Instantiate(new GameObject(typeof(T).Name), gameSingletonParent.transform);
            //     t = go.AddComponent<T>();
            // }
            //
            // if (t is NetworkBehaviour)
            // {
            //     if (!t.gameObject.TryGetComponent<NetworkIdentity>(out _))
            //     {
            //         t.gameObject.AddComponent<NetworkIdentity>();
            //     }
            // }
            builder.RegisterComponentInHierarchy<T>()
                .AsSelf()
                .AsImplementedInterfaces();
        }
    }
}