using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TEngine;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic.Takeover
{
    internal static class TakeoverUIResources
    {
        private const string FontLocation = "takeover_font_328_Chaparral_Pro";

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

        public static void ApplyFont(GameObject root, Font font)
        {
            if (root == null || font == null) return;
            foreach (Text text in root.GetComponentsInChildren<Text>(true))
                text.font = font;
        }

        public static void ReleaseFont(Font font)
        {
            if (font != null) GameModule.Resource.UnloadAsset(font);
        }
    }
}
