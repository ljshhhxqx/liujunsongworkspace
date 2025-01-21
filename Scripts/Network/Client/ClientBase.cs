using HotUpdate.Scripts.Network.Server.Sync;
using HotUpdate.Scripts.Tool.Message;
using Mirror;
using Model;
using Network.Server;
using Tool.Coroutine;
using Tool.GameEvent;
using Tool.Message;
using Tool.ObjectPool;
using UI.UIBase;
using UnityEngine;
using VContainer;

namespace Network.Client
{
    public abstract class ClientBase : NetworkBehaviour
    {
        protected PlayerGameModel playerGameModel;
        protected GameEventManager gameEventManager;
        protected IConfigProvider configProvider;
        protected RepeatedTask repeatedTask;
        protected MessageCenter messageCenter;
        protected FrameSyncManager frameSyncManager;
        protected UIManager uiManager;
        
        [Inject]
        protected virtual void Init(PlayersGameModelManager playersGameModelManager,
         GameEventManager gameEventManager, IConfigProvider configProvider, RepeatedTask repeatedTask, MessageCenter messageCenter, UIManager uiManager,
         FrameSyncManager frameSyncManager)
        {
            this.gameEventManager = gameEventManager;
            this.configProvider = configProvider;
            this.repeatedTask = repeatedTask;
            this.messageCenter = messageCenter;
            this.uiManager = uiManager;
            this.frameSyncManager = frameSyncManager;
            //playerGameModel = playersGameModelManager.GetPlayerModel(connectionToClient.connectionId);
            InitCallback();
        }

        public override void OnStartClient()
        {
        }

        protected abstract void InitCallback();
    }
}