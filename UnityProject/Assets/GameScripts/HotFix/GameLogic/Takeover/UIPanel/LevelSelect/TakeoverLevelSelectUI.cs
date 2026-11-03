using System;
using Cysharp.Threading.Tasks;
using TEngine;
using UnityEngine;

namespace GameLogic.Takeover
{
    [Window(UILayer.UI, location: "TakeoverLevelSelectUI", fullScreen: true)]
    public sealed class TakeoverLevelSelectUI : UIWindow
    {
        private TakeoverRunner _runner;
        private TakeoverLevelSelectWidget _levelSelect;
        private Font _font;

        protected override void OnCreate()
        {
            _runner = UserData as TakeoverRunner;
            if (_runner == null)
                throw new InvalidOperationException("TakeoverLevelSelectUI requires a TakeoverRunner.");

            _levelSelect = CreateWidget<TakeoverLevelSelectWidget>(
                "m_rect_TakeoverRuntimeUI/m_item_LevelSelect");
            if (_levelSelect == null)
                throw new InvalidOperationException("TakeoverLevelSelectUI prefab is missing its level widget.");
            _levelSelect.SetRunner(_runner);
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
        }
    }
}
