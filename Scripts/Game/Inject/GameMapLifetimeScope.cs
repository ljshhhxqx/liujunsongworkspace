using HotUpdate.Scripts.Audio;
using HotUpdate.Scripts.Buff;
using HotUpdate.Scripts.Collector;
using HotUpdate.Scripts.Game.Map;
using HotUpdate.Scripts.Network.Server.InGame;
using HotUpdate.Scripts.Weather;
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
            //builder.Register<NetworkAudioManager>();
            //builder.Register<WeatherManager>();
            builder.RegisterComponentInHierarchy<MapBoundDefiner>();
            builder.RegisterComponentInHierarchy<NetworkManagerCustom>();
            builder.RegisterComponentInHierarchy<PlayerInGameManager>();
            //builder.Register<BuffManager>();
            //builder.Register<ItemsSpawnerManager>();
            builder.RegisterComponentInHierarchy<GameLoopController>();
            builder.RegisterComponentInHierarchy<NetworkAudioManager>();
            builder.RegisterComponentInHierarchy<BuffManager>();
            builder.RegisterComponentInHierarchy<ItemsSpawnerManager>();
            builder.RegisterComponentInHierarchy<WeatherManager>();
            builder.RegisterComponentInHierarchy<GameMapInit>();
            Debug.Log("GameMapLifetimeScope Configured!!!");
        }
    }
}