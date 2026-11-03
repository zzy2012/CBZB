using System;
using UnityEngine.UI;

namespace GameLogic.Takeover
{
    public sealed class TakeoverLevelButtonWidget : UIWidget
    {
        private Button _button;
        private Text _label;
        private Action<int> _onClick;
        private int _levelId;

        protected override void ScriptGenerator()
        {
            _button = gameObject.GetComponent<Button>();
            _label = FindChildComponent<Text>("m_text_Label");
        }

        protected override void RegisterEvent()
        {
            if (_button == null || _label == null)
                throw new InvalidOperationException(
                    "Level button widget requires a root Button and m_text_Label Text.");
            _button.onClick.AddListener(HandleClick);
        }

        public void SetLevel(int levelId, bool unlocked, string label, Action<int> onClick)
        {
            _levelId = levelId;
            _button.interactable = unlocked;
            _label.text = label;
            _onClick = onClick;
        }

        private void HandleClick()
        {
            if (_button.interactable) _onClick?.Invoke(_levelId);
        }
    }
}
