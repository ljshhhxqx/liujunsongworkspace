using System;
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
        private TextMeshProUGUI friendInfoText;
        [SerializeField]
        private GameObject requestGo;
        [SerializeField]
        private GameObject requestReceivedGo;
        [SerializeField]
        private GameObject friendsGo;
        [SerializeField]
        private GameObject notFriendsGo;
        [SerializeField]
        private Button removeFriendButton;
        [SerializeField]
        private Button acceptFriendButton;
        [SerializeField]
        private Button rejectFriendButton;
        [SerializeField]
        private Button addFriendButton;
        private FriendItemData _currentFriendData;
        
        public override void SetData<T>(T data)
        {
            removeFriendButton.onClick.RemoveAllListeners();
            if (data is FriendItemData itemData && !_currentFriendData.Equals(itemData))
            {
                _currentFriendData = itemData;
                nameText.text = _currentFriendData.Name;
                levelText.text = $"Lv.{_currentFriendData.Level}";
                switch (_currentFriendData.FriendStatus)
                {
                    case FriendStatus.None:
                        requestGo?.SetActive(false);
                        requestReceivedGo?.SetActive(false);
                        friendsGo?.SetActive(false);
                        notFriendsGo?.SetActive(true);
                        break;
                    case FriendStatus.RequestSent:
                        requestGo?.SetActive(true);
                        requestReceivedGo?.SetActive(false);
                        friendsGo?.SetActive(false);
                        notFriendsGo?.SetActive(false);
                        break;
                    case FriendStatus.RequestReceived:
                        requestGo?.SetActive(false);
                        requestReceivedGo?.SetActive(true);
                        friendsGo?.SetActive(false);
                        notFriendsGo?.SetActive(false);
                        
                        break;
                    case FriendStatus.Friends:
                        requestGo?.SetActive(false);
                        requestReceivedGo?.SetActive(false);
                        friendsGo?.SetActive(true);
                        notFriendsGo?.SetActive(false);
                        switch (_currentFriendData.Status)
                        {
                            case PlayerStatus.Offline:
                                friendInfoText.text = $"上次在线时间: {_currentFriendData.LastLoginTime}";
                                friendInfoText.color = Color.gray;
                                break;
                            case PlayerStatus.Online:
                                friendInfoText.text = $"在线";
                                friendInfoText.color = Color.green;
                                break;
                            case PlayerStatus.InGame:
                                friendInfoText.text = $"在游戏中";
                                friendInfoText.color = Color.yellow;
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
                removeFriendButton.onClick.AddListener(() =>
                {
                    itemData.OnRemove?.Invoke(itemData.Id, itemData.PlayerId);
                });
            }
        }

        public override void Clear()
        {
            
        }
    }
}