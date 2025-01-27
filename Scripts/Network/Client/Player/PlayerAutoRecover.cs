// using System;
// using System.Collections.Generic;
// using Mirror;
// using Newtonsoft.Json;
// using UnityEngine;
//
// namespace HotUpdate.Scripts.Network.Client.Player
// {
//     public class PlayerAutoRecover : NetworkBehaviour
//     {
//         private PlayerPropertyComponent _playerPropertyComponent;
//
//         private void Start()
//         {
//             _playerPropertyComponent = GetComponent<PlayerPropertyComponent>();
//         }
//         
//         
//
//     }
//     
//     public interface INetworkCommand
//     {
//         int Tick { get; }
//         void Execute(IGameState state); // 执行命令
//         void Predict(IGameState state); // 预测结果
//     }
//
//     // 游戏状态接口
//     public interface IGameState
//     {
//         void ApplyServerState(IGameState state); // 应用服务器状态
//         bool NeedsReconciliation(IGameState state); // 检查是否需要和解
//         IGameState Clone(); // 用于回滚
//     }
//     
//     public struct NetworkCommand : INetworkCommand
//     {
//         public int Tick { get; }
//         public void Execute(IGameState state)
//         {
//         }
//
//         public void Predict(IGameState state)
//         {
//             
//         }
//
//         public NetworkCommand(int tick)
//         {
//             Tick = tick;
//         }
//     }
//     
//     public abstract class NetworkStateManager<TState> : NetworkBehaviour 
//         where TState : class, IGameState, new()
//     {
//         protected int _currentTick;
//         protected float _tickTimer;
//         [SerializeField] protected float _tickRate = 1/30f; // 20hz
//         
//         protected Queue<INetworkCommand> _pendingCommands = new Queue<INetworkCommand>();
//         protected TState _currentState;
//         protected TState _lastServerState;
//
//         // 命令执行事件
//         public event Action<INetworkCommand> OnCommandExecuted;
//
//         protected virtual void Start()
//         {
//             _currentState = new TState();
//             _lastServerState = new TState();
//         }
//
//         protected virtual void Update()
//         {
//             if (!isLocalPlayer) return;
//
//             _tickTimer += Time.deltaTime;
//             while (_tickTimer >= _tickRate)
//             {
//                 _tickTimer -= _tickRate;
//                 _currentTick++;
//                 ProcessTick();
//             }
//         }
//
//         protected virtual void ProcessTick()
//         {
//             if (!isLocalPlayer) return;
//             OnCommandExecuted?.Invoke(new NetworkCommand(_currentTick));
//         }
//
//         public virtual void ExecuteCommand(INetworkCommand command)
//         {
//             if (!isLocalPlayer) return;
//
//             // 本地预测
//             command.Predict(_currentState);
//
//             // 记录命令
//             _pendingCommands.Enqueue(command);
//
//             // 发送到服务
//             CmdExecuteCommand(JsonConvert.SerializeObject(command));
//         }
//         [Command]
//         protected virtual void CmdExecuteCommand(string commandJson)
//         {
//             // 服务器执行命令
//             var command = JsonUtility.FromJson<INetworkCommand>(commandJson);
//             command.Execute(_currentState);
//             
//             // 广播新状态给所有客户端
//             RpcUpdateState(JsonConvert.SerializeObject(_currentState), command.Tick);
//         }
//
//         [ClientRpc]
//         protected virtual void RpcUpdateState(string stateJson, int serverTick)
//         {
//             var serverState = JsonConvert.DeserializeObject<TState>(stateJson);
//             
//             if (isLocalPlayer)
//             {
//                 _lastServerState = serverState;
//                 ReconcileState(serverTick);
//             }
//             else
//             {
//                 // 非本地玩家直接应用服务器状态
//                 _currentState.ApplyServerState(serverState);
//             }
//         }
//
//         protected virtual void ReconcileState(int serverTick)
//         {
//             if (!isLocalPlayer) return;
//
//             // 清除已确认的命令
//             while (_pendingCommands.Count > 0 && 
//                    _pendingCommands.Peek().Tick <= serverTick)
//             {
//                 _pendingCommands.Dequeue();
//             }
//
//             // 检查是否需要和解
//             if (_currentState.NeedsReconciliation(_lastServerState))
//             {
//                 Debug.Log("State reconciliation needed");
//                 
//                 // 回滚到服务器状态
//                 _currentState.ApplyServerState(_lastServerState);
//                 
//                 // 重新应用未确认的命令
//                 foreach (var cmd in _pendingCommands)
//                 {
//                     cmd.Predict(_currentState);
//                 }
//             }
//         }
//
//         // 获取当前状态
//         public virtual TState GetCurrentState()
//         {
//             return _currentState;
//         }
//
//         // 调试信息
//         protected virtual void OnGUI()
//         {
//             if (!isLocalPlayer) return;
//
//             GUI.Label(new Rect(10, 10, 200, 20), 
//                 $"Tick: {_currentTick}, Pending Commands: {_pendingCommands.Count}");
//         }
//     }
//     // 属性变化命令
//     public class PropertyChangeCommand : INetworkCommand
//     {
//         public int Tick { get; private set; }
//         public PropertyTypeEnum PropertyType { get; private set; }
//         public float ChangeAmount { get; private set; }
//
//         public PropertyChangeCommand(int tick, PropertyTypeEnum type, float amount)
//         {
//             Tick = tick;
//             PropertyType = type;
//             ChangeAmount = amount;
//         }
//
//         public void Execute(IGameState state)
//         {
//             var propertyState = state as PropertyState;
//             propertyState?.ChangeProperty(PropertyType, ChangeAmount);
//         }
//
//         public void Predict(IGameState state)
//         {
//             Execute(state);
//         }
//     }
//
// // 属性状态
//     public class PropertyState : IGameState
//     {
//         private Dictionary<PropertyTypeEnum, float> _properties 
//             = new Dictionary<PropertyTypeEnum, float>();
//
//         public void ChangeProperty(PropertyTypeEnum type, float amount)
//         {
//             if (!_properties.ContainsKey(type))
//                 _properties[type] = 0;
//
//             _properties[type] += amount;
//         }
//
//         public void ApplyServerState(IGameState state)
//         {
//             var serverState = state as PropertyState;
//             if (serverState != null)
//             {
//                 _properties = new Dictionary<PropertyTypeEnum, float>(serverState._properties);
//             }
//         }
//
//         public bool NeedsReconciliation(IGameState state)
//         {
//             var serverState = state as PropertyState;
//             if (serverState == null) return false;
//
//             foreach (var kvp in _properties)
//             {
//                 if (!serverState._properties.ContainsKey(kvp.Key)) return true;
//                 if (Mathf.Abs(kvp.Value - serverState._properties[kvp.Key]) > 0.1f)
//                     return true;
//             }
//             return false;
//         }
//
//         public IGameState Clone()
//         {
//             var clone = new PropertyState();
//             clone._properties = new Dictionary<PropertyTypeEnum, float>(_properties);
//             return clone;
//         }
//     }
//         // 属性状态管理器
// public class PropertyStateManager : NetworkStateManager<PropertyState>
// {
//     [Header("Property Settings")]
//     [SerializeField] private float _strengthRecoveryRate = 5f;
//     [SerializeField] private float _healthRecoveryRate = 2f;
//     
//     // 属性配置
//     private Dictionary<PropertyTypeEnum, PropertyConfig> _propertyConfigs 
//         = new Dictionary<PropertyTypeEnum, PropertyConfig>();
//
//     protected override void Start()
//     {
//         base.Start();
//         InitializePropertyConfigs();
//     }
//
//     private void InitializePropertyConfigs()
//     {
//         // 初始化属性配置
//         // _propertyConfigs[PropertyTypeEnum.Strength] = new PropertyConfig
//         // {
//         //     baseValue = 100f,
//         //     minValue = 0f,
//         //     maxValue = 100f,
//         //     recoveryRate = _strengthRecoveryRate,
//         //     autoRecover = true
//         // };
//         //
//         // _propertyConfigs[PropertyTypeEnum.Health] = new PropertyConfig
//         // {
//         //     baseValue = 100f,
//         //     minValue = 0f,
//         //     maxValue = 100f,
//         //     recoveryRate = _healthRecoveryRate,
//         //     autoRecover = true
//         // };
//
//         // 初始化状态
//         if (isServer)
//         {
//             foreach (var kvp in _propertyConfigs)
//             {
//                 _currentState.SetProperty(kvp.Key, kvp.Value.baseValue);
//             }
//         }
//     }
//
//     // 每个tick的处理逻辑
//     protected override void ProcessTick()
//     {
//         base.ProcessTick();
//         if (!isLocalPlayer) return;
//
//         // 处理自动恢复
//         foreach (var kvp in _propertyConfigs)
//         {
//             if (!kvp.Value.autoRecover) continue;
//
//             float currentValue = _currentState.GetPropertyValue(kvp.Key);
//             if (currentValue < kvp.Value.maxValue)
//             {
//                 float recoveryAmount = kvp.Value.recoveryRate * _tickRate;
//                 var cmd = new PropertyChangeCommand(
//                     _currentTick,
//                     kvp.Key,
//                     recoveryAmount
//                 );
//                 ExecuteCommand(cmd);
//             }
//         }
//     }
//
//     // 公共接口：修改属性
//     public void ModifyProperty(PropertyTypeEnum type, float amount)
//     {
//         if (!isLocalPlayer) return;
//         if (!_propertyConfigs.TryGetValue(type, out var config)) return;
//
//         float currentValue = _currentState.GetPropertyValue(type);
//         float newValue = Mathf.Clamp(
//             currentValue + amount,
//             config.minValue,
//             config.maxValue
//         );
//
//         var cmd = new PropertyChangeCommand(
//             _currentTick,
//             type,
//             newValue - currentValue
//         );
//         ExecuteCommand(cmd);
//     }
//
//     // 获取属性值
//     public float GetPropertyValue(PropertyTypeEnum type)
//     {
//         return _currentState.GetPropertyValue(type);
//     }
//
//     // 调试显示
//     protected override void OnGUI()
//     {
//         base.OnGUI();
//         if (!isLocalPlayer) return;
//
//         int y = 50;
//         foreach (var kvp in _propertyConfigs)
//         {
//             GUI.Label(new Rect(10, y, 200, 20),
//                 $"{kvp.Key}: {GetPropertyValue(kvp.Key):F2}/{kvp.Value.maxValue}");
//             y += 25;
//         }
//     }
// }
// }