using AOTScripts.Tool;
using TMPro;
using UI.UIs.Common;
using UnityEngine;
using UnityEngine.UI;

namespace HotUpdate.Scripts.UI.UIs.Panel.Item
{
    public class RoomListItem : ItemBase
    {
        [SerializeField]
        private TextMeshProUGUI roomNameText;
        [SerializeField]
        private TextMeshProUGUI roomIdText;
        [SerializeField]
        private TextMeshProUGUI roomOwnerText;
        [SerializeField]
        private TextMeshProUGUI roomTypeText;
        [SerializeField]
        private TextMeshProUGUI roomStatusText;
        [SerializeField]
        private Button joinButton;
        
        public override void SetData<T>(T data)
        {
            if (data is RoomListItemData roomData)
            {
                roomNameText.text = roomData.RoomName;
                roomIdText.text = roomData.RoomId;
                roomOwnerText.text = roomData.RoomOwnerName;
                roomTypeText.text = roomData.RoomType;
                roomStatusText.text = roomData.RoomStatus;
                joinButton.BindDebouncedListener(() =>
                {
                    roomData.OnJoinClick(roomData.RoomId);
                });
            }
        }

        public override void Clear()
        {
            joinButton.onClick.RemoveAllListeners();
        }
    }
}
