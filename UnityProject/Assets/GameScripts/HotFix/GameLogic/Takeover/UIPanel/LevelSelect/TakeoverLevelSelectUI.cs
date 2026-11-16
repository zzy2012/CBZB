using System;
using Cysharp.Threading.Tasks;
using TEngine;
using UnityEngine;

namespace GameLogic.Takeover
{
    /// <summary>管理关卡选择窗口及其字体资源。</summary>
    [Window(UILayer.UI, location: "TakeoverLevelSelectUI", fullScreen: true)]
    public sealed class TakeoverLevelSelectUI : UIWindow, ITakeoverFontSource
    {
        /// <summary>关卡选择窗口使用的战斗运行器。</summary>
        private TakeoverRunner _runner;
        /// <summary>关卡选择内容组件。</summary>
        private TakeoverLevelSelectWidget _levelSelect;
        /// <summary>政令购买面板组件。</summary>
        private TakeoverEdictPanelWidget _edictPanel;
        /// <summary>关卡选择窗口持有的字体资源。</summary>
        private Font _font;

        /// <summary>界面使用的字体。</summary>
        public Font Font => _font;

        /// <summary>创建关卡选择窗口并初始化界面组件。</summary>
        protected override void OnCreate()
        {
            _runner = UserData as TakeoverRunner;
            if (_runner == null)
                throw new InvalidOperationException("TakeoverLevelSelectUI requires a TakeoverRunner.");

            _levelSelect = CreateWidget<TakeoverLevelSelectWidget>(
                "m_rect_TakeoverRuntimeUI/m_item_LevelSelect");
            if (_levelSelect == null)
                throw new InvalidOperationException("TakeoverLevelSelectUI prefab is missing its level widget.");

            _edictPanel = CreateWidget<TakeoverEdictPanelWidget>(
                "m_rect_TakeoverRuntimeUI/m_item_LevelSelect/m_rect_EdictPanel");
            if (_edictPanel == null)
                throw new InvalidOperationException("TakeoverLevelSelectUI prefab is missing its edict panel.");
            _edictPanel.SetVisible(false);

            _levelSelect.SetRunner(_runner);
            _levelSelect.SetEdictPanel(_edictPanel);
            LoadFontAsync().Forget();
        }

        /// <summary>刷新关卡选择窗口的界面状态。</summary>
        protected override void OnRefresh()
        {
            _runner = UserData as TakeoverRunner ?? _runner;
            _levelSelect?.SetRunner(_runner);
        }

        /// <summary>销毁窗口时释放已加载的字体资源。</summary>
        protected override void OnDestroy()
        {
            TakeoverUIResources.ReleaseFont(_font);
            _font = null;
        }

        /// <summary>异步加载字体并应用到关卡选择窗口。</summary>
        private async UniTask LoadFontAsync()
        {
            _font = await TakeoverUIResources.LoadAndApplyFontAsync(
                gameObject, gameObject.GetCancellationTokenOnDestroy());
            _edictPanel?.SetFont(_font);
        }
    }
}
