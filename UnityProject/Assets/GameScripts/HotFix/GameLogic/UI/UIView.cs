using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TEngine;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameLogic.UI
{
    /// <summary>
    /// UI Toolkit 界面基类。只负责 UXML/USS 装载、根节点和生命周期；
    /// 具体控件查询与事件绑定在子类 OnCreate 里完成。
    /// </summary>
    public abstract class UIView
    {
        public VisualElement Root { get; private set; }
        public bool IsOpen { get; private set; }
        internal UILayerKind Layer { get; set; }

        protected abstract string UxmlLocation { get; }

        protected virtual string[] ExtraStyleSheets => Array.Empty<string>();

        internal async UniTask CreateAsync(object args, CancellationToken token)
        {
            VisualTreeAsset tree = await UIToolkitAssets.LoadVisualTreeAsync(UxmlLocation, token);
            token.ThrowIfCancellationRequested();

            Root = UIToolkitAssets.InstantiateContent(tree);
            if (Root == null) Root = BuildFallbackRoot();

            StyleSheet common = await UIToolkitAssets.LoadStyleSheetAsync(
                UIToolkitAssets.CommonStyleLocation, token);
            if (common != null) Root.styleSheets.Add(common);
            else Log.Error($"[TakeoverUI] 公共 USS 加载失败，界面会没有样式: {UIToolkitAssets.CommonStyleLocation}");

            foreach (string location in ExtraStyleSheets)
            {
                StyleSheet sheet = await UIToolkitAssets.LoadStyleSheetAsync(location, token);
                if (sheet != null) Root.styleSheets.Add(sheet);
            }

            IsOpen = true;
            try
            {
                OnCreate(args);
            }
            catch (Exception exception)
            {
                Log.Error($"[TakeoverUI] {GetType().Name} 初始化失败: {exception}");
            }

            UIFontProvider.ApplyToTree(Root);
            UIToolkitTheme.Apply(Root);
        }

        internal void Destroy()
        {
            if (!IsOpen)

            {
                if (Root != null) Root.RemoveFromHierarchy();
                return;
            }

            IsOpen = false;
            try
            {
                OnClose();
            }
            catch (Exception exception)
            {
                Log.Error($"[TakeoverUI] {GetType().Name} 关闭失败: {exception}");
            }

            Root?.RemoveFromHierarchy();
            Root = null;
        }

        protected virtual void OnCreate(object args) { }

        public virtual void Tick(float unscaledDelta) { }

        protected virtual void OnClose() { }

        protected T Q<T>(string name) where T : VisualElement
        {
            if (Root == null) return null;
            T element = Root.Q<T>(name);
            if (element == null)
                Log.Error($"[TakeoverUI] {GetType().Name} 缺少节点: {UxmlLocation}/{name}");
            return element;
        }

        protected void Require(params string[] names)
        {
            if (Root == null || names == null) return;
            foreach (string name in names)
            {
                if (Root.name == name) continue;
                if (Root.Q(name) == null)
                    Log.Error($"[TakeoverUI] {GetType().Name} 缺少节点: {UxmlLocation}/{name}");
            }
        }

        private VisualElement BuildFallbackRoot()
        {
            var root = new VisualElement();
            root.style.flexGrow = 1;
            root.style.backgroundColor = new Color(0.08f, 0.07f, 0.06f, 0.96f);
            var label = new Label($"UI 资源加载失败: {UxmlLocation}");
            label.style.color = new Color(1f, 0.42f, 0.32f, 1f);
            label.style.fontSize = 18;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            root.Add(label);
            return root;
        }
    }
}
