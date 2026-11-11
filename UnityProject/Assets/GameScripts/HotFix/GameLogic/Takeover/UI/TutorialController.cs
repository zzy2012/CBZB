using GameLogic.UI;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic.Takeover.UI
{
    public sealed class TutorialController
    {
        private Text _text;
        private Button _next;
        private int _index;

        public void Bind(Transform host)
        {
            RectTransform panel = TakeoverUiFactory.CreatePanel(host, "TutorialPanel",
                new Vector2(520f, 104f), new Color(0.055f, 0.065f, 0.055f, 0.88f));
            TakeoverUiFactory.Anchor(panel, new Vector2(0.34f, 0.025f),
                new Vector2(0.66f, 0.16f), Vector2.zero, Vector2.zero, new Vector2(0.5f, 0f));
            _text = TakeoverUiFactory.CreateText(panel, "TutorialText", string.Empty, 13,
                Color.white);
            TakeoverUiFactory.Anchor(_text.rectTransform, new Vector2(0.04f, 0.16f),
                new Vector2(0.96f, 0.95f), Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
            _next = TakeoverUiFactory.CreateButton(panel, "NextTutorial", "继续提示",
                Next, 12, new Color(0.22f, 0.21f, 0.15f, 1f));
            TakeoverUiFactory.Anchor(_next.GetComponent<RectTransform>(),
                new Vector2(0.72f, 0.02f), new Vector2(0.96f, 0.2f),
                Vector2.zero, Vector2.zero, new Vector2(0.5f, 0f));
            _index = Campaign.TutorialStep % TakeoverUiContent.TutorialCount;
            ShowLine();
        }

        private void Next()
        {
            _index++;
            if (_index >= TakeoverUiContent.TutorialCount)
            {
                _index = 0;
                Campaign.SetTutorialStep(0);
                _text.text = "战术提示：点击城堡可随时打开招募与升级面板。";
                return;
            }
            Campaign.SetTutorialStep(_index);
            ShowLine();
        }

        private void ShowLine()
        {
            if (_text != null) _text.text = "战术提示：" + TakeoverUiContent.TutorialLine(_index);
        }
    }
}
