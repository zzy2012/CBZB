using System;
using UnityEngine.UI;

namespace GameLogic.Takeover
{
    /// <summary>显示并响应单个关卡选择按钮。</summary>
    public sealed class TakeoverLevelButtonWidget : UIWidget
    {
        /// <summary>关卡按钮的 Unity 按钮组件。</summary>
        private Button _button;
        /// <summary>关卡按钮的文本组件。</summary>
        private Text _label;
        /// <summary>点击关卡按钮时执行的回调。</summary>
        private Action<int> _onClick;
        /// <summary>当前按钮绑定的关卡标识。</summary>
        private int _levelId;

        /// <summary>生成关卡按钮所需的界面组件。</summary>
        protected override void ScriptGenerator()
        {
            _button = gameObject.GetComponent<Button>();
            _label = FindChildComponent<Text>("m_text_Label");
        }

        /// <summary>注册关卡按钮的点击事件。</summary>
        protected override void RegisterEvent()
        {
            if (_button == null || _label == null)
                throw new InvalidOperationException(
                    "Level button widget requires a root Button and m_text_Label Text.");
            _button.onClick.AddListener(HandleClick);
        }

        /// <summary>配置关卡按钮的标识、解锁状态、文本和点击回调。</summary>
        public void SetLevel(int levelId, bool unlocked, string label, Action<int> onClick)
        {
            _levelId = levelId;
            _button.interactable = unlocked;
            _label.text = label;
            _onClick = onClick;
        }

        /// <summary>处理关卡按钮点击并调用对应回调。</summary>
        private void HandleClick()
        {
            if (_button.interactable) _onClick?.Invoke(_levelId);
        }
    }
}
