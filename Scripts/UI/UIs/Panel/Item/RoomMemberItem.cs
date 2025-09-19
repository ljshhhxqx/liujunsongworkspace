using TMPro;
using UI.UIs.Common;
using UnityEngine;
using UnityEngine.UI;

namespace HotUpdate.Scripts.UI.UIs.Panel.Item
{
    public class RoomMemberItem : ItemBase
    {
        [SerializeField]
        private TextMeshProUGUI nameText;
        [SerializeField]
        private TextMeshProUGUI levelText;
        [SerializeField]
        private GameObject addFriend;
        [SerializeField] 
        private GameObject friend;
        [SerializeField]
        private Button addFriendBtn;

        public override void SetData<T>(T data)
        {
            addFriendBtn.onClick.RemoveAllListeners();
            if (data is RoomMemberItemData roomMemberItemData)
            {
                Debug.Log($"SetData: {roomMemberItemData}");
                nameText.text = string.IsNullOrEmpty(roomMemberItemData.Name) ? "" : roomMemberItemData.PlayerId;
                levelText.text = $"Lv{roomMemberItemData.Level}";
                addFriend.SetActive(roomMemberItemData.IsFriend && !roomMemberItemData.IsSelf);
                friend.SetActive(roomMemberItemData.IsFriend);
                addFriendBtn.onClick.AddListener(() => 
                { 
                    // TODO: add friend logic
                    
                });
                return;
            }
            Debug.Log($"Error: Data {data} is not RoomMemberItemData");
        }

        public override void Clear()
        {
            
        }
    }
}
