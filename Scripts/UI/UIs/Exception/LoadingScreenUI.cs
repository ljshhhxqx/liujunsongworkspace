using System;
using Tool.Coroutine;
using UI.UIBase;
using UnityEngine;
using VContainer;

namespace UI.UIs.Exception
{
    public class LoadingScreenUI : ScreenUIBase
    {
        [SerializeField]
        private GameObject logo;
        [SerializeField]
        private float rotationSpeed = 5f;
        [SerializeField]
        private float rotationInterval = 0.02f;
        private float _elapsedTime;
        private Quaternion _targetRotation;
        private Quaternion _initialRotation;
        private RepeatedTask _repeatedTask;
        
        public override UIType Type => UIType.Loading;
        public override UICanvasType CanvasType=> UICanvasType.Exception;
        
        [Inject]
        private void Init(RepeatedTask repeatedTask)
        {
            _repeatedTask = repeatedTask;
            logo.SetActive(true);
            logo.transform.rotation = Quaternion.identity;
            _repeatedTask.StartRepeatingTask(RotateLogo, rotationInterval);
        }

        private void RotateLogo()
        {
            _elapsedTime = 0f;
            _initialRotation = logo.transform.rotation;
            _targetRotation = Quaternion.Euler(_initialRotation.eulerAngles + new Vector3(0f, 0, -rotationSpeed));
            while (_elapsedTime < rotationInterval)
            {
                logo.transform.rotation = Quaternion.Slerp(_initialRotation, _targetRotation, _elapsedTime / rotationInterval);
                _elapsedTime += Time.deltaTime;
            }
            logo.transform.rotation = _targetRotation;
        }

        private void OnDestroy()
        {
            _repeatedTask.StopRepeatingTask(RotateLogo);
        }
    }
}
