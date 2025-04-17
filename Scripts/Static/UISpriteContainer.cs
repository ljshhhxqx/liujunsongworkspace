using System.Collections.Generic;
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

        public static void InitUISprites(SpriteInfo[] sprites)
        {
            for (int i = 0; i < sprites.Length; i++)
            {
                Sprites.Add(sprites[i].Name, sprites[i]);
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