using System;
using Cysharp.Threading.Tasks;
using TEngine;
using UnityEngine;

namespace GameLogic.Takeover
{
    [Window(UILayer.UI, location: "TakeoverBattleUI", fullScreen: true)]
    public sealed class TakeoverBattleUI : UIWindow, ITakeoverFontSource
    {
        private TakeoverRunner _runner;
        private TakeoverBattleHudWidget _battleHud;
        private Font _font;

        public TakeoverRunner Runner => _runner;
        public Font Font => _font;
        public RectTransform SquadBadgeLayer => _battleHud?.SquadBadgeLayer;

        protected override void OnCreate()
        {
            _runner = UserData as TakeoverRunner;
            if (_runner == null)
                throw new InvalidOperationException("TakeoverBattleUI requires a TakeoverRunner.");

            _battleHud = CreateWidget<TakeoverBattleHudWidget>(
                "m_rect_TakeoverRuntimeUI/m_item_BattleHud");
            if (_battleHud == null)
                throw new InvalidOperationException("TakeoverBattleUI prefab is missing its battle HUD.");

            _battleHud.BindRunner(_runner);
            _runner.RegisterBattleUI(this);
            LoadFontAsync().Forget();
        }

        protected override void OnRefresh()
        {
            TakeoverRunner runner = UserData as TakeoverRunner;
            if (runner == null) return;
            if (_runner != null && _runner != runner)
                _runner.UnregisterBattleUI(this);
            _runner = runner;
            _battleHud?.BindRunner(_runner);
            _runner.RegisterBattleUI(this);
        }

        protected override void OnDestroy()
        {
            _runner?.UnregisterBattleUI(this);
            TakeoverUIResources.ReleaseFont(_font);
            _font = null;
        }

        public void ShowBattle()
        {
            _battleHud?.ShowBattle();
        }

        public TakeoverSquadBadgeWidget CreateSquadBadgeWidget()
        {
            return _battleHud != null ? _battleHud.CreateSquadBadge() : null;
        }

        public TakeoverBuildingBadgeWidget CreateBuildingBadgeWidget()
        {
            return _battleHud != null ? _battleHud.CreateBuildingBadge() : null;
        }

        private async UniTask LoadFontAsync()
        {
            _font = await TakeoverUIResources.LoadAndApplyFontAsync(
                gameObject, gameObject.GetCancellationTokenOnDestroy());
        }
    }
}
