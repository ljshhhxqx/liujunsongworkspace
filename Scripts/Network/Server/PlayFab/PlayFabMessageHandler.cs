using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AOTScripts.Tool;
using HotUpdate.Scripts.Network.Server.PlayFab;
using HotUpdate.Scripts.Tool.GameEvent;
using HotUpdate.Scripts.UI.UIBase;
using Network.Data;
using Newtonsoft.Json;
using PlayFab;
using PlayFab.CloudScriptModels;
using Tool.Coroutine;
using Tool.GameEvent;
using UI.UIBase;
using UnityEngine;
using VContainer;

namespace Network.Server.PlayFab
{
    public class PlayFabMessageHandler
    {
        // RepeatedTask是一套用UniTask封装的定时器，可以方便地实现重复任务
        private readonly RepeatedTask _repeatedTask;
        private readonly UIManager _uiManager;
        private readonly GameEventManager _gameEventManager;
        private bool _isProcessingPopup;
        private bool _isProcessingTest;
        private readonly Dictionary<int, string> _lastMessageIds = new Dictionary<int, string>
        {
            { (int)MessageScope.System, "0-0" },
            { (int)MessageScope.Private, "0-0" },
            { (int)MessageScope.Group, "0-0" },
            { (int)MessageScope.Global, "0-0" },
            // Add other message types as needed
        };
        // //将需要同步的数据缓存起来，避免重复请求;key为事件名，value为事件数据的集合
        // private readonly Dictionary<string, List<PlayerEventData>> _cachedEvents = new Dictionary<string, List<PlayerEventData>>();
        
        [Inject]
        private PlayFabMessageHandler(RepeatedTask repeatedTask, UIManager uiManager, GameEventManager gameEventManager)
        {
            _repeatedTask = repeatedTask;
            // UIManager是用来显示弹窗的
            _uiManager = uiManager;
            _gameEventManager = gameEventManager;
            _gameEventManager.Subscribe<PlayerLoginEvent>(OnLoginEvent);
            _gameEventManager.Subscribe<PlayerLogoutEvent>(OnLogoutEvent);
        }

        private void OnLogoutEvent(PlayerLogoutEvent playerLogoutEvent)
        {
            _repeatedTask.StopRepeatingTask(GetNewMessages);
        }

        private void OnLoginEvent(PlayerLoginEvent playerLoginEvent)
        {
            _repeatedTask.StartRepeatingTask(GetNewMessages, 2);
        }

        public void SendMessage(Message message)
        {
            var request = new ExecuteEntityCloudScriptRequest
            {
                FunctionName = "SendMessage",
                GeneratePlayStreamEvent = true,
                Entity = PlayFabData.EntityKey.Value,
                FunctionParameter = new
                {
                    id = message.id,
                    messageType = message.messageType,
                    senderId = message.senderId,
                    content = message.content,
                    timestamp = message.timestamp,
                    isPermanent = message.isPermanent,
                    messageScope = message.messageScope,
                    targetId = message.targetId,
                    groupId = message.groupId,
                    expirationTime = message.expirationTime,
                    status = message.status,
                }
            };

            PlayFabCloudScriptAPI.ExecuteEntityCloudScript(request, OnMessageSent, OnError);
        }

        private void OnMessageSent(ExecuteCloudScriptResult result)
        {
            if (result.Error != null)
            {
                throw new Exception($"{result.Error.Error}-${result.Error.Message}-${result.Error.StackTrace}");
            }
            Debug.Log($"Message {result.FunctionResult} sent successfully");
        }

        // public void GetGroupMessages(string groupId)
        // {
        //     var request = new ExecuteCloudScriptRequest
        //     {
        //         FunctionName = "GetMessages",
        //         FunctionParameter = new
        //         {
        //             lastMessageIds = _lastMessageIds.ToDictionary(kvp => kvp.Key.ToString(), kvp => kvp.Value),
        //             groupId = "" // 如果需要的话
        //         }
        //     };
        // }

        public void GetNewMessages()
        {
            var request = new ExecuteEntityCloudScriptRequest
            {
                FunctionName = "GetNewMessages",
                FunctionParameter = new
                {
                    lastMessageIds = _lastMessageIds.ToDictionary(kvp => kvp.Key.ToString(), kvp => kvp.Value),
                    playerId = PlayFabData.PlayFabId.Value
                },
                GeneratePlayStreamEvent = true,
                Entity = PlayFabData.EntityKey.Value,
            };

            PlayFabCloudScriptAPI.ExecuteEntityCloudScript(request, OnGetNewMessages, OnError);
        }

        private void OnGetNewMessages(ExecuteCloudScriptResult result)
        {
            if (result.Error != null)
            {
                throw new Exception($"{result.Error.Error}-${result.Error.Message}-${result.Error.StackTrace}");
            }
            if (!_isProcessingTest)
            {
                _gameEventManager.Publish(new GameMessageListeningEvent());
                _isProcessingTest = true;
            }
            
            if (result.FunctionResult == null)
            {
                Debug.Log("No new messages");
                return;
            }
            
            var json = result.FunctionResult.ToString();
            var messages = JsonConvert.DeserializeObject<GetNewMessagesResponse>(json).messages;
            Debug.Log($"Received {messages.Count} new messages");
            ProcessMessages(messages);
        }

        private void ProcessMessages(List<Message> messages)
        {
            foreach (var message in messages)
            {
                Debug.Log($"Processing message {JsonUtility.ToJson(message)}");
                switch (message.displayType)
                {
                    case (int)DisplayType.Popup:
                        if (!_isProcessingPopup)
                        {
                            HandleMessageType(message);
                            _isProcessingPopup = true;
                        }
                        break;
                    case (int)DisplayType.Chat:
                        //_roomManager.AddToChatWindow(message);
                        MarkAsProcessed(message.id, (MessageScope)message.messageScope);
                        break;
                    case (int)DisplayType.Notification:
                        HandleMessageType(message);
                        Debug.Log($"Message {message.id} Notification message received");
                        MarkAsProcessed(message.id, (MessageScope)message.messageScope);
                        Debug.Log($"Message {message.id} marked as processed");
                        //_uiManager.ShowNotification(message);
                        break;
                    case (int)DisplayType.Email:
                        break;
                    default:
                        break;
                }

                // 更新最后处理的消息ID
                if (CompareIds(message.id, _lastMessageIds[message.displayType]) > 0)
                {
                    _lastMessageIds[message.displayType] = message.id;
                }
            }
        }

        private void HandleMessageType(Message message)
        {
            switch (message.messageType)
            {
                case (int)MessageType.Invitation:

                    break;
                case (int)MessageType.RequestJoinRoom:

                    break;
                case (int)MessageType.SystemNotification:
                    break;
                case (int)MessageType.ApproveJoinRoom:
                    break;
                case (int)MessageType.DownloadFile:
                    var downloadFileMes = ConvertToMessageContent<DownloadFileMessage>(message.content);
                    DownloadFile(downloadFileMes.fileName);
                    break;
                case (int)MessageType.Chat:
                    break;
                case (int)MessageType.Test:
                    Test(message.content);
                    break;
                default:
                    break;
            }
        }

        private int CompareIds(string idA, string idB)
        {
            long ConvertFromBase36(string base36Value)
            {
                var result = 0;
                foreach (char c in base36Value)
                {
                    int value;
                    if (char.IsDigit(c))
                    {
                        value = c - '0'; // '0' to '9' -> 0 to 9
                    }
                    else if (c is >= 'a' and <= 'z')
                    {
                        value = c - 'a' + 10; // 'a' to 'z' -> 10 to 35
                    }
                    else
                    {
                        throw new ArgumentException($"Invalid character '{c}' in base36 string: {base36Value}.");
                    }

                    // Check for overflow before multiplying
                    if (result > (long.MaxValue - value) / 36)
                    {
                        throw new OverflowException($"Base36 value '{base36Value}' is too large.");
                    }

                    result = result * 36 + value;
                }
                return result;
            }
            var partsA = idA.Split('-');
            var partsB = idB.Split('-');

            if (partsA.Length != 2 || partsB.Length != 2)
            {
                throw new ArgumentException("ID format is invalid. Expected format: 'part1-part2'.");
            }

            var partA1 = ConvertFromBase36(partsA[0]);
            var partA2 = ConvertFromBase36(partsA[1]);
            var partB1 = ConvertFromBase36(partsB[0]);
            var partB2 = ConvertFromBase36(partsB[1]);

            if (partA1 != partB1)
            {
                return partA1.CompareTo(partB1);
            }
            return partA2.CompareTo(partB2);
        }
        
        private void MarkAsProcessed(string messageId, MessageScope messageScope)
        {
            var request = new ExecuteEntityCloudScriptRequest
            {
                FunctionName = "MarkMessageAsProcessed",
                FunctionParameter = new { messageId = messageId, playerId = PlayFabData.PlayFabId.Value, messageScope = (int)messageScope },
                GeneratePlayStreamEvent = true,
                Entity = PlayFabData.EntityKey.Value,
            };

            PlayFabCloudScriptAPI.ExecuteEntityCloudScript(request, OnMarkAsProcessedSuccess, OnError);
        }

        private void OnMarkAsProcessedSuccess(ExecuteCloudScriptResult result)
        {
            if (result.Error != null)
            {
                throw new Exception($"{result.Error.Error}-${result.Error.Message}-${result.Error.StackTrace}");
            }
            result.DebugEntityCloudScriptResult();
            Debug.Log($"Message {result.FunctionResult} marked as processed successfully");
        }

        private void OnError(PlayFabError error)
        {
            Debug.LogError($"PlayFab Error: {error.ErrorMessage}");
        }

        private void DownloadFile(string fileName)
        {
            var request = new ExecuteEntityCloudScriptRequest
            {
                FunctionName = "GetPlayerFile",
                FunctionParameter = new { fileName = fileName, playerId = PlayFabData.PlayFabId.Value },
                GeneratePlayStreamEvent = true,
                Entity = PlayFabData.EntityKey.Value,
            };

            PlayFabCloudScriptAPI.ExecuteEntityCloudScript(request, OnGetPlayerFileSuccess, OnError);
        }

        private void OnGetPlayerFileSuccess(ExecuteCloudScriptResult result)
        {
            if (result.Error != null)
            {
                throw new Exception($"{result.Error.Error}-${result.Error.Message}-${result.Error.StackTrace}");
            }

            if (result.FunctionResult != null)
            {
                var resultObject = JsonUtility.FromJson<DownloadFileMessage>(result.FunctionResult.ToString());
                if (!string.IsNullOrEmpty(resultObject.fileContents))
                {
                    // 获取持久化数据路径
                    string persistentDataPath = Application.persistentDataPath;

                    // 构建要保存文件的完整路径
                    string filePath = Path.Combine(persistentDataPath, $"{resultObject.fileName}.bytes");

                    // 将文件内容写入到构建的文件路径中
                    File.WriteAllBytes(filePath, Convert.FromBase64String(resultObject.fileContents));

                    Debug.Log("File saved to: " + filePath);
                }
                else
                {
                    Debug.Log("File not found or empty");
                }
            }
        }

        private void Test(string content)
        {
            var messageContent = JsonUtility.FromJson<TestMessage>(content);
            Debug.Log($"Test message received {messageContent.testContent}");
        }
        
        private T ConvertToMessageContent<T>(string content) where T : IMessageContent, new()
        {
            var messageContent = JsonUtility.FromJson<T>(content);
            return messageContent;
            // 这里你需要将服务器返回的消息内容转换为Message对象并处理
        }
    }
}