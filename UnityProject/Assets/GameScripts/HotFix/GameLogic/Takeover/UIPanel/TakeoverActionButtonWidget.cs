using System;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic.Takeover
{
    /// <summary>根据操作描述配置和显示操作按钮。</summary>
    public sealed class TakeoverActionButtonWidget : UIWidget
    {
        /// <summary>可交互按钮的基础颜色。</summary>
        private static readonly Color EnabledColor = new Color(0.20f, 0.48f, 0.26f, 0.95f);
        /// <summary>鼠标悬停时按钮的颜色。</summary>
        private static readonly Color HighlightColor = new Color(0.27f, 0.60f, 0.33f, 0.95f);
        /// <summary>按钮按下时显示的颜色。</summary>
        private static readonly Color PressedColor = new Color(0.15f, 0.38f, 0.21f, 0.95f);
        /// <summary>不可交互按钮显示的颜色。</summary>
        private static readonly Color DisabledColor = new Color(0.30f, 0.30f, 0.30f, 0.85f);

        /// <summary>操作按钮的 Unity 按钮组件。</summary>
        private Button _button;
        /// <summary>操作按钮上的文本组件。</summary>
        private Text _label;
        /// <summary>操作按钮的矩形变换组件。</summary>
        private RectTransform _rect;
        /// <summary>按钮创建时文本的默认字号。</summary>
        private int _defaultFontSize;

        /// <summary>生成操作按钮所需的界面组件。</summary>
        protected override void ScriptGenerator()
        {
            _button = gameObject.GetComponent<Button>();
            _label = FindChildComponent<Text>("m_text_Label");
            _rect = rectTransform;
        }

        /// <summary>获取按钮和文本组件并记录默认显示状态。</summary>
        protected override void OnCreate()
        {
            if (_button == null || _label == null)
                throw new InvalidOperationException(
                    "TakeoverUIActionButton prefab requires a root Button and m_text_Label Text.");
            _defaultFontSize = _label.fontSize;
        }

        /// <summary>根据操作描述配置按钮外观和回调。</summary>
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

        /// <summary>设置按钮是否可交互。</summary>
        public void SetInteractable(bool interactable)
        {
            if (_button != null) _button.interactable = interactable;
        }

        /// <summary>根据按钮状态更新按钮配色。</summary>
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
