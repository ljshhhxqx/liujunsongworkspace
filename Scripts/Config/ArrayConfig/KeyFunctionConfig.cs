using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UI.UIBase;
using UnityEngine;

namespace HotUpdate.Scripts.Config.ArrayConfig
{
    [CreateAssetMenu(fileName = "KeyFunctionConfig", menuName = "ScriptableObjects/KeyFunctionConfig")]
    public class KeyFunctionConfig : ConfigBase
    {
        [ReadOnly]
        [SerializeField]
        public List<KeyFunctionData> keyFunctionData;
        
        protected override void ReadFromCsv(List<string[]> textAsset)
        {
            for (int i = 1; i < textAsset.Count; i++)
            {
                var data = new KeyFunctionData();
                var text = textAsset[i];
                data.keyFunction = (KeyFunction) Enum.Parse(typeof(KeyFunction), text[0]);
                data.key = text[1];
                keyFunctionData.Add(data);
            }
        }

        public UIType GetUIType(KeyFunction keyFunction)
        {
            var data = keyFunctionData.Find(x => x.keyFunction == keyFunction);
            if (data.key == null)
            {
                return UIType.None;
            }
            return data.uIType;
        }

        public bool IsKeyFunction(out KeyFunction keyFunction)
        {
            keyFunction = KeyFunction.None;
            for (int i = 0; i < keyFunctionData.Count; i++)
            {
                if (Input.GetButtonDown(keyFunctionData[i].key))
                {
                    keyFunction = keyFunctionData[i].keyFunction;
                    return true;
                }
            }
            return false;
        }
    }

    [Serializable]
    public struct KeyFunctionData
    {
        public KeyFunction keyFunction;
        public string key;
        public UIType uIType;
    }

    public enum KeyFunction
    {
        None,
        Shop,
        Inventory,
        Quest,
        Equip,
        Reset
    }

    public static class KeyFunctionExtension
    {
        public static UIType GetUIType(this KeyFunction keyFunction)
        {
            switch (keyFunction)
            {
                case KeyFunction.Shop:
                    return UIType.Shop;
                case KeyFunction.Inventory:
                    return UIType.Backpack;
                default:
                    return UIType.None;
            }
        }
    }
}