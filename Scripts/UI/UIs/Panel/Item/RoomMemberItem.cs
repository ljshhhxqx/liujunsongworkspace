using TMPro;
using UI.UIs.Common;
using UnityEngine;
using UnityEngine.UI;

namespace UI.UIs.Panel.Item
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
                nameText.text = roomMemberItemData.Name;
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
    }
}
