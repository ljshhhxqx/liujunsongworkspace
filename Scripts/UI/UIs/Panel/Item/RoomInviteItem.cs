using AOTScripts.Tool;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HotUpdate.Scripts.UI.UIs.Panel.Item
{
    public class RoomInviteItem : ItemBase
    {
        [SerializeField]
        private TextMeshProUGUI nameText;
        [SerializeField]
        private TextMeshProUGUI levelText;
        [SerializeField]
        private Button inviteButton;

        public override void SetData<T>(T data)
        {
            if (data is RoomInviteItemData itemData)
            {
                nameText.text = itemData.Name;
                levelText.text = $"Lv{itemData.Level}";
                inviteButton.BindDebouncedListener(() => { itemData.OnInviteClick?.Invoke(itemData.PlayerId); } );
                return;
            }
            Debug.Log($"Error: Data {data} is not RoomInviteItemData");
        }
    }
}
