using Common;
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
        protected UIManager uiManager;
        
        [Inject]
        protected virtual void Init(PlayersGameModelManager playersGameModelManager,
         GameEventManager gameEventManager, IConfigProvider configProvider, RepeatedTask repeatedTask, MessageCenter messageCenter, UIManager uiManager)
        {
            this.gameEventManager = gameEventManager;
            this.configProvider = configProvider;
            this.repeatedTask = repeatedTask;
            this.messageCenter = messageCenter;
            this.uiManager = uiManager;
            //playerGameModel = playersGameModelManager.GetPlayerModel(connectionToClient.connectionId);
            InitCallback();
        }

        public override void OnStartClient()
        {
            ObjectInjectProvider.Instance.Inject(this);
        }

        protected abstract void InitCallback();
    }
}