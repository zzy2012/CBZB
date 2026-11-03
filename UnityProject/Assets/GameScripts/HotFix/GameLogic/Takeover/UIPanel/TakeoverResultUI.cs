using System;
using Cysharp.Threading.Tasks;
using TEngine;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic.Takeover
{
    [Window(UILayer.UI, location: "TakeoverResultUI", fullScreen: true)]
    public sealed class TakeoverResultUI : UIWindow
    {
        private TakeoverRunner _runner;
        private Text _title;
        private Text _body;
        private Button _replayButton;
        private Button _nextButton;
        private Button _menuButton;
        private Font _font;

        protected override void ScriptGenerator()
        {
            _title = Required<Text>(
                "m_rect_TakeoverRuntimeUI/m_rect_Result/m_rect_ResultPanel/m_text_Title");
            _body = Required<Text>(
                "m_rect_TakeoverRuntimeUI/m_rect_Result/m_rect_ResultPanel/m_text_Body");
            _replayButton = Required<Button>(
                "m_rect_TakeoverRuntimeUI/m_rect_Result/m_rect_ResultPanel/m_btn_Replay");
            _nextButton = Required<Button>(
                "m_rect_TakeoverRuntimeUI/m_rect_Result/m_rect_ResultPanel/m_btn_Next");
            _menuButton = Required<Button>(
                "m_rect_TakeoverRuntimeUI/m_rect_Result/m_rect_ResultPanel/m_btn_Menu");
        }

        protected override void RegisterEvent()
        {
            _replayButton.onClick.AddListener(() => _runner?.NewGame());
            _nextButton.onClick.AddListener(StartNextLevel);
            _menuButton.onClick.AddListener(() => _runner?.ShowLevelSelect());
        }

        protected override void OnCreate()
        {
            _runner = UserData as TakeoverRunner;
            if (_runner == null)
                throw new InvalidOperationException("TakeoverResultUI requires a TakeoverRunner.");
            RefreshResult();
            LoadFontAsync().Forget();
        }

        protected override void OnRefresh()
        {
            _runner = UserData as TakeoverRunner ?? _runner;
            RefreshResult();
        }

        protected override void OnDestroy()
        {
            TakeoverUIResources.ReleaseFont(_font);
            _font = null;
        }

        private void StartNextLevel()
        {
            if (_runner == null) return;
            _runner.StartLevel(Campaign.NextLevel(_runner.LevelAssetId));
        }

        private void RefreshResult()
        {
            BattleSimulation simulation = _runner != null ? _runner.Simulation : null;
            if (simulation == null) return;

            _title.text = simulation.HumanVictory ? "胜利" : "战败";
            if (simulation.HumanVictory)
            {
                _body.text =
                    $"L{simulation.LevelAssetId}  用时 {simulation.Time:F0}s   " +
                    $"{new string('★', simulation.Stars)}\n累计经验 {Campaign.Xp}";
            }
            else
            {
                _body.text =
                    $"L{simulation.LevelAssetId}  用时 {simulation.Time:F0}s\n重新部署后再次挑战";
            }
        }

        private async UniTask LoadFontAsync()
        {
            _font = await TakeoverUIResources.LoadAndApplyFontAsync(
                gameObject, gameObject.GetCancellationTokenOnDestroy());
        }

        private T Required<T>(string path) where T : Component
        {
            T component = FindChildComponent<T>(path);
            if (component == null)
                throw new InvalidOperationException(
                    $"TakeoverResultUI prefab is missing {typeof(T).Name}: {path}");
            return component;
        }
    }
}
