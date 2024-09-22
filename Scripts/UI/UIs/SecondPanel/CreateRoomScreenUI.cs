using System.Collections.Generic;
using System.Linq;
using Data;
using HotUpdate.Scripts.Config;
using Network.Server.PlayFab;
using TMPro;
using UI.UIBase;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace HotUpdate.Scripts.UI.UIs.SecondPanel
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
        [SerializeField]
        private TMP_Dropdown mapDropdown;
        
        public override UIType Type => UIType.CreateRoom;
        public override UICanvasType CanvasType => UICanvasType.SecondPanel;

        [Inject]
        private void Init(UIManager uiManager, PlayFabRoomManager playFabRoomManager, IConfigProvider configProvider)
        {
            _uiManager = uiManager;
            _playFabRoomManager = playFabRoomManager;
            quitButton.BindDebouncedListener(() =>
            {
                _uiManager.CloseUI(Type);
            });
            var config = configProvider.GetConfig<MapConfig>();
            var configs = config.GetMapConfigDatas(_ => true).ToList();
            createRoomButton.BindDebouncedListener(() =>
            {
                _playFabRoomManager.CreateRoom(new RoomCustomInfo
                {
                    RoomName = string.IsNullOrEmpty(roomNameInputField.text)
                        ? roomNameInputField.placeholder.GetComponent<TextMeshProUGUI>().text
                        : roomNameInputField.text,
                    RoomPassword = roomPasswordInputField.text,
                    RoomType = publicToggle.isOn ? 0 : 1,
                    MaxPlayers = int.Parse(maxPlayersDropdown.options[maxPlayersDropdown.value].text),
                    MapType = int.Parse(mapDropdown.options[mapDropdown.value].text),
                });
            });
            var options = new List<TMP_Dropdown.OptionData>();
            foreach (var configData in configs)
            {
                options.Add(new TMP_Dropdown.OptionData(configData.mapType.ToString()));
            }
            mapDropdown.ClearOptions();
            mapDropdown.AddOptions(options);
            mapDropdown.onValueChanged.AddListener(value =>
            {
                maxPlayersDropdown.gameObject.SetActive(value > 0);
                var configMap = config.GetMapConfigData((MapType)value);
                maxPlayersDropdown.ClearOptions();
                maxPlayersDropdown.AddOptions(GetDropdownOptions(configMap.maxPlayer, configMap.minPlayer));
            });

        }

        private List<TMP_Dropdown.OptionData> GetDropdownOptions(int max, int min)
        {
            var options = new List<TMP_Dropdown.OptionData>();
            for (int i = min; i <= max; i++)
            {
                options.Add(new TMP_Dropdown.OptionData(i.ToString()));
            }
            return options;
        }
        
        private void OnDestroy()
        {
            mapDropdown.onValueChanged.RemoveAllListeners();
        }
    }
}
