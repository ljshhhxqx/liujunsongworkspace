using System.Collections.Generic;
using HotUpdate.Scripts.Config.ArrayConfig;
using UnityEngine;

namespace HotUpdate.Scripts.Static
{
    public static class UISpriteContainer
    {
        private static readonly Dictionary<string, SpriteInfo> Sprites = new Dictionary<string, SpriteInfo>();

        public static Sprite GetSprite(string name)
        {
            if (Sprites.TryGetValue(name, out var sprite))
            {
                return sprite.Sprite;
            }

            Debug.LogWarning($"UISpriteContainer: {name} not found in the container.");
            return null;
        }

        public static Sprite GetQualitySprite(QualityType quality)
        {
            if (Sprites.TryGetValue(quality.ToString(), out var sprite))
            {
                return sprite.Sprite;
            }

            Debug.LogWarning($"UISpriteContainer: {quality} not found in the container.");
            return null;
        }

        public static void InitUISprites(SpriteInfo[] sprites)
        {
            for (int i = 0; i < sprites.Length; i++)
            {
                Sprites.TryAdd(sprites[i].Name, sprites[i]);
            }
        }

        public static bool RemoveSprite(string name)
        {
            return Sprites.Remove(name);
        }
        
        public static bool ContainsSprite(string name)
        {
            return Sprites.ContainsKey(name);   
        }
        
        public static void Clear()
        {
            Sprites.Clear();
        }
    }

    public struct SpriteInfo
    {
        public string Name;
        public Sprite Sprite;
    }
}