using HotUpdate.Scripts.UI.UIs.Panel.Item;
using TMPro;
using UnityEngine;

namespace HotUpdate.Scripts.UI.UIs.Overlay
{
    public class GameResultItem : ItemBase
    {
        [SerializeField]
        private TextMeshProUGUI rankText;

        [SerializeField]
        private TextMeshProUGUI nameText;

        [SerializeField]
        private TextMeshProUGUI scoreText;
        
        [SerializeField]
        private GameObject winIcon;
        
        public override void SetData<T>(T data)
        {
            if (data is PlayerGameResultItemData playerGameResultItemData)
            {
                rankText.text = playerGameResultItemData.Rank.ToString();
                nameText.text = playerGameResultItemData.PlayerName;
                scoreText.text = playerGameResultItemData.Score.ToString();
                winIcon.SetActive(playerGameResultItemData.IsWin);
            }
        }

        public override void Clear()
        {
        }
    }
}