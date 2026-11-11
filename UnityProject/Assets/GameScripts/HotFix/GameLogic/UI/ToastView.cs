using GameLogic;
using GameLogic.Takeover.UI;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic.UI
{
    [Window(UILayer.Tips, location: "TakeoverUIWindow")]
    public sealed class ToastView : UIView
    {
        private RectTransform _panel;
        private Text _label;
        private float _remain;

        protected override void OnCreate(object args)
        {
            ClearContent();
            _panel = TakeoverUiFactory.CreatePanel(Root, "ToastPanel",
                new Vector2(780f, 56f), new Color(0.025f, 0.03f, 0.025f, 0.9f));
            TakeoverUiFactory.Anchor(_panel, new Vector2(0.5f, 0.9f),
                new Vector2(0.5f, 0.9f), new Vector2(780f, 56f),
                Vector2.zero, new Vector2(0.5f, 0.5f));
            _label = TakeoverUiFactory.CreateText(_panel, "ToastText", string.Empty, 18,
                new Color(0.96f, 0.91f, 0.76f, 1f), TextAnchor.MiddleCenter);
            TakeoverUiFactory.Stretch(_label.rectTransform);
            _panel.gameObject.SetActive(false);
        }

        public void Show(string message, float seconds)
        {
            if (_label == null || _panel == null) return;
            _label.text = message;
            _panel.gameObject.SetActive(true);
            _remain = Mathf.Max(0.2f, seconds);
        }

        public override void Tick(float unscaledDelta)
        {
            if (_panel == null || _remain <= 0f) return;
            _remain -= unscaledDelta;
            if (_remain <= 0f)
            {
                _panel.gameObject.SetActive(false);
                _remain = 0f;
            }
        }
    }
}
