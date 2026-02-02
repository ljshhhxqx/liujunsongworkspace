using System.Collections.Generic;
using AOTScripts.Tool.ECS;
using HotUpdate.Scripts.Config;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Network.Server.InGame;
using HotUpdate.Scripts.Tool.GameEvent;
using HotUpdate.Scripts.Tool.Message;
using UnityEngine;
using VContainer;
using PropertyCalculator = AOTScripts.Data.PropertyCalculator;

namespace HotUpdate.Scripts.Buff
{
    public class BuffManager
    {
        private readonly List<BuffManagerData> _activeBuffs = new List<BuffManagerData>();
        private MessageCenter _messageCenter;
        private ConstantBuffConfig _constantBuffConfig;
        private RandomBuffConfig _randomBuffConfig;
        private GameEventManager _gameEventManager;

        [Inject]
        private void Init(IConfigProvider configProvider, MessageCenter messageCenter, GameEventManager gameEventManager)
        {
            _constantBuffConfig = configProvider.GetConfig<ConstantBuffConfig>();
            _randomBuffConfig = configProvider.GetConfig<RandomBuffConfig>();
            _gameEventManager = gameEventManager;
            _messageCenter = messageCenter;//
            Debug.Log("BuffManager init");
        }

        public PropertyCalculator AddBuffToPlayer(PropertyCalculator target, int connectionId, BuffExtraData buffExtraData, CollectObjectBuffSize size,
            int? casterId = null)
        {
            var buff = buffExtraData.buffType == BuffType.Constant ? _constantBuffConfig.GetBuff(buffExtraData) : _randomBuffConfig.GetBuff(buffExtraData);
            return AddBuff(target, connectionId, buff, size, casterId);
        }
        
        private PropertyCalculator AddBuff(PropertyCalculator target, int connectionId, BuffData buffData, CollectObjectBuffSize size, int? casterId = null)
        {
            var newBuff = new BuffBase(buffData, connectionId, casterId);
            var buffManagerData = new BuffManagerData
            {
                BuffData = newBuff,
                Size = size
            };
            _activeBuffs.Add(buffManagerData);
            return ApplyBuff(newBuff, target);
        }

        private PropertyCalculator ApplyBuff(BuffBase buff, PropertyCalculator target)
        {
            return target.UpdateCalculator(buff.BuffData.increaseDataList);
        }

        private void Update()
        {
            for (var i = _activeBuffs.Count - 1; i >= 0; i--)
            {
                _activeBuffs[i] = _activeBuffs[i].Update(Time.deltaTime);
                if (_activeBuffs[i].BuffData.IsExpired())
                {
                    //OnServerBuffRemoved?.Invoke(_activeBuffs[i].BuffData.TargetPlayerId, _activeBuffs[i].BuffData.BuffData.increaseDataList);
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