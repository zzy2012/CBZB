using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic.Takeover
{
    /// <summary>显示关卡选择内容并与战斗入口交互。</summary>
    public sealed class TakeoverLevelSelectWidget : UIWidget
    {
        /// <summary>关卡选择按钮集合。</summary>
        private readonly List<TakeoverLevelButtonWidget> _levelButtons =
            new List<TakeoverLevelButtonWidget>(8);
        /// <summary>重置战役进度的按钮。</summary>
        private Button _resetButton;
        /// <summary>重置按钮上的文本组件。</summary>
        private Text _resetLabel;
        /// <summary>打开政令面板的按钮。</summary>
        private Button _edictsButton;
        /// <summary>关卡选择界面中的政令面板。</summary>
        private TakeoverEdictPanelWidget _edictPanel;
        /// <summary>用于开始关卡的战斗运行器。</summary>
        private TakeoverRunner _runner;
        /// <summary>是否已进入二次确认重置状态。</summary>
        private bool _resetArmed;
        /// <summary>关卡详情面板。</summary>
        private RectTransform _details;
        /// <summary>关卡详情标题文本组件。</summary>
        private Text _detailsTitle;
        /// <summary>显示关卡历史成绩的文本组件。</summary>
        private Text _detailsRecord;
        /// <summary>显示关卡详情提示的文本组件。</summary>
        private Text _detailsHint;
        /// <summary>开始所选关卡的按钮。</summary>
        private Button _fightButton;
        /// <summary>关闭关卡详情面板的按钮。</summary>
        private Button _closeDetailsButton;
        /// <summary>当前在详情面板中选中的关卡标识。</summary>
        private int _selectedLevel;

        /// <summary>生成关卡选择界面所需的组件。</summary>
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

        /// <summary>注册关卡选择界面的按钮事件。</summary>
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

        /// <summary>打开当前阵营的政令选择面板。</summary>
        private void HandleOpenEdicts()
        {
            _edictPanel?.SetVisible(true);
        }

        /// <summary>创建关卡列表并初始化界面控件。</summary>
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

        /// <summary>刷新关卡选择界面和解锁状态。</summary>
        protected override void OnRefresh()
        {
            RefreshLevelButtons();
        }

        /// <summary>设置关卡选择组件使用的战斗运行器。</summary>
        public void SetRunner(TakeoverRunner runner)
        {
            if (_runner != runner)
                _resetArmed = false;
            _runner = runner;
            RefreshLevelButtons();
        }

        /// <summary>显示所选关卡的详情信息。</summary>
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

        /// <summary>关闭当前关卡详情面板。</summary>
        private void CloseDetails()
        {
            _selectedLevel = 0;
            _details.gameObject.SetActive(false);
        }

        /// <summary>开始当前选中的关卡。</summary>
        private void StartSelectedLevel()
        {
            if (_runner == null || _selectedLevel == 0 || !Campaign.IsUnlocked(_selectedLevel))
                return;

            int levelId = _selectedLevel;
            CloseDetails();
            _runner.StartLevel(levelId);
        }

        /// <summary>处理战役存档重置及二次确认。</summary>
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

        /// <summary>根据解锁进度刷新所有关卡按钮。</summary>
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

        /// <summary>查找并返回必需的界面组件。</summary>
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
