using System;
using Cysharp.Threading.Tasks;
using TEngine;
using UnityEngine;

namespace GameLogic.Takeover
{
    /// <summary>管理战斗窗口及其 HUD 组件。</summary>
    [Window(UILayer.UI, location: "TakeoverBattleUI", fullScreen: true)]
    public sealed class TakeoverBattleUI : UIWindow, ITakeoverFontSource
    {
        /// <summary>当前战斗界面绑定的运行器。</summary>
        private TakeoverRunner _runner;
        /// <summary>战斗界面的 HUD 组件。</summary>
        private TakeoverBattleHudWidget _battleHud;
        /// <summary>界面加载并持有的字体资源。</summary>
        private Font _font;

        /// <summary>负责驱动当前战斗表现的运行器。</summary>
        public TakeoverRunner Runner => _runner;
        /// <summary>界面使用的字体。</summary>
        public Font Font => _font;
        /// <summary>用于放置部队标记的界面层。</summary>
        public RectTransform SquadBadgeLayer => _battleHud?.SquadBadgeLayer;

        /// <summary>创建战斗窗口并初始化 HUD。</summary>
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

        /// <summary>刷新战斗窗口及其运行器绑定。</summary>
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

        /// <summary>销毁战斗窗口时释放字体资源。</summary>
        protected override void OnDestroy()
        {
            _runner?.UnregisterBattleUI(this);
            TakeoverUIResources.ReleaseFont(_font);
            _font = null;
        }

        /// <summary>显示战斗界面的 HUD。</summary>
        public void ShowBattle()
        {
            _battleHud?.ShowBattle();
        }

        /// <summary>通过 HUD 创建部队标记组件。</summary>
        public TakeoverSquadBadgeWidget CreateSquadBadgeWidget()
        {
            return _battleHud != null ? _battleHud.CreateSquadBadge() : null;
        }

        /// <summary>通过 HUD 创建建筑标记组件。</summary>
        public TakeoverBuildingBadgeWidget CreateBuildingBadgeWidget()
        {
            return _battleHud != null ? _battleHud.CreateBuildingBadge() : null;
        }

        /// <summary>异步加载字体并应用到战斗窗口。</summary>
        private async UniTask LoadFontAsync()
        {
            _font = await TakeoverUIResources.LoadAndApplyFontAsync(
                gameObject, gameObject.GetCancellationTokenOnDestroy());
        }
    }
}
