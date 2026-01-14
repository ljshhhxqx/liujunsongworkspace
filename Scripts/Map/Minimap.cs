using System.Collections.Generic;
using AOTScripts.Tool.ObjectPool;
using DG.Tweening;
using HotUpdate.Scripts.Collector;
using HotUpdate.Scripts.Data;
using HotUpdate.Scripts.Network.UI;
using HotUpdate.Scripts.Static;
using HotUpdate.Scripts.Tool.ObjectPool;
using HotUpdate.Scripts.Tool.ReactiveProperty;
using UI.UIBase;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace HotUpdate.Scripts.Map
{
    public class Minimap : ScreenUIBase
    {
        [SerializeField] 
        private Image minimap;
        [SerializeField]
        private Image targetPrefab;
        [SerializeField]
        private RectTransform map;
        private Vector2 _minimapPanelSize;
        private Bounds _worldBounds;
        private readonly Dictionary<int, GameObject> _minimapItems = new Dictionary<int, GameObject>();
        private readonly Dictionary<int, Sequence> _minimapSequence = new Dictionary<int, Sequence>();

        [Inject]
        private void Init()
        {
            targetPrefab.gameObject.SetActive(false);
            GameLoopDataModel.GameSceneName.Subscribe(sceneName =>
            {
                var sprite = UISpriteContainer.GetSprite($"{sceneName.ToString()}_MiniMap");
                SetMinimapSprite(sprite);
            }).AddTo(this);
        }
        
        private void SetMinimapSprite(Sprite sprite)
        {
            minimap.sprite = sprite;
        }

        private void SetMinimapItems(GameObject item, MinimapItemData minimapItemData)
        {
            var image = item.GetComponent<Image>();
            var icon = minimapItemData.TargetType == MinimapTargetType.Treasure ? UISpriteContainer.GetSprite(minimapItemData.TargetType.ToString()+"_"+minimapItemData.QualityType.ToString()) : UISpriteContainer.GetSprite(minimapItemData.TargetType.ToString());
            image.sprite = icon;
            var canvasGroup = item.GetComponent<CanvasGroup>();
            canvasGroup.alpha = 1f;
            image.transform.localPosition = MinimapHelper.GetMinimapPosition(minimapItemData.WorldPosition, _worldBounds, _minimapPanelSize);
            if (minimapItemData.TargetType == MinimapTargetType.Enemy)
            {
                if (_minimapSequence.TryGetValue(minimapItemData.Id, out var sequence))
                {
                    sequence.Kill();
                    sequence = DOTween.Sequence();
                    sequence.AppendInterval(1f);
                    sequence.Append(canvasGroup.DOFade(0f, 1f));
                    sequence.OnComplete(() =>
                    {
                        _minimapSequence.Remove(minimapItemData.Id);
                    });
                }
            }
            item.gameObject.SetActive(true);
        }

        public void BindPositions(HReactiveDictionary<int, MinimapItemData> worldPositions)
        {
            _minimapPanelSize = new Vector2(map.rect.width, map.rect.height);
            _worldBounds = MapBoundDefiner.Instance.MapBounds;
            worldPositions.ObserveAdd((x, y) =>
            {
                if (_minimapItems.ContainsKey(x))
                {
                    return;
                }
                var item = GameObjectPoolManger.Instance.GetObject(targetPrefab.gameObject, Vector3.zero, Quaternion.identity, map);
                SetMinimapItems(item, y);
                _minimapItems.Add(x, item);
            }).AddTo(this);
            worldPositions.ObserveRemove((x, y) =>
            {
                if (!_minimapItems.TryGetValue(x, out var item))
                {
                    return;
                }
                GameObjectPoolManger.Instance.ReturnObject(item.gameObject);
                _minimapItems.Remove(x);
            }).AddTo(this);
            worldPositions.ObserveUpdate((x, y, z) =>
            {
                if (!_minimapItems.TryGetValue(x, out var item))
                {
                    Debug.LogWarning("Minimap item not found.");
                    return;
                }
                
                SetMinimapItems(item, y);
                _minimapItems[x] = item;
            }).AddTo(this);
            worldPositions.ObserveClear(_ =>
            {
                foreach (var item in _minimapItems.Values)
                {
                    GameObjectPoolManger.Instance.ReturnObject(item.gameObject);
                }
                _minimapItems.Clear();
            }).AddTo(this);
        }

        public override UIType Type => UIType.Minimap;
        public override UICanvasType CanvasType =>UICanvasType.Overlay;
    }

    public static class MinimapHelper
    {
        public static Vector2 GetMinimapPosition(Vector3 worldPosition, Bounds worldBounds, Vector2 minimapPanelSize)
        {
            Vector2 uv = GetMinimapUV(worldPosition, worldBounds);
        
            float localX = (uv.x - 0.5f) * minimapPanelSize.x;
            float localY = (uv.y - 0.5f) * minimapPanelSize.y;
        
            return new Vector2(localX, localY);
        }
        
        private static Vector2 GetMinimapUV(Vector3 worldPosition, Bounds worldBounds)
        {
            Vector3 localPos = worldPosition - worldBounds.min;
        
            float u = localPos.x / worldBounds.size.x;
            float v = localPos.z / worldBounds.size.z;
        
            return new Vector2(u, v);
        }
    }
}
