using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Network.Inject;
using HotUpdate.Scripts.Network.NetworkMes;
using HotUpdate.Scripts.Network.Server.Sync;
using Mirror;
using VContainer;

namespace HotUpdate.Scripts.Network.Client.Player
{
    public class PlayerDamageJudgement : NetworkAutoInjectComponent
    {
        private PlayerAnimationComponent _animationComponent;
        private PlayerPropertyComponent _playerPropertyComponent;
        private MirrorNetworkMessageHandler _messageHandler;
        private FrameSyncManager _frameSyncManager;
        private JsonDataConfig _jsonDataConfig;

        [Inject]
        private void Init(IConfigProvider configProvider, MirrorNetworkMessageHandler handler, FrameSyncManager frameSyncManager)
        {
            _animationComponent = GetComponent<PlayerAnimationComponent>();
            _playerPropertyComponent = GetComponent<PlayerPropertyComponent>();
            _jsonDataConfig = configProvider.GetConfig<JsonDataConfig>();
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
            // var attackData = new AttackData
            // {
            //     attackerId = connectionToClient.connectionId,
            //     attackDirection = transform.forward,
            //     attackOrigin = transform.position,
            //     attack = _playerPropertyComponent.GetPropertyValue(PropertyTypeEnum.Attack),
            //     angle = _playerPropertyComponent.PlayerAttackData.attackAngle,
            //     radius = _playerPropertyComponent.PlayerAttackData.attackRadius,
            //     minHeight = _playerPropertyComponent.PlayerAttackData.minAttackHeight,
            //     criticalRate = _playerPropertyComponent.GetPropertyValue(PropertyTypeEnum.CriticalRate),
            //     criticalDamageRatio = _playerPropertyComponent.GetPropertyValue(PropertyTypeEnum.CriticalDamageRatio),
            // };
            // _messageHandler.SendToServer(new MirrorPlayerAttackHitMessage(attackData, _frameSyncManager.GetCurrentFrame()));
        }
    }
}