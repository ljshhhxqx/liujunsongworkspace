using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;

namespace Network.Server.PlayFab
{
    public class PlayerFriendManager
    {
        public void AddFriend(string friendPlayFabId)
        {
            var request = new AddFriendRequest
            {
                FriendPlayFabId = friendPlayFabId
            };
            PlayFabClientAPI.AddFriend(request, OnAddFriendSuccess, OnAddFriendError);
        }

        private void OnAddFriendSuccess(AddFriendResult result)
        {
            Debug.Log("Friend added successfully.");
        }

        private void OnAddFriendError(PlayFabError error)
        {
            Debug.LogError($"Failed to add friend: {error.GenerateErrorReport()}");
        }

        public void GetFriends()
        {
            var request = new GetFriendsListRequest();
            PlayFabClientAPI.GetFriendsList(request, OnGetFriendsSuccess, OnGetFriendsError);
        }

        private void OnGetFriendsSuccess(GetFriendsListResult result)
        {
            foreach (var friend in result.Friends)
            {
                Debug.Log($"Friend: {friend.TitleDisplayName} ({friend.FriendPlayFabId})");
            }
        }

        private void OnGetFriendsError(PlayFabError error)
        {
            Debug.LogError($"Failed to get friends: {error.GenerateErrorReport()}");
        }

        public void RemoveFriend(string friendPlayFabId)
        {
            var request = new RemoveFriendRequest
            {
                FriendPlayFabId = friendPlayFabId
            };
            PlayFabClientAPI.RemoveFriend(request, OnRemoveFriendSuccess, OnRemoveFriendError);
        }

        private void OnRemoveFriendSuccess(RemoveFriendResult result)
        {
            Debug.Log("Friend removed successfully.");
        }

        private void OnRemoveFriendError(PlayFabError error)
        {
            Debug.LogError($"Failed to remove friend: {error.GenerateErrorReport()}");
        }

    }
}