using System;
using Cysharp.Threading.Tasks;
using TEngine;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic.Takeover
{
    /// <summary>显示战斗结算结果的窗口。</summary>
    [Window(UILayer.UI, location: "TakeoverResultUI", fullScreen: true)]
    public sealed class TakeoverResultUI : UIWindow
    {
        /// <summary>用于重开或继续关卡的战斗运行器。</summary>
        private TakeoverRunner _runner;
        /// <summary>战斗结算标题文本组件。</summary>
        private Text _title;
        /// <summary>战斗结算详情文本组件。</summary>
        private Text _body;
        /// <summary>重新挑战当前关卡的按钮。</summary>
        private Button _replayButton;
        /// <summary>进入下一关的按钮。</summary>
        private Button _nextButton;
        /// <summary>返回关卡选择界面的按钮。</summary>
        private Button _menuButton;
        /// <summary>结算窗口持有的字体资源。</summary>
        private Font _font;

        /// <summary>生成战斗结算窗口所需的界面组件。</summary>
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

        /// <summary>注册战斗结算窗口的按钮事件。</summary>
        protected override void RegisterEvent()
        {
            _replayButton.onClick.AddListener(() => _runner?.NewGame());
            _nextButton.onClick.AddListener(StartNextLevel);
            _menuButton.onClick.AddListener(() => _runner?.ShowLevelSelect());
        }

        /// <summary>创建结算窗口并初始化显示内容。</summary>
        protected override void OnCreate()
        {
            _runner = UserData as TakeoverRunner;
            if (_runner == null)
                throw new InvalidOperationException("TakeoverResultUI requires a TakeoverRunner.");
            RefreshResult();
            LoadFontAsync().Forget();
        }

        /// <summary>根据战斗结果刷新结算窗口。</summary>
        protected override void OnRefresh()
        {
            _runner = UserData as TakeoverRunner ?? _runner;
            RefreshResult();
        }

        /// <summary>销毁结算窗口时释放字体资源。</summary>
        protected override void OnDestroy()
        {
            TakeoverUIResources.ReleaseFont(_font);
            _font = null;
        }

        /// <summary>开始战役中的下一关。</summary>
        private void StartNextLevel()
        {
            if (_runner == null) return;
            _runner.StartLevel(Campaign.NextLevel(_runner.LevelAssetId));
        }

        /// <summary>根据胜负和关卡成绩生成结算信息。</summary>
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

        /// <summary>异步加载字体并应用到结算窗口。</summary>
        private async UniTask LoadFontAsync()
        {
            _font = await TakeoverUIResources.LoadAndApplyFontAsync(
                gameObject, gameObject.GetCancellationTokenOnDestroy());
        }

        /// <summary>查找并返回指定的必需界面组件。</summary>
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
