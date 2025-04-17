using System.Collections.Generic;
using System.Linq;
using AOTScripts.Tool;
using Data;
using HotUpdate.Scripts.Config;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.UI.UIBase;
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
        [SerializeField]
        private TMP_Dropdown mapMode;
        [SerializeField]
        private TMP_Dropdown mapTime;
        [SerializeField]
        private TMP_Dropdown mapScore;
        
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
            var jsonDataConfig = configProvider.GetConfig<JsonDataConfig>();
            var config = configProvider.GetConfig<MapConfig>();
            var mapConfigDatas = config.GetMapConfigDatas(_ => true).ToList();
            var gameModeData = jsonDataConfig.GameModeData;
            var options = new List<TMP_Dropdown.OptionData>();
            foreach (var configData in mapConfigDatas)
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
            
            mapTime.ClearOptions();
            var optionsTime = new List<TMP_Dropdown.OptionData>();
            for (int i = 1; i <= gameModeData.times.Count; i++)
            {
                optionsTime.Add(new TMP_Dropdown.OptionData(gameModeData.times[i - 1].ToString()));
            }
            mapTime.AddOptions(optionsTime);
            
            mapScore.ClearOptions();
            var optionsScore = new List<TMP_Dropdown.OptionData>();
            for (int i = 1; i <= gameModeData.scores.Count; i++)
            {
                optionsScore.Add(new TMP_Dropdown.OptionData(gameModeData.scores[i - 1].ToString()));
            }
            mapScore.AddOptions(optionsScore);

            mapMode.onValueChanged.AddListener(value =>
            {
                mapScore.gameObject.SetActive(value == (int)GameMode.Score);
                mapTime.gameObject.SetActive(value == (int)GameMode.Time);
            });
            createRoomButton.BindDebouncedListener(() =>
            {
                var time = int.Parse(mapTime.options[mapTime.value].text);
                var score = int.Parse(mapScore.options[mapScore.value].text);
                _playFabRoomManager.CreateRoom(new RoomCustomInfo
                {
                    RoomName = string.IsNullOrEmpty(roomNameInputField.text)
                        ? roomNameInputField.placeholder.GetComponent<TextMeshProUGUI>().text
                        : roomNameInputField.text,
                    RoomPassword = roomPasswordInputField.text,
                    RoomType = publicToggle.isOn ? 0 : 1,
                    MaxPlayers = int.Parse(maxPlayersDropdown.options[maxPlayersDropdown.value].text),
                    MapType = int.Parse(mapDropdown.options[mapDropdown.value].text),
                    GameMode = int.Parse(mapMode.options[mapMode.value].text),
                    GameTime = time,
                    GameScore = score
                });
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
