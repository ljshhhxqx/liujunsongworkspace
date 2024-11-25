using Common;
using Config;
using Data;
using HotUpdate.Scripts.Audio;
using HotUpdate.Scripts.Collector;
using HotUpdate.Scripts.Game;
using HotUpdate.Scripts.Network.Server.InGame;
using HotUpdate.Scripts.Tool.Message;
using HotUpdate.Scripts.Weather;
using Model;
using Network.Server.Edgegap;
using Network.Server.PlayFab;
using Tool.Coroutine;
using Tool.GameEvent;
using Tool.Message;
using UI.UIBase;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Inject
{
    /// <summary>
    /// 全局生命周期容器
    /// </summary>
    public class GameMainLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterEntryPoint<GameLauncher>();
            builder.Register<ConfigManager>(Lifetime.Singleton);
            //builder.Register<PlayerManager>(Lifetime.Singleton);
            ////builder.Register<GameCommonVariant>(Lifetime.Singleton);
            //builder.Register<CollectItemSpawner>(Lifetime.Singleton);
            builder.Register<UIAudioManager>(Lifetime.Singleton);
            builder.Register<PlayFabAccountManager>(Lifetime.Singleton);
            builder.Register<PlayerDataManager>(Lifetime.Singleton);
            builder.Register<PlayFabRoomManager>(Lifetime.Singleton);
            builder.Register<LocationManager>(Lifetime.Singleton);
            builder.Register<EdgegapManager>(Lifetime.Singleton);
            builder.Register<UIManager>(Lifetime.Singleton);
            builder.Register<PlayersGameModelManager>(Lifetime.Singleton);
            builder.Register<ConfigProvider>(Lifetime.Scoped).AsImplementedInterfaces();
            builder.Register<PlayFabClientCloudScriptCaller>(Lifetime.Scoped).AsImplementedInterfaces();
            builder.Register<GameModelManager>(Lifetime.Scoped).AsImplementedInterfaces();
            builder.Register<DependencyInjectionManager>(Lifetime.Scoped).AsImplementedInterfaces();
            builder.Register<MessageCenter>(Lifetime.Singleton);
            builder.Register<GameEventManager>(Lifetime.Singleton);
            builder.Register<GameSceneManager>(Lifetime.Singleton);
            builder.Register<PlayFabMessageHandler>(Lifetime.Singleton);
            builder.Register<RepeatedTask>(Lifetime.Singleton);
            builder.RegisterComponentInHierarchy<GameOnlineDefine>();
            builder.RegisterComponentInHierarchy<PlayerInGameManager>();
            builder.RegisterComponentInHierarchy<ObjectInjectProvider>();
            //builder.RegisterInstance(this);
            Debug.Log("GameLifetimeScope Configure");
        }
    }
}