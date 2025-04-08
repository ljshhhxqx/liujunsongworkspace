using HotUpdate.Scripts.Config.ArrayConfig;
using MemoryPack;
using Mirror;
using Sirenix.OdinInspector;
using UnityEngine;

namespace HotUpdate.Scripts.Collector
{
    public class DroppedItem : NetworkBehaviour
    {
        [SerializeField]
        private ParticleSystem itemGlowPS;    // 物品发光特效
        [SerializeField]
        private ParticleSystem beamPS;        // 光束特效
        
        void Start()
        {
            SetupItemGlowEffect();
            SetupBeamEffect();
        }

        // 设置物品发光特效
        [Button("Setup Item Glow Effect")]
        private void SetupItemGlowEffect()
        {
            // 创建物品发光粒子系统
            itemGlowPS = gameObject.GetComponent<ParticleSystem>();
            var main = itemGlowPS.main;
            var emission = itemGlowPS.emission;
            var shape = itemGlowPS.shape;
            var systemRenderer = itemGlowPS.GetComponent<ParticleSystemRenderer>();

            // 主模块设置
            main.loop = true;
            main.playOnAwake = true;
            main.duration = 1f;
            main.startLifetime = 1f;
            main.startSize = 1f;
            main.startColor = new Color(1f, 1f, 1f, 0.5f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            // 发射模块设置
            emission.rateOverTime = 10;

            // 形状模块设置
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.5f;
            shape.radiusThickness = 0f;

            // 渲染器设置
            systemRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            systemRenderer.material = new Material(Shader.Find("Particles/Additive"));
        }

        // 设置光束特效
        [Button("Setup Item Glow Effect")]
        private void SetupBeamEffect()
        {
            // 创建光束粒子系统
            GameObject beamObj = new GameObject("Beam");
            beamObj.transform.parent = transform;
            beamObj.transform.localPosition = Vector3.zero;
            
            beamPS = beamObj.AddComponent<ParticleSystem>();
            var main = beamPS.main;
            var emission = beamPS.emission;
            var shape = beamPS.shape;
            var particleSystemRenderer = beamPS.GetComponent<ParticleSystemRenderer>();
            var colorOverLifetime = beamPS.colorOverLifetime;

            // 主模块设置
            main.loop = true;
            main.playOnAwake = true;
            main.duration = 1f;
            main.startLifetime = 2f;
            main.startSpeed = 2f;
            main.startSize = 0.5f;
            main.startColor = new Color(1f, 1f, 1f, 0.3f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0f;

            // 发射模块设置
            emission.rateOverTime = 30;

            // 形状模块设置
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.radius = 0.1f;
            shape.length = 0.1f;
            shape.radiusThickness = 1f;
            shape.arc = 360f;

            // 颜色随生命周期变化
            colorOverLifetime.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { 
                    new GradientColorKey(Color.white, 0.0f), 
                    new GradientColorKey(Color.white, 1.0f) 
                },
                new GradientAlphaKey[] { 
                    new GradientAlphaKey(0.3f, 0.0f), 
                    new GradientAlphaKey(0f, 1.0f) 
                }
            );
            colorOverLifetime.color = gradient;

            // 渲染器设置
            particleSystemRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            particleSystemRenderer.material = new Material(Shader.Find("Particles/Additive"));
            particleSystemRenderer.sortMode = ParticleSystemSortMode.Distance;
        }

        // 播放特效
        public void PlayEffects()
        {
            itemGlowPS.Play();
            beamPS.Play();
        }

        // 停止特效
        public void StopEffects()
        {
            itemGlowPS.Stop();
            beamPS.Stop();
        }
    }
    
    [MemoryPackable]
    public partial struct DroppedItemSceneData
    {
        [MemoryPackOrder(0)]
        public int ItemId;
        [MemoryPackOrder(1)] 
        public int ConfigId;
        [MemoryPackOrder(2)] 
        public QualityType Quality;
    }
}