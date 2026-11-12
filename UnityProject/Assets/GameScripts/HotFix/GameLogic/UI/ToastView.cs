using UnityEngine;
using UnityEngine.UIElements;

namespace GameLogic.UI
{
    /// <summary>顶部居中提示条，复用单实例，显示 2.3 秒后自动隐藏。</summary>
    public sealed class ToastView : UIView
    {
        private Label _label;
        private VisualElement _panel;
        private float _remain;

        protected override string UxmlLocation => "TakeoverToastView";

        protected override void OnCreate(object args)
        {
            _panel = Q<VisualElement>("toast-panel");
            _label = Q<Label>("toast-label");

            // 提示条不拦截鼠标，否则全屏层会把世界点击全部吃掉。
            if (Root != null) Root.pickingMode = PickingMode.Ignore;
            if (_panel != null) _panel.pickingMode = PickingMode.Ignore;
            if (_label != null) _label.pickingMode = PickingMode.Ignore;

            Hide();
        }

        public void Show(string message, float seconds)
        {
            if (_label == null || _panel == null) return;
            _label.text = message;
            _panel.style.display = DisplayStyle.Flex;
            _remain = Mathf.Max(0.2f, seconds);
        }

        public override void Tick(float unscaledDelta)
        {
            if (_panel == null || _remain <= 0f) return;
            _remain -= unscaledDelta;
            if (_remain <= 0f) Hide();
        }

        private void Hide()
        {
            if (_panel == null) return;
            _panel.style.display = DisplayStyle.None;
            _remain = 0f;
        }
    }
}
