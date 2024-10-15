using AOTScripts.Tool.ECS;
using HotUpdate.Scripts.Config;
using HotUpdate.Scripts.Network.Client.Player;
using HotUpdate.Scripts.Network.Server.InGame;
using Mirror;
using Network.NetworkMes;
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
        private PlayerInGameManager _playerDataManager;
        private BuffDatabase _buffDatabase;

        [Inject]
        private void Init(IConfigProvider configProvider, PlayerInGameManager playerDataManager)
        {
            _buffDatabase = configProvider.GetConfig<BuffDatabase>();
            _playerDataManager = playerDataManager;
            NetworkClient.RegisterHandler<GameStartMessage>(OnGameReady);
            Debug.Log("BuffManager init");
        }

        private void OnGameReady(GameStartMessage gameReadyEvent)
        {
            _isOn = true;
        }
        
        public void AddBuffToPlayer(PlayerPropertyComponent targetStats, PropertyTypeEnum propertyTypeEnum)
        {
            var buff = _buffDatabase.GetRandomBuffType(propertyTypeEnum);
            AddBuff(targetStats, buff);
            //AddBuff(targetStats, buffType);
        }

        private void AddBuff(PlayerPropertyComponent targetStats, BuffType buffType)
        {
            var buffTypeData = _buffDatabase.GetBuffData(buffType);
            if (buffTypeData.HasValue)
            {
                var newBuff = new BuffBase(buffTypeData.Value.buffType, buffTypeData.Value.propertyTypeEnum, buffTypeData.Value.duration, buffTypeData.Value.effectStrength, targetStats.ConnectionID);
                ApplyBuff(newBuff, targetStats);
                _activeBuffs.Add(newBuff);
            }
            else
            {
                var randomBuffData = _buffDatabase.GetRandomBuffData(buffType);
                if (randomBuffData.HasValue)
                {
                    var newRandomBuff = new RandomBuff(randomBuffData.Value.buffType, randomBuffData.Value.propertyTypeEnum, randomBuffData.Value.durationRange, randomBuffData.Value.effectStrengthRange, targetStats.ConnectionID);
                    ApplyBuff(newRandomBuff, targetStats);
                    _randomBuffs.Add(newRandomBuff);
                }
            }
        }

        private void ApplyBuff(IBuff buff, PlayerPropertyComponent targetStats)
        {
            targetStats.IncreaseProperty(buff.PropertyTypeEnum, buff.EffectStrength);
        }

        private void RemoveBuff(IBuff buff, PlayerPropertyComponent targetStats)
        { 
            targetStats.IncreaseProperty(buff.PropertyTypeEnum, -buff.EffectStrength);
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
                    var targetStats = _playerDataManager.GetPlayer(buff.TargetPlayerId);
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
                    var targetStats = _playerDataManager.GetPlayer(randomBuff.TargetPlayerId);
                    RemoveBuff(randomBuff, targetStats.PlayerProperty);
                    _randomBuffs.RemoveAt(i);
                }
            }   
        }

        private void OnDestroy()
        {
            _isOn = false;
        }
    }
}