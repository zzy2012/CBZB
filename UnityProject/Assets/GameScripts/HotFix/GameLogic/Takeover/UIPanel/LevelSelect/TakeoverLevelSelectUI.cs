using System;
using Cysharp.Threading.Tasks;
using TEngine;
using UnityEngine;

namespace GameLogic.Takeover
{
    [Window(UILayer.UI, location: "TakeoverLevelSelectUI", fullScreen: true)]
    public sealed class TakeoverLevelSelectUI : UIWindow, ITakeoverFontSource
    {
        private TakeoverRunner _runner;
        private TakeoverLevelSelectWidget _levelSelect;
        private TakeoverEdictPanelWidget _edictPanel;
        private Font _font;

        public Font Font => _font;

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

        protected override void OnRefresh()
        {
            _runner = UserData as TakeoverRunner ?? _runner;
            _levelSelect?.SetRunner(_runner);
        }

        protected override void OnDestroy()
        {
            TakeoverUIResources.ReleaseFont(_font);
            _font = null;
        }

        private async UniTask LoadFontAsync()
        {
            _font = await TakeoverUIResources.LoadAndApplyFontAsync(
                gameObject, gameObject.GetCancellationTokenOnDestroy());
            _edictPanel?.SetFont(_font);
        }
    }
}
