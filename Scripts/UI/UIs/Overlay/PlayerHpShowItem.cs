using AOTScripts.Data;
using AOTScripts.Tool;
using AOTScripts.Tool.ObjectPool;
using DG.Tweening;
using HotUpdate.Scripts.Config;
using HotUpdate.Scripts.UI.UIs.Panel.Item;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HotUpdate.Scripts.UI.UIs.Overlay
{
    public class PlayerHpItem : ItemBase
    {
        [SerializeField]
        private RectTransform indicatorTransform;
        [SerializeField]
        private Slider hpSlider;
        [SerializeField]
        private Slider mpSlider;
        [SerializeField]
        private TextMeshProUGUI nameText;
        [SerializeField]
        private GameObject damageOrHealTextPrefab;

        private Sequence _sequence;
        private PlayerHpItemData _data;
        public int PlayerId { get; private set; }
        
        public override void SetData<T>(T data)
        {
            if (data is PlayerHpItemData playerHpItemData)
            {
                DataChanged(playerHpItemData);
            }
        }

        public void DataChanged(PlayerHpItemData playerHpItemData)
        {
            gameObject.SetActive(true);
            _data = playerHpItemData;
            PlayerId = playerHpItemData.PlayerId;
            nameText.text = playerHpItemData.Name;
            hpSlider.value = playerHpItemData.CurrentHp / playerHpItemData.MaxHp;
            mpSlider.value = playerHpItemData.CurrentMp / playerHpItemData.MaxMp;
            SetDamageOrHealText((int)playerHpItemData.DiffValue, _data.PropertyType);
        }

        public void SetDamageOrHealText(int damageOrHeal, PropertyTypeEnum propertyType)
        {
            var isHeal = damageOrHeal > 0;
            var go = GameObjectPoolManger.Instance.GetObject(damageOrHealTextPrefab, parent: damageOrHealTextPrefab.transform.parent);
            var text = go.GetComponent<TextMeshProUGUI>();
            go.gameObject.SetActive(true);
            var property = EnumHeaderParser.GetHeader(propertyType);
            text.text = isHeal ? $"{property}+{damageOrHeal}" : $"{property}-{damageOrHeal}";
            text.transform.localPosition = Vector3.zero;
            text.transform.localScale = Vector3.one;
            text.color = isHeal ? Color.green : Color.red;
            _sequence?.Kill();
            _sequence = DOTween.Sequence()
                .Append(text.transform.DOLocalMoveY(50f, 1f).SetEase(Ease.OutCubic).OnComplete(() =>
                {
                    GameObjectPoolManger.Instance.ReturnObject(go);
                }))
                .Join(text.transform.DOScale(1.5f, 1f).SetEase(Ease.OutCubic).SetEase(Ease.OutCubic))
                .AppendInterval(4f)
                .AppendCallback(() =>
                {
                    gameObject.SetActive(false);
                });
        }

        public override void Clear()
        {
        }

        public void Show(FollowTargetParams followTargetParams)
        {
            followTargetParams.IndicatorUI = indicatorTransform;
            followTargetParams.Target = _data.TargetPosition;
            followTargetParams.Player = _data.PlayerPosition;
            GameStaticExtensions.FollowTarget(followTargetParams);
        }
    }
}