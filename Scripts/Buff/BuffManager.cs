using AOTScripts.Tool.ECS;
using HotUpdate.Scripts.Config;
using HotUpdate.Scripts.Network.Client.Player;
using HotUpdate.Scripts.Network.Server.InGame;
using Mirror;
using Tool.GameEvent;
using UnityEngine;
using VContainer;

namespace HotUpdate.Scripts.Buff
{
    public class BuffManager : ServerNetworkComponent
    {
        [SyncVar]
        private bool _isOn;
        private readonly SyncList<BuffBase> _activeBuffs = new SyncList<BuffBase>();
        private readonly SyncList<RandomBuff> _randomBuffs = new SyncList<RandomBuff>();
        private readonly PlayerInGameManager _playerInGameManager;
        private BuffDatabase _buffDatabase;

        [Inject]
        private void Init(IConfigProvider configProvider, GameEventManager gameEventManager, PlayerInGameManager playerInGameManager)
        {
            _buffDatabase = configProvider.GetConfig<BuffDatabase>();
            gameEventManager.Subscribe<GameReadyEvent>(OnGameReady);
            Debug.Log("BuffManager init");
        }

        private void OnGameReady(GameReadyEvent gameReadyEvent)
        {
            CmdSwitchBuffManager(true);
        }

        [Command]
        private void CmdSwitchBuffManager(bool isOn)
        {
            _isOn = isOn;
        }
        
        [Command]
        public void CmdAddBuff(PlayerPropertyComponent targetStats, BuffType buffType)
        {
            AddBuff(targetStats, buffType);
        }

        [ClientRpc]
        private void AddBuff(PlayerPropertyComponent targetStats, BuffType buffType)
        {
            var buffData = _buffDatabase.GetBuffData(buffType);
            if (buffData.HasValue)
            {
                var newBuff = new BuffBase(buffData.Value.buffType, buffData.Value.propertyTypeEnum, buffData.Value.duration, buffData.Value.effectStrength, targetStats.PlayerId);
                ApplyBuff(newBuff, targetStats);
                _activeBuffs.Add(newBuff);
            }
            else
            {
                var randomBuffData = _buffDatabase.GetRandomBuffData(buffType);
                if (randomBuffData.HasValue)
                {
                    var newRandomBuff = new RandomBuff(randomBuffData.Value.buffType, randomBuffData.Value.propertyTypeEnum, randomBuffData.Value.durationRange, randomBuffData.Value.effectStrengthRange, targetStats.PlayerId);
                    ApplyBuff(newRandomBuff, targetStats);
                    _randomBuffs.Add(newRandomBuff);
                }
            }
        }

        private void ApplyBuff(IBuff buff, PlayerPropertyComponent targetStats)
        {
            targetStats.ModifyProperty(buff.PropertyTypeEnum, buff.EffectStrength);
        }

        private void RemoveBuff(IBuff buff, PlayerPropertyComponent targetStats)
        { 
            targetStats.RevertProperty(buff.PropertyTypeEnum, buff.EffectStrength);
        }

        private void Update()
        {
            if (!_isOn)
            {
                return;
            }
            for (var i = _activeBuffs.Count - 1; i >= 0; i--)
            {
                var buff = _activeBuffs[i];
                buff.Update(Time.deltaTime);
                if (buff.IsExpired())
                {
                    var targetStats = _playerInGameManager.GetPlayer(buff.TargetPlayerId);
                    RemoveBuff(buff, targetStats.PlayerProperty);
                    _activeBuffs.RemoveAt(i);
                }
            }
            
            for (var i = _randomBuffs.Count - 1; i >= 0; i--)
            {
                var randomBuff = _randomBuffs[i];
                randomBuff.Update(Time.deltaTime);
                if (randomBuff.IsExpired())
                {
                    var targetStats = _playerInGameManager.GetPlayer(randomBuff.TargetPlayerId);
                    RemoveBuff(randomBuff, targetStats.PlayerProperty);
                    _randomBuffs.RemoveAt(i);
                }
            }   
        }

        private void OnDestroy()
        {
            CmdSwitchBuffManager(false);
        }
    }
}