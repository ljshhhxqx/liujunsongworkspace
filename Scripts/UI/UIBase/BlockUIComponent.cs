using System.Collections.Generic;
using System.Linq;
using HotUpdate.Scripts.UI.UIBase;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace UI.UIBase
{
    [RequireComponent(typeof(Image))]
    public class BlockUIComponent : MonoBehaviour
    {
        private UIType _uIType;
        private UICanvasType _uiCanvasType;
        private UIManager _uiManager;
        private Image _blockImage;
        private RectTransform _panelRectTransform;
        private Canvas _parentCanvas;
        private List<RectTransform> _childGraphicRectTransforms = new List<RectTransform>();

        [Inject]
        private void Init(UIManager uiManager)
        {
            _uiManager = uiManager;
            _panelRectTransform = GetComponent<RectTransform>();
            _parentCanvas = GetComponentInParent<Canvas>();
            _blockImage ??= GetComponent<Image>();
            var childGraphicsArray = GetComponentsInChildren<Graphic>();
            _childGraphicRectTransforms = childGraphicsArray.Select(x => x.transform as RectTransform)
                .Where(x => x != null && x.gameObject != gameObject)
                .ToList();
            _blockImage.raycastTarget = false;
        }

        void Update()
        {
            if (Input.GetMouseButtonDown(0))
            { 
                Vector2 mousePosition = Input.mousePosition;

                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        _parentCanvas.transform as RectTransform, mousePosition, _parentCanvas.worldCamera, out var localPoint))
                {
                    if (IsClickOnBlankArea(localPoint))
                    {
                        OnBlankAreaClicked();
                    }
                }
            }
        }
        

        private bool IsClickOnBlankArea(Vector2 localPoint)
        {
            // 检查点击是否在面板区域内
            if (!_panelRectTransform.rect.Contains(localPoint))
            {
                return false;
            }

            // 检查点击是否在任何子 Image 区域内
            foreach (RectTransform childRect in _childGraphicRectTransforms)
            {
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        childRect, Input.mousePosition, _parentCanvas.worldCamera, out var childLocalPoint))
                {
                    if (childRect.rect.Contains(childLocalPoint))
                    {
                        return false; // 点击在子 Image 区域内
                    }
                }
            }

            return true; // 点击在面板的空白区域
        }

        private void OnBlankAreaClicked()
        {
            Debug.Log("Clicked on blank area within the panel");
            // 在这里添加关闭面板的逻辑
            _uiManager.CloseUI(_uIType); // 或者使用其他方式隐藏/销毁面板
        }
        
        public void SetUIType(UIType uiType, UICanvasType uiCanvasType)
        {
            if (_uIType != UIType.None) return;
            _uIType = uiType;
            _uiCanvasType = uiCanvasType;
        }
    }
}