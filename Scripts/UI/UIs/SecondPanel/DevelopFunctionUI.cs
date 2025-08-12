using System;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Network.PredictSystem.Data;
using HotUpdate.Scripts.Network.PredictSystem.State;
using HotUpdate.Scripts.Network.PredictSystem.SyncSystem;
using HotUpdate.Scripts.UI.UIBase;
using Mirror;
using TMPro;
using UI.UIBase;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace HotUpdate.Scripts.UI.UIs.SecondPanel
{
    public class DevelopFunctionUI: ScreenUIBase
    {
        [SerializeField] private Button equipmentButton;
        [SerializeField] private Button consumeButton;
        [SerializeField] private Button itemButton;
        [SerializeField] private Button confirmButton;
        [SerializeField] private TMP_InputField inputField;
        private ItemConfig _itemConfig;
        private const string Equipment = "Equipment";

        [Inject]
        private void Initialize(IConfigProvider configProvider, UIManager uiManager)
        {
            _itemConfig = configProvider.GetConfig<ItemConfig>();
            equipmentButton.OnClickAsObservable()
                .Subscribe(_ =>
                {
                    var equip = _itemConfig.RandomEquipItemId();
                    inputField.text = $"{equip} 1 0";
                })
                .AddTo(this);
            consumeButton.OnClickAsObservable()
                .Subscribe(_ =>
                {
                    var consume = _itemConfig.RandomConsumeItemId();
                    var count = UnityEngine.Random.Range(1, 11);
                    inputField.text = $"{consume} {count}";
                })
                .AddTo(this);
            itemButton.OnClickAsObservable()
                .Subscribe(_ =>
                {
                    var item = _itemConfig.GetRandomItemId();
                    var count = UnityEngine.Random.Range(1, 3);
                    inputField.text = $"{item} {count}";
                })
                .AddTo(this);
            var gameSyncManager = FindObjectOfType<GameSyncManager>();
            confirmButton.OnClickAsObservable()
                .ThrottleFirst(TimeSpan.FromMilliseconds(0.5f*1000))
                .Subscribe(_ =>
                {
                    CheckInputAndSend(inputField.text, gameSyncManager, uiManager);
                })
                .AddTo(this);
        }

        private void CheckInputAndSend(string input, GameSyncManager gameSyncManager, UIManager uiManager)
        {
            if (string.IsNullOrEmpty(input) || string.IsNullOrWhiteSpace(input))
            {
                uiManager.ShowTips("请输入指令");
                return;
            }
            var strs = input.Split(" ");
            if (strs.Length < 2)
            {
                uiManager.ShowTips("指令格式有错误");
                return;
            }

            if (!int.TryParse(strs[0], out int itemId))
            {
                uiManager.ShowTips("id错误");
                return;
            }
            var itemData = _itemConfig.GetGameItemData(itemId);
            if (itemData.id == 0)
            {
                uiManager.ShowTips("id未能在配置文件中查找到");
                return;
            }

            if (!int.TryParse(strs[1], out int count))
            {
                uiManager.ShowTips("数量错误");
                return;
            }
            count = Mathf.Max(1, count);
            var isEquip = false;
            if (strs.Length == 3)
            {
                isEquip = strs[2] == "1";
            }
            
            for (int i = 0; i < count; i++)
            {
                var itemIds = new int[1];
                itemIds[0] = HybridIdGenerator.GenerateItemId(itemId, GameSyncManager.CurrentTick);
                var items = new MemoryList<ItemsCommandData>();
                items.Add(new ItemsCommandData
                {
                    ItemUniqueId = itemIds,
                    ItemConfigId = itemId,
                    ItemType = itemData.itemType
                });
                var itemGetCommand = new ItemsGetCommand();
                itemGetCommand.Header =
                    GameSyncManager.CreateNetworkCommandHeader(NetworkClient.connection.connectionId, CommandType.Item);
                itemGetCommand.Items = items;
                gameSyncManager.EnqueueCommand(NetworkCommandExtensions.SerializeCommand(itemGetCommand).Item1);
            }

        }

        public override UIType Type => UIType.DevelopFunction;
        public override UICanvasType CanvasType => UICanvasType.SecondPanel;
    }
}