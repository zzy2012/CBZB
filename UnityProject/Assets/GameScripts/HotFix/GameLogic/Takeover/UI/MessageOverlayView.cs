using System;
using System.Collections.Generic;
using GameLogic.UI;
using TEngine;
using UnityEngine.UIElements;

namespace GameLogic.Takeover.UI
{
    public class OverlayArgs
    {
        public TakeoverRunner Runner;
    }

    public sealed class OverlayButtonSpec
    {
        public readonly string Label;
        public readonly bool Primary;
        public readonly Action Click;

        public OverlayButtonSpec(string label, bool primary, Action click)
        {
            Label = label;
            Primary = primary;
            Click = click;
        }
    }

    public sealed class OverlaySpec
    {
        public string Title;
        public string Body;
        public readonly List<OverlayButtonSpec> Buttons = new List<OverlayButtonSpec>();
    }

    /// <summary>暂停/设置/结算共用的遮罩窗口。</summary>
    public abstract class MessageOverlayView : UIView
    {
        protected TakeoverRunner Runner { get; private set; }

        protected override string UxmlLocation => "TakeoverMessageOverlayView";

        protected override void OnCreate(object args)
        {
            Runner = ResolveRunner(args);
            Require("title", "body", "actions");

            OverlaySpec spec = BuildSpec(args) ?? new OverlaySpec { Title = string.Empty, Body = string.Empty };

            Label title = Q<Label>("title");
            if (title != null) title.text = spec.Title;

            Label body = Q<Label>("body");
            if (body != null) body.text = spec.Body;

            VisualElement actions = Q<VisualElement>("actions");
            if (actions != null)
            {
                actions.Clear();
                foreach (OverlayButtonSpec buttonSpec in spec.Buttons)
                {
                    var button = new Button { text = buttonSpec.Label };
                    button.AddToClassList("button");
                    button.AddToClassList(buttonSpec.Primary ? "button--gold" : "button--ghost");
                    OverlayButtonSpec captured = buttonSpec;
                    button.clicked += () =>
                    {
                        try
                        {
                            captured.Click?.Invoke();
                        }
                        catch (Exception exception)
                        {
                            Log.Error($"[TakeoverUI] 遮罩按钮 {captured.Label} 失败: {exception}");
                        }
                    };
                    actions.Add(button);
                }
            }

            OnOverlayOpened();
        }

        protected abstract OverlaySpec BuildSpec(object args);

        protected virtual void OnOverlayOpened() { }

        private static TakeoverRunner ResolveRunner(object args)
        {
            if (args is TakeoverRunner runner) return runner;
            return (args as OverlayArgs)?.Runner;
        }
    }
}
