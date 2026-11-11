using GameLogic.UI;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic.Takeover.UI
{
    public sealed class InspectorController
    {
        private RectTransform _root;
        private Text _title;
        private Text _body;

        public void Bind(Transform host)
        {
            _root = TakeoverUiFactory.CreatePanel(host, "TargetInspector",
                new Vector2(390f, 152f), new Color(0.055f, 0.065f, 0.055f, 0.88f));
            TakeoverUiFactory.Anchor(_root, new Vector2(0.018f, 0.04f),
                new Vector2(0.31f, 0.23f), Vector2.zero, Vector2.zero, Vector2.zero);
            _title = TakeoverUiFactory.CreateText(_root, "InspectorTitle", string.Empty, 17,
                TakeoverUiFactory.Accent);
            _body = TakeoverUiFactory.CreateText(_root, "InspectorBody", string.Empty, 14);
            TakeoverUiFactory.Anchor(_title.rectTransform, new Vector2(0.04f, 0.68f),
                new Vector2(0.96f, 0.96f), Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
            TakeoverUiFactory.Anchor(_body.rectTransform, new Vector2(0.04f, 0.05f),
                new Vector2(0.96f, 0.68f), Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
            _body.alignment = TextAnchor.UpperLeft;
            Hide();
        }

        public void Refresh(BattleActionPlan plan)
        {
            if (_root == null) return;
            if (plan == null)
            {
                Hide();
                return;
            }
            _root.gameObject.SetActive(true);
            _title.text = plan.Title;
            _body.text = plan.Body;
        }

        public void Hide()
        {
            if (_root != null) _root.gameObject.SetActive(false);
        }
    }
}
