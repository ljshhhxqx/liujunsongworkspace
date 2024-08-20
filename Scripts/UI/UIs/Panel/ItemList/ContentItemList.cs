using System.Collections.Generic;
using UI.UIs.Common;
using UnityEngine;

namespace UI.UIs.Panel.ItemList
{
    public sealed class ContentItemList : MonoBehaviour
    {
        [SerializeField]
        private ItemBase itemPrefab;
        [SerializeField]
        private Transform content;
        private readonly List<ItemBase> _itemList = new List<ItemBase>();

        public void SetItemList<T>(T[] itemDataList) where T : ItemBaseData, new()
        {
            itemPrefab.gameObject.SetActive(true);
            _itemList.ForEach(x => Destroy(x.gameObject));
            _itemList.Clear();
            if (itemDataList is { Length: > 0 })
            {
                for (int i = 0; i < itemDataList.Length; i++)
                {
                    var itemData = itemDataList[i];
                    var item = Instantiate(itemPrefab.gameObject, content);
                    var itemBase = item.GetComponent<ItemBase>();
                    itemBase.SetData(itemData);
                    _itemList.Add(itemBase);
                }
                itemPrefab.gameObject.SetActive(false);
                return;
            }
            Debug.LogWarning($"ItemList: SetItemList failed, itemDataList --{itemDataList.GetType()}-- is null or empty.");
        }
    }
}
