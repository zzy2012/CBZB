using System;
using GameLogic.UI;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic.Takeover.UI
{
    public static class TakeoverUiFactory
    {
        private static readonly Color PanelColor = new Color(0.055f, 0.065f, 0.055f, 0.96f);
        private static readonly Color ButtonColor = new Color(0.20f, 0.19f, 0.13f, 0.98f);
        private static readonly Color AccentColor = new Color(0.78f, 0.64f, 0.31f, 1f);

        public static RectTransform CreateRoot(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            Stretch(rect);
            return rect;
        }

        public static RectTransform CreatePanel(Transform parent, string name, Vector2 size,
            Color? color = null, bool raycastTarget = false)
        {
            Image image = CreateImage(parent, name, color ?? PanelColor, raycastTarget);
            SetCentered(image.rectTransform, size);
            return image.rectTransform;
        }

        public static Image CreateImage(Transform parent, string name, Color color,
            bool raycastTarget = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = raycastTarget;
            return image;
        }

        public static Text CreateText(Transform parent, string name, string text, int fontSize = 20,
            Color? color = null, TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            Text label = go.GetComponent<Text>();
            label.font = UIFontProvider.Font;
            label.text = text ?? string.Empty;
            label.fontSize = fontSize;
            label.color = color ?? new Color(0.92f, 0.89f, 0.78f, 1f);
            label.alignment = alignment;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            label.supportRichText = true;
            label.rectTransform.sizeDelta = new Vector2(0f, 36f);
            return label;
        }

        public static Button CreateButton(Transform parent, string name, string text,
            Action onClick, int fontSize = 18, Color? color = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.color = color ?? ButtonColor;
            Button button = go.GetComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            button.colors = new ColorBlock
            {
                normalColor = Color.white,
                highlightedColor = new Color(1f, 0.91f, 0.62f, 1f),
                pressedColor = new Color(0.76f, 0.65f, 0.42f, 1f),
                selectedColor = Color.white,
                disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.65f),
                colorMultiplier = 1f,
                fadeDuration = 0.08f,
            };
            if (onClick != null) button.onClick.AddListener(() => onClick());

            Text label = CreateText(go.transform, name + "Label", text, fontSize,
                new Color(0.96f, 0.91f, 0.76f, 1f), TextAnchor.MiddleCenter);
            Stretch(label.rectTransform);
            return button;
        }

        public static RectTransform CreateRow(Transform parent, string name, float spacing = 8f)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup));
            go.transform.SetParent(parent, false);
            HorizontalLayoutGroup layout = go.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            return (RectTransform)go.transform;
        }

        public static RectTransform CreateColumn(Transform parent, string name, float spacing = 8f,
            bool expandWidth = true)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup));
            go.transform.SetParent(parent, false);
            VerticalLayoutGroup layout = go.GetComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = expandWidth;
            layout.childForceExpandHeight = false;
            return (RectTransform)go.transform;
        }

        public static RectTransform CreateScrollView(Transform parent, string name, out RectTransform content)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(ScrollRect), typeof(Image), typeof(Mask));
            root.transform.SetParent(parent, false);
            Image viewport = root.GetComponent<Image>();
            viewport.color = new Color(0f, 0f, 0f, 0.12f);
            viewport.raycastTarget = true;
            root.GetComponent<Mask>().showMaskGraphic = false;

            var contentObject = new GameObject(name + "Content", typeof(RectTransform),
                typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentObject.transform.SetParent(root.transform, false);
            content = (RectTransform)contentObject.transform;
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0f, 0f);

            VerticalLayoutGroup layout = contentObject.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            ContentSizeFitter fitter = contentObject.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scroll = root.GetComponent<ScrollRect>();
            scroll.viewport = (RectTransform)root.transform;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            return (RectTransform)root.transform;
        }

        public static void SetPreferredHeight(Component component, float height)
        {
            LayoutElement element = component.GetComponent<LayoutElement>();
            if (element == null) element = component.gameObject.AddComponent<LayoutElement>();
            element.minHeight = height;
            element.preferredHeight = height;
        }

        public static void SetPreferredWidth(Component component, float width)
        {
            LayoutElement element = component.GetComponent<LayoutElement>();
            if (element == null) element = component.gameObject.AddComponent<LayoutElement>();
            element.minWidth = width;
            element.preferredWidth = width;
        }

        public static void SetCentered(RectTransform rect, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
        }

        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
        }

        public static void Anchor(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 size, Vector2 position, Vector2 pivot)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            rect.pivot = pivot;
        }

        public static Color Accent => AccentColor;
        public static Color Panel => PanelColor;
    }
}
