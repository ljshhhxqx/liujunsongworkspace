﻿using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace HotUpdate.Scripts.Config.ArrayConfig
{
    [CreateAssetMenu(fileName = "WeaponConfig", menuName = "ScriptableObjects/WeaponConfig")]
    public class WeaponConfig : ConfigBase
    {
        [ReadOnly]
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


        protected override void ReadFromCsv(List<string[]> textAsset)
        {
            weaponConfigData.Clear();
            for (var i = 2; i < textAsset.Count; i++)
            {
                var data = textAsset[i];
                var weaponConfig = new WeaponConfigData();
                weaponConfig.weaponID = int.Parse(data[0]);
                weaponConfig.weaponType = Enum.Parse<WeaponType>(data[1]);
                weaponConfig.attack = float.Parse(data[2]);
                weaponConfig.defense = float.Parse(data[3]);
                weaponConfig.speed = float.Parse(data[4]);
                weaponConfig.range = float.Parse(data[5]);
                weaponConfig.angle = float.Parse(data[6]);
                weaponConfig.skillID = int.Parse(data[7]);
                weaponConfigData.Add(weaponConfig);
            }
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