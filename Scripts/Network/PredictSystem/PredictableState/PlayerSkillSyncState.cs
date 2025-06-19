using System.Collections.Generic;
using AOTScripts.Tool.ObjectPool;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Network.PredictSystem.Data;
using HotUpdate.Scripts.Network.PredictSystem.State;
using HotUpdate.Scripts.Network.PredictSystem.SyncSystem;
using Mirror;
using UnityEngine;
using AnimationState = HotUpdate.Scripts.Config.JsonConfig.AnimationState;

namespace HotUpdate.Scripts.Network.PredictSystem.PredictableState
{
    public class PlayerSkillSyncState : SyncStateBase
    {
        private SkillConfig _skillConfig;
        private SkillConfigData _currentSkillConfigData;
        private readonly Dictionary<string, GameObject> _skillObjects = new Dictionary<string, GameObject>();
        private Transform _spawnTransform;
        protected override ISyncPropertyState CurrentState { get; set; }
        protected override CommandType CommandType => CommandType.Skill;
        protected override void SetState<T>(T state)
        {
            if (state is PlayerSkillState playerSkillState)
            {
                CurrentState = playerSkillState;
                foreach (var key in _skillObjects.Keys)
                {
                    _skillObjects[key].transform.position = playerSkillState.SkillCheckers[key].GetSkillEffectPosition();
                }
            }
        }

        protected override void Init(GameSyncManager gameSyncManager, IConfigProvider configProvider)
        {
            base.Init(gameSyncManager, configProvider);
            _skillConfig = configProvider.GetConfig<SkillConfig>();
            _spawnTransform = GameObject.FindGameObjectWithTag("SpawnedObjects").transform;
        }

        public void ApplyState<T>(T state) where T : ISyncPropertyState
        {
            SetState(state);
        }

        protected override void ProcessCommand(INetworkCommand networkCommand)
        {
            
        }

        public SkillConfigData GetSkillConfigData(AnimationState animationState)
        {
            if (CurrentState is PlayerSkillState playerSkillState)
            {
                var skillChecker = playerSkillState.SkillCheckers[animationState.ToString()];
                var skillConfigData = _skillConfig.GetSkillData(skillChecker.GetCommonSkillCheckerHeader().ConfigId);
                return skillConfigData;
            }
            return default;
        }

        [ClientRpc]
        public void RpcSpawnSkillEffect(int skillConfigId, Vector3 position, string code)
        {
            _currentSkillConfigData = _skillConfig.GetSkillData(skillConfigId);
            var effectName = _currentSkillConfigData.particleName;
            var resource = ResourceManager.Instance.GetResource<GameObject>(effectName);
            var effect = GameObjectPoolManger.Instance.GetObject(resource);
            effect.transform.position = position;
            effect.transform.rotation = Quaternion.identity;
            effect.transform.localScale = Vector3.one;
            effect.transform.parent = _spawnTransform;
            _skillObjects.Add(code, effect);
        }
    }
}