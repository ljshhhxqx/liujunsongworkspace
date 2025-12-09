using UnityEngine;

namespace HotUpdate.Scripts.UI.UIs.WorldUI
{
    public class UIFollower : MonoBehaviour
    {
        private GameObject _target;
        private Camera _worldCamera;
        private Canvas _canvas;
        private RectTransform _rectTransform;
        private RectTransform _parentRectTransform;

        public void Initialize(GameObject target, Camera worldCamera, Canvas canvas)
        {
            _rectTransform = GetComponent<RectTransform>();
            _parentRectTransform = (RectTransform)transform.parent;
            _target = target;
            _worldCamera = worldCamera;
            _canvas = canvas;
        }

        private void LateUpdate()
        {
            if (!_target.activeSelf)
            {
                return;
            }
            var vp = _worldCamera.WorldToViewportPoint(_target.transform.position);
            var sp = _canvas.worldCamera.ViewportToScreenPoint(vp);
            RectTransformUtility.ScreenPointToWorldPointInRectangle(_parentRectTransform, sp, _canvas.worldCamera, out var worldPoint);
            _rectTransform.position = worldPoint;
        }
    }
}