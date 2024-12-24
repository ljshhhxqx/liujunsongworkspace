using System;
using HotUpdate.Scripts.Config;
using Mirror;
using Sirenix.Utilities;
using UnityEngine;

namespace HotUpdate.Scripts.Network.Client.Player
{
    public class WeaponComponent : NetworkBehaviour
    {
        [SerializeField] 
        private WeaponType weaponType;

        private void Reset()
        {
            ChangeWeaponType();
        }

        private void ChangeWeaponType()
        {
            if (!name.IsNullOrWhitespace())
            {
                var splitType = name.Split('_');
                foreach (var type in splitType)
                {
                    if (Enum.TryParse(type, out WeaponType result))
                    {
                        weaponType = result;
                        return;
                    }
                }
                Debug.LogError($"WeaponType转换失败 {splitType}");
            }
        }
    }
}