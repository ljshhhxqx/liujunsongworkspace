using HotUpdate.Scripts.Audio;
using HotUpdate.Scripts.Buff;
using HotUpdate.Scripts.Collector;
using HotUpdate.Scripts.Weather;
using VContainer;
using VContainer.Unity;

namespace HotUpdate.Scripts.Game.Inject
{
    public class GameMapLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.Register<NetworkAudioManager>(Lifetime.Singleton);
            builder.Register<WeatherManager>(Lifetime.Singleton);
            builder.Register<BuffManager>(Lifetime.Singleton);
            builder.Register<ItemsSpawnerManager>(Lifetime.Singleton);
        }
    }
}