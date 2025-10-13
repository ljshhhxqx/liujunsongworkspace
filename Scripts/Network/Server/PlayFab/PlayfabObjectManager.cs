using System;
using System.Collections.Generic;
using HotUpdate.Scripts.Network.Data;
using HotUpdate.Scripts.Tool.GameEvent;
using PlayFab;
using PlayFab.DataModels;
using UniRx;
using UnityEngine;
using VContainer;
using EntityKey = PlayFab.DataModels.EntityKey;

namespace HotUpdate.Scripts.Network.Server.PlayFab
{
    public class PlayfabObjectManager
    {
        private string _entityId;
        private string _entityType;
        
        [Inject]
        private void Init(GameEventManager gameEventManager)
        {
            PlayFabData.EntityKey.Subscribe(entityKey =>
            {
                _entityId = entityKey.Id;
                _entityType = entityKey.Type;
            });
        }

        public void SetEntityObject(string objectName, object data)
        {
            var request = new SetObjectsRequest
            {
                Entity = new EntityKey
                {
                    Id = _entityId,
                    Type = _entityType
                },
                Objects = new List<SetObject>
                {
                    new SetObject
                    {
                        ObjectName = objectName,
                        DataObject = data
                    }
                }
            };
    
            PlayFabDataAPI.SetObjects(request, result =>
            {
                Debug.Log($"实体对象 {objectName} 设置成功!");
            }, error =>
            {
                Debug.LogError($"设置实体对象失败: {error.ErrorMessage}");
            });
        }
        
        public void GetEntityObject<T>(string objectName, Action<T> onSuccess)
        {
            var request = new GetObjectsRequest
            {
                Entity = new EntityKey
                {
                    Id = _entityId,
                    Type = _entityType
                }
            };
    
            PlayFabDataAPI.GetObjects(request, result =>
            {
                if (result.Objects != null && result.Objects.ContainsKey(objectName))
                {
                    var obj = result.Objects[objectName];
                    T data = JsonUtility.FromJson<T>(obj.DataObject.ToString());
                    onSuccess?.Invoke(data);
                }
                else
                {
                    Debug.LogWarning($"实体对象 {objectName} 不存在");
                    onSuccess?.Invoke(default(T));
                }
            }, error =>
            {
                Debug.LogError($"获取实体对象失败: {error.ErrorMessage}");
                onSuccess?.Invoke(default(T));
            });
        }
    }
}