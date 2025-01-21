// using System.Collections;
// using System.Collections.Generic;
// using Mirror;
// using UnityEngine;
//
// namespace HotUpdate.Scripts.Network.Data.PredictableObject
// {
//     public class PlayerProperty : PredictableNetworkBehaviour
// {
//     [System.Serializable]
//     private class PropertyData
//     {
//         public float baseValue;
//         public float minValue;
//         public float maxValue;
//         public bool autoRecover;
//     }
//
//     [PredictableSyncVar]
//     private PredictableSyncDictionary<PropertyTypeEnum, float> _currentValues 
//         = new PredictableSyncDictionary<PropertyTypeEnum, float>();
//
//     [PredictableSyncVar]
//     private PredictableList<PropertyModifier> _activeModifiers 
//         = new PredictableList<PropertyModifier>();
//
//     // 本地配置数据
//     private Dictionary<PropertyTypeEnum, PropertyData> _propertyConfigs 
//         = new Dictionary<PropertyTypeEnum, PropertyData>();
//
//     [Header("Recovery Settings")]
//     [SerializeField] private float recoveryTickRate = 1f;
//     private float lastRecoveryTime;
//
//     [Header("Property Settings")]
//     [SerializeField] private float baseSpeed = 5f;
//     [SerializeField] private float baseStrength = 100f;
//     [SerializeField] private float baseHealth = 100f;
//     [SerializeField] private float strengthRecoveryRate = 5f;
//     [SerializeField] private float healthRecoveryRate = 1f;
//
//     private PlayerController playerController;
//
//     protected override void Awake()
//     {
//         base.Awake();
//         //playerController = GetComponent<PlayerController>();
//         InitializePropertyConfigs();
//         
//         // 订阅服务器确认的事件
//         _currentValues.OnValueChanged += OnServerPropertyChanged;
//     }
//
//     private void InitializePropertyConfigs()
//     {
//         // 速度
//         _propertyConfigs[PropertyTypeEnum.Speed] = new PropertyData
//         {
//             baseValue = baseSpeed,
//             minValue = 1f,
//             maxValue = 10f,
//             autoRecover = false
//         };
//
//         // 体力
//         _propertyConfigs[PropertyTypeEnum.Strength] = new PropertyData
//         {
//             baseValue = baseStrength,
//             minValue = 0f,
//             maxValue = baseStrength,
//             autoRecover = true
//         };
//
//         // 生命值
//         _propertyConfigs[PropertyTypeEnum.Health] = new PropertyData
//         {
//             baseValue = baseHealth,
//             minValue = 0f,
//             maxValue = baseHealth,
//             autoRecover = true
//         };
//
//         // 初始化当前值
//         foreach (var kvp in _propertyConfigs)
//         {
//             if (isServer)
//             {
//                 _currentValues.ServerSet(kvp.Key, kvp.Value.baseValue);
//             }
//             else if (isLocalPlayer)
//             {
//                 _currentValues.PredictSet(kvp.Key, kvp.Value.baseValue);
//             }
//         }
//     }
//
//     protected override void Update()
//     {
//         base.Update();
//
//         if (!isLocalPlayer) return;
//
//         // 处理自动恢复
//         if (Time.time - lastRecoveryTime >= recoveryTickRate)
//         {
//             ProcessAutoRecovery();
//             lastRecoveryTime = Time.time;
//         }
//     }
//
//     private void ProcessAutoRecovery()
//     {
//         foreach (var kvp in _propertyConfigs)
//         {
//             if (kvp.Value.autoRecover)
//             {
//                 float recoveryAmount = 0;
//                 switch (kvp.Key)
//                 {
//                     case PropertyTypeEnum.Strength:
//                         recoveryAmount = strengthRecoveryRate * recoveryTickRate;
//                         break;
//                     case PropertyTypeEnum.Health:
//                         recoveryAmount = healthRecoveryRate * recoveryTickRate;
//                         break;
//                 }
//
//                 if (recoveryAmount > 0)
//                 {
//                     ModifyPropertyLocally(kvp.Key, recoveryAmount);
//                 }
//             }
//         }
//     }
//
//     public float GetPropertyValue(PropertyTypeEnum type)
//     {
//         return _currentValues.Get(type);
//     }
//
//     public bool HasEnoughProperty(PropertyTypeEnum type, float amount)
//     {
//         if (!_currentValues.ContainsKey(type)) return false;
//         return _currentValues.Get(type) >= amount;
//     }
//
//     public bool ConsumeProperty(PropertyTypeEnum type, float amount)
//     {
//         if (!HasEnoughProperty(type, amount)) return false;
//
//         ModifyPropertyLocally(type, -amount);
//         return true;
//     }
//
//     private void ModifyPropertyLocally(PropertyTypeEnum type, float amount)
//     {
//         if (!_propertyConfigs.ContainsKey(type)) return;
//
//         var config = _propertyConfigs[type];
//         float currentValue = _currentValues.Get(type);
//         float newValue = Mathf.Clamp(currentValue + amount, config.minValue, config.maxValue);
//
//         _currentValues.PredictSet(type, newValue);
//         
//         // 发送到服务器验证
//         if (isLocalPlayer)
//         {
//             CmdModifyProperty(type, amount);
//         }
//     }
//
//     [Command]
//     private void CmdModifyProperty(PropertyTypeEnum type, float amount)
//     {
//         if (!_propertyConfigs.ContainsKey(type)) return;
//
//         var config = _propertyConfigs[type];
//         float currentValue = _currentValues.Get(type);
//         float newValue = Mathf.Clamp(currentValue + amount, config.minValue, config.maxValue);
//
//         _currentValues.ServerSet(type, newValue);
//     }
//
//     private void OnServerPropertyChanged(PropertyTypeEnum type, float oldValue, float newValue)
//     {
//         switch (type)
//         {
//             case PropertyTypeEnum.Speed:
//                 playerController.UpdateMoveSpeed(newValue);
//                 break;
//             case PropertyTypeEnum.Strength:
//                 playerController.UpdateStrength(newValue);
//                 break;
//             case PropertyTypeEnum.Health:
//                 playerController.UpdateHealth(newValue);
//                 if (newValue <= 0)
//                 {
//                     playerController.Die();
//                 }
//                 break;
//         }
//     }
//
//     // 添加修改器系统
//     public void AddModifier(PropertyModifier modifier)
//     {
//         if (!isLocalPlayer) return;
//
//         _activeModifiers.Add(modifier);
//         ApplyModifier(modifier);
//         
//         if (modifier.duration > 0)
//         {
//             StartCoroutine(RemoveModifierAfterDelay(modifier));
//         }
//     }
//
//     private void ApplyModifier(PropertyModifier modifier)
//     {
//         if (!_currentValues.ContainsKey(modifier.propertyType)) return;
//
//         float baseValue = _propertyConfigs[modifier.propertyType].baseValue;
//         float currentValue = _currentValues.Get(modifier.propertyType);
//
//         switch (modifier.type)
//         {
//             case ModifierType.Additive:
//                 ModifyPropertyLocally(modifier.propertyType, modifier.value);
//                 break;
//             case ModifierType.Multiplicative:
//                 float multipliedValue = baseValue * modifier.value;
//                 ModifyPropertyLocally(modifier.propertyType, multipliedValue - currentValue);
//                 break;
//             case ModifierType.Override:
//                 ModifyPropertyLocally(modifier.propertyType, modifier.value - currentValue);
//                 break;
//         }
//     }
//
//     private IEnumerator RemoveModifierAfterDelay(PropertyModifier modifier)
//     {
//         yield return new WaitForSeconds(modifier.duration);
//         RemoveModifier(modifier);
//     }
//
//     private void RemoveModifier(PropertyModifier modifier)
//     {
//         int index = _activeModifiers.IndexOf(modifier);
//         if (index != -1)
//         {
//             _activeModifiers.RemoveAt(index);
//             RecalculateProperty(modifier.propertyType);
//         }
//     }
//
//     private void RecalculateProperty(PropertyTypeEnum type)
//     {
//         if (!_propertyConfigs.ContainsKey(type)) return;
//
//         float baseValue = _propertyConfigs[type].baseValue;
//         float finalValue = baseValue;
//
//         // 重新应用所有相关的修改器
//         foreach (var modifier in _activeModifiers.Where(m => m.propertyType == type))
//         {
//             switch (modifier.type)
//             {
//                 case ModifierType.Additive:
//                     finalValue += modifier.value;
//                     break;
//                 case ModifierType.Multiplicative:
//                     finalValue *= modifier.value;
//                     break;
//                 case ModifierType.Override:
//                     finalValue = modifier.value;
//                     break;
//             }
//         }
//
//         ModifyPropertyLocally(type, finalValue - _currentValues.Get(type));
//     }
//
//     // 用于调试
//     private void OnGUI()
//     {
//         if (!isLocalPlayer) return;
//
//         int y = 10;
//         foreach (var kvp in _propertyConfigs)
//         {
//             GUI.Label(new Rect(10, y, 200, 20), 
//                 $"{kvp.Key}: {_currentValues.Get(kvp.Key):F2}/{kvp.Value.maxValue:F2}");
//             y += 25;
//         }
//     }
// }
//
// public enum ModifierType
// {
//     Additive,       // 加法修改
//     Multiplicative, // 乘法修改
//     Override        // 覆盖修改
// }
//
// public class PropertyModifier
// {
//     public PropertyTypeEnum propertyType;
//     public ModifierType type;
//     public float value;
//     public float duration;
// }
// }