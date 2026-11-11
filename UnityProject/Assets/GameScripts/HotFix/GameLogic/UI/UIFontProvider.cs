using TEngine;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic.UI
{
    public static class UIFontProvider
    {
        private const string ResourceFontPath = "UI/Fonts/TakeoverCjk";
        private static Font _font;

        public static Font Font
        {
            get
            {
                if (_font != null) return _font;
                _font = Resources.Load<Font>(ResourceFontPath);
                if (_font == null)
                {
                    Log.Error("[TakeoverUI] 中文字体未安装，使用 Unity 内置字体。");
                    _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                }
                return _font;
            }
        }

        public static void ApplyToTree(Transform root)
        {
            if (root == null || Font == null) return;
            foreach (Text text in root.GetComponentsInChildren<Text>(true))
                text.font = Font;
        }

        public static void Release()
        {
            _font = null;
        }
    }
}
