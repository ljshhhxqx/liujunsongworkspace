using System.Collections.Generic;
using AOTScripts.Tool.ECS;
using HotUpdate.Scripts.Config;
using HotUpdate.Scripts.Network.Client.Player;
using HotUpdate.Scripts.Network.Server.InGame;
using HotUpdate.Scripts.Tool.Message;
using UnityEngine;
using VContainer;

namespace HotUpdate.Scripts.Buff
{
    public class BuffManager : ServerNetworkComponent
    {
        private readonly List<BuffBase> _activeBuffs = new List<BuffBase>();
        private PlayerInGameManager _playerDataManager;
        private MessageCenter _messageCenter;
        private BuffDatabase _buffDatabase;

        [Inject]
        private void Init(IConfigProvider configProvider, PlayerInGameManager playerDataManager, MessageCenter messageCenter)
        {
            _buffDatabase = configProvider.GetConfig<BuffDatabase>();
            _playerDataManager = playerDataManager;
            _messageCenter = messageCenter;
            BuffDataReaderWriter.RegisterReaderWriter();
            Debug.Log("BuffManager init");
        }

        public void AddBuffToPlayer(PlayerPropertyComponent targetStats, BuffExtraData buffExtraData, int? casterId = null)
        {
            var buff = _buffDatabase.GetBuff(buffExtraData);
            AddBuff(targetStats, buff, casterId);
        }

        private void AddBuff(PlayerPropertyComponent targetStats, BuffData buffData, int? casterId = null)
        {
            var newBuff = new BuffBase(buffData, targetStats.ConnectionID, casterId);
            ApplyBuff(newBuff, targetStats);
            _activeBuffs.Add(newBuff);
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
                var buff = _activeBuffs[i];
                buff.Update(Time.deltaTime);
                if (buff.IsExpired())
                {
                    var targetStats = _playerDataManager.GetPlayer(buff.TargetPlayerId);
                    RemoveBuff(buff, targetStats.PlayerProperty);
                    _activeBuffs.RemoveAt(i);
                }
            }
        }
    }
}