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
    /// 简单的 UI Toolkit 管理器：
    /// 创建一个运行时 PanelSettings + UIDocument，按层打开/关闭 UIView，提供 Toast 与 UI 命中检测。
    /// 不依赖 TEngine 的 UIWindow/UIWidget 规范。
    /// </summary>
    public static class UIManager
    {
        private const string ThemeResourcePath = "UI/TakeoverRuntimeTheme";

        private static UIDocument _document;
        private static PanelSettings _panelSettings;
        private static VisualElement _root;

        private static readonly Dictionary<UILayerKind, VisualElement> Layers =
            new Dictionary<UILayerKind, VisualElement>();
        private static readonly Dictionary<UILayerKind, UIView> Current =
            new Dictionary<UILayerKind, UIView>();
        private static readonly List<UIView> OpenViews = new List<UIView>(16);

        private static ToastView _toastView;
        private static string _pendingToast;
        private static float _pendingToastSeconds;
        private static int _screenVersion;

        private static bool _initialized;
        private static bool _initializing;
        private static bool _failed;
        private static bool _shuttingDown;

        public static bool IsInitialized => _initialized;

        public static VisualElement Root => _root;

        public static async UniTask InitializeAsync(CancellationToken token = default)
        {
            if (_initialized) return;
            if (_initializing)
            {
                await UniTask.WaitUntil(() => _initialized || _failed);
                return;
            }

            _initializing = true;
            _failed = false;
            try
            {
                if (_document == null) CreateDocument();

                // UIDocument 在 OnEnable 时创建面板；等一帧再取 rootVisualElement。
                await UniTask.Yield(PlayerLoopTiming.Update, token);
                _root = _document != null ? _document.rootVisualElement : null;
                if (_root == null)
                    throw new InvalidOperationException("UIDocument.rootVisualElement 为空");

                _root.pickingMode = PickingMode.Ignore;
                _root.style.flexGrow = 1;
                _root.style.position = Position.Absolute;
                _root.style.left = 0;
                _root.style.top = 0;
                _root.style.right = 0;
                _root.style.bottom = 0;

                BuildLayers();
                UIFontProvider.Initialize(_root);
                _initialized = true;
            }
            catch (Exception exception)
            {
                _failed = true;
                Log.Error($"[TakeoverUI] UIManager 初始化失败: {exception}");
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
            bool closed = false;
            for (int i = OpenViews.Count - 1; i >= 0; i--)
            {
                if (OpenViews[i] is T)
                {
                    CloseView(OpenViews[i]);
                    closed = true;
                }
            }
            return closed;
        }

        public static void ClosePopupAndOverlay()
        {
            CloseCurrent(UILayerKind.Popup);
            CloseCurrent(UILayerKind.Overlay);
        }

        public static void CloseAll()
        {
            for (int i = OpenViews.Count - 1; i >= 0; i--)
                CloseView(OpenViews[i]);
            Current.Clear();
            OpenViews.Clear();
            _toastView = null;
        }

        public static bool IsOpen<T>() where T : UIView
        {
            for (int i = 0; i < OpenViews.Count; i++)
            {
                if (OpenViews[i] is T) return true;
            }
            return false;
        }

        public static T Get<T>() where T : UIView
        {
            for (int i = 0; i < OpenViews.Count; i++)
            {
                if (OpenViews[i] is T view) return view;
            }
            return null;
        }

        public static void Toast(string message, float seconds = 2.3f)
        {
            if (string.IsNullOrEmpty(message)) return;
            if (!_initialized || _shuttingDown)
            {
                Log.Info($"[TakeoverUI] {message}");
                return;
            }

            if (_toastView == null)
            {
                _pendingToast = message;
                _pendingToastSeconds = seconds;
                OpenToastAsync().Forget();
                return;
            }

            _toastView.Show(message, seconds);
        }

        /// <summary>鼠标是否命中真正的 UI 控件（全屏层/根节点都是 Ignore）。</summary>
        public static bool IsPointerOverUI()
        {
            if (!_initialized || _root == null) return false;
            IPanel panel = _root.panel;
            if (panel == null) return false;

            var screen = new Vector2(Input.mousePosition.x, Input.mousePosition.y);
            Vector2 panelPoint = RuntimePanelUtils.ScreenToPanel(panel, screen);
            VisualElement picked = panel.Pick(panelPoint);
            return picked != null && picked != panel.visualTree;
        }

        public static void Tick(float unscaledDelta)
        {
            if (!_initialized) return;
            for (int i = OpenViews.Count - 1; i >= 0; i--)
            {
                if (i < OpenViews.Count && OpenViews[i] != null)
                    OpenViews[i].Tick(unscaledDelta);
            }
        }

        public static void Shutdown()
        {
            if (_shuttingDown) return;
            _shuttingDown = true;
            try
            {
                for (int i = OpenViews.Count - 1; i >= 0; i--)
                    OpenViews[i]?.Destroy();
                OpenViews.Clear();
                Current.Clear();
                _toastView = null;

                UIFontProvider.Release();
                UIToolkitTheme.Release();
                UIToolkitAssets.ReleaseAll();
                UISpriteCache.ReleaseAll();

                if (_panelSettings != null)
                {
                    UnityEngine.Object.Destroy(_panelSettings);
                    _panelSettings = null;
                }
                if (_document != null)
                {
                    UIDocument document = _document;
                    _document = null;
                    UnityEngine.Object.Destroy(document.gameObject);
                }
            }
            finally
            {
                _root = null;
                Layers.Clear();
                _initialized = false;
                _failed = false;
                _shuttingDown = false;
            }
        }

        internal static void OnHostDestroyed()
        {
            if (_document == null) return;
            Shutdown();
        }

        private static void CreateDocument()
        {
            var go = new GameObject("TakeoverUIRuntime");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.SetActive(false);

            _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            _panelSettings.name = "TakeoverPanelSettings(Runtime)";

            ThemeStyleSheet theme = Resources.Load<ThemeStyleSheet>(ThemeResourcePath);
#if UNITY_EDITOR
            if (theme == null)
            {
                theme = UnityEditor.AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(
                    "Assets/Resources/UI/TakeoverRuntimeTheme.tss");
                if (theme != null) Log.Warning("[TakeoverUI] 主题 Resources 加载失败，已用 AssetDatabase 回退。");
            }
#endif
            if (theme != null)
            {
                _panelSettings.themeStyleSheet = theme;
                Log.Info($"[TakeoverUI] 主题: {theme.name}");
            }
            else
            {
                Log.Error($"[TakeoverUI] 主题缺失: Resources/{ThemeResourcePath}.tss");
            }

            _panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            _panelSettings.referenceResolution = new Vector2Int(1366, 768);
            _panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            _panelSettings.match = 0.5f;
            _panelSettings.sortingOrder = 100;
            _panelSettings.targetTexture = null;
            _panelSettings.clearColor = false;
            _panelSettings.clearDepthStencil = false;

            var document = go.AddComponent<UIDocument>();
            document.panelSettings = _panelSettings;
            go.AddComponent<UIManagerHost>();
            go.SetActive(true);
            _document = document;
        }

        private static void BuildLayers()
        {
            Layers.Clear();
            UILayerKind[] kinds =
            {
                UILayerKind.Screen,
                UILayerKind.Persistent,
                UILayerKind.Popup,
                UILayerKind.Overlay,
                UILayerKind.Toast,
            };

            foreach (UILayerKind kind in kinds)
            {
                var layer = new VisualElement
                {
                    name = "layer-" + kind,
                    pickingMode = PickingMode.Ignore,
                };
                layer.style.position = Position.Absolute;
                layer.style.left = 0;
                layer.style.top = 0;
                layer.style.right = 0;
                layer.style.bottom = 0;
                layer.style.flexGrow = 1;
                _root.Add(layer);
                Layers[kind] = layer;
            }
        }

        private static async UniTask<T> OpenViewAsync<T>(UILayerKind layer, object args)
            where T : UIView, new()
        {
            if (!_initialized) await InitializeAsync();
            if (!_initialized || _shuttingDown) return null;
            if (!Layers.TryGetValue(layer, out VisualElement container) || container == null)
                return null;

            int screenVersion = layer == UILayerKind.Screen ? ++_screenVersion : 0;

            var view = new T { Layer = layer };
            try
            {
                await view.CreateAsync(args, CancellationToken.None);
            }
            catch (Exception exception)
            {
                Log.Error($"[TakeoverUI] 打开 {typeof(T).Name} 失败: {exception}");
                view.Destroy();
                return null;
            }

            if (!_initialized || _shuttingDown || view.Root == null)
            {
                view.Destroy();
                return null;
            }

            if (layer == UILayerKind.Screen && screenVersion != _screenVersion)
            {
                view.Destroy();
                return null;
            }

            if (layer == UILayerKind.Screen) ClosePopupAndOverlay();
            CloseCurrent(layer);

            container.Add(view.Root);
            OpenViews.Add(view);
            Current[layer] = view;
            if (view is ToastView toast) _toastView = toast;
            return view;
        }

        private static void CloseCurrent(UILayerKind layer)
        {
            if (!Current.TryGetValue(layer, out UIView view) || view == null) return;
            CloseView(view);
        }

        private static void CloseView(UIView view)
        {
            if (view == null) return;
            if (Current.TryGetValue(view.Layer, out UIView current) && current == view)
                Current.Remove(view.Layer);
            OpenViews.Remove(view);
            if (_toastView == view) _toastView = null;
            view.Destroy();
        }

        private static async UniTaskVoid OpenToastAsync()
        {
            ToastView view = await OpenViewAsync<ToastView>(UILayerKind.Toast, null);
            if (view == null) return;
            if (!string.IsNullOrEmpty(_pendingToast))
            {
                view.Show(_pendingToast, _pendingToastSeconds);
                _pendingToast = null;
            }
        }
    }

}
