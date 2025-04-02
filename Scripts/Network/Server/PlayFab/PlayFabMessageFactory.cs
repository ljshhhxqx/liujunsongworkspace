using System;
using System.Collections.Generic;
using HotUpdate.Scripts.Network.Server.PlayFab;
using Newtonsoft.Json;
using UnityEngine;

namespace Network.Server.PlayFab
{
    public static class PlayFabMessageFactory
    {
        public static IMessageContent ConvertMessage(Dictionary<string, object> messageData)
        {
            var messageType = (MessageType)Convert.ToInt32(messageData["type"]);

            var message = messageType switch
            {
                // MessageType.Invitation => JsonUtility.FromJson<InvitationMessage>(
                //     JsonConvert.SerializeObject(messageData)),
                MessageType.SystemNotification => JsonUtility.FromJson<IMessageContent>(
                    JsonConvert.SerializeObject(messageData)),
                _ => throw new ArgumentException($"Unknown message type: {messageType}")
            };

            return message;
        }

        public static Message CreateMessage()
        {
            return new Message();
        }
    }
}