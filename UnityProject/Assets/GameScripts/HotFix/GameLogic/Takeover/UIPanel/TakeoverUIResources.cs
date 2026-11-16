using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TEngine;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic.Takeover
{
    /// <summary>窗口若暴露了原版字体，动态列表按钮创建时会自动套用。</summary>
    public interface ITakeoverFontSource
    {
        /// <summary>界面使用的字体。</summary>
        Font Font { get; }
    }

    /// <summary>负责加载、应用和释放 Takeover 界面字体资源。</summary>
    internal static class TakeoverUIResources
    {
        /// <summary>Takeover 界面字体的资源位置。</summary>
        private const string FontLocation = "takeover_font_328_Chaparral_Pro";

        /// <summary>异步加载界面字体并应用到指定对象层级。</summary>
        public static async UniTask<Font> LoadAndApplyFontAsync(GameObject root, CancellationToken token)
        {
            Text templateText = root.GetComponentInChildren<Text>(true);
            Font fallbackFont = templateText != null ? templateText.font : null;
            Font loadedFont = null;
            try
            {
                loadedFont = await GameModule.Resource.LoadAssetAsync<Font>(FontLocation, token);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (Exception exception)
            {
                Log.Warning($"[Takeover] 字体加载失败，使用界面默认字体: {exception.Message}");
            }

            if (token.IsCancellationRequested)
            {
                if (loadedFont != null) GameModule.Resource.UnloadAsset(loadedFont);
                return null;
            }

            Font font = loadedFont != null ? loadedFont : fallbackFont;
            if (font != null)
            {
                foreach (Text text in root.GetComponentsInChildren<Text>(true))
                    text.font = font;
            }
            return loadedFont;
        }

        /// <summary>将字体应用到指定对象层级中的文本。</summary>
        public static void ApplyFont(GameObject root, Font font)
        {
            if (root == null || font == null) return;
            foreach (Text text in root.GetComponentsInChildren<Text>(true))
                text.font = font;
        }

        /// <summary>释放界面持有的字体资源。</summary>
        public static void ReleaseFont(Font font)
        {
            if (font != null) GameModule.Resource.UnloadAsset(font);
        }
    }
}
