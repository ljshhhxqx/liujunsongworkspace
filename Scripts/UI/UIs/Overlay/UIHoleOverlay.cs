using System;
using AOTScripts.Data.UI;
using UI.UIBase;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

namespace HotUpdate.Scripts.UI.UIs.Overlay
{
    public class UIHoleOverlay : ScreenUIBase
    {
        [SerializeField]
        private Image holeImage;
        private static readonly int HoleRadius = Shader.PropertyToID("_Radius");
        private static readonly int HoleAlpha = Shader.PropertyToID("_Alpha");

        private Material _holeMaterial;

        public void BindGoldData(IObservable<ValuePropertyData> goldData)
        {
            _holeMaterial ??= holeImage.material;
            goldData.Subscribe(data =>
            {
                SetHoleMaterial(data.Fov / 1000f, 0.9f);
            }).AddTo(this);
        }

        public void SetHoleMaterial(float radius, float alpha)
        {
            _holeMaterial.SetFloat(HoleRadius, radius);
            _holeMaterial.SetFloat(HoleAlpha, alpha);
        }

        public override UIType Type => UIType.UIHoleOverlay;
        public override UICanvasType CanvasType => UICanvasType.Overlay;
    }
}
