using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Serialization;
using AnimationState = HotUpdate.Scripts.Config.JsonConfig.AnimationState;

namespace HotUpdate.Scripts.Config.ArrayConfig
{
    [CreateAssetMenu(fileName = "KeyAnimationConfig", menuName = "ScriptableObjects/KeyAnimationConfig")]
    public class KeyAnimationConfig : ConfigBase
    {
        [SerializeField]
        private List<KeyAnimationConfigData> keyAnimationConfigData;
        
        protected override void ReadFromCsv(List<string[]> textAsset)
        {
            keyAnimationConfigData = new List<KeyAnimationConfigData>();
            for (int i = 2; i < textAsset.Count; i++)
            {
                var key = textAsset[i];
                var data = new KeyAnimationConfigData();
                data.animationState = (AnimationState) Enum.Parse(typeof(AnimationState), key[0]);
                data.isComboKey = bool.Parse(key[1]);
                data.animationKeys = JsonConvert.DeserializeObject<string[]>(key[2]);
                keyAnimationConfigData.Add(data);
            }
        }

        public List<AnimationState> GetAllActiveActions()
        {
            var activeKeys = new List<AnimationState>();
            foreach (var data in keyAnimationConfigData)
            {
                if (data.isComboKey)
                {
                    var isComboKeysPressed = true;
                    foreach (var key in data.animationKeys)
                    {
                        if (!Input.GetButtonDown(key))
                        {
                            isComboKeysPressed = false;
                        }
                    }
                    if (isComboKeysPressed)
                    {
                        activeKeys.Add(data.animationState);
                    }
                }
                else
                {
                    if (Input.GetButtonDown(data.animationKeys[0]))
                        activeKeys.Add(data.animationState);
                }
            }
            return activeKeys;
        }
    }
    
    [Serializable]
    public struct KeyAnimationConfigData
    {
        public AnimationState animationState;
        public bool isComboKey;
        public string[] animationKeys;
    }
}