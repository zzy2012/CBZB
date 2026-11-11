using Cysharp.Threading.Tasks;
using System.Linq;
using GameLogic;
using GameLogic.UI;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic.Takeover.UI
{
    [Window(UILayer.UI, location: "TakeoverUIWindow", fullScreen: true)]
    public sealed class BattleHudView : UIView
    {
        private TakeoverRunner _runner;
        private WorldBadgeLayer _badgeLayer;
        private InspectorController _inspector;
        private SpellBarController _spellBar;
        private BattleMenuController _battleMenu;
        private GmPanelController _gmPanel;
        private TutorialController _tutorial;
        private Text _title;
        private Text _resources;
        private Text _clock;
        private Button _speedButton;
        private float _refreshTimer;

        protected override void OnCreate(object args)
        {
            _runner = args as TakeoverRunner;
            if (_runner == null)
            {
                UIManager.Close<BattleHudView>();
                return;
            }

            ClearContent();
            var passThrough = Root.gameObject.AddComponent<CanvasGroup>();
            passThrough.interactable = true;
            passThrough.blocksRaycasts = true;

            RectTransform status = TakeoverUiFactory.CreateRow(Root, "StatusBar", 12f);
            TakeoverUiFactory.Anchor(status, new Vector2(0.015f, 0.91f),
                new Vector2(0.985f, 0.99f), Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
            _title = TakeoverUiFactory.CreateText(status, "BattleTitle", string.Empty, 20,
                TakeoverUiFactory.Accent);
            _resources = TakeoverUiFactory.CreateText(status, "BattleResources", string.Empty, 16);
            _clock = TakeoverUiFactory.CreateText(status, "BattleClock", string.Empty, 16,
                Color.white, TextAnchor.MiddleRight);
            TakeoverUiFactory.SetPreferredWidth(_title, 280f);
            TakeoverUiFactory.SetPreferredWidth(_clock, 250f);
            TakeoverUiFactory.SetPreferredHeight(status, 58f);

            RectTransform controls = TakeoverUiFactory.CreateRow(Root, "BattleControls", 6f);
            TakeoverUiFactory.Anchor(controls, new Vector2(0.63f, 0.82f),
                new Vector2(0.985f, 0.9f), Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
            Button settings = AddControl(controls, "Settings", "设置",
                () => UIManager.OpenOverlayAsync<SettingsOverlayView>(_runner).Forget());
            Button pause = AddControl(controls, "Pause", "暂停",
                () => UIManager.OpenOverlayAsync<PauseOverlayView>(_runner).Forget());
            _speedButton = AddControl(controls, "Speed", "×1",
                () =>
                {
                    _runner.CycleSpeed();
                    UIManager.Toast($"战斗速度：×{_runner.Speed:0}");
                    RefreshHud();
                });
            TakeoverUiFactory.SetPreferredWidth(settings, 110f);
            TakeoverUiFactory.SetPreferredWidth(pause, 110f);
            TakeoverUiFactory.SetPreferredWidth(_speedButton, 110f);
            TakeoverUiFactory.SetPreferredHeight(controls, 44f);

            RectTransform badgeRoot = TakeoverUiFactory.CreateRoot(Root, "WorldBadgeLayer");
            _badgeLayer = new WorldBadgeLayer(_runner, badgeRoot);

            _inspector = new InspectorController();
            _inspector.Bind(Root);
            _spellBar = new SpellBarController(_runner);
            _spellBar.Bind(Root);
            _battleMenu = new BattleMenuController(_runner);
            _battleMenu.Bind(Root);
            _gmPanel = new GmPanelController(_runner);
            _gmPanel.Bind(Root);
            _tutorial = new TutorialController();
            _tutorial.Bind(Root);
            RefreshHud();
        }

        public override void Tick(float unscaledDelta)
        {
            if (_runner == null) return;
            _badgeLayer?.Sync();
            _refreshTimer -= unscaledDelta;
            if (_refreshTimer > 0f) return;
            _refreshTimer = 0.1f;
            RefreshHud();
        }

        private void RefreshHud()
        {
            BattleSimulation simulation = _runner != null ? _runner.Simulation : null;
            if (simulation == null) return;
            PlayerState human = simulation.Human;
            if (_title != null)
                _title.text = $"{TakeoverUiContent.FactionName(_runner.HumanFaction)}战役 · 边境前线";
            if (_resources != null)
                _resources.text = $"金币 {UIFormat.Number(human.Gold)}  法力 {Mathf.FloorToInt(human.Mana)}/500  " +
                                  $"补给 {human.SupplyUsed}/{human.SupplyCap}  " +
                                  $"部队 {human.Squads.Count(squad => !squad.IsDead)}  " +
                                  $"敌城 {CountEnemyKeyBuildings(simulation)}  荣誉 {UIFormat.Number(Campaign.Honor)}";
            if (_clock != null)
                _clock.text = $"{UIFormat.Time(simulation.Time)} · 三星 < 05:30";
            if (_speedButton != null)
            {
                Text label = _speedButton.GetComponentInChildren<Text>();
                if (label != null) label.text = $"×{_runner.Speed:0}";
            }

            BattleActionPlan plan = TakeoverActionPlanner.Build(_runner);
            _inspector?.Refresh(plan);
            _battleMenu?.Refresh(plan);
            _spellBar?.Refresh();
            _gmPanel?.RefreshSpeed(_runner.Speed);
            _gmPanel?.RefreshAi(_runner.HumanAiControlled);
        }

        private static Button AddControl(Transform parent, string name, string label,
            System.Action click)
        {
            Button button = TakeoverUiFactory.CreateButton(parent, name, label, click, 15);
            TakeoverUiFactory.SetPreferredHeight(button, 40f);
            return button;
        }

        private static int CountEnemyKeyBuildings(BattleSimulation simulation)
        {
            int count = 0;
            foreach (PlayerState player in simulation.Players)
            {
                if (player.IsHuman) continue;
                foreach (MapNode node in player.OwnedNodes)
                    if (BattleSim.IsKeyBuilding(node.Building)) count++;
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
