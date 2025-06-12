using System.Threading;
using HotUpdate.Scripts.Network.PredictSystem.UI;
using HotUpdate.Scripts.Tool.Coroutine;
using UnityEngine;

namespace HotUpdate.Scripts.Player
{
    public class PlayerControlEffect : MonoBehaviour
    {
        private static readonly int IceAmount = Shader.PropertyToID("_IceAmount");
        private static readonly int SnowAmount = Shader.PropertyToID("_SnowAmount");
        private static readonly int StoneAmount = Shader.PropertyToID("_StoneAmount");

        [SerializeField] private SkinnedMeshRenderer originalMesh;
        [SerializeField] private SkinnedMeshRenderer effectMesh;
        private MaterialPropertyBlock _effectMaterialPropertyBlock;
        private CancellationTokenSource _effectTokenSource;
        private CancellationTokenSource _transparencyTokenSource;

        private void Start()
        {
            _effectMaterialPropertyBlock = new MaterialPropertyBlock();
        }

        public void SetEffect(PlayerControlData effectData)
        {
            if (!effectMesh || effectData.ControlSkill == ControlSkillType.None)
                return;
            _effectTokenSource?.Cancel();
            _effectTokenSource = new CancellationTokenSource();
            originalMesh.enabled = false;
            effectMesh.enabled = true;

            var iceAmount = effectData.ControlSkill == ControlSkillType.Frozen ? 1 : 0;
            var snowAmount = effectData.ControlSkill == ControlSkillType.Slowdown ? 1 : 0;
            var stoneAmount = effectData.ControlSkill == ControlSkillType.Stoned ? 1 : 0;

            // 设置效果参数
            _effectMaterialPropertyBlock.SetFloat(IceAmount, iceAmount);
            _effectMaterialPropertyBlock.SetFloat(SnowAmount, snowAmount);
            _effectMaterialPropertyBlock.SetFloat(StoneAmount, stoneAmount);

            // 设置UV变换
            // _effectMaterialPropertyBlock.SetVector("_IceNoise_ST", new Vector4(1, 1, 0, _Time.y * 0.1f));
            // _effectMaterialPropertyBlock.SetVector("_StoneDetail_ST", new Vector4(5, 5, 0, 0));

            effectMesh.SetPropertyBlock(_effectMaterialPropertyBlock);
            DelayInvoker.DelayInvoke(effectData.Duration, () =>
            {
                originalMesh.enabled = true;
                effectMesh.enabled = false;
            }, token: _effectTokenSource.Token);
        }

        public void SetTransparency(float transparency, float duration)
        {
            if (!effectMesh)
            {
                return;
            }
            _transparencyTokenSource?.Cancel();
            _transparencyTokenSource = new CancellationTokenSource();
            originalMesh.enabled = false;
            effectMesh.enabled = true;
            _effectMaterialPropertyBlock.SetFloat("_Transparency", transparency);
            effectMesh.SetPropertyBlock(_effectMaterialPropertyBlock);
            DelayInvoker.DelayInvoke(duration, () =>
            {
                originalMesh.enabled = true;
                effectMesh.enabled = false;
            }, token: _transparencyTokenSource.Token);
        }
    }
}