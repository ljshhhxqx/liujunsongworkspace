using System.Collections.Generic;
using AOTScripts.Tool.ObjectPool;
using HotUpdate.Scripts.UI.UIs.Panel.Item;
using UI.UIs.Common;
using UnityEngine;

namespace HotUpdate.Scripts.UI.UIs.Panel.ItemList
{
    public sealed class ContentItemList : MonoBehaviour
    {
        [SerializeField]
        private ItemBase itemPrefab;
        [SerializeField]
        private Transform content;
        public List<ItemBase> ItemBases { get; } = new List<ItemBase>();
        public List<IItemBaseData> ItemBaseDatas { get; } = new List<IItemBaseData>();
        
        public T GetItem<T>(int index) where T : ItemBase
        {
            if (index < 0 || index >= ItemBases.Count)
            {
                Debug.LogWarning($"ItemList: GetItem failed, index --{index}-- out of range.");
                return null;
            }
            return ItemBases[index] as T;
        }

        public void SetItemList<T>(T[] itemDataList) where T : IItemBaseData, new()
        {
            itemPrefab.gameObject.SetActive(true);
            ItemBases.ForEach(x => Destroy(x.gameObject));
            ItemBases.Clear();
            ItemBaseDatas.Clear();
            if (itemDataList.Length > 0)
            {
                foreach (var itemData in itemDataList)
                {
                    var item = GameObjectPoolManger.Instance.GetObject(prefab: itemPrefab.gameObject, parent: content);
                    var itemBase = item.GetComponent<ItemBase>();
                    itemBase.Clear();
                    itemBase.SetData(itemData);
                    ItemBases.Add(itemBase);
                    ItemBaseDatas.Add(itemData);
                }
                itemPrefab.gameObject.SetActive(false);
                return;
            }
            Debug.LogWarning($"ItemList: SetItemList failed, itemDataList --{itemDataList.GetType()}-- is null or empty.");
        }
    }
}
