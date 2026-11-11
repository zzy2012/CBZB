using System.Collections.Generic;
using GameLogic.UI;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic.Takeover.UI
{
    public sealed class GmPanelController
    {
        private static readonly float[] Speeds = { 1f, 2f, 4f, 8f, 16f, 32f, 64f };

        private readonly TakeoverRunner _runner;
        private readonly List<Text> _speedLabels = new List<Text>();
        private RectTransform _root;
        private RectTransform _content;
        private Button _toggle;
        private Text _aiLabel;
        private bool _expanded;

        public GmPanelController(TakeoverRunner runner) { _runner = runner; }

        public void Bind(Transform host)
        {
            _root = TakeoverUiFactory.CreatePanel(host, "GmPanel",
                new Vector2(300f, 44f), new Color(0.055f, 0.065f, 0.055f, 0.88f));
            TakeoverUiFactory.Anchor(_root, new Vector2(0.78f, 0.56f),
                new Vector2(0.98f, 0.82f), Vector2.zero, Vector2.zero, new Vector2(1f, 1f));
            _toggle = TakeoverUiFactory.CreateButton(_root, "GmToggle", "GM",
                () => SetExpanded(!_expanded), 14);
            TakeoverUiFactory.Anchor(_toggle.GetComponent<RectTransform>(),
                new Vector2(0f, 0.84f), new Vector2(1f, 1f),
                Vector2.zero, Vector2.zero, new Vector2(0.5f, 1f));

            _content = TakeoverUiFactory.CreateColumn(_root, "GmContent", 4f);
            TakeoverUiFactory.Anchor(_content, new Vector2(0f, 0f),
                new Vector2(1f, 0.82f), Vector2.zero, Vector2.zero, new Vector2(0.5f, 0f));
            _content.gameObject.AddComponent<Image>().color =
                new Color(0.055f, 0.065f, 0.055f, 0.92f);
            AddButton("Gold", "金币 +1000", () => _runner?.GrantResources(1000, 0));
            AddButton("Mana", "法力 +500", () => _runner?.GrantResources(0, 500));
            AddButton("Win", "强制胜利", () => _runner?.GmEndBattle(true));
            AddButton("Lose", "强制失败", () => _runner?.GmEndBattle(false));
            Button ai = AddButton("Ai", "AI", () => _runner?.ToggleHumanAi());
            _aiLabel = ai.GetComponentInChildren<Text>();

            RectTransform speedRow = TakeoverUiFactory.CreateRow(_content, "SpeedOptions", 3f);
            foreach (float speed in Speeds)
            {
                float captured = speed;
                Button button = TakeoverUiFactory.CreateButton(speedRow, $"Speed{speed:0}",
                    $"×{speed:0}", () => _runner?.SetSpeed(captured), 11);
                TakeoverUiFactory.SetPreferredWidth(button, 45f);
                TakeoverUiFactory.SetPreferredHeight(button, 32f);
                _speedLabels.Add(button.GetComponentInChildren<Text>());
            }
            _root.gameObject.SetActive(true);
            SetExpanded(false);
            RefreshAi(_runner != null && _runner.HumanAiControlled);
        }

        public void SetExpanded(bool expanded)
        {
            _expanded = expanded;
            if (_content != null) _content.gameObject.SetActive(expanded);
            if (_toggle != null)
            {
                Text label = _toggle.GetComponentInChildren<Text>();
                if (label != null) label.text = expanded ? "GM ▾" : "GM";
            }
        }

        public void SetVisible(bool visible)
        {
            if (_root != null) _root.gameObject.SetActive(visible);
        }

        public void RefreshSpeed(float speed)
        {
            for (int i = 0; i < _speedLabels.Count && i < Speeds.Length; i++)
                _speedLabels[i].text = $"×{Speeds[i]:0}" +
                                       (Mathf.Approximately(speed, Speeds[i]) ? "*" : string.Empty);
        }

        public void RefreshAi(bool controlled)
        {
            if (_aiLabel != null) _aiLabel.text = controlled ? "AI*" : "AI";
        }

        private Button AddButton(string name, string label, System.Action action)
        {
            Button button = TakeoverUiFactory.CreateButton(_content, name, label,
                action, 12, new Color(0.16f, 0.17f, 0.14f, 1f));
            TakeoverUiFactory.SetPreferredHeight(button, 32f);
            return button;
        }
    }
}
