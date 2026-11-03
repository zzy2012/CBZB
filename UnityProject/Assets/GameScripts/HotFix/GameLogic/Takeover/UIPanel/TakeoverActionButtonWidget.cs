using System;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic.Takeover
{
    public sealed class TakeoverActionButtonWidget : UIWidget
    {
        private static readonly Color EnabledColor = new Color(0.20f, 0.48f, 0.26f, 0.95f);
        private static readonly Color HighlightColor = new Color(0.27f, 0.60f, 0.33f, 0.95f);
        private static readonly Color PressedColor = new Color(0.15f, 0.38f, 0.21f, 0.95f);
        private static readonly Color DisabledColor = new Color(0.30f, 0.30f, 0.30f, 0.85f);

        private Button _button;
        private Text _label;
        private RectTransform _rect;
        private int _defaultFontSize;

        protected override void ScriptGenerator()
        {
            _button = gameObject.GetComponent<Button>();
            _label = FindChildComponent<Text>("m_text_Label");
            _rect = rectTransform;
        }

        protected override void OnCreate()
        {
            if (_button == null || _label == null)
                throw new InvalidOperationException(
                    "TakeoverUIActionButton prefab requires a root Button and m_text_Label Text.");
            _defaultFontSize = _label.fontSize;
        }

        public void Configure(TakeoverActionPlan plan, bool stateTint)
        {
            gameObject.name = $"m_item_{plan.Name}";
            _rect.anchorMin = new Vector2(0.5f, 0.5f);
            _rect.anchorMax = new Vector2(0.5f, 0.5f);
            _rect.pivot = new Vector2(0.5f, 0.5f);
            _rect.anchoredPosition = plan.Position;
            _rect.sizeDelta = plan.Size;

            _button.onClick.RemoveAllListeners();
            if (plan.Click != null) _button.onClick.AddListener(plan.Click);

            _label.text = plan.Label;
            _label.fontSize = plan.FontSize > 0 ? plan.FontSize : _defaultFontSize;
            if (OwnerWindow is ITakeoverFontSource fontSource)
                TakeoverUIResources.ApplyFont(gameObject, fontSource.Font);

            if (stateTint) ApplyStateColors();
            SetInteractable(plan.Enabled == null || plan.Enabled());
        }

        public void SetInteractable(bool interactable)
        {
            if (_button != null) _button.interactable = interactable;
        }

        private void ApplyStateColors()
        {
            _button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = _button.colors;
            colors.normalColor = EnabledColor;
            colors.highlightedColor = HighlightColor;
            colors.selectedColor = EnabledColor;
            colors.pressedColor = PressedColor;
            colors.disabledColor = DisabledColor;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.05f;
            _button.colors = colors;
            if (_button.targetGraphic != null)
                _button.targetGraphic.color = Color.white;
        }
    }
}
