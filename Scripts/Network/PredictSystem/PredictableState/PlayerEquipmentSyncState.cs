using AOTScripts.Data;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Network.Client.Player;
using HotUpdate.Scripts.Network.PredictSystem.Calculator;
using HotUpdate.Scripts.Network.PredictSystem.PlayerInput;
using HotUpdate.Scripts.Network.PredictSystem.SyncSystem;
using UnityEngine;
using VContainer;
using ISyncPropertyState = AOTScripts.Data.ISyncPropertyState;
using PlayerEquipmentState = AOTScripts.Data.PlayerEquipmentState;

namespace HotUpdate.Scripts.Network.PredictSystem.PredictableState
{
    public class PlayerEquipmentSyncState : SyncStateBase
    {
        private WeaponIKController _weaponIKController;
        protected override ISyncPropertyState CurrentState { get; set; }
        protected override CommandType CommandType => CommandType.Equipment;
        private WeaponType _currentWeaponType;
        private WeaponConfigData _weaponConfigData;
        private WeaponConfig _weaponConfig;

        [Inject]
        protected override void Init(GameSyncManager gameSyncManager, IConfigProvider configProvider)
        {
            base.Init(gameSyncManager, configProvider);
            _weaponConfig = configProvider.GetConfig<WeaponConfig>();
            _weaponIKController = GetComponent<WeaponIKController>();
        }

        protected override void SetState<T>(T state)
        {
            if (state is not PlayerEquipmentState equipmentState)
                return;

            bool isWeaponEquipped = false;
            WeaponConfigData weaponConfigData = default;
            for (int i = 0; i < equipmentState.EquipmentDatas.Count; i++)
            {
                var data = equipmentState.EquipmentDatas[i];
                if (data == null)
                    continue;
                equipmentState.EquipmentDatas[i] = data;
                if (data.EquipmentPartType == EquipmentPart.Weapon)
                {
                    isWeaponEquipped = true;
                    weaponConfigData =  _weaponConfig.GetWeaponConfigData(data.EquipConfigId);
                    _currentWeaponType = _weaponConfigData.weaponType;
                }
            }

            if (!isWeaponEquipped)
            {
                _currentWeaponType = WeaponType.None;
                _weaponConfigData = default;
            }
            
            if (_currentWeaponType != WeaponType.None)
            {
                if (_currentWeaponType != _weaponConfigData.weaponType)
                {
                    _weaponConfigData = weaponConfigData;
                    var res = ResourceManager.Instance.GetResource<GameObject>(_weaponConfigData.prefabName);
                    if (res != null)
                    {
                        _weaponIKController.SetWeapon(res);
                    }
                }
            }
            else
            {
                _weaponIKController.SetWeapon(null);
            }
        }

        protected override void ProcessCommand(INetworkCommand networkCommand)
        {
            if (CurrentState is not PlayerEquipmentState equipmentState)
            {
                return;
            }

            if (networkCommand is EquipmentCommand equipmentCommand)
            {
                PlayerEquipmentCalculator.CommandEquipment(equipmentCommand, ref equipmentState);
                CurrentState = equipmentState;
            }
        }

        public void ApplyState<T>(T state) where T : ISyncPropertyState
        {
            SetState(state);
        }
    }
}