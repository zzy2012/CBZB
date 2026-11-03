using UnityEngine.UIElements;

namespace GameLogic.Takeover.UI
{
    /// <summary>战斗教程提示条与“继续提示”。</summary>
    public sealed class TutorialController
    {
        private Label _text;
        private VisualElement _avatar;
        private Label _tag;
        private int _index;

        public void Bind(Label text, VisualElement avatar, Label tag, Button next)
        {
            _text = text;
            _avatar = avatar;
            _tag = tag;
            _index = Campaign.TutorialStep % TakeoverUiContent.TutorialCount;
            if (next != null)
            {
                next.text = "继续提示";
                next.clicked += Next;
            }
            ShowLine();
        }

        private void Next()
        {
            _index++;
            if (_index >= TakeoverUiContent.TutorialCount)
            {
                _index = 0;
                Campaign.SetTutorialStep(0);
                SetAvatarVisible(false);
                if (_text != null)
                    _text.text = "战术提示：点击城堡可随时打开招募与升级面板。";
                return;
            }

            Campaign.SetTutorialStep(_index);
            ShowLine();
        }

        private void ShowLine()
        {
            if (_text != null)
                _text.text = "战术提示：" + TakeoverUiContent.TutorialLine(_index);
            SetAvatarVisible(true);
        }

        private void SetAvatarVisible(bool visible)
        {
            DisplayStyle style = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (_avatar != null) _avatar.style.display = style;
            if (_tag != null) _tag.style.display = style;
        }
    }
}
