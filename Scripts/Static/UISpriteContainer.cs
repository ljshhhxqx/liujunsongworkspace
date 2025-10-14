using System.Collections.Generic;
using HotUpdate.Scripts.Config.ArrayConfig;
using UnityEngine;

namespace HotUpdate.Scripts.Static
{
    public static class UISpriteContainer
    {
        private static readonly Dictionary<string, Sprite> CurrentSprites = new Dictionary<string, Sprite>();
        private static readonly Dictionary<string, Dictionary<string, Sprite>> SpritesByName = new Dictionary<string, Dictionary<string, Sprite>>();

        public static Sprite GetSprite(string name)
        {
            if (CurrentSprites.TryGetValue(name, out var sprite))
            {
                return sprite;
            }

            Debug.LogWarning($"UISpriteContainer: {name} not found in the container.");
            return null;
        }

        public static Sprite GetQualitySprite(QualityType quality)
        {
            if (CurrentSprites.TryGetValue(quality.ToString(), out var sprite))
            {
                return sprite;
            }

            Debug.LogWarning($"UISpriteContainer: {quality} not found in the container.");
            return null;
        }

        public static void InitUISprites(string path, Dictionary<string, Sprite> spriteInfos)
        {
            CurrentSprites.Clear();
            SpritesByName.Clear();
            foreach (var spriteInfo in spriteInfos)
            {
                CurrentSprites.Add(spriteInfo.Key, spriteInfo.Value);
            }
            SpritesByName.Add(path, spriteInfos);
        }

        public static bool RemoveSprite(string name)
        {
            return CurrentSprites.Remove(name);
        }
        
        public static bool ContainsSprite(string name)
        {
            return CurrentSprites.ContainsKey(name);   
        }
        
        public static void Clear(string path)
        {
            if (SpritesByName.ContainsKey(path))
            {
                SpritesByName.Remove(path);
            }
            CurrentSprites.Clear();
        }
    }

    public struct SpriteInfo
    {
        public string Name;
        public Sprite Sprite;
    }
}