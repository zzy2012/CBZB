using System.Linq;
using Cysharp.Threading.Tasks;
using GameLogic.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameLogic.Takeover.UI
{
    /// <summary>
    /// 战斗 HUD：标题、控制按钮、资源、教程、检查器、法术栏、战斗菜单、GM 与头顶徽标。
    /// </summary>
    public sealed class BattleHudView : UIView
    {
        private TakeoverRunner _runner;
        private WorldBadgeLayer _badgeLayer;
        private InspectorController _inspector;
        private SpellBarController _spellBar;
        private BattleMenuController _battleMenu;
        private GmPanelController _gmPanel;
        private TutorialController _tutorial;

        private Label _title;
        private Label _gold;
        private Label _mana;
        private Label _supply;
        private Label _army;
        private Label _honor;
        private Label _enemy;
        private Label _clock;
        private Button _speedButton;
        private Button _pauseButton;
        private float _refreshTimer;

        protected override string UxmlLocation => "TakeoverBattleHudView";

        protected override string[] ExtraStyleSheets =>
            new[] { UIToolkitAssets.ScreenStyleLocation, UIToolkitAssets.BattleStyleLocation };

        protected override void OnCreate(object args)
        {
            _runner = args as TakeoverRunner;
            if (_runner == null)
            {
                UIManager.Close<BattleHudView>();
                return;
            }

            Require("battle-title", "settings-button", "pause-button", "speed-button",
                "badge-layer", "inspector", "inspector-title", "inspector-body",
                "spell-list", "menu-host", "gm-host", "tutorial-text", "tutorial-avatar",
                "tutorial-tag", "next-tutorial-button", "commander-name",
                "gold-value", "mana-value", "supply-value", "army-value", "honor-value",
                "enemy-value", "clock-label");

            _title = Q<Label>("battle-title");
            // HUD 根节点铺满全屏，但空白区域必须让世界点击穿透。
            if (Root != null) Root.pickingMode = PickingMode.Ignore;
            _gold = Q<Label>("gold-value");
            _mana = Q<Label>("mana-value");
            _supply = Q<Label>("supply-value");
            _army = Q<Label>("army-value");
            _honor = Q<Label>("honor-value");
            _enemy = Q<Label>("enemy-value");
            _clock = Q<Label>("clock-label");
            _speedButton = Q<Button>("speed-button");
            _pauseButton = Q<Button>("pause-button");

            Label commander = Q<Label>("commander-name");
            if (commander != null) commander.text = "边境统帅";

            Button settings = Q<Button>("settings-button");
            if (settings != null)
            {
                settings.text = "设置";
                settings.clicked += () =>
                    UIManager.OpenOverlayAsync<SettingsOverlayView>(_runner).Forget();
            }

            if (_pauseButton != null)
            {
                _pauseButton.text = "暂停";
                _pauseButton.clicked += () =>
                    UIManager.OpenOverlayAsync<PauseOverlayView>(_runner).Forget();
            }

            if (_speedButton != null)
            {
                _speedButton.clicked += () =>
                {
                    _runner.CycleSpeed();
                    UIManager.Toast($"战斗速度：×{_runner.Speed:0}");
                    RefreshHud();
                };
            }

            _badgeLayer = new WorldBadgeLayer(_runner, Q<VisualElement>("badge-layer"));

            VisualElement menuHost = Q<VisualElement>("menu-host");
            if (menuHost != null) menuHost.pickingMode = PickingMode.Ignore;
            VisualElement gmHost = Q<VisualElement>("gm-host");
            if (gmHost != null) gmHost.pickingMode = PickingMode.Ignore;

            _inspector = new InspectorController();
            _inspector.Bind(Q<VisualElement>("inspector"));

            _spellBar = new SpellBarController(_runner);
            _spellBar.Bind(Q<VisualElement>("spell-list"));

            _battleMenu = new BattleMenuController(_runner);
            _battleMenu.LoadAsync(Q<VisualElement>("menu-host")).Forget();

            _gmPanel = new GmPanelController(_runner);
            _gmPanel.LoadAsync(Q<VisualElement>("gm-host")).Forget();

            _tutorial = new TutorialController();
            _tutorial.Bind(Q<Label>("tutorial-text"), Q<VisualElement>("tutorial-avatar"),
                Q<Label>("tutorial-tag"), Q<Button>("next-tutorial-button"));

            RefreshHud(true);
        }

        public override void Tick(float unscaledDelta)
        {
            if (_runner == null) return;
            _badgeLayer?.Sync();

            _refreshTimer -= unscaledDelta;
            if (_refreshTimer > 0f) return;
            _refreshTimer = 0.1f;
            RefreshHud(false);
        }

        private void RefreshHud(bool force = false)
        {
            BattleSimulation simulation = _runner != null ? _runner.Simulation : null;
            if (simulation == null) return;

            PlayerState human = simulation.Human;
            if (_title != null)
                _title.text = $"{TakeoverUiContent.FactionName(_runner.HumanFaction)}战役 · 边境前线";

            if (_gold != null) _gold.text = UIFormat.Number(human.Gold);
            if (_mana != null) _mana.text = $"{Mathf.FloorToInt(human.Mana)}/500";
            if (_supply != null) _supply.text = $"{human.SupplyUsed}/{human.SupplyCap}";
            if (_army != null) _army.text = human.Squads.Count(squad => !squad.IsDead).ToString();
            if (_honor != null) _honor.text = UIFormat.Number(Campaign.Honor);
            if (_enemy != null) _enemy.text = CountEnemyKeyBuildings(simulation).ToString();
            if (_clock != null) _clock.text = $"{UIFormat.Time(simulation.Time)} · 三星 < 05:30";
            if (_speedButton != null) _speedButton.text = $"×{_runner.Speed:0}";

            BattleActionPlan plan = TakeoverActionPlanner.Build(_runner);
            _inspector?.Refresh(plan);
            _battleMenu?.Refresh(plan);
            _spellBar?.Refresh();
            _gmPanel?.RefreshSpeed(_runner.Speed);
            _gmPanel?.RefreshAi(_runner.HumanAiControlled);
        }

        private static int CountEnemyKeyBuildings(BattleSimulation simulation)
        {
            int count = 0;
            foreach (PlayerState player in simulation.Players)
            {
                if (player.IsHuman) continue;
                foreach (MapNode node in player.OwnedNodes)
                {
                    if (BattleSim.IsKeyBuilding(node.Building)) count++;
                }
            }
            return count;
        }

        protected override void OnClose()
        {
            _badgeLayer?.Clear();
            _badgeLayer = null;
            _runner = null;
        }
    }
}
