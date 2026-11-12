using System.Collections.Generic;
using System.Text;
using Cysharp.Threading.Tasks;
using GameLogic.UI;
using UnityEngine;
using UnityEngine.UIElements;
namespace GameLogic.Takeover.UI
{
    /// <summary>战斗菜单：部队/升级/建造 tab + 动作按钮。</summary>
    public sealed class BattleMenuController
    {
        private static readonly Dictionary<string, string> GroupNames = new Dictionary<string, string>
        {
            { "units", "部队" },
            { "upgrade", "升级" },
            { "build", "建造" },
        };

        private readonly TakeoverRunner _runner;
        private VisualElement _root;
        private VisualElement _tabHost;
        private VisualElement _optionList;
        private readonly List<Button> _optionButtons = new List<Button>();
        private readonly List<BattleAction> _currentActions = new List<BattleAction>();
        private readonly StringBuilder _signatureBuilder = new StringBuilder(256);
        private string _tabSignature;
        private string _signature;
        private string _selectedGroup;

        public BattleMenuController(TakeoverRunner runner)
        {
            _runner = runner;
        }

        public async UniTask LoadAsync(VisualElement host)
        {
            if (host == null) return;
            VisualTreeAsset tree = await UIToolkitAssets.LoadVisualTreeAsync("TakeoverBattleMenuView");
            if (tree == null) return;
            _root = UIToolkitAssets.InstantiateContent(tree);
            if (_root == null) return;
            _root.style.display = DisplayStyle.None;
            host.Add(_root);

            _tabHost = _root.Q<VisualElement>("tab-host");
            _optionList = _root.Q<VisualElement>("option-list");
        }

        public void Refresh(BattleActionPlan plan)
        {            if (_root == null) return;
            if (plan == null || !plan.ShowMenu || plan.Actions.Count == 0)
            {
                Hide();
                return;
            }

            _root.style.display = DisplayStyle.Flex;

            string tabSignature = BuildTabSignature(plan);
            if (tabSignature != _tabSignature)
            {
                _tabSignature = tabSignature;
                RebuildTabs(plan);
            }

            if (string.IsNullOrEmpty(_selectedGroup) || !HasGroup(plan, _selectedGroup))
                _selectedGroup = plan.Actions[0].Group;

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

            UIFontProvider.ApplyToTree(_root);
            UIToolkitTheme.Apply(_root);
        }

        public void Hide()
        {
            if (_root != null) _root.style.display = DisplayStyle.None;
            _tabSignature = null;
            _signature = null;
            _selectedGroup = null;
        }

        private void RebuildTabs(BattleActionPlan plan)
        {
            if (_tabHost == null) return;
            _tabHost.Clear();

            var groups = new List<string>();
            foreach (BattleAction action in plan.Actions)
            {
                if (!groups.Contains(action.Group)) groups.Add(action.Group);
            }

            foreach (string group in groups)
            {
                string captured = group;
                var tab = new Button { text = GroupNames.TryGetValue(group, out string name) ? name : group };
                tab.AddToClassList("tab");
                if (group == _selectedGroup) tab.AddToClassList("is-active");
                tab.clicked += () =>
                {
                    _selectedGroup = captured;
                    _signature = null;
                    _tabSignature = null;
                    Refresh(TakeoverActionPlanner.Build(_runner));
                };
                _tabHost.Add(tab);
            }
        }

        private void RebuildOptions(BattleActionPlan plan)
        {
            if (_optionList == null) return;
            _optionList.Clear();
            _optionButtons.Clear();
            _currentActions.Clear();

            foreach (BattleAction action in plan.Actions)
            {
                if (action.Group != _selectedGroup) continue;

                BattleAction captured = action;
                var button = new Button { text = string.IsNullOrEmpty(action.Detail) ? action.Label : $"{action.Label}\n{action.Detail}" };
                button.AddToClassList("unit-option");
                button.SetEnabled(action.Enabled);
                button.clicked += () =>
                {
                    try
                    {
                        captured.Click?.Invoke();
                    }
                    catch (System.Exception exception)
                    {
                        Debug.LogError($"[TakeoverUI] 战斗菜单动作 {captured.Id} 失败: {exception}");
                    }
                };
                _optionList.Add(button);
                _optionButtons.Add(button);
                _currentActions.Add(action);
            }

            UIToolkitTheme.Apply(_root);
        }

        private void UpdateEnabledStates(BattleActionPlan plan)
        {
            int index = 0;
            foreach (BattleAction action in plan.Actions)
            {
                if (action.Group != _selectedGroup) continue;
                if (index < _optionButtons.Count)
                    _optionButtons[index].SetEnabled(action.Enabled);
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
            {
                if (action.Group != _selectedGroup) continue;
                _signatureBuilder.Append(action.Id).Append('|')
                    .Append(action.Label).Append('|')
                    .Append(action.Detail).Append(';');
            }
            return _signatureBuilder.ToString();
        }

        private static bool HasGroup(BattleActionPlan plan, string group)
        {
            foreach (BattleAction action in plan.Actions)
            {
                if (action.Group == group) return true;
            }
            return false;
        }
    }
}
