using System.Collections.Generic;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;

namespace Network.Server.PlayFab
{
    public class PlayFabGroup
    {
        public void CreateGroup(string groupName)
        {
            var request = new CreateSharedGroupRequest
            {
                SharedGroupId = groupName
            };
            PlayFabClientAPI.CreateSharedGroup(request, OnGroupCreated, OnError);
        }

        private void OnError(PlayFabError obj)
        {
            Debug.Log("Error creating group: " + obj.ErrorMessage);
        }

        private void OnGroupCreated(CreateSharedGroupResult result)
        {
            Debug.Log("Group created: " + result.SharedGroupId);
        }

        public void JoinGroup(string groupId)
        {
            var request = new AddSharedGroupMembersRequest
            {
                SharedGroupId = groupId,
                PlayFabIds = new List<string> { PlayFabSettings.staticPlayer.PlayFabId }
            };
            PlayFabClientAPI.AddSharedGroupMembers(request, OnGroupJoined, OnError);
        }

        private void OnGroupJoined(AddSharedGroupMembersResult result)
        {
            Debug.Log("Joined group successfully");
        }

    }
}