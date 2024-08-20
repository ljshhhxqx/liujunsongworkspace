using System.Collections.Generic;
using Data;
using Network.Server.PlayFab;
using TMPro;
using UI.UIBase;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace UI.UIs.SecondPanel
{
    public class CreateRoomScreenUI : ScreenUIBase
    {
        private UIManager _uiManager;
        private PlayFabRoomManager _playFabRoomManager;
        
        [SerializeField]
        private TMP_InputField roomNameInputField;
        [SerializeField]
        private TMP_InputField roomPasswordInputField;
        [SerializeField]
        private Toggle publicToggle;
        [SerializeField]
        private Button createRoomButton;
        [SerializeField]
        private Button quitButton;
        [SerializeField]
        private TMP_Dropdown maxPlayersDropdown;
        
        public override UIType Type => UIType.CreateRoom;
        public override UICanvasType CanvasType => UICanvasType.SecondPanel;

        [Inject]
        private void Init(UIManager uiManager, PlayFabRoomManager playFabRoomManager)
        {
            _uiManager = uiManager;
            _playFabRoomManager = playFabRoomManager;
            quitButton.BindDebouncedListener(() =>
            {
                _uiManager.CloseUI(Type);
            });
            createRoomButton.BindDebouncedListener(() =>
            {
                _playFabRoomManager.CreateRoom(new RoomCustomInfo
                {
                    RoomName = string.IsNullOrEmpty(roomNameInputField.text) ? roomNameInputField.placeholder.GetComponent<TextMeshProUGUI>().text : roomNameInputField.text,
                    RoomPassword = roomPasswordInputField.text,
                    RoomType = publicToggle.isOn ? 0 : 1,
                    MaxPlayers = int.Parse(maxPlayersDropdown.options[maxPlayersDropdown.value].text) 
                });
            });
            maxPlayersDropdown.ClearOptions();
            maxPlayersDropdown.AddOptions(GetDropdownOptions(4, 1));
        }

        private List<TMP_Dropdown.OptionData> GetDropdownOptions(int maxPlayers, int minPlayers)
        {
            var options = new List<TMP_Dropdown.OptionData>();
            for (int i = minPlayers; i <= maxPlayers; i++)
            {
                options.Add(new TMP_Dropdown.OptionData(i.ToString()));
            }
            return options;
        }
    }
}
