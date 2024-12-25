using System;
using System.Linq;
using HotUpdate.Scripts.Config;
using HotUpdate.Scripts.Network.NetworkMes;
using HotUpdate.Scripts.Network.Server.Sync;
using HotUpdate.Scripts.Tool.Message;
using Mirror;
using Network.NetworkMes;
using Tool.Message;
using VContainer;

namespace HotUpdate.Scripts.Network.Client.Player
{
    public class PlayerDamageJudgement : NetworkBehaviour
    {
        private PlayerAnimationComponent _animationComponent;
        private PlayerPropertyComponent _playerPropertyComponent;
        private PlayerDataConfig _playerDataConfig;
        private MirrorNetworkMessageHandler _messageHandler;
        private FrameSyncManager _frameSyncManager;

        [Inject]
        private void Init(IConfigProvider configProvider, MirrorNetworkMessageHandler handler, FrameSyncManager frameSyncManager)
        {
            _animationComponent = GetComponent<PlayerAnimationComponent>();
            _playerPropertyComponent = GetComponent<PlayerPropertyComponent>();
            _playerDataConfig = configProvider.GetConfig<PlayerDataConfig>();
            _animationComponent.OnAttackHit += OnAttackHit;
            _frameSyncManager = frameSyncManager;
            _messageHandler = handler;
        }

        public void TakeDamage(DamageResult damageResult)
        {
            if (damageResult.isDead)
            {
                _animationComponent.SetHp(0);
                _animationComponent.SetDeath();
                return;
            }

            if (!(damageResult.damageAmount > 0)) return;
            _animationComponent.SetHp(_playerPropertyComponent.GetPropertyValue(PropertyTypeEnum.Health) - damageResult.damageAmount);
            _animationComponent.SetHit();
        }

        private void OnAttackHit()
        {
            var attackData = new AttackData
            {
                attackerId = netId,
                attackDirection = transform.forward,
                attackOrigin = transform.position,
                attack = _playerPropertyComponent.GetPropertyValue(PropertyTypeEnum.Attack),
                angle = _playerPropertyComponent.PlayerAttackData.attackAngle,
                radius = _playerPropertyComponent.PlayerAttackData.attackRadius,
                minHeight = _playerPropertyComponent.PlayerAttackData.minAttackHeight,
                criticalRate = _playerPropertyComponent.GetPropertyValue(PropertyTypeEnum.CriticalRate),
                criticalDamageRatio = _playerPropertyComponent.GetPropertyValue(PropertyTypeEnum.CriticalDamageRatio),
            };
            _messageHandler.SendToServer(new MirrorPlayerAttackHitMessage(attackData, _frameSyncManager.GetCurrentFrame()));
        }
    }
}