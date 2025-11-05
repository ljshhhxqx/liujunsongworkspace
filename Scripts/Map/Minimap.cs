using System.Collections.Generic;
using AOTScripts.Tool.ObjectPool;
using Coffee.UIEffects;
using HotUpdate.Scripts.Collector;
using HotUpdate.Scripts.Network.UI;
using HotUpdate.Scripts.Static;
using HotUpdate.Scripts.Tool.GameEvent;
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
        private Image targetPrefab;
        [SerializeField]
        private RectTransform map;
        private Vector2 _minimapPanelSize;
        private Bounds _worldBounds;
        private GameEventManager _gameEventManager;
        private readonly Dictionary<int, Image> _minimapItems = new Dictionary<int, Image>();
        
        [Inject]
        private void Init(GameEventManager gameEventManager)
        {
            _gameEventManager = gameEventManager;
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
                var item = GameObjectPoolManger.Instance.GetObject(targetPrefab.gameObject, Vector3.zero, Quaternion.identity, transform);
                var icon = UISpriteContainer.GetSprite(y.TargetType.ToString());

                var image = item.GetComponent<Image>();
                var effect = item.GetComponent<UIEffect>();
                image.sprite = icon;
                image.transform.localPosition = MinimapHelper.GetMinimapPosition(y.WorldPosition, _worldBounds, _minimapPanelSize);
                _minimapItems.Add(x, image);
                effect.enabled = y.TargetType == MinimapTargetType.Player;
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
                var effect = item.GetComponent<UIEffect>();
                var icon = UISpriteContainer.GetSprite(z.TargetType.ToString());
                item.sprite = icon;
                item.transform.localPosition = MinimapHelper.GetMinimapPosition(z.WorldPosition, _worldBounds, _minimapPanelSize);
                effect.enabled = y.TargetType == MinimapTargetType.Player;
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
