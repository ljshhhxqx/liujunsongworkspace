using System;
using System.Collections.Generic;
using AOTScripts.Tool.ObjectPool;
using HotUpdate.Scripts.UI.UIs.Panel.Item;
using UI.UIs.Common;
using UniRx;
using UnityEngine;

namespace HotUpdate.Scripts.UI.UIs.Panel.ItemList
{
    public sealed class ContentItemList : MonoBehaviour
    {
        [SerializeField]
        private ItemBase itemPrefab;
        [SerializeField]
        private Transform content;
        public Dictionary<int, ItemBase> ItemBases { get; } = new Dictionary<int, ItemBase>();
        public Dictionary<int, IItemBaseData> ItemBaseDatas { get; } = new Dictionary<int, IItemBaseData>();

        private void Start()
        {
            content ??= transform;
            itemPrefab.gameObject.SetActive(false);
        }

        public T GetItem<T>(int index) where T : ItemBase
        {
            if (index < 0 || index >= ItemBases.Count)
            {
                Debug.LogWarning($"ItemList: GetItem failed, index --{index}-- out of range.");
                return null;
            }
            return ItemBases[index] as T;
        }

        public void AddItem<T>(int key, T item) where T : IItemBaseData, new()
        {
            if (!ItemBases.TryGetValue(key, out var itemBase))
            {
                itemPrefab.gameObject.SetActive(true);
                itemBase = GameObjectPoolManger.Instance.GetObject(prefab: itemPrefab.gameObject, parent: content).GetComponent<ItemBase>();
                itemBase.Clear();
                itemBase.SetData(item);
                ItemBases.Add(key, itemBase);
                ItemBaseDatas.Add(key, item);
                itemPrefab.gameObject.SetActive(false);
                return;
            }
            Debug.LogWarning($"ItemList: AddItem failed, key --{key}-- already exists.");
        }
        
        public void RemoveItem(int key)
        {
            if (!ItemBases.TryGetValue(key, out var itemBase))
            {
                Debug.LogWarning($"ItemList: RemoveItem failed, key --{key}-- not exists.");    
                return;
            }
            GameObjectPoolManger.Instance.ReturnObject(itemBase.gameObject);
            ItemBases.Remove(key);
            ItemBaseDatas.Remove(key);
        }
        
        public void Clear()
        {
            foreach (var key in ItemBaseDatas.Keys)
            {
                GameObjectPoolManger.Instance.ReturnObject(ItemBases[key].gameObject);
            }
            ItemBases.Clear();
            ItemBaseDatas.Clear();
        }
        
        public void ReplaceItem<T>(int key, T itemData) where T : IItemBaseData, new()
        {
            if (!ItemBases.TryGetValue(key, out var itemBase))
            {
                AddItem(key, itemData);
                return;
            }
            itemBase.SetData(itemData);
            ItemBaseDatas[key] = itemData;
        }

        public void SetItemList<T>(IDictionary<int, T> itemDict) where T : IItemBaseData, new()
        {
            itemPrefab.gameObject.SetActive(true);
            foreach (var key in ItemBases.Keys)
            {
                GameObjectPoolManger.Instance.ReturnObject(ItemBases[key].gameObject);
            }
            ItemBases.Clear();
            ItemBaseDatas.Clear();
            if (itemDict.Count > 0)
            {
                foreach (var itemData in itemDict)
                {
                    var item = GameObjectPoolManger.Instance.GetObject(prefab: itemPrefab.gameObject, parent: content);
                    var itemBase = item.GetComponent<ItemBase>();
                    itemBase.Clear();
                    itemBase.SetData(itemData.Value);
                    ItemBases.Add(itemData.Key, itemBase);
                    ItemBaseDatas.Add(itemData.Key, itemData.Value);
                }
                itemPrefab.gameObject.SetActive(false);
                return;
            }
            Debug.LogWarning($"ItemList: SetItemList failed, itemDict --{itemDict.GetType().Name}-- is null or empty.");
        }
    }
}
