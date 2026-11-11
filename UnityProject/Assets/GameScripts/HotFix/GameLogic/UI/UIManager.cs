using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GameLogic.Takeover.UI;
using TEngine;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GameLogic.UI
{
    /// <summary>Game-specific navigation facade backed by the TEngine UIModule.</summary>
    public static class UIManager
    {
        private static readonly Dictionary<Type, UIView> OpenViews = new Dictionary<Type, UIView>();
        private static readonly Dictionary<UILayerKind, UIView> Current =
            new Dictionary<UILayerKind, UIView>();

        private static bool _initialized;
        private static bool _initializing;
        private static bool _toastOpening;
        private static string _pendingToast;
        private static float _pendingToastSeconds;

        public static bool IsInitialized => _initialized;

        public static async UniTask InitializeAsync(System.Threading.CancellationToken token = default)
        {
            if (_initialized) return;
            if (_initializing)
            {
                await UniTask.WaitUntil(() => _initialized || !_initializing, cancellationToken: token);
                return;
            }

            _initializing = true;
            try
            {
                _ = GameModule.UI;
                await UniTask.Yield(PlayerLoopTiming.Update, token);
                if (UIModule.UIRoot == null)
                    throw new InvalidOperationException("TEngine UIRoot is unavailable.");
                _initialized = true;
            }
            catch (Exception exception)
            {
                Log.Error($"[TakeoverUI] TEngine UIModule 初始化失败: {exception}");
            }
            finally
            {
                _initializing = false;
            }
        }

        public static UniTask<T> ShowScreenAsync<T>(object args = null) where T : UIView, new()
        {
            return OpenViewAsync<T>(UILayerKind.Screen, args);
        }

        public static UniTask<T> OpenPopupAsync<T>(object args = null) where T : UIView, new()
        {
            return OpenViewAsync<T>(UILayerKind.Popup, args);
        }

        public static UniTask<T> OpenOverlayAsync<T>(object args = null) where T : UIView, new()
        {
            return OpenViewAsync<T>(UILayerKind.Overlay, args);
        }

        public static UniTask<T> OpenPersistentAsync<T>(object args = null) where T : UIView, new()
        {
            return OpenViewAsync<T>(UILayerKind.Persistent, args);
        }

        public static bool Close<T>() where T : UIView
        {
            if (!OpenViews.ContainsKey(typeof(T)) && !GameModule.UI.HasWindow(typeof(T)))
                return false;
            GameModule.UI.CloseUI<T>();
            ForgetView(typeof(T));
            return true;
        }

        public static void ClosePopupAndOverlay()
        {
            CloseCurrent(UILayerKind.Popup);
            CloseCurrent(UILayerKind.Overlay);
        }

        public static void CloseAll()
        {
            var types = new List<Type>(OpenViews.Keys);
            foreach (Type type in types)
                GameModule.UI.CloseUI(type);
            OpenViews.Clear();
            Current.Clear();
        }

        public static bool IsOpen<T>() where T : UIView
        {
            return OpenViews.ContainsKey(typeof(T)) || GameModule.UI.HasWindow(typeof(T));
        }

        public static T Get<T>() where T : UIView
        {
            return OpenViews.TryGetValue(typeof(T), out UIView view) ? view as T : null;
        }

        public static void Toast(string message, float seconds = 2.3f)
        {
            if (string.IsNullOrEmpty(message)) return;
            if (!_initialized)
            {
                Log.Info($"[TakeoverUI] {message}");
                return;
            }

            ToastView view = Get<ToastView>();
            if (view != null)
            {
                view.Show(message, seconds);
                return;
            }

            _pendingToast = message;
            _pendingToastSeconds = seconds;
            OpenToastAsync().Forget();
        }

        public static bool IsPointerOverUI()
        {
            EventSystem eventSystem = EventSystem.current;
            return eventSystem != null && eventSystem.IsPointerOverGameObject();
        }

        public static void Tick(float unscaledDelta) { }

        public static void Shutdown()
        {
            CloseAll();
            _initialized = false;
            _initializing = false;
            _pendingToast = null;
            _toastOpening = false;
        }

        internal static void OnHostDestroyed() { }

        private static async UniTask<T> OpenViewAsync<T>(UILayerKind layer, object args)
            where T : UIView, new()
        {
            if (!_initialized) await InitializeAsync();
            if (!_initialized) return null;

            if (layer == UILayerKind.Screen)
            {
                CloseCurrent(UILayerKind.Screen);
                ClosePopupAndOverlay();
            }
            else
            {
                CloseCurrent(layer);
            }

            T view = await GameModule.UI.ShowUIAsyncAwait<T>(args);
            if (view == null)
            {
                Log.Error($"[TakeoverUI] 打开 {typeof(T).Name} 失败。");
                return null;
            }

            OpenViews[typeof(T)] = view;
            Current[layer] = view;
            return view;
        }

        private static void CloseCurrent(UILayerKind layer)
        {
            if (!Current.TryGetValue(layer, out UIView view) || view == null) return;
            GameModule.UI.CloseUI(view.GetType());
            ForgetView(view.GetType());
        }

        private static void ForgetView(Type type)
        {
            OpenViews.Remove(type);
            var layers = new List<UILayerKind>();
            foreach (KeyValuePair<UILayerKind, UIView> pair in Current)
                if (pair.Value == null || pair.Value.GetType() == type) layers.Add(pair.Key);
            foreach (UILayerKind layer in layers) Current.Remove(layer);
        }

        private static async UniTaskVoid OpenToastAsync()
        {
            if (_toastOpening) return;
            _toastOpening = true;
            try
            {
                ToastView view = await OpenViewAsync<ToastView>(UILayerKind.Toast, null);
                if (view == null || string.IsNullOrEmpty(_pendingToast)) return;

                string message = _pendingToast;
                float seconds = _pendingToastSeconds;
                _pendingToast = null;
                view.Show(message, seconds);
            }
            finally
            {
                _toastOpening = false;
                if (!string.IsNullOrEmpty(_pendingToast) && Get<ToastView>() != null)
                {
                    string message = _pendingToast;
                    float seconds = _pendingToastSeconds;
                    _pendingToast = null;
                    Get<ToastView>().Show(message, seconds);
                }
            }
        }
    }
}
