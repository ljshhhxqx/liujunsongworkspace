using System.Collections.Generic;
using AOTScripts.Tool.ObjectPool;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Network.PredictSystem.Calculator;
using HotUpdate.Scripts.Network.PredictSystem.Data;
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
        private readonly Dictionary<AnimationState, GameObject> _skillObjects = new Dictionary<AnimationState, GameObject>();
        private Transform _spawnTransform;
        protected override ISyncPropertyState CurrentState { get; set; }
        protected override CommandType CommandType => CommandType.Skill;

        protected override void Init(GameSyncManager gameSyncManager, IConfigProvider configProvider)
        {
            base.Init(gameSyncManager, configProvider);
            _skillConfig = configProvider.GetConfig<SkillConfig>();
            _spawnTransform = GameObject.FindGameObjectWithTag("SpawnedObjects").transform;
        }

        public override bool NeedsReconciliation<T>(T state)
        {
            return true;
        }

        public override void Simulate(INetworkCommand command)
        {
            if (CurrentState is not PlayerSkillState playerSkillState)
            {
                return;
            }
            if (command is SkillLoadCommand skillLoadCommand)
            {
                Debug.Log($"[SkillLoadCommand] Player {NetworkClient.localPlayer.connectionToClient.connectionId} skill {skillLoadCommand.SkillConfigId} start load");
                ISkillChecker checker;
                var skillData = _skillConfig.GetSkillData(skillLoadCommand.SkillConfigId);
                if (!skillLoadCommand.IsLoad)
                {
                    checker = playerSkillState.SkillCheckers[skillLoadCommand.KeyCode];
                    var skillCommonHeader = checker.GetCommonSkillCheckerHeader();
                    if (skillLoadCommand.SkillConfigId != skillCommonHeader.ConfigId)
                    {
                        return;
                    }

                    playerSkillState.SkillCheckers.Remove(skillLoadCommand.KeyCode);
                }
                else
                {
                    checker = PlayerSkillCalculator.CreateSkillChecker(skillData);
                    playerSkillState.SkillCheckers??=new Dictionary<AnimationState, ISkillChecker>();
                    playerSkillState.SkillCheckers.Add(skillLoadCommand.KeyCode, checker);
                }
                CurrentState = playerSkillState;
            }
        }

        public void ApplyState<T>(T state) where T : ISyncPropertyState
        {
            if (state is PlayerSkillState playerSkillState)
            {
                for (int i = 0; i < playerSkillState.SkillCheckerDatas.Count; i++)
                {
                    var skillData = playerSkillState.SkillCheckerDatas[i];
                    if (!playerSkillState.SkillCheckers.TryGetValue(skillData.AnimationState, out var skillChecker))
                    {
                        Debug.LogError($"SkillCheckerData {skillData.AnimationState} not found");
                        continue;
                    }
                    skillChecker.SetSkillData(skillData);
                    playerSkillState.SkillCheckers[skillData.AnimationState] = skillChecker;
                }
                foreach (var key in _skillObjects.Keys)
                {
                    _skillObjects[key].transform.position = playerSkillState.SkillCheckers[key].GetSkillEffectPosition();
                }
            }
        }

        public bool IsSkillExist(AnimationState animationState)
        {
            if (CurrentState is PlayerSkillState playerSkillState)
            {
                if (playerSkillState.SkillCheckers == null || playerSkillState.SkillCheckers.Count == 0)
                {
                    //Debug.LogWarning("SkillCheckers is null or empty");
                    return false;
                }
                return playerSkillState.SkillCheckers.ContainsKey(animationState);
            }
            return false;
        }

        public SkillConfigData GetSkillConfigData(AnimationState animationState)
        {
            if (CurrentState is PlayerSkillState playerSkillState)
            {
                if (playerSkillState.SkillCheckers == null || playerSkillState.SkillCheckers.Count == 0)
                {
                    //Debug.LogWarning("SkillCheckers is null or empty");
                    return default;
                }
                if (!playerSkillState.SkillCheckers.TryGetValue(animationState, out var skillChecker))
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