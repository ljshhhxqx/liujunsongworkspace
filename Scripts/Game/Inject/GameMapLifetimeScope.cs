using HotUpdate.Scripts.Audio;
using HotUpdate.Scripts.Buff;
using HotUpdate.Scripts.Collector;
using HotUpdate.Scripts.Game.Map;
using HotUpdate.Scripts.Weather;
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
            builder.Register<MapBoundDefiner>(Lifetime.Singleton);
            //builder.Register<BuffManager>();
            //builder.Register<ItemsSpawnerManager>();
            builder.RegisterComponentInHierarchy<GameMapInit>();
            Debug.Log("GameMapLifetimeScope Configured!!!");
        }
    }
}