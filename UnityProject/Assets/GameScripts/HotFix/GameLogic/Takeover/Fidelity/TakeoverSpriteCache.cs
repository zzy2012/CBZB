using System;
using System.Collections.Generic;
using TEngine;
using UnityEngine;

namespace GameLogic.Takeover
{
    /// <summary>
    /// Explicit YooAsset sprite cache for world rendering. Unlike TEngine's
    /// generic SetSprite helper, this path does not depend on
    /// ResourceExtComponent's delayed object-pool initialization.
    /// </summary>
    public static class TakeoverSpriteCache
    {
        private static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();
        private static readonly HashSet<string> Loading = new HashSet<string>();

        public static void Apply(SpriteRenderer renderer, string location,
            Action<SpriteRenderer> callback = null)
        {
            if (renderer == null || string.IsNullOrEmpty(location)) return;
            if (Sprites.TryGetValue(location, out var cached))
            {
                Assign(renderer, cached, callback);
                return;
            }

            Load(renderer, location, callback);
        }

        private static async void Load(SpriteRenderer renderer, string location,
            Action<SpriteRenderer> callback)
        {
            if (!Loading.Add(location))
            {
                while (Loading.Contains(location)) await Cysharp.Threading.Tasks.UniTask.Yield();
                if (Sprites.TryGetValue(location, out var late))
                    Assign(renderer, late, callback);
                return;
            }

            try
            {
                if (!GameModule.Resource.CheckLocationValid(location)) return;
                var sprite = await GameModule.Resource.LoadAssetAsync<Sprite>(location);
                if (sprite == null) return;
                Sprites[location] = sprite;
                Assign(renderer, sprite, callback);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Takeover] 贴图加载失败 {location}: {exception.Message}");
            }
            finally
            {
                Loading.Remove(location);
            }
        }

        private static void Assign(SpriteRenderer renderer, Sprite sprite,
            Action<SpriteRenderer> callback)
        {
            if (renderer == null) return;
            renderer.sprite = sprite;
            callback?.Invoke(renderer);
        }

        public static void Clear()
        {
            foreach (var sprite in Sprites.Values)
            {
                if (sprite != null) GameModule.Resource.UnloadAsset(sprite);
            }
            Sprites.Clear();
            Loading.Clear();
        }
    }
}
