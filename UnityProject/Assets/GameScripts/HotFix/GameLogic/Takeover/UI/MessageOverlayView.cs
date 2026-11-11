using System;
using System.Collections.Generic;
using GameLogic;
using GameLogic.UI;
using TEngine;
using UnityEngine;
using UnityEngine.UI;

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

    public abstract class MessageOverlayView : UIView
    {
        protected TakeoverRunner Runner { get; private set; }

        protected override void OnCreate(object args)
        {
            Runner = args as TakeoverRunner ?? (args as OverlayArgs)?.Runner;
            ClearContent();

            Image scrim = TakeoverUiFactory.CreateImage(Root, "OverlayScrim",
                new Color(0f, 0f, 0f, 0.72f), true);
            TakeoverUiFactory.Stretch(scrim.rectTransform);
            RectTransform panel = TakeoverUiFactory.CreatePanel(Root, "OverlayPanel",
                new Vector2(620f, 400f), TakeoverUiFactory.Panel, true);
            VerticalLayoutGroup layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(34, 34, 24, 20);
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            OverlaySpec spec = BuildSpec(args) ??
                               new OverlaySpec { Title = string.Empty, Body = string.Empty };
            TakeoverUiFactory.CreateText(panel, "OverlayTitle", spec.Title, 30,
                TakeoverUiFactory.Accent, TextAnchor.MiddleCenter);
            Text body = TakeoverUiFactory.CreateText(panel, "OverlayBody", spec.Body, 18,
                Color.white, TextAnchor.MiddleCenter);
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.verticalOverflow = VerticalWrapMode.Overflow;
            LayoutElement bodyLayout = body.gameObject.AddComponent<LayoutElement>();
            bodyLayout.flexibleHeight = 1f;
            RectTransform actions = TakeoverUiFactory.CreateRow(panel, "OverlayActions", 12f);
            TakeoverUiFactory.SetPreferredHeight(actions, 54f);

            foreach (OverlayButtonSpec buttonSpec in spec.Buttons)
            {
                OverlayButtonSpec captured = buttonSpec;
                Button button = TakeoverUiFactory.CreateButton(actions, captured.Label,
                    captured.Label, () =>
                    {
                        try
                        {
                            captured.Click?.Invoke();
                        }
                        catch (Exception exception)
                        {
                            Log.Error($"[TakeoverUI] 遮罩按钮 {captured.Label} 失败: {exception}");
                        }
                    }, 17, captured.Primary
                        ? new Color(0.39f, 0.31f, 0.16f, 1f)
                        : new Color(0.18f, 0.19f, 0.16f, 1f));
                TakeoverUiFactory.SetPreferredWidth(button, 190f);
                TakeoverUiFactory.SetPreferredHeight(button, 48f);
            }

            OnOverlayOpened();
        }

        protected abstract OverlaySpec BuildSpec(object args);

        protected virtual void OnOverlayOpened() { }
    }
}
