using System;
using System.Collections.Generic;
using Data;
using UnityEngine.Serialization;

namespace Network.Server.PlayFab
{
    public enum MessageType
    {
        Invitation,
        RequestJoinRoom,
        ApproveJoinRoom,
        DownloadFile,
        Chat,
        SystemNotification = -1,
        Test = -2,
        // 可以根据需要添加更多类型
    }
    
    public enum MessageScope
    {
        System,
        Private,
        Group,
        Global,
    }

    public enum DisplayType
    {
        Popup,
        Chat,
        Notification,
        Email,
        // 可以根据需要添加更多类型
    }

    [Serializable]
    public class Message
    {
        /// <summary>
        /// 消息的唯一标识符(服务器创建)
        /// </summary>
        public string id;
        /// <summary>
        /// 发送者的 PlayFab ID
        /// </summary>
        public string senderId;
        /// <summary>
        /// 接收者的 PlayFab ID
        /// </summary>
        public string targetId;
        /// <summary>
        /// 将具体的消息类数据转为 JSON 字符串
        /// </summary>
        public string content;
        /// <summary>
        /// 消息创建时间(服务器创建)
        /// </summary>
        public string timestamp;
        /// <summary>
        /// 消息类型(可能有多种类型)
        /// </summary>
        public int messageType;
        /// <summary>
        /// 消息的作用范围
        /// 1. System: 系统消息，所有用户都可以收到
        /// 2. Private: 私聊消息，只有两个用户之间可以收到
        /// 3. Group: 群聊消息，只有群组内的用户之间可以收到
        /// 4. Global: 全局消息，所有用户都可以收到
        /// </summary>
        public int messageScope;
        /// <summary>
        /// 消息的显示类型
        /// 1. Popup: 弹出消息框
        /// 2. Chat: 聊天消息
        /// 3. Notification: 通知消息
        /// 4. Email: 邮件消息
        /// </summary>
        public int displayType;
        /// <summary>
        /// 群组消息id(不是群组消息则为空)
        /// </summary>
        public string groupId;
        /// <summary>
        /// 是否永久消息
        /// </summary>
        public bool isPermanent;
        /// <summary>
        /// 消息过期时间(如果客户端不设置，服务器会设置一个默认过期时间；如果是永久消息，则不会过期)
        /// </summary>
        public string expirationTime;
        /// <summary>
        /// 消息的状态(服务器设置，客户端只读)
        /// </summary>
        public string status;
    }
    
    [Serializable]
    public class SendMessageResponse
    {
        public bool success;
        public string message;
        public string errorMessage;
    }

    [Serializable]
    public class GetNewMessagesResponse
    {
        public List<Message> messages;
    }

    [Serializable]
    public class MessageContent
    {
        
    }

    // 特定类型的消息可以继承自 Message 类
    [Serializable]
    public class RequestJoinRoomMessage : MessageContent
    {
        public string roomId;
        public string roomName;
        public string requesterId;
        public string requesterName;
    }
    
    [Serializable]
    public class InvitationMessage : MessageContent
    {
        public string inviterId;
        public string inviterName;
        public string roomId;
        public string roomName;
    }
    
    [Serializable]
    public class ApproveJoinRoomMessage : MessageContent
    {
        public RoomData roomData;
    }

    [Serializable]
    public class DownloadFileMessage : MessageContent
    {
        public string fileContents;
        public string fileName;
        public string errorMessage;
    }

    [Serializable]
    public class TestMessage : MessageContent
    {
        public string testContent;
    }
}