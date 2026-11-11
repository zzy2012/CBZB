using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic.UI
{
    public static class UISpriteCache
    {
        private static readonly Dictionary<string, Sprite> Sprites =
            new Dictionary<string, Sprite>();

        public static async UniTask<Sprite> LoadAsync(string location,
            CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(location)) return null;
            if (Sprites.TryGetValue(location, out Sprite sprite) && sprite != null)
                return sprite;

#if UNITY_EDITOR
            sprite = LoadEditorSprite(location);
            if (sprite != null)
            {
                Sprites[location] = sprite;
                return sprite;
            }
#endif
            sprite = await GameModule.Resource.LoadAssetAsync<Sprite>(location, token);
            token.ThrowIfCancellationRequested();
            if (sprite != null) Sprites[location] = sprite;
            return sprite;
        }

        public static async UniTask ApplyAsync(Image image, string location,
            CancellationToken token = default)
        {
            if (image == null || string.IsNullOrEmpty(location)) return;
            Sprite sprite = await LoadAsync(location, token);
            if (sprite != null && image != null) image.sprite = sprite;
        }

        public static void SetTint(Image image, Color color)
        {
            if (image != null) image.color = color;
        }

        public static void ReleaseAll()
        {
            Sprites.Clear();
        }

#if UNITY_EDITOR
        private static Sprite LoadEditorSprite(string location)
        {
            foreach (string folder in new[]
                     {
                         "Assets/AssetRaw/Actor/Takeover/Sprites",
                         "Assets/AssetRaw/Scenes/Takeover/Maps",
                         "Assets/AssetRaw/UI/Icons",
                     })
            {
                Sprite sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                    $"{folder}/{location}.png");
                if (sprite != null) return sprite;
            }

            return null;
        }
#endif
    }
}
