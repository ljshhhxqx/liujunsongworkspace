using System;
using System.Collections.Generic;
using Config;
using UnityEngine;

namespace HotUpdate.Scripts.Config
{
    [CreateAssetMenu(fileName = "WeaponConfig", menuName = "ScriptableObjects/WeaponConfig")]
    public class WeaponConfig : ConfigBase
    {
        [SerializeField]
        private List<WeaponConfigData> weaponConfigData;
        
        public WeaponConfigData GetWeaponConfigData(int weaponID)
        {
            foreach (var data in weaponConfigData)
            {
                if (data.weaponID == weaponID)
                {
                    return data;
                }
            }

            Debug.LogError("WeaponConfigData not found for weaponID: " + weaponID);
            return new WeaponConfigData();
        }

        public List<WeaponConfigData> GetRandomWeapons(WeaponType type)
        {
            var weapons = weaponConfigData.FindAll(data => data.weaponType == type);
            if (weapons.Count != 0)
            {
                return weapons;
            }
            Debug.LogError("WeaponConfigData not found for WeaponType: " + type);
            return new List<WeaponConfigData>();
        }

        public WeaponConfigData GetRandomWeapon(WeaponType type)
        {
            var weapons = GetRandomWeapons(type);
            if (weapons.Count == 0)
            {
                return new WeaponConfigData();
            }

            return weapons[UnityEngine.Random.Range(0, weapons.Count)];
        }
    }

    [Serializable]
    public struct WeaponConfigData
    {
        public int weaponID;
        public WeaponType weaponType;
        public float attack;
        public float defense;
        public float speed;
        public float range;
        public float angle;
        public int skillID;
    }

    public enum WeaponType
    {
        Sword1,
        Sword2,
        Sword3,
        Sword4,
        Sword5,
        Sword6,
        Sword7,
        Sword8,
        Sword9,
        Sword10,
        Sword11,
        Sword12,
        Sword13,
        Sword14,
        Sword15,
    }
}
