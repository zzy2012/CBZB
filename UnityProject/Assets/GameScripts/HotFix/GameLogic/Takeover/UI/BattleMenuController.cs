using System.Collections.Generic;
using System.Text;
using GameLogic.UI;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic.Takeover.UI
{
    public sealed class BattleMenuController
    {
        private static readonly Dictionary<string, string> GroupNames = new Dictionary<string, string>
        {
            { "units", "部队" }, { "upgrade", "升级" }, { "build", "建造" },
        };

        private readonly TakeoverRunner _runner;
        private readonly List<Button> _optionButtons = new List<Button>();
        private readonly StringBuilder _signatureBuilder = new StringBuilder(256);
        private RectTransform _root;
        private RectTransform _tabs;
        private RectTransform _options;
        private string _tabSignature;
        private string _signature;
        private string _selectedGroup;

        public BattleMenuController(TakeoverRunner runner) { _runner = runner; }

        public void Bind(Transform host)
        {
            _root = TakeoverUiFactory.CreatePanel(host, "BattleActionMenu",
                new Vector2(390f, 300f), new Color(0.055f, 0.065f, 0.055f, 0.88f));
            TakeoverUiFactory.Anchor(_root, new Vector2(0.018f, 0.25f),
                new Vector2(0.31f, 0.68f), Vector2.zero, Vector2.zero, new Vector2(0f, 0f));
            var layout = _root.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 8, 8);
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            _tabs = TakeoverUiFactory.CreateRow(_root, "ActionTabs", 5f);
            TakeoverUiFactory.SetPreferredHeight(_tabs, 38f);
            RectTransform scroll = TakeoverUiFactory.CreateScrollView(_root, "ActionOptions", out _options);
            scroll.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;
            Hide();
        }

        public void Refresh(BattleActionPlan plan)
        {
            if (_root == null) return;
            if (plan == null || !plan.ShowMenu || plan.Actions.Count == 0)
            {
                Hide();
                return;
            }

            _root.gameObject.SetActive(true);
            if (string.IsNullOrEmpty(_selectedGroup) || !HasGroup(plan, _selectedGroup))
                _selectedGroup = plan.Actions[0].Group;
            string tabs = BuildTabSignature(plan);
            if (tabs != _tabSignature)
            {
                _tabSignature = tabs;
                RebuildTabs(plan);
            }
            string signature = BuildSignature(plan);
            if (signature != _signature)
            {
                _signature = signature;
                RebuildOptions(plan);
            }
            else
            {
                UpdateEnabledStates(plan);
            }
        }

        public void Hide()
        {
            if (_root != null) _root.gameObject.SetActive(false);
            _tabSignature = null;
            _signature = null;
            _selectedGroup = null;
        }

        private void RebuildTabs(BattleActionPlan plan)
        {
            ClearChildren(_tabs);
            var groups = new List<string>();
            foreach (BattleAction action in plan.Actions)
                if (!groups.Contains(action.Group)) groups.Add(action.Group);
            foreach (string group in groups)
            {
                string captured = group;
                Button tab = TakeoverUiFactory.CreateButton(_tabs, "Tab" + group,
                    GroupNames.TryGetValue(group, out string name) ? name : group,
                    () =>
                    {
                        _selectedGroup = captured;
                        _signature = null;
                        _tabSignature = null;
                        Refresh(TakeoverActionPlanner.Build(_runner));
                    }, 13);
                tab.GetComponent<Image>().color = group == _selectedGroup
                    ? TakeoverUiFactory.Accent
                    : new Color(0.18f, 0.19f, 0.16f, 1f);
                TakeoverUiFactory.SetPreferredHeight(tab, 34f);
            }
        }

        private void RebuildOptions(BattleActionPlan plan)
        {
            ClearChildren(_options);
            _optionButtons.Clear();
            foreach (BattleAction action in plan.Actions)
            {
                if (action.Group != _selectedGroup) continue;
                BattleAction captured = action;
                string text = string.IsNullOrEmpty(action.Detail)
                    ? action.Label
                    : $"{action.Label}  ·  {action.Detail}";
                Button button = TakeoverUiFactory.CreateButton(_options, action.Id, text,
                    () => captured.Click?.Invoke(), 14,
                    new Color(0.15f, 0.17f, 0.13f, 1f));
                button.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleLeft;
                button.interactable = action.Enabled;
                TakeoverUiFactory.SetPreferredHeight(button, 44f);
                _optionButtons.Add(button);
            }
        }

        private void UpdateEnabledStates(BattleActionPlan plan)
        {
            int index = 0;
            foreach (BattleAction action in plan.Actions)
            {
                if (action.Group != _selectedGroup) continue;
                if (index < _optionButtons.Count)
                    _optionButtons[index].interactable = action.Enabled;
                index++;
            }
        }

        private string BuildTabSignature(BattleActionPlan plan)
        {
            _signatureBuilder.Clear();
            foreach (BattleAction action in plan.Actions)
                _signatureBuilder.Append(action.Group).Append(';');
            return _signatureBuilder.ToString();
        }

        private string BuildSignature(BattleActionPlan plan)
        {
            _signatureBuilder.Clear();
            _signatureBuilder.Append(plan.TargetKey).Append('#').Append(_selectedGroup).Append(';');
            foreach (BattleAction action in plan.Actions)
                if (action.Group == _selectedGroup)
                    _signatureBuilder.Append(action.Id).Append('|').Append(action.Label).Append('|')
                        .Append(action.Detail).Append(';');
            return _signatureBuilder.ToString();
        }

        private static bool HasGroup(BattleActionPlan plan, string group)
        {
            foreach (BattleAction action in plan.Actions)
                if (action.Group == group) return true;
            return false;
        }

        private static void ClearChildren(Transform parent)
        {
            if (parent == null) return;
            for (int i = parent.childCount - 1; i >= 0; i--)
                Object.Destroy(parent.GetChild(i).gameObject);
        }
    }
}
