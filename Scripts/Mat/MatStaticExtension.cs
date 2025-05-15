using System.Collections.Generic;
using UnityEngine;

namespace HotUpdate.Scripts.Mat
{
    public static class MatStaticExtension
    {
        private static readonly Dictionary<Renderer, MaterialPropertyBlock> MatProps = new Dictionary<Renderer, MaterialPropertyBlock>();
        private static readonly int Mode = Shader.PropertyToID("_Mode");

        // 获取或创建 MaterialPropertyBlock
        public static MaterialPropertyBlock GetPropertyBlock(Renderer renderer)
        {
            if (!MatProps.TryGetValue(renderer, out var propBlock))
            {
                propBlock = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(propBlock); // 同步当前材质属性（可选）
                MatProps.Add(renderer, propBlock);
            }
            return propBlock;
        }

        // 清理缓存（如对象被销毁时）
        public static void ClearCache(Renderer renderer)
        {
            if (MatProps.ContainsKey(renderer))
            {
                MatProps.Remove(renderer);
            }
        }

        public static StandardShaderType GetStandardShaderType(MaterialPropertyBlock propBlock)
        {
            int shaderType = propBlock.GetInt(Mode);
            switch (shaderType)
            {
                case 0:
                    return StandardShaderType.Opaque;
                case 1:
                    return StandardShaderType.Cutout;
                case 2:
                    return StandardShaderType.Fade;
                case 3:
                    return StandardShaderType.Transparent;
                default:
                    return StandardShaderType.Opaque;
            }
        }
    }
    
    public enum StandardShaderType
    {
        Opaque,
        Cutout,
        Fade,
        Transparent
    }
}