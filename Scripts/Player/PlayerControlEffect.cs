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
        private static readonly int Transparency = Shader.PropertyToID("_Transparency");

        [SerializeField] private SkinnedMeshRenderer[] originalMesh;
        [SerializeField] private SkinnedMeshRenderer[] effectMesh;
        private MaterialPropertyBlock _effectMaterialPropertyBlock;
        private CancellationTokenSource _effectTokenSource;
        private CancellationTokenSource _transparencyTokenSource;

        private void Start()
        {
            _effectMaterialPropertyBlock = new MaterialPropertyBlock();
        }

        public void SetEffect(ControlSkillType controlSkillType, float duration = 0f)
        {
            if (controlSkillType == ControlSkillType.None)
            {
                _effectTokenSource?.Cancel();
                return;
            }
            if (effectMesh == null || effectMesh.Length == 0)
                return;
            _effectTokenSource?.Cancel();
            _effectTokenSource = new CancellationTokenSource();
            for (int i = 0; i < originalMesh.Length; i++)
            {
                var originalMeshRenderer = originalMesh[i];
                originalMeshRenderer.enabled = false;
            }
            
            for (int i = 0; i < effectMesh.Length; i++)
            {
                var effectMeshRenderer = effectMesh[i];
                effectMeshRenderer.enabled = true;
            }

            var iceAmount = controlSkillType == ControlSkillType.Frozen ? 1 : 0;
            var snowAmount = controlSkillType == ControlSkillType.Slowdown ? 1 : 0;
            var stoneAmount = controlSkillType == ControlSkillType.Stoned ? 1 : 0;

            // 设置效果参数
            _effectMaterialPropertyBlock.SetFloat(IceAmount, iceAmount);
            _effectMaterialPropertyBlock.SetFloat(SnowAmount, snowAmount);
            _effectMaterialPropertyBlock.SetFloat(StoneAmount, stoneAmount);

            // 设置UV变换
            // _effectMaterialPropertyBlock.SetVector("_IceNoise_ST", new Vector4(1, 1, 0, _Time.y * 0.1f));
            // _effectMaterialPropertyBlock.SetVector("_StoneDetail_ST", new Vector4(5, 5, 0, 0));

            for (int i = 0; i < effectMesh.Length; i++)
            {
                var effectMeshRenderer = effectMesh[i];
                effectMeshRenderer.SetPropertyBlock(_effectMaterialPropertyBlock);
            }
            if (duration == 0f)
            {
                return;
            }
            DelayInvoker.DelayInvoke(duration, () =>
            {
                for (int i = 0; i < effectMesh.Length; i++)
                {
                    var effectMeshRenderer = effectMesh[i];
                    effectMeshRenderer.enabled = false;
                }
                for (int i = 0; i < originalMesh.Length; i++)
                {
                    var originalMeshRenderer = originalMesh[i];
                    originalMeshRenderer.enabled = true;
                }
            }, token: _effectTokenSource.Token);
        }

        public void SetTransparency(float transparency, float duration = 0f)
        {
            if (effectMesh == null || effectMesh.Length == 0)
            {
                return;
            }
            _transparencyTokenSource?.Cancel();
            _transparencyTokenSource = new CancellationTokenSource();
            
            for (int i = 0; i < originalMesh.Length; i++)
            {
                var originalMeshRenderer = originalMesh[i];
                originalMeshRenderer.enabled = false;
            }
            
            for (int i = 0; i < effectMesh.Length; i++)
            {
                var effectMeshRenderer = effectMesh[i];
                effectMeshRenderer.enabled = true;
            }
            _effectMaterialPropertyBlock.SetFloat(Transparency, transparency);
            for (int i = 0; i < effectMesh.Length; i++)
            {
                var effectMeshRenderer = effectMesh[i];
                effectMeshRenderer.SetPropertyBlock(_effectMaterialPropertyBlock);
            }
            if (duration!=0f)
            {
                DelayInvoker.DelayInvoke(duration, () =>
                {
                    for (int i = 0; i < effectMesh.Length; i++)
                    {
                        var effectMeshRenderer = effectMesh[i];
                        effectMeshRenderer.enabled = false;
                    }
                    for (int i = 0; i < originalMesh.Length; i++)
                    {
                        var originalMeshRenderer = originalMesh[i];
                        originalMeshRenderer.enabled = true;
                    }
                }, token: _transparencyTokenSource.Token);
            }
        }
    }
}