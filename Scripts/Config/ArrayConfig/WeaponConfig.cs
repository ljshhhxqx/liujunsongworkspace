using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;
using Random = UnityEngine.Random;

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
        
        public WeaponConfigData GetWeaponConfigByItemID(int itemID)
        {
            foreach (var data in weaponConfigData)
            {
                if (data.itemID == itemID)
                {
                    return data;
                }
            }

            Debug.LogError("ArmorConfigData not found for itemID: " + itemID);
            return new WeaponConfigData();
        }

        public int GetWeaponBattleConditionID(int itemID)
        {
            return GetWeaponConfigByItemID(itemID).battleEffectConditionId;
        }

        public WeaponConfigData GetRandomWeapon(WeaponType type)
        {
            var weapons = GetRandomWeapons(type);
            if (weapons.Count == 0)
            {
                return new WeaponConfigData();
            }

            return weapons[Random.Range(0, weapons.Count)];
        }
        
        protected override void ReadFromCsv(List<string[]> textAsset)
        {
            weaponConfigData.Clear();
            for (var i = 2; i < textAsset.Count; i++)
            {
                var data = textAsset[i];
                var weaponConfig = new WeaponConfigData();
                weaponConfig.weaponID = int.Parse(data[0]);
                weaponConfig.itemID = int.Parse(data[1]);
                weaponConfig.weaponName = data[2];
                weaponConfig.weaponType = Enum.Parse<WeaponType>(data[3]);
                weaponConfig.skillID = int.Parse(data[4]);
                weaponConfig.quality = Enum.Parse<QualityType>(data[5]);
                //weaponConfig.battleEffectConditionId = int.Parse(data[6]);
                weaponConfig.battleEffectConditionDescription = data[7];
                weaponConfigData.Add(weaponConfig);
            }
        }
        
        #if UNITY_EDITOR
        
        [SerializeField]
        private ConstantBuffConfig constantBuffConfig;
        [SerializeField]
        private BattleEffectConditionConfig battleEffectConditionConfig;

        [Button("将Weapon的条件加入到BattleEffectConditionConfig")]
        public void GenerateWeaponConditionExcel()
        {
            for (int i = 0; i < weaponConfigData.Count; i++)
            {
                var data = weaponConfigData[i];
                var condition = battleEffectConditionConfig.AnalysisDataString(data.battleEffectConditionDescription);
                var maxId = battleEffectConditionConfig.GetConditionMaxId();
                if (condition.id == 0)
                {
                    Debug.Log("battleEffectConditionId not found for weaponID: " + data.weaponID+ "Start to generate a new one");
                    condition.id = battleEffectConditionConfig.GetConditionMaxId() + 1;
                    data.battleEffectConditionId = condition.id;
                    battleEffectConditionConfig.AddConditionData(condition);
                    weaponConfigData[i] = data;
                    EditorUtility.SetDirty(this);
                }
            }
        }
#endif
    }

    [Serializable]
    public struct WeaponConfigData
    {
        public int weaponID;
        public string weaponName;
        public int itemID;
        public QualityType quality;
        public WeaponType weaponType;
        public int skillID;
        public int battleEffectConditionId;
        public string battleEffectConditionDescription;
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
        Sword10,
    }
}
