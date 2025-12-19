using System.Collections;
using UnityEngine;

namespace HotUpdate.Scripts.Collector.Effect
{
    public class SweepParticleSystem : MonoBehaviour
    {
        [Header("粒子系统")] public ParticleSystem sweepParticleSystem;
        public ParticleSystem hitParticleSystem;
        public TrailRenderer weaponTrail;

        [Header("横扫参数")] public float sweepRadius = 2f;
        public float sweepAngle = 180f; // 半圆形扫射
        public float sweepDuration = 0.5f;
        public AnimationCurve sweepCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("攻击频率影响")] public float baseSweepSpeed = 1f;
        public float normalSpeedMultiplier = 1f;
        public float fastSpeedMultiplier = 1.5f;
        public float superFastSpeedMultiplier = 2f;

        [Header("攻击力影响")] public Color normalColor = Color.yellow;
        public Color strongColor = Color.red;
        public Color superColor = Color.blue;
        public float normalSize = 1f;
        public float strongSize = 1.5f;
        public float superSize = 2f;

        [Header("目标点（可选）")] public Transform targetPoint;

        private Coroutine _sweepCoroutine;
        private ParticleSystem.MainModule _mainModule;
        private ParticleSystem.MainModule _hitMain;

        void Start()
        {
            if (sweepParticleSystem)
            {
                _mainModule = sweepParticleSystem.main;
            }

            if (hitParticleSystem)
            {
                _hitMain = hitParticleSystem.main;
            }

            // 初始状态
            SetTrailActive(false);
        }

        public void TriggerSweep(AttackPowerLevel powerLevel, AttackSpeedLevel speedLevel)
        {
            // 停止正在进行的横扫
            if (_sweepCoroutine != null)
                StopCoroutine(_sweepCoroutine);

            // 开始新的横扫
            _sweepCoroutine = StartCoroutine(SweepAnimation(powerLevel, speedLevel));
        }

        private IEnumerator SweepAnimation(AttackPowerLevel powerLevel, AttackSpeedLevel speedLevel)
        {
            // 1. 根据攻击力设置粒子效果
            SetupParticlesByPower(powerLevel);

            // 2. 根据攻击频率计算横扫速度
            float speedMultiplier = GetSpeedMultiplier(speedLevel);
            float currentSweepDuration = sweepDuration / speedMultiplier;

            // 3. 激活武器轨迹
            SetTrailActive(true);

            // 4. 开始横扫动画
            float startAngle = -sweepAngle / 2;
            float endAngle = sweepAngle / 2;

            float elapsedTime = 0f;
            while (elapsedTime < currentSweepDuration)
            {
                elapsedTime += Time.deltaTime;
                float t = elapsedTime / currentSweepDuration;
                t = sweepCurve.Evaluate(t);

                // 计算当前角度
                float currentAngle = Mathf.Lerp(startAngle, endAngle, t);

                // 计算横扫位置
                Vector3 sweepPosition = CalculateSweepPosition(currentAngle);

                // 更新粒子系统位置
                UpdateParticlePosition(sweepPosition, currentAngle);

                yield return null;
            }

            // 5. 播放打击特效
            PlayHitEffect();

            // 6. 停用武器轨迹
            SetTrailActive(false);

            _sweepCoroutine = null;
        }

        private void SetupParticlesByPower(AttackPowerLevel powerLevel)
        {
            if (!sweepParticleSystem) return;

            var emission = sweepParticleSystem.emission;
            var shape = sweepParticleSystem.shape;
            var colorOverLifetime = sweepParticleSystem.colorOverLifetime;

            switch (powerLevel)
            {
                case AttackPowerLevel.Normal:
                    _mainModule.startColor = normalColor;
                    _mainModule.startSize = normalSize;
                    emission.rateOverTime = 50f;
                    break;

                case AttackPowerLevel.Strong:
                    _mainModule.startColor = strongColor;
                    _mainModule.startSize = strongSize;
                    emission.rateOverTime = 100f;
                    break;

                case AttackPowerLevel.Super:
                    _mainModule.startColor = superColor;
                    _mainModule.startSize = superSize;
                    emission.rateOverTime = 200f;
                    break;
            }

            // 设置形状为弧形
            shape.arc = sweepAngle;
            shape.radius = sweepRadius;

            // 播放粒子系统
            sweepParticleSystem.Play();
        }

        private float GetSpeedMultiplier(AttackSpeedLevel speedLevel)
        {
            switch (speedLevel)
            {
                case AttackSpeedLevel.Normal:
                    return normalSpeedMultiplier;
                case AttackSpeedLevel.Fast:
                    return fastSpeedMultiplier;
                case AttackSpeedLevel.SuperFast:
                    return superFastSpeedMultiplier;
                default:
                    return baseSweepSpeed;
            }
        }

        private Vector3 CalculateSweepPosition(float angle)
        {
            // 将角度转换为弧度
            float radian = angle * Mathf.Deg2Rad;

            // 计算位置（以transform.forward为0度）
            Vector3 direction = Quaternion.Euler(0, angle, 0) * transform.forward;
            Vector3 position = transform.position + direction * sweepRadius;

            return position;
        }

        private void UpdateParticlePosition(Vector3 position, float angle)
        {
            // 更新横扫粒子系统的旋转
            if (sweepParticleSystem)
            {
                Vector3 rotation = sweepParticleSystem.transform.eulerAngles;
                rotation.y = transform.eulerAngles.y + angle;
                sweepParticleSystem.transform.eulerAngles = rotation;

                // 如果有目标点，朝向目标
                if (targetPoint)
                {
                    sweepParticleSystem.transform.LookAt(targetPoint);
                }
            }
        }

        private void PlayHitEffect()
        {
            if (!hitParticleSystem) return;

            // 在目标点播放打击特效
            if (targetPoint)
            {
                hitParticleSystem.transform.position = targetPoint.position;
            }
            else
            {
                // 在前方播放打击特效
                Vector3 hitPosition = transform.position + transform.forward * sweepRadius;
                hitParticleSystem.transform.position = hitPosition;
            }

            hitParticleSystem.Play();
        }

        private void SetTrailActive(bool active)
        {
            if (weaponTrail)
            {
                weaponTrail.enabled = active;
                if (!active)
                {
                    weaponTrail.Clear();
                }
            }
        }

        // 工具方法：在编辑器中可视化横扫范围
        void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying)
            {
                Gizmos.color = Color.yellow;

                int segments = 36;
                float segmentAngle = sweepAngle / segments;

                Vector3 previousPoint = CalculateSweepPosition(-sweepAngle / 2);

                for (int i = 1; i <= segments; i++)
                {
                    float angle = -sweepAngle / 2 + segmentAngle * i;
                    Vector3 point = CalculateSweepPosition(angle);

                    Gizmos.DrawLine(previousPoint, point);
                    previousPoint = point;
                }

                // 绘制起始和结束线
                Gizmos.DrawLine(transform.position, CalculateSweepPosition(-sweepAngle / 2));
                Gizmos.DrawLine(transform.position, CalculateSweepPosition(sweepAngle / 2));
            }
        }
    }
}