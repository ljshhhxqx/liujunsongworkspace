using Common;
using Mirror;
using Model;
using Network.Server;
using Tool.Coroutine;
using Tool.GameEvent;
using Tool.Message;
using Tool.ObjectPool;
using UnityEngine;
using VContainer;

namespace Network.Client
{
    public abstract class ClientBase : NetworkBehaviour
    {
        protected PlayerGameModel playerGameModel;
        protected GameEventManager gameEventManager;
        protected IConfigProvider configProvider;
        protected GameCommonVariant gameCommonVariant;
        protected RepeatedTask repeatedTask;
        protected MessageCenter messageCenter;
        
        [Inject]
        protected virtual void Init(PlayersGameModelManager playersGameModelManager,
         GameEventManager gameEventManager, IConfigProvider configProvider, GameCommonVariant gameCommonVariant, RepeatedTask repeatedTask, MessageCenter messageCenter)
        {
            this.gameEventManager = gameEventManager;
            this.configProvider = configProvider;
            this.gameCommonVariant = gameCommonVariant;
            this.repeatedTask = repeatedTask;
            this.messageCenter = messageCenter;
            //playerGameModel = playersGameModelManager.GetPlayerModel(connectionToClient.connectionId);
            InitCallback();
        }

        [Client]
        public override void OnStartClient()
        {
            ObjectInjectProvider.Instance.Inject(this);
        }

        protected abstract void InitCallback();
    }
}