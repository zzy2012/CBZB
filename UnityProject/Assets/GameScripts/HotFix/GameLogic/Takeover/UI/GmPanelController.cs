using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GameLogic.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameLogic.Takeover.UI
{
    /// <summary>
    /// 原 TakeoverGmPanelWidget 的 UI Toolkit 版本：折叠面板、加钱/加蓝、胜负、AI 托管与 7 档调速。
    /// 默认折叠；内容容器背景透明且不拦截地图操作，只有按钮/文本可点。
    /// </summary>
    public sealed class GmPanelController
    {
        private static readonly float[] Speeds = { 1f, 2f, 4f, 8f, 16f, 32f, 64f };

        private readonly TakeoverRunner _runner;
        private readonly List<Label> _speedLabels = new List<Label>(Speeds.Length);
        private VisualElement _root;
        private VisualElement _content;
        private Button _toggle;
        private Label _aiLabel;
        private bool _expanded;

        public GmPanelController(TakeoverRunner runner)
        {
            _runner = runner;
        }

        public async UniTask LoadAsync(VisualElement host)
        {
            if (host == null) return;
            VisualTreeAsset tree = await UIToolkitAssets.LoadVisualTreeAsync("TakeoverGmPanelView");
            if (tree == null)
            {
                Debug.LogError("[TakeoverUI] 加载 GM 面板失败");
                return;
            }

            _root = UIToolkitAssets.InstantiateContent(tree);
            if (_root == null) return;
            _root.pickingMode = PickingMode.Ignore;
            host.Add(_root);

            _toggle = _root.Q<Button>("gm-toggle");
            _content = _root.Q<VisualElement>("gm-content");
            if (_content != null) _content.pickingMode = PickingMode.Ignore;
            Label title = _root.Q<Label>("gm-title");
            Label hint = _root.Q<Label>("gm-hint");
            if (title != null) title.text = "GM 面板";
            if (hint != null) hint.text = "调试用：资源、胜负与速度。折叠后不遮挡地图。";

            if (_toggle != null)
            {
                _toggle.text = "GM";
                _toggle.clicked += () => SetExpanded(!_expanded);
            }

            BindButton("gm-gold", () => _runner.GrantResources(1000, 0));
            BindButton("gm-mana", () => _runner.GrantResources(0, 500));
            BindButton("gm-win", () => _runner.GmEndBattle(true));
            BindButton("gm-lose", () => _runner.GmEndBattle(false));

            Button ai = _root.Q<Button>("gm-ai");
            _aiLabel = _root.Q<Label>("gm-ai-label");
            if (ai != null) ai.clicked += () => _runner.ToggleHumanAi();

            for (int i = 0; i < Speeds.Length; i++)
            {
                float speed = Speeds[i];
                string name = $"gm-speed-{speed:0}";
                BindButton(name, () => _runner.SetSpeed(speed));
                Label label = _root.Q<Label>(name + "-label");
                if (label != null) _speedLabels.Add(label);
            }

            SetExpanded(false);
            RefreshAi(_runner != null && _runner.HumanAiControlled);
            UIFontProvider.ApplyToTree(_root);
            UIToolkitTheme.Apply(_root);
        }

        public void SetExpanded(bool expanded)
        {
            _expanded = expanded;
            if (_content != null)
                _content.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;
            if (_toggle != null) _toggle.text = expanded ? "GM ▾" : "GM";
        }

        public void SetVisible(bool visible)
        {
            if (_root != null) _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void RefreshSpeed(float speed)
        {
            for (int i = 0; i < _speedLabels.Count && i < Speeds.Length; i++)
            {
                string label = $"×{Speeds[i]:0}";
                _speedLabels[i].text = Mathf.Approximately(speed, Speeds[i]) ? label + "*" : label;
            }
        }

        public void RefreshAi(bool controlled)
        {
            if (_aiLabel != null) _aiLabel.text = controlled ? "AI*" : "AI";
        }

        private void BindButton(string name, System.Action action)
        {
            Button button = _root?.Q<Button>(name);
            if (button == null)
            {
                Debug.LogError($"[TakeoverUI] GM 面板缺少按钮: {name}");
                return;
            }
            button.clicked += () =>
            {
                try
                {
                    action?.Invoke();
                }
                catch (System.Exception exception)
                {
                    Debug.LogError($"[TakeoverUI] GM 按钮 {name} 失败: {exception}");
                }
            };
        }
    }
}
