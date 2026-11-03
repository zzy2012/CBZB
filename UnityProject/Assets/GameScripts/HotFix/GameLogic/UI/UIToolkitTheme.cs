using UnityEngine;
using UnityEngine.UIElements;

namespace GameLogic.UI
{
    /// <summary>
    /// UI Toolkit 运行时主题补丁：
    /// 1) 给所有 TextElement（含 Button/Label 派生类）写内联中文字体；
    /// 2) 用内联样式覆盖 Button 的默认主题（白色 9-slice + 白色文字），
    ///    保证 USS 的配色即使被主题抢优先级也仍然可见。
    /// </summary>
    public static class UIToolkitTheme
    {
        private static readonly Color ButtonBg = new Color32(0x2b, 0x21, 0x18, 0xff);
        private static readonly Color ButtonFg = new Color32(0xf1, 0xe2, 0xc0, 0xff);
        private static readonly Color GoldBg = new Color32(0xd7, 0x9a, 0x2f, 0xff);
        private static readonly Color GoldFg = new Color32(0x35, 0x23, 0x11, 0xff);

        public static void Apply(VisualElement root)
        {
            if (root == null) return;

            UIFontProvider.ApplyToTree(root);

            if (root is Button rootButton) StyleButton(rootButton);
            foreach (Button button in root.Query<Button>().ToList()) StyleButton(button);
        }

        private static void StyleButton(Button button)
        {
            if (button == null) return;

            // 主题的按钮背景是 9-slice 图片；显式置空并 tint 成全透明，
            // 避免它盖住 background-color，也避免白底白字把按钮文字吃掉。
            button.style.backgroundImage = new StyleBackground(StyleKeyword.None);
            button.style.unityBackgroundImageTintColor = new Color(1f, 1f, 1f, 0f);

            Color background = ButtonBg;
            Color foreground = ButtonFg;
            int borderWidth = 2;
            Color borderColor = new Color32(0xf5, 0xc4, 0x55, 0xff);
            int radius = 18;

            if (button.ClassListContains("button--gold"))
            {
                background = GoldBg;
                foreground = GoldFg;
            }
            else if (button.ClassListContains("button--ghost"))
            {
                background = new Color32(0x59, 0x46, 0x33, 0xff);
                borderColor = new Color32(0xb8, 0x8d, 0x4c, 0xff);
            }
            else if (button.ClassListContains("round-button"))
            {
                background = new Color32(0x6e, 0x2c, 0xa1, 0xff);
                foreground = new Color32(0xff, 0xf4, 0xd5, 0xff);
                borderColor = new Color32(0xf8, 0xc8, 0x57, 0xff);
                radius = 21;
            }
            else if (button.ClassListContains("close-button"))
            {
                background = new Color32(0x35, 0x1e, 0x14, 0xff);
                foreground = new Color32(0xff, 0xd8, 0x69, 0xff);
                borderColor = new Color32(0xec, 0xb4, 0x47, 0xff);
                radius = 15;
            }
            else if (button.ClassListContains("save-card"))
            {
                background = new Color32(0x30, 0x28, 0x20, 0xff);
                foreground = new Color32(0xe8, 0xd7, 0xb0, 0xff);
                borderWidth = 1;
                borderColor = new Color32(0xb8, 0x8d, 0x4c, 0xff);
                radius = 10;
            }
            else if (button.ClassListContains("faction-card"))
            {
                background = new Color32(0x2b, 0x21, 0x18, 0xff);
                foreground = new Color32(0xe8, 0xd7, 0xb0, 0xff);
                borderWidth = button.ClassListContains("is-selected") ? 2 : 1;
                borderColor = button.ClassListContains("is-selected")
                    ? new Color32(0xf4, 0xb5, 0x2f, 0xff)
                    : new Color32(0x5a, 0x46, 0x30, 0xff);
                radius = 12;
            }
            else if (button.ClassListContains("mission-node"))
            {
                background = Color.clear;
                borderWidth = 0;
                radius = 0;
            }
            else if (button.ClassListContains("skill-card"))
            {
                background = new Color32(0x2b, 0x21, 0x18, 0xff);
                foreground = new Color32(0xe8, 0xd7, 0xb0, 0xff);
                borderWidth = 1;
                borderColor = new Color32(0x8a, 0x69, 0x43, 0xff);
                radius = 10;
            }
            else if (button.ClassListContains("tab"))
            {
                bool active = button.ClassListContains("is-active");
                background = active ? GoldBg : ButtonBg;
                foreground = active ? GoldFg : ButtonFg;
                borderColor = active
                    ? new Color32(0xd7, 0xa4, 0x3c, 0xff)
                    : new Color32(0x5a, 0x46, 0x30, 0xff);
                radius = 6;
            }
            else if (button.ClassListContains("spell-button") ||
                     button.ClassListContains("unit-option") ||
                     button.ClassListContains("gm-toggle"))
            {
                background = ButtonBg;
                foreground = ButtonFg;
                borderWidth = 1;
                borderColor = new Color32(0x8a, 0x69, 0x43, 0xff);
                radius = 6;
            }
            else if (button.ClassListContains("button--danger"))
            {
                background = new Color32(0x6a, 0x23, 0x18, 0xff);
                foreground = new Color32(0xff, 0xe1, 0xd0, 0xff);
            }

            button.style.backgroundColor = background;
            button.style.color = foreground;
            button.style.borderTopWidth = new StyleFloat(borderWidth);
            button.style.borderRightWidth = new StyleFloat(borderWidth);
            button.style.borderBottomWidth = new StyleFloat(borderWidth);
            button.style.borderLeftWidth = new StyleFloat(borderWidth);
            button.style.borderTopColor = borderColor;
            button.style.borderRightColor = borderColor;
            button.style.borderBottomColor = borderColor;
            button.style.borderLeftColor = borderColor;
            button.style.borderTopLeftRadius = new StyleLength((float)radius);
            button.style.borderTopRightRadius = new StyleLength((float)radius);
            button.style.borderBottomLeftRadius = new StyleLength((float)radius);
            button.style.borderBottomRightRadius = new StyleLength((float)radius);
        }

        public static void Release()
        {
        }
    }
}
