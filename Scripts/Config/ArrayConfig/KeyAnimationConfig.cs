﻿using System;
using System.Collections.Generic;
using HotUpdate.Scripts.Common;
using Newtonsoft.Json;
using UnityEngine;
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

        public AnimationState GetAllActiveActions()
        {
            var activeKeys = AnimationState.None;
            foreach (var data in keyAnimationConfigData)
            {
                if (data.isComboKey)
                {
                    var isComboKeysPressed = true;
                    foreach (var key in data.animationKeys)
                    {
                        if (!Input.GetButton(key))
                        {
                            isComboKeysPressed = false;
                        }
                    }
                    if (isComboKeysPressed)
                    {
                        activeKeys = activeKeys.AddState(data.animationState);
                    }
                }
                else
                {
                    if (Input.GetButton(data.animationKeys[0]))
                        activeKeys =activeKeys.AddState(data.animationState);
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