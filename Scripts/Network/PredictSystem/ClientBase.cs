using HotUpdate.Scripts.Network.Server.Sync;
using HotUpdate.Scripts.Tool.Coroutine;
using HotUpdate.Scripts.Tool.GameEvent;
using HotUpdate.Scripts.Tool.Message;
using HotUpdate.Scripts.UI.UIBase;
using Mirror;
using Model;
using Network.Server;
using Tool.GameEvent;
using Tool.Message;
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
        protected MessageCenter messageCenter;
        protected FrameSyncManager frameSyncManager;
        protected UIManager uiManager;
        
        [Inject]
        protected virtual void Init(PlayersGameModelManager playersGameModelManager,
         GameEventManager gameEventManager, IConfigProvider configProvider, MessageCenter messageCenter, UIManager uiManager,
         FrameSyncManager frameSyncManager)
        {
            this.gameEventManager = gameEventManager;
            this.configProvider = configProvider;
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