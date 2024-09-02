using Mirror;
using Tool.Message;
using UnityEngine;

namespace Network.Server
{
    public class PlayerControlServer : ServerSystemBase
    {
        private GameDataConfig _gameDataConfig;
        private PlayerDataConfig _playerConfigData;
        private float _lastSprintTime;
        private float _sprintTime;
        private Vector3 _lastPosition;
        private Vector3 _predictPosition;
        
        protected override void InitCallback()
        {
            _gameDataConfig = configProvider.GetConfig<GameDataConfig>();
            _playerConfigData = configProvider.GetConfig<PlayerDataConfig>();
            messageCenter.Register<PlayerMovedMessage>(OnPlayerMoved);
            messageCenter.Register<PlayerRotatedMessage>(OnPlayerRotated);
            messageCenter.Register<PlayerInputMessage>(OnPlayerInput);
            repeatedTask.StartRepeatingTask(AdjustPosition, 1);
        }

        private void OnPlayerInput(PlayerInputMessage message)
        {
            if (message.IsRunning)
            {
                if (_lastSprintTime != 0)
                {
                    _lastSprintTime = Time.time;
                }
                else
                {
                    _sprintTime += Time.time - _lastSprintTime;
                }
            }
        }

        private void OnPlayerMoved(PlayerMovedMessage message)
        {
            var nowSprintTime = _sprintTime;
            _sprintTime = 0;
            _lastSprintTime = 0;
            if (_lastPosition != Vector3.zero)
            {
                _lastPosition = message.PreviousPosition;
                _predictPosition = (_gameDataConfig.GameConfigData.SyncTime - nowSprintTime) * message.Movement * _playerConfigData.PlayerConfigData.MoveSpeed 
                                   + nowSprintTime * message.Movement * _playerConfigData.PlayerConfigData.RunSpeed;
                var isOverPosition = (_predictPosition - transform.position).magnitude > 0.5f;
                _predictPosition = isOverPosition ? _lastPosition : _predictPosition;
                RpcPlayerMoved(_predictPosition);
            }
            else
            {
                _lastPosition = message.PreviousPosition;
            }
        }

        private void OnPlayerRotated(PlayerRotatedMessage message)
        {
            if (!isLocalPlayer)
            {
                RpcPlayerRotated(message.Quaternion);
            }
        }
        
        [ClientRpc]
        private void RpcPlayerRotated(Quaternion quaternion)
        {
            if (isLocalPlayer) return;
            //transform.rotation = Quaternion.Slerp(transform.rotation, quaternion, _gameDataConfig.GameConfigData.SyncTime * _playerConfigData.PlayerConfigData.RotateSpeed);
        }
        
        [ClientRpc]
        private void RpcPlayerMoved(Vector3 position)
        {
            if (isLocalPlayer) return;
            //transform.position = Vector3.Lerp(transform.position, position, _gameDataConfig.GameConfigData.SyncTime * _playerConfigData.PlayerConfigData.MoveSpeed);
        }
        
        [Server]
        private void AdjustPosition()
        {
            if (_predictPosition == Vector3.zero)
            {
                return;
            }
            //transform.position = Vector3.Lerp(transform.position, _predictPosition, _gameDataConfig.GameConfigData.SyncTime * _playerConfigData.PlayerConfigData.MoveSpeed);
        }

        protected override void UpdateCallback()
        {
            
        }

        protected override void DestroyCallback()
        {
        }
    }
}