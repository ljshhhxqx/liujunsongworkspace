using DG.Tweening;
using HotUpdate.Scripts.Network.PredictSystem.Interact;
using HotUpdate.Scripts.Tool.GameEvent;
using Mirror;
using UnityEngine;

namespace HotUpdate.Scripts.Collector.Collects
{
    public class HiddenItem : CollectBehaviour, IPoolable
    {
        [SyncVar]
        private HiddenItemData _hiddenItemData;
        private bool _isHidden;
        
        private Sequence _sequence;
    
        public void HideItem()
        {
            _isHidden = true;
            SetEnabled(false);
            // Collider保持启用，仍然可以交互
        }
    
        public void RevealItem()
        {
            _isHidden = false;
            SetEnabled(true);
        }
        

        protected override void OnInitialize()
        {
            
        }

        public void Init(HiddenItemData hiddenItemData, bool serverHandler, uint id)
        {
            _hiddenItemData = hiddenItemData;
            NetId = id;
            ServerHandler = serverHandler;
            if (serverHandler)
            {
                GameEventManager.Publish(new SceneItemInfoChanged(NetId, transform.position, new SceneItemInfo
                {
                    health = 1,
                    sceneItemId = id,
                }));
            }
            switch (hiddenItemData.hideType)
            {
                case HideType.Inactive:
                    HideItem();
                    break;
                case HideType.Mystery:
                    MysteryItem();
                    break;
                case HideType.Translucence:
                    TranslucenceItem();
                    break;
            }
        }

        private void TranslucenceItem()
        {
            _sequence?.Kill();
            _sequence = DOTween.Sequence();
            _sequence.AppendCallback(() =>
            {
                SetColor(Color.red);
            });
            _sequence.AppendInterval(_hiddenItemData.translucenceTime);
            _sequence.AppendCallback(() =>
            {
                SetColor(Color.yellow);
            });
            _sequence.AppendInterval(_hiddenItemData.translucenceTime);
            _sequence.AppendCallback(() =>
            {
                SetColor(Color.green);
            });
            _sequence.AppendInterval(_hiddenItemData.translucenceTime);
            _sequence.AppendCallback(() =>
            {
                SetColor(Color.cyan);
            });
            _sequence.AppendInterval(_hiddenItemData.translucenceTime);
            _sequence.AppendCallback(() =>
            {
                SetColor(Color.blue);
            });
            _sequence.AppendCallback(() =>
            {
                SetColor(Color.white);
            });
            _sequence.SetLoops(int.MaxValue, LoopType.Yoyo);
        }

        private void MysteryItem()
        {
            _sequence?.Kill();
            _sequence = DOTween.Sequence();
            _sequence.AppendCallback(() =>
            {
                SetEnabled(false);
            });
            _sequence.AppendInterval(_hiddenItemData.mysteryTime);
            _sequence.AppendCallback(() =>
            {
                SetEnabled(true);
            });
            _sequence.SetLoops(int.MaxValue, LoopType.Restart);
        }

        public void OnSelfSpawn()
        {
            HideItem();
        }

        public void OnSelfDespawn()
        {
            _sequence?.Kill();
            
            SetEnabled(true);
        }
    }
}