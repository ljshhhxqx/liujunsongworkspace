using AOTScripts.Tool;
using UnityEngine;

namespace HotUpdate.Scripts.UI.UIs.WorldUI
{
    public class UIFollower : MonoBehaviour
    {
        private GameObject _target;
        private Camera _worldCamera; // 场景主摄像机（World -> Screen）
        private RectTransform _rectTransform;
        private RectTransform _parentRectTransform;
        private FollowTargetParams _followTargetParams;
        private Transform _playerTransform;

        public void Initialize(GameObject target, Camera worldCamera, Transform playerTransform)
        {
            _rectTransform = GetComponent<RectTransform>();
            _target = target;
            _worldCamera = worldCamera;
            _parentRectTransform = transform.parent.GetComponent<RectTransform>();
            _followTargetParams = new FollowTargetParams();
            _followTargetParams.IndicatorUI = _rectTransform;
            _followTargetParams.CanvasRect = _parentRectTransform;
            _followTargetParams.MainCamera = _worldCamera;
            _followTargetParams.ShowBehindIndicator = false;
            _followTargetParams.ScreenBorderOffset = 1f;
            _playerTransform = playerTransform;
        }

        private void LateUpdate()
        {
            if (!_target || !_target.activeSelf)
            {
                return; 
                
            } 
            _followTargetParams.Target = _target.transform.position; 
            _followTargetParams.Player = _playerTransform.position;
            GameStaticExtensions.FollowTarget(_followTargetParams);
        }

    }
}