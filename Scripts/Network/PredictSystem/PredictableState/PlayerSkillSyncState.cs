using System.Collections.Generic;
using AOTScripts.Tool.ObjectPool;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Network.PredictSystem.Calculator;
using HotUpdate.Scripts.Network.PredictSystem.Data;
using HotUpdate.Scripts.Network.PredictSystem.PlayerInput;
using HotUpdate.Scripts.Network.PredictSystem.State;
using HotUpdate.Scripts.Network.PredictSystem.SyncSystem;
using HotUpdate.Scripts.Skill;
using Mirror;
using UnityEngine;
using AnimationState = HotUpdate.Scripts.Config.JsonConfig.AnimationState;

namespace HotUpdate.Scripts.Network.PredictSystem.PredictableState
{
    public class PlayerSkillSyncState : PredictableStateBase
    {
        private SkillConfig _skillConfig;
        private SkillConfigData _currentSkillConfigData;
        private PlayerComponentController _playerComponentController;
        private readonly Dictionary<AnimationState, GameObject> _skillObjects = new Dictionary<AnimationState, GameObject>();
        private Transform _spawnTransform;
        protected override ISyncPropertyState CurrentState { get; set; }
        protected override CommandType CommandType => CommandType.Skill;

        protected override void Init(GameSyncManager gameSyncManager, IConfigProvider configProvider)
        {
            base.Init(gameSyncManager, configProvider);
            _playerComponentController = GetComponent<PlayerComponentController>();
            _skillConfig = configProvider.GetConfig<SkillConfig>();
            _spawnTransform = GameObject.FindGameObjectWithTag("SpawnedObjects").transform;
        }

        public override bool NeedsReconciliation<T>(T state)
        {
            return true;
        }

        public override void Simulate(INetworkCommand command)
        {
            if (CurrentState is not PlayerSkillState playerSkillState || NetworkIdentity.isServer)
            {
                return;
            }
            if (command is SkillLoadCommand skillLoadCommand)
            {
                Debug.Log($"[SkillLoadCommand] Player {NetworkClient.localPlayer.connectionToClient.connectionId} skill {skillLoadCommand.SkillConfigId} start load");
                ISkillChecker checker;
                var skillData = _skillConfig.GetSkillData(skillLoadCommand.SkillConfigId);
                var skillCheckers = _playerComponentController.GetSkillCheckerDict();
                if (!skillLoadCommand.IsLoad)
                {
                    checker = skillCheckers[skillLoadCommand.KeyCode];
                    var skillCommonHeader = checker.GetCommonSkillCheckerHeader();
                    if (skillLoadCommand.SkillConfigId != skillCommonHeader.ConfigId)
                    {
                        return;
                    }

                    skillCheckers.Remove(skillLoadCommand.KeyCode);
                }
                else
                {
                    checker = PlayerSkillCalculator.CreateSkillChecker(skillData, skillLoadCommand.KeyCode);
                    skillCheckers.Add(skillLoadCommand.KeyCode, checker);
                }
                CurrentState = playerSkillState;
            }
        }
        
        public void InitializeState<T>(T state) where T : ISyncPropertyState
        {
            if (state is PlayerSkillState playerSkillState)
            {
                CurrentState = state;         
            }   
        }

        public void ApplyState<T>(T state) where T : ISyncPropertyState
        {
            if (state is PlayerSkillState appliedState && CurrentState is PlayerSkillState playerSkillState)
            {
                var skillCheckers = _playerComponentController.GetSkillCheckerDict();
                if (appliedState.SkillCheckerDatas.Count == 0 && skillCheckers.Count == 0)
                {
                    return;
                }
                for (int i = 0; i < playerSkillState.SkillCheckerDatas.Count; i++)
                {
                    var skillData = appliedState.SkillCheckerDatas[i];
                    if (!skillCheckers.TryGetValue(skillData.AnimationState, out var skillChecker))
                    {
                        Debug.LogError($"SkillCheckerData {skillData.AnimationState} not found");
                        continue;
                    }
                    skillChecker.SetSkillData(skillData);
                    skillCheckers[skillData.AnimationState] = skillChecker;
                }
                foreach (var key in _skillObjects.Keys)
                {
                    _skillObjects[key].transform.position = skillCheckers[key].GetSkillEffectPosition();
                }
            }
        }

        public bool IsSkillExist(AnimationState animationState)
        {
            if (CurrentState is PlayerSkillState playerSkillState)
            {
                var skillCheckers = _playerComponentController.GetSkillCheckerDict();
                if (skillCheckers == null || skillCheckers.Count == 0)
                {
                    //Debug.LogWarning("SkillCheckers is null or empty");
                    return false;
                }
                return skillCheckers.ContainsKey(animationState);
            }
            return false;
        }

        public SkillConfigData GetSkillConfigData(AnimationState animationState)
        {
            if (CurrentState is PlayerSkillState playerSkillState)
            {
                var skillCheckers = _playerComponentController.GetSkillCheckerDict();
                if (skillCheckers == null || skillCheckers.Count == 0)
                {
                    //Debug.LogWarning("SkillCheckers is null or empty");
                    return default;
                }
                if (!skillCheckers.TryGetValue(animationState, out var skillChecker))
                {
                    Debug.LogWarning($"SkillChecker for {animationState} is null or empty");
                    return default;
                }

                var skillConfigData = _skillConfig.GetSkillData(skillChecker.GetCommonSkillCheckerHeader().ConfigId);
                return skillConfigData;
            }
            return default;
        }

        [ClientRpc]
        public void RpcSpawnSkillEffect(int skillConfigId, Vector3 position, AnimationState code)
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