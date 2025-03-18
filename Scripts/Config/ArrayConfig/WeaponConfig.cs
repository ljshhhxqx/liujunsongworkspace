using System;
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
                weaponConfig.skillID = int.Parse(data[2]);
                weaponConfig.itemID = int.Parse(data[3]);
                weaponConfig.quality = Enum.Parse<QualityType>(data[4]);
                weaponConfigData.Add(weaponConfig);
            }
        }
    }

    [Serializable]
    public struct WeaponConfigData
    {
        public int weaponID;
        public int itemID;
        public QualityType quality;
        public WeaponType weaponType;
        public int skillID;
    }

    //默认值
    public struct AttackConfigData
    {
        //攻击半径
        public float AttackRadius;
        //攻击角度
        public float AttackRange;
        //攻击高度
        public float AttackHeight;
        
        public AttackConfigData(float attackRadius, float attackRange, float attackHeight)
        {
            AttackRadius = attackRadius;
            AttackRange = attackRange;
            AttackHeight = attackHeight;
        }
    }

    public enum WeaponType
    {
        None,
        Sword1,
        Sword2,
        Sword3,
        Sword4,
        Sword5,
        Sword6,
        Sword7,
        Sword8,
        Sword9,
        Katana1,
        Katana2,
        Katana3,
        Katana4,
        Katana5,
        Katana6,
    }
}
