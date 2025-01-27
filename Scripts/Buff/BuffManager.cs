using System.Collections.Generic;
using AOTScripts.Tool.ECS;
using HotUpdate.Scripts.Config;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Network.Client.Player;
using HotUpdate.Scripts.Network.Server.InGame;
using HotUpdate.Scripts.Tool.Message;
using UnityEngine;
using VContainer;

namespace HotUpdate.Scripts.Buff
{
    public class BuffManager : ServerNetworkComponent
    {
        private readonly List<BuffManagerData> _activeBuffs = new List<BuffManagerData>();
        private PlayerInGameManager _playerDataManager;
        private MessageCenter _messageCenter;
        private ConstantBuffConfig _constantBuffConfig;
        private RandomBuffConfig _randomBuffConfig;

        [Inject]
        private void Init(IConfigProvider configProvider, PlayerInGameManager playerDataManager, MessageCenter messageCenter)
        {
            _constantBuffConfig = configProvider.GetConfig<ConstantBuffConfig>();
            _randomBuffConfig = configProvider.GetConfig<RandomBuffConfig>();
            _playerDataManager = playerDataManager;
            _messageCenter = messageCenter;
            BuffDataReaderWriter.RegisterReaderWriter();
            Debug.Log("BuffManager init");
        }

        public void AddBuffToPlayer(PlayerPropertyComponent targetStats, BuffExtraData buffExtraData, CollectObjectBuffSize size, int? casterId = null)
        {
            var buff = buffExtraData.buffType == BuffType.Constant ? _constantBuffConfig.GetBuff(buffExtraData) : _randomBuffConfig.GetBuff(buffExtraData);
            AddBuff(targetStats, buff, size, casterId);
        }

        private void AddBuff(PlayerPropertyComponent targetStats, BuffData buffData, CollectObjectBuffSize size, int? casterId = null)
        {
            var newBuff = new BuffBase(buffData, targetStats.ConnectionID, casterId);
            ApplyBuff(newBuff, targetStats);
            var buffManagerData = new BuffManagerData
            {
                BuffData = newBuff,
                Size = size
            };
            _activeBuffs.Add(buffManagerData);
        }

        private void ApplyBuff(BuffBase buff, PlayerPropertyComponent targetStats)
        {
            targetStats.IncreaseProperty(buff.BuffData.propertyType, buff.BuffData.increaseDataList);
        }

        private void RemoveBuff(BuffBase buff, PlayerPropertyComponent targetStats)
        { 
            for (var i = 0; i < buff.BuffData.increaseDataList.Count; i++)
            {
                if (buff.BuffData.increaseDataList[i].increaseType == BuffIncreaseType.Current)
                    continue;
                targetStats.IncreaseProperty(buff.BuffData.propertyType, buff.BuffData.increaseDataList[i].increaseType,-buff.BuffData.increaseDataList[i].increaseValue);
            }
        }

        private void Update()
        {
            if (!isServer || _activeBuffs.Count == 0)
            {
                return;
            }
            for (var i = _activeBuffs.Count - 1; i >= 0; i--)
            {
                _activeBuffs[i] = _activeBuffs[i].Update(Time.deltaTime);
                if (_activeBuffs[i].BuffData.IsExpired())
                {
                    var targetStats = _playerDataManager.GetPlayer(_activeBuffs[i].BuffData.TargetPlayerId);
                    //RemoveBuff(_activeBuffs[i].BuffData, targetStats.PlayerProperty);
                    _activeBuffs.RemoveAt(i);
                }
            }
        }
        
        private struct BuffManagerData
        {
            public BuffBase BuffData;
            public CollectObjectBuffSize Size;

            public BuffManagerData Update(float deltaTime)
            {
                return new BuffManagerData
                {
                    BuffData = BuffData.Update(deltaTime),
                    Size = Size
                };
            }
        }
    }
}