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
        private Button _edictsButton;
        private TakeoverEdictPanelWidget _edictPanel;
        private TakeoverRunner _runner;
        private bool _resetArmed;
        private RectTransform _details;
        private Text _detailsTitle;
        private Text _detailsRecord;
        private Text _detailsHint;
        private Button _fightButton;
        private Button _closeDetailsButton;
        private int _selectedLevel;

        protected override void ScriptGenerator()
        {
            Required<Text>("m_rect_ChapterPanel/m_text_Title");
            Required<Text>("m_rect_ChapterPanel/m_text_Hint");
            _resetButton = Required<Button>("m_rect_ChapterPanel/m_btn_ResetSave");
            _resetLabel = Required<Text>("m_rect_ChapterPanel/m_btn_ResetSave/m_text_Label");
            _edictsButton = Required<Button>("m_rect_ChapterPanel/m_btn_Edicts");
            _details = Required<RectTransform>("m_rect_LevelDetails");
            _detailsTitle = Required<Text>("m_rect_LevelDetails/m_text_EdictTitle");
            _detailsRecord = Required<Text>("m_rect_LevelDetails/m_text_EdictPoints");
            _detailsHint = Required<Text>("m_rect_LevelDetails/m_text_EdictHint");
            _fightButton = Required<Button>("m_rect_LevelDetails/m_btn_Fight");
            _closeDetailsButton = Required<Button>("m_rect_LevelDetails/m_btn_EdictClose");
            _fightButton.GetComponentInChildren<Text>().text = "去战斗";
            _closeDetailsButton.GetComponentInChildren<Text>().text = "返回";
        }

        protected override void RegisterEvent()
        {
            _resetButton.onClick.AddListener(HandleResetSave);
            _edictsButton.onClick.AddListener(HandleOpenEdicts);
            _closeDetailsButton.onClick.AddListener(CloseDetails);
            _fightButton.onClick.AddListener(StartSelectedLevel);
        }

        /// <summary>教令面板由窗口创建（和本章面板同级），这里只负责打开。</summary>
        public void SetEdictPanel(TakeoverEdictPanelWidget panel)
        {
            _edictPanel = panel;
        }

        private void HandleOpenEdicts()
        {
            _edictPanel?.SetVisible(true);
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
            if (!Campaign.IsUnlocked(levelId)) return;

            _selectedLevel = levelId;
            var record = Campaign.Record(levelId);
            _detailsTitle.text = $"西利亚公国 · 第 {levelId} 关";
            _detailsRecord.text = record.BestStars > 0
                ? new string('★', record.BestStars)
                : "尚未通关";
            _detailsHint.text = record.BestSeconds >= 0f
                ? $"最快通关 {record.BestSeconds:F0} 秒"
                : "占领敌方城堡，赢得战斗。";
            _details.gameObject.SetActive(true);
        }

        private void CloseDetails()
        {
            _selectedLevel = 0;
            _details.gameObject.SetActive(false);
        }

        private void StartSelectedLevel()
        {
            if (_runner == null || _selectedLevel == 0 || !Campaign.IsUnlocked(_selectedLevel))
                return;

            int levelId = _selectedLevel;
            CloseDetails();
            _runner.StartLevel(levelId);
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
