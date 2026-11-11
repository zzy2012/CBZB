using System;
using TEngine;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic.UI
{
    /// <summary>Takeover UGUI window base using TEngine's UIWindow lifecycle.</summary>
    public abstract class UIView : UIWindow
    {
        protected RectTransform Root => rectTransform;

        protected override void OnCreate()
        {
            if (Root == null)
                throw new InvalidOperationException($"{GetType().Name} requires a RectTransform root.");
            OnCreate(UserData);
        }

        protected virtual void OnCreate(object args) { }

        protected override void OnRefresh()
        {
            OnRefresh(UserData);
        }

        protected virtual void OnRefresh(object args) { }

        protected override void OnUpdate()
        {
            Tick(Time.unscaledDeltaTime);
        }

        protected override void OnDestroy()
        {
            OnClose();
        }

        public virtual void Tick(float unscaledDelta) { }

        protected virtual void OnClose() { }

        protected T Q<T>(string path) where T : Component
        {
            T component = FindChildComponent<T>(path);
            if (component == null)
                Log.Error($"[TakeoverUI] {GetType().Name} 缺少节点或组件: {path} ({typeof(T).Name})");
            return component;
        }

        protected void Require(params string[] paths)
        {
            if (paths == null) return;
            foreach (string path in paths)
            {
                if (FindChild(path) == null)
                    Log.Error($"[TakeoverUI] {GetType().Name} 缺少节点: {path}");
            }
        }

        protected void ClearContent()
        {
            if (Root == null) return;
            for (int i = Root.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(Root.GetChild(i).gameObject);
        }
    }
}
