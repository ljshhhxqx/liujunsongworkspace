using AOTScripts.Data;
using AOTScripts.Tool;
using AOTScripts.Tool.ObjectPool;
using DG.Tweening;
using HotUpdate.Scripts.Config;
using HotUpdate.Scripts.Network.Server.InGame;
using HotUpdate.Scripts.Network.UI;
using HotUpdate.Scripts.UI.UIs.Panel.Item;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
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
        private TextMeshProUGUI hpMpDamageText;
        [SerializeField]
        private GameObject hpmpPanel;

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
            _data = playerHpItemData;
            PlayerId = playerHpItemData.PlayerId;
            hpmpPanel.SetActive(PlayerId != PlayerInGameManager.Instance.LocalPlayerId);
            nameText.text = playerHpItemData.Name;
            hpSlider.value = playerHpItemData.CurrentHp / playerHpItemData.MaxHp;
            mpSlider.value = playerHpItemData.CurrentMp / playerHpItemData.MaxMp;
            SetDamageOrHealText((int)playerHpItemData.DiffValue, _data.PropertyType);
            gameObject.SetActive(true);
        }

        public void SetDamageOrHealText(int damageOrHeal, PropertyTypeEnum propertyType)
        {
            _sequence?.Kill();
            var isHeal = damageOrHeal > 0;
            var property = EnumHeaderParser.GetHeader(propertyType);
            hpMpDamageText.gameObject.SetActive(PlayerId == PlayerInGameManager.Instance.LocalPlayerId);
            hpMpDamageText.text = isHeal ? $"{property}+{damageOrHeal}" : $"{property}-{damageOrHeal}";
            hpMpDamageText.transform.localPosition = Vector3.zero;
            hpMpDamageText.transform.localRotation = Quaternion.identity;
            hpMpDamageText.transform.localScale = Vector3.one;
            hpMpDamageText.color = isHeal ? Color.green : Color.red;
            _sequence = DOTween.Sequence()
                .Append(hpMpDamageText.transform.DOLocalMoveY(50f, 1f).SetEase(Ease.OutCubic).OnComplete(() =>
                {
                    hpMpDamageText.gameObject.SetActive(false);
                }))
                .Join(hpMpDamageText.transform.DOScale(1.5f, 1f).SetEase(Ease.OutCubic).SetEase(Ease.OutCubic))
                .AppendInterval(3f)
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