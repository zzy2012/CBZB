using System;
#if UNITY_EDITOR
using System.Collections.Generic;
#endif
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace GameLogic.Takeover
{
    /// <summary>
    /// 将世界场景精灵加载转交给 TEngine 共享资源缓存。
    /// </summary>
    public static class TakeoverSpriteCache
    {
#if UNITY_EDITOR
        private static readonly Dictionary<string, Sprite> EditorSprites =
            new Dictionary<string, Sprite>();
#endif

        public static bool Apply(SpriteRenderer renderer, string location,
            Action<SpriteRenderer> callback = null)
        {
            if (renderer == null || string.IsNullOrEmpty(location)) return false;
#if UNITY_EDITOR
            Sprite editorSprite = LoadEditorSprite(location);
            if (editorSprite != null)
            {
                renderer.sprite = editorSprite;
                callback?.Invoke(renderer);
                return true;
            }
#endif
            if (!GameModule.Resource.CheckLocationValid(location)) return false;
            renderer.SetSprite(location, callback, renderer.GetCancellationTokenOnDestroy());
            return true;
        }

#if UNITY_EDITOR
        private static Sprite LoadEditorSprite(string location)
        {
            if (EditorSprites.TryGetValue(location, out Sprite cached) && cached != null)
                return cached;

            foreach (string folder in new[]
                     {
                         "Assets/AssetRaw/Actor/Takeover/Sprites",
                         "Assets/AssetRaw/Scenes/Takeover/Maps",
                         "Assets/AssetRaw/UI/Icons",
                     })
            {
                Sprite sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                    $"{folder}/{location}.png");
                if (sprite == null) continue;
                EditorSprites[location] = sprite;
                return sprite;
            }

            return null;
        }
#endif
    }
}
