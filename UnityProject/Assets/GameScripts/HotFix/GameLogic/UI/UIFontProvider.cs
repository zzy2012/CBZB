using TEngine;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameLogic.UI
{
    /// <summary>
    /// UI Toolkit 运行时的中文字体。
    /// UI Toolkit 不支持 Font.CreateDynamicFontFromOSFont 的动态字体（会报 Include Font Data 错误），
    /// 所以从 Resources 读取一个带 Font Data 的 TTF/TTC（由编辑器菜单从系统字体安装）。
    /// </summary>
    public static class UIFontProvider
    {
        private const string ResourceFontPath = "UI/Toolkit/Fonts/TakeoverCjk";

        private static Font _font;

        public static bool Ready => _font != null;

        public static Font Font => _font;

        public static void Initialize(VisualElement root)
        {
            if (_font != null)
            {
                Apply(root);
                return;
            }

            _font = Resources.Load<Font>(ResourceFontPath);
            if (_font != null)
            {
                Apply(root);
                Log.Info($"[TakeoverUI] 中文字体: {_font.name}");
                return;
            }

            Log.Error("[TakeoverUI] 中文字体未安装：请执行菜单 TEngine → Takeover → 安装中文字体（从系统复制）。");
            Font fallback = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (fallback == null) fallback = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (fallback != null)
            {
                _font = fallback;
                Apply(root);
            }
        }

        private static void Apply(VisualElement root)
        {
            if (root == null || _font == null) return;
            var definition = new StyleFontDefinition(FontDefinition.FromFont(_font));
            root.style.unityFontDefinition = definition;
            root.style.unityFont = new StyleFont(_font);
        }

        /// <summary>
        /// 默认主题可能给 TextElement 设置了 FontAsset，仅靠继承会被覆盖；
        /// 这里给整棵树（含 Button/Label 等派生类、动态创建的卡片/徽标）写内联字体。
        /// 注意用 Query&lt;VisualElement&gt; 再判断 is TextElement，Query&lt;TextElement&gt; 不会命中派生类型。
        /// </summary>
        public static void ApplyToTree(VisualElement root)
        {
            if (root == null || _font == null) return;
            var definition = new StyleFontDefinition(FontDefinition.FromFont(_font));
            var font = new StyleFont(_font);
            if (root is TextElement self)
            {
                self.style.unityFontDefinition = definition;
                self.style.unityFont = font;
            }

            foreach (VisualElement element in root.Query<VisualElement>().ToList())
            {
                if (element is TextElement text)
                {
                    text.style.unityFontDefinition = definition;
                    text.style.unityFont = font;
                }
            }
        }

        public static void Release()
        {
            // Resources / built-in 字体由 Unity 管理，这里只断开引用，不能 Destroy。
            _font = null;
        }
    }
}
