using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameLogic.UI
{
    /// <summary>Sprite（兵种/建筑/状态图标）加载的薄封装，资产缓存由 UIResourceLoader 统一管理。</summary>
    public static class UISpriteCache
    {
        public static UniTask<Sprite> LoadAsync(string location, CancellationToken token = default)
        {
            return UIToolkitAssets.LoadSpriteAsync(location, token);
        }

        /// <summary>把 Sprite 设为元素背景；失败保持原样（由调用方兜底颜色/文本）。</summary>
        public static async UniTask ApplyBackgroundAsync(
            VisualElement element, string location, CancellationToken token = default)
        {
            if (element == null || string.IsNullOrEmpty(location)) return;
            Sprite sprite = await UIToolkitAssets.LoadSpriteAsync(location, token);
            if (sprite == null) return;
            element.style.backgroundImage = new StyleBackground(sprite);
        }

        public static void SetTint(VisualElement element, Color color)
        {
            if (element == null) return;
            element.style.unityBackgroundImageTintColor = new StyleColor(color);
        }

        public static void ReleaseAll()
        {
            // UIResourceLoader.ReleaseAll 会统一 UnloadAsset，这里不需要重复处理。
        }
    }
}
