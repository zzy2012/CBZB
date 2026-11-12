using UnityEngine.UIElements;

namespace GameLogic.Takeover.UI
{
    /// <summary>选中目标的标题/正文检查器。</summary>
    public sealed class InspectorController
    {
        private VisualElement _root;
        private Label _title;
        private Label _body;

        public void Bind(VisualElement root)
        {
            _root = root;
            if (_root == null) return;
            _title = _root.Q<Label>("inspector-title");
            _body = _root.Q<Label>("inspector-body");
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

            _root.style.display = DisplayStyle.Flex;
            if (_title != null) _title.text = plan.Title;
            if (_body != null) _body.text = plan.Body;
        }

        public void Hide()
        {
            if (_root != null) _root.style.display = DisplayStyle.None;
        }
    }
}
