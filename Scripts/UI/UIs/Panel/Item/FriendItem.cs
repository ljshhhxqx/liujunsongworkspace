using Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HotUpdate.Scripts.UI.UIs.Panel.Item
{
    public class FriendItem : ItemBase
    {
        [SerializeField]
        private TextMeshProUGUI nameText;
        [SerializeField]
        private TextMeshProUGUI levelText;
        [SerializeField]
        private Button removeFriendButton;
        [SerializeField]
        private GameObject offlineImage;
        [SerializeField]
        private TextMeshProUGUI lastLoginText;
        [SerializeField]
        private GameObject onlineImage;
        [SerializeField]
        private GameObject inGameImage;
        private FriendItemData _currentFriendData;
        
        public override void SetData<T>(T data)
        {
            removeFriendButton.onClick.RemoveAllListeners();
            if (data is FriendItemData itemData && !_currentFriendData.Equals(itemData))
            {
                _currentFriendData = itemData;
                nameText.text = itemData.Name;
                levelText.text = $"Lv.{itemData.Level}";
                offlineImage.SetActive(itemData.Status == PlayerStatus.Offline);
                onlineImage.SetActive(itemData.Status == PlayerStatus.Online);
                inGameImage.SetActive(itemData.Status == PlayerStatus.InGame);
                lastLoginText.text = itemData.LastLoginTime;
                removeFriendButton.onClick.AddListener(() =>
                {
                    itemData.OnRemove?.Invoke(itemData.PlayerId);
                });
            }
        }

        public override void Clear()
        {
            
        }
    }
}