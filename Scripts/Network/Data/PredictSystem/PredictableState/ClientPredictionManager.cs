using System;
using System.Collections.Generic;
using System.Linq;
using HotUpdate.Scripts.Network.Data.PredictSystem.Data;
using HotUpdate.Scripts.Network.Data.PredictSystem.State;
using Mirror;
using Newtonsoft.Json;
using UnityEngine;
using VContainer;

namespace HotUpdate.Scripts.Network.Data.PredictSystem.PredictableState
{
    public class ClientPredictionManager : MonoBehaviour
    {
        private static int LocalPlayerId => NetworkClient.localPlayer.connectionToClient.connectionId;
        // private readonly Dictionary<CommandType, IPredictableState> _predictStates 
        //     = new Dictionary<CommandType, IPredictableState>();
    
        private readonly Dictionary<CommandType, Queue<INetworkCommand>> _pendingCommands 
            = new Dictionary<CommandType, Queue<INetworkCommand>>();

        [Inject]
        private void Init()
        {
            InitializePredictionStates();
        }
        
        private void InitializePredictionStates()
        {
            var commandTypes = Enum.GetValues(typeof(CommandType));
            foreach (CommandType type in commandTypes)
            {
                //_predictStates[type] = type.GetPredictableState();
                _pendingCommands[type] = new Queue<INetworkCommand>();
            }
        }

        // public void AddPredictedCommand<T>(T command) where T : INetworkCommand
        // {
        //     var header = command.GetHeader();
        //     if (header.connectionId != LocalPlayerId) return;
        //     if (_predictStates.TryGetValue(header.commandType, out var state))
        //     {
        //         state.Simulate(command);
        //         _pendingCommands[header.commandType].Enqueue(command);
        //     }
        // }

        public void SetServerState(IPropertyState stateUpdate)
        {
            
        }

        // public void OnServerStateReceived(NetworkStateUpdate stateUpdate)
        // {
        //     foreach (var stateData in stateUpdate.states)
        //     {
        //         if (_predictStates.TryGetValue(stateData.systemType, out var state))
        //         {
        //             // 解析状态以区分不同玩家
        //             var systemState = JsonConvert.DeserializeObject<BaseSystemState>(stateData.stateJson);
        //         
        //             foreach (var playerState in systemState.playerStates)
        //             {
        //                 if (playerState.playerId == LocalPlayerId)
        //                 {
        //                     // 本地玩家：需要和解
        //                     // if (state.NeedsReconciliation(playerState.stateJson))
        //                     // {
        //                     //     state.ApplyServerState(playerState.stateJson);
        //                     //
        //                     //     // 重新应用未确认的命令
        //                     //     var commands = _pendingCommands[stateData.systemType];
        //                     //     foreach (var cmd in commands.Where(cmd => 
        //                     //                  cmd.GetHeader().tick > stateUpdate.tick))
        //                     //     {
        //                     //         state.Simulate(cmd);
        //                     //     }
        //                     // }
        //             
        //                     // 清理已确认的命令
        //                     CleanupConfirmedCommands(stateData.systemType, stateUpdate.tick);
        //                 }
        //                 else
        //                 {
        //                     // 其他玩家：直接应用服务器状态
        //                     //state.ApplyServerState(playerState.stateJson);
        //                 }
        //             }
        //         }
        //     }
        // }

        private void CleanupConfirmedCommands(CommandType type, int confirmedTick)
        {
            var commands = _pendingCommands[type];
            while (commands.Count > 0 && 
                   commands.Peek().GetHeader().tick <= confirmedTick)
            {
                commands.Dequeue();
            }
        }
    }
}