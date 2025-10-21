using System;
using AOTScripts.Data;
using HotUpdate.Scripts.Config;
using TMPro;
using UnityEngine;

namespace HotUpdate.Scripts.UI.UIs.Panel.Item
{
    public class PlayerConnectItem : ItemBase
    {
        [SerializeField]
        private TextMeshProUGUI playerNameText;
        [SerializeField]
        private TextMeshProUGUI playerLevelText;
        [SerializeField]
        private TextMeshProUGUI playerConnectionStatusText;
        [SerializeField]
        private TextMeshProUGUI playerDutyText;

        public PlayerConnectionData PlayerConnectData { get; private set; }

        public override void SetData<T>(T data)
        {
            if (data is PlayerConnectionData playerConnectData)
            {
                PlayerConnectData = playerConnectData;
                playerNameText.text = playerConnectData.Name;
                playerLevelText.text = $"Lv.{playerConnectData.Level}";
                playerConnectionStatusText.text = EnumHeaderParser.GetHeader(playerConnectData.Status);
                playerDutyText.text = EnumHeaderParser.GetHeader(playerConnectData.Duty);
            }
        }

        public override void Clear()
        {
            
        }
    }
}