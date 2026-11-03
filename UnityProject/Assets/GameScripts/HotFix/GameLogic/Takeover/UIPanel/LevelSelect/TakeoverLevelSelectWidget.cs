using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic.Takeover
{
    public sealed class TakeoverLevelSelectWidget : UIWidget
    {
        private readonly List<TakeoverLevelButtonWidget> _levelButtons =
            new List<TakeoverLevelButtonWidget>(8);
        private Button _resetButton;
        private Text _resetLabel;
        private TakeoverRunner _runner;
        private bool _resetArmed;

        protected override void ScriptGenerator()
        {
            Required<Text>("m_rect_ChapterPanel/m_text_Title");
            Required<Text>("m_rect_ChapterPanel/m_text_Hint");
            _resetButton = Required<Button>("m_rect_ChapterPanel/m_btn_ResetSave");
            _resetLabel = Required<Text>("m_rect_ChapterPanel/m_btn_ResetSave/m_text_Label");
        }

        protected override void RegisterEvent()
        {
            _resetButton.onClick.AddListener(HandleResetSave);
        }

        protected override void OnCreate()
        {
            for (int levelId = 1; levelId <= 8; levelId++)
            {
                var button = CreateWidget<TakeoverLevelButtonWidget>(
                    $"m_rect_ChapterPanel/m_item_LevelButton{levelId}");
                if (button == null)
                    throw new InvalidOperationException($"Missing Level{levelId} button widget.");
                _levelButtons.Add(button);
            }
        }

        protected override void OnRefresh()
        {
            RefreshLevelButtons();
        }

        public void SetRunner(TakeoverRunner runner)
        {
            if (_runner != runner)
                _resetArmed = false;
            _runner = runner;
            RefreshLevelButtons();
        }

        private void HandleLevelClicked(int levelId)
        {
            _runner?.StartLevel(levelId);
        }

        private void HandleResetSave()
        {
            if (!_resetArmed)
            {
                _resetArmed = true;
                _resetLabel.text = "再次点击确认";
                TakeoverToastUI.Show("再次点击将清除全部首章进度", 4f);
                return;
            }

            Campaign.Reset();
            _resetArmed = false;
            _resetLabel.text = "清除进度";
            RefreshLevelButtons();
            TakeoverToastUI.Show("进度已重置");
        }

        private void RefreshLevelButtons()
        {
            for (int i = 0; i < _levelButtons.Count; i++)
            {
                int levelId = i + 1;
                bool unlocked = Campaign.IsUnlocked(levelId);
                var record = Campaign.Record(levelId);
                string label = unlocked
                    ? $"L{levelId}\n{new string('★', record.BestStars)}" +
                      (record.BestSeconds >= 0f ? $"  {record.BestSeconds:F0}s" : string.Empty)
                    : $"L{levelId}\n未解锁";
                _levelButtons[i].SetLevel(levelId, unlocked, label, HandleLevelClicked);
            }

            if (!_resetArmed)
                _resetLabel.text = "清除进度";
        }

        private T Required<T>(string path) where T : Component
        {
            T component = FindChildComponent<T>(path);
            if (component == null)
                throw new InvalidOperationException(
                    $"TakeoverLevelSelectWidget prefab is missing {typeof(T).Name}: {path}");
            return component;
        }
    }
}
