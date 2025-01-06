using System;
using System.Collections.Generic;
using UnityEngine;

namespace HotUpdate.Scripts.Config
{
    [CreateAssetMenu(fileName = "PlayerData", menuName = "ScriptableObjects/PlayerData")]
    public class PlayerDataConfig : ConfigBase
    {
        protected override void ReadFromCsv(List<string[]> textAsset)
        {
            
        }
    }
}