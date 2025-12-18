using System.Collections;
using HotUpdate.Scripts.Collector.Collects;
using UnityEngine;

namespace HotUpdate.Scripts.Collector.Effect
{
    public class AttackStateController : MonoBehaviour
    {
        [Header("状态")] 
        public AttackState currentState = AttackState.Searching;
        public bool hasTarget = false;

        [Header("效果控制器")] public EffectController effectController;
        public AttackEffectMapper effectMapper;

        [Header("动画参数")] 
        public float searchDistortionSpeed = 1f; // 寻敌时的扭曲速度
        public float attackDistortionSpeed = 2f; // 发现敌人时的扭曲速度（2倍）
        public float attackAnimationMultiplier = 3f; // 攻击时肢解/闪光动画速度倍率

        [Header("时间控制")] public float attackCooldown = 0.5f;
        private float _lastAttackTime = 0f;
        private Coroutine _attackCoroutine;

        public enum AttackState
        {
            Searching, // 寻敌状态
            FoundTarget, // 发现敌人
            Attacking, // 攻击中
            Cooldown // 冷却中
        }

        void Start()
        {
            if (!effectController)
                effectController = GetComponent<EffectController>();

            if (!effectMapper)
                effectMapper = GetComponent<AttackEffectMapper>();

            // 初始状态：缓慢播放扭曲动画
            StartSearchingAnimation();
        }

        public void UpdateAttackState()
        {
            if (hasTarget)
            {
                if (currentState != AttackState.Attacking)
                {
                    SetState(AttackState.FoundTarget);
                    StartFoundTargetAnimation();
                }
            }
            else
            {
                SetState(AttackState.Searching);
                StartSearchingAnimation();
            }
        }

        public void TriggerAttack()
        {
            if (currentState is AttackState.Attacking or AttackState.Cooldown)
                return;

            if (Time.time - _lastAttackTime < attackCooldown)
                return;

            SetState(AttackState.Attacking);

            // 停止正在进行的动画协程
            if (_attackCoroutine != null)
                StopCoroutine(_attackCoroutine);

            // 开始攻击动画序列
            _attackCoroutine = StartCoroutine(AttackAnimationSequence());
        }

        private void SetState(AttackState newState)
        {
            AttackState previousState = currentState;
            currentState = newState;

            Debug.Log($"Attack State Changed: {previousState} -> {newState}");
        }

        private void StartSearchingAnimation()
        {
            // 缓慢播放扭曲动画
            if (effectController)
            {
                effectController.SetDistortionSpeed(searchDistortionSpeed);
                effectController.SetDistortionIntensity(effectMapper.distortionIntensity * 0.5f); // 寻敌时强度减半
                effectController.SetFlashIntensity(0f); // 不闪光
            }
        }

        private void StartFoundTargetAnimation()
        {
            // 快速播放扭曲动画（2倍速度）
            if (effectController)
            {
                effectController.SetDistortionSpeed(attackDistortionSpeed);
                effectController.SetDistortionIntensity(effectMapper.distortionIntensity);
            }
        }

        private IEnumerator AttackAnimationSequence()
        {
            float duration = effectMapper.attackDuration; // 总攻击时间不超过1秒

            // 阶段1：停止扭曲，开始肢解和闪光
            float phase1Duration = duration * 0.3f;
            yield return StartCoroutine(AttackPhase1(phase1Duration));

            // 阶段2：保持攻击效果
            float phase2Duration = duration * 0.4f;
            yield return StartCoroutine(AttackPhase2(phase2Duration));

            // 阶段3：收回到原状
            float phase3Duration = duration * 0.3f;
            yield return StartCoroutine(AttackPhase3(phase3Duration));

            // 根据是否还有敌人决定下一步
            if (hasTarget)
            {
                SetState(AttackState.FoundTarget);
                StartFoundTargetAnimation();
            }
            else
            {
                SetState(AttackState.Searching);
                StartSearchingAnimation();
            }

            _lastAttackTime = Time.time;
            _attackCoroutine = null;
        }

        private IEnumerator AttackPhase1(float duration)
        {
            // 停止扭曲动画
            effectController.SetDistortionIntensity(0f);

            // 快速播放肢解动画
            float startDisintegration = 0f;
            float targetDisintegration = effectMapper.disintegrationIntensity;
            float startTime = Time.time;

            while (Time.time - startTime < duration)
            {
                float t = (Time.time - startTime) / duration;
                float currentDisintegration = Mathf.Lerp(startDisintegration,
                    targetDisintegration,
                    t * attackAnimationMultiplier);

                effectController.SetDisintegrationIntensity(currentDisintegration);
                yield return null;
            }

            effectController.SetDisintegrationIntensity(targetDisintegration);
        }

        private IEnumerator AttackPhase2(float duration)
        {
            // 播放闪光动画
            float flashCycleDuration = 0.1f / effectMapper.animationSpeed; // 闪光周期受攻击频率影响

            float startTime = Time.time;
            while (Time.time - startTime < duration)
            {
                // 脉冲式闪光
                float pulse = Mathf.Sin((Time.time - startTime) * Mathf.PI * 2 / flashCycleDuration) * 0.5f + 0.5f;
                float currentFlashIntensity = effectMapper.flashIntensity * pulse;

                effectController.SetFlashIntensity(currentFlashIntensity);
                yield return null;
            }
        }

        private IEnumerator AttackPhase3(float duration)
        {
            // 快速收回到原状
            float startDisintegration = effectController.GetDisintegrationIntensity();
            float startFlash = effectController.GetFlashIntensity();

            float startTime = Time.time;
            while (Time.time - startTime < duration)
            {
                float t = (Time.time - startTime) / duration;
                float currentDisintegration = Mathf.Lerp(startDisintegration, 0f, t * attackAnimationMultiplier);
                float currentFlash = Mathf.Lerp(startFlash, 0f, t * attackAnimationMultiplier);

                effectController.SetDisintegrationIntensity(currentDisintegration);
                effectController.SetFlashIntensity(currentFlash);
                yield return null;
            }

            effectController.SetDisintegrationIntensity(0f);
            effectController.SetFlashIntensity(0f);
        }

        // 外部调用接口
        public void SetHasTarget(bool hasTarget)
        {
            this.hasTarget = hasTarget;
            UpdateAttackState();
        }

        public void StartAttack(float attackPower, float attackInterval)
        {
            // 更新攻击参数
            if (effectMapper)
            {
                effectMapper.SetAttackParameters(attackPower, attackInterval);
            }

            TriggerAttack();
        }
    }
}