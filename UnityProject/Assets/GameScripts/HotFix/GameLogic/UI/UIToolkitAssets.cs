using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TEngine;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameLogic.UI
{
    /// <summary>
    /// UI Toolkit 资源加载与缓存。location = YooAsset 地址（当前工程按文件名寻址）。
    /// 注意：不要命名为 UIResourceLoader——GameLogic 下已有 TEngine UIModule 的同名类型会遮蔽它。
    /// </summary>
    public static class UIToolkitAssets
    {
        public const string CommonStyleLocation = "TakeoverCommonStyles";
        public const string ScreenStyleLocation = "TakeoverScreenStyles";
        public const string BattleStyleLocation = "TakeoverBattleStyles";

        private static readonly Dictionary<string, UnityEngine.Object> Assets =
            new Dictionary<string, UnityEngine.Object>();

        public static async UniTask<VisualTreeAsset> LoadVisualTreeAsync(
            string location, CancellationToken token = default)
        {
            return await LoadAsync<VisualTreeAsset>(location, token);
        }

        public static async UniTask<StyleSheet> LoadStyleSheetAsync(
            string location, CancellationToken token = default)
        {
            return await LoadAsync<StyleSheet>(location, token);
        }

        public static async UniTask<Sprite> LoadSpriteAsync(
            string location, CancellationToken token = default)
        {
            return await LoadAsync<Sprite>(location, token);
        }

        /// <summary>
        /// VisualTreeAsset.Instantiate() 返回的是 TemplateContainer；这里剥掉包装，
        /// 直接返回 UXML 的根 VisualElement，方便定位/命中判断和样式挂载。
        /// </summary>
        public static VisualElement InstantiateContent(VisualTreeAsset tree)
        {
            if (tree == null) return null;
            TemplateContainer container = tree.Instantiate();
            if (container == null) return null;
            if (container.childCount == 0) return container;

            VisualElement content = container[0];
            content.RemoveFromHierarchy();
            return content;
        }

        private static async UniTask<T> LoadAsync<T>(string location, CancellationToken token)
            where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(location)) return null;
            if (Assets.TryGetValue(location, out UnityEngine.Object cached))
                return cached as T;

            try
            {
                T asset = await GameModule.Resource.LoadAssetAsync<T>(location, token);
                if (asset == null) asset = LoadEditorFallback<T>(location);

                if (asset == null)
                {
                    Log.Error($"[TakeoverUI] 加载失败: {typeof(T).Name} location={location} " +
                              $"locationValid={SafeCheckLocation(location)}");
                    return null;
                }

                if (asset is StyleSheet sheet && sheet.importedWithErrors)
                    Log.Error($"[TakeoverUI] USS 导入有错误，样式可能整体失效: {location}");

                Assets[location] = asset;
                return asset;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (Exception exception)
            {
                Log.Error($"[TakeoverUI] 加载 UI 资源失败: {location} -> {exception.Message}");
                return null;
            }
        }

        private static bool SafeCheckLocation(string location)
        {
            try
            {
                return GameModule.Resource.CheckLocationValid(location);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 编辑器兜底：YooAsset 处于 Offline/Host 模式或清单未重建时，直接读 AssetDatabase，
        /// 避免 UXML/USS 没进包导致运行时整屏空白。玩家包仍走 YooAsset。
        /// </summary>
        private static T LoadEditorFallback<T>(string location) where T : UnityEngine.Object
        {
#if UNITY_EDITOR
            string extension = null;
            if (typeof(T) == typeof(VisualTreeAsset)) extension = ".uxml";
            else if (typeof(T) == typeof(StyleSheet)) extension = ".uss";

            if (extension != null)
            {
                string path = $"Assets/AssetRaw/UI/Toolkit/{location}{extension}";
                T asset = UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null)
                {
                    Log.Warning($"[TakeoverUI] YooAsset 未找到 {location}，已用 AssetDatabase 回退: {path}");
                    return asset;
                }
            }

            if (typeof(T) == typeof(Sprite))
            {
                foreach (string folder in new[]
                         {
                             "Assets/AssetRaw/Actor/Takeover/Sprites",
                             "Assets/AssetRaw/UI/Icons",
                         })
                {
                    string path = $"{folder}/{location}.png";
                    T asset = UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
                    if (asset != null) return asset;
                }
            }
#endif
            return null;
        }

        /// <summary>整个 UI 释放时统一归还；失败不应影响退出流程。</summary>
        public static void ReleaseAll()
        {
            foreach (KeyValuePair<string, UnityEngine.Object> pair in Assets)
            {
                try
                {
                    if (pair.Value != null) GameModule.Resource.UnloadAsset(pair.Value);
                }
                catch (Exception exception)
                {
                    Log.Warning($"[TakeoverUI] 释放 UI 资源失败 {pair.Key}: {exception.Message}");
                }
            }
            Assets.Clear();
        }
    }
}
