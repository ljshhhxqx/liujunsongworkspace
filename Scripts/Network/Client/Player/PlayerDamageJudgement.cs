using HotUpdate.Scripts.Config;
using HotUpdate.Scripts.Network.NetworkMes;
using HotUpdate.Scripts.Network.Server.Sync;
using HotUpdate.Scripts.Tool.Message;
using Mirror;
using Network.NetworkMes;
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
            };
            _messageHandler.SendToServer(new MirrorPlayerAttackHitMessage(attackData, _frameSyncManager.GetCurrentFrame()));
        }
    }
}