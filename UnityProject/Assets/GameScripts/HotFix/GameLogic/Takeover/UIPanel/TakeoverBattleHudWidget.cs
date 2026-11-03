using System;
using System.Collections.Generic;
using System.Linq;
using TEngine;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic.Takeover
{
    public sealed class TakeoverBattleHudWidget : UIWidget
    {
        private static readonly float[] BottomSpeeds = { 1f, 4f, 16f, 64f, 256f };
        private static readonly Color SpeedIdleColor = new Color(0.18f, 0.15f, 0.10f, 0.95f);
        private static readonly Color SpeedActiveColor = new Color(0.38f, 0.31f, 0.15f, 0.98f);

        private readonly List<Button> _bottomSpeedButtons = new List<Button>(3);
        private readonly List<Text> _bottomSpeedLabels = new List<Text>(3);
        private TakeoverRunner _runner;
        private TakeoverInspectorWidget _inspector;
        private TakeoverSpellBarWidget _spellBar;
        private TakeoverGmPanelWidget _gmPanel;
        private Text _resources;
        private Text _clock;
        private Button _pauseButton;
        private Button _restartButton;
        private Button _levelsButton;
        private Button _settingsButton;
        private Button _aiButton;
        private Text _aiLabel;
        private static readonly Color AiIdleColor = new Color(0.18f, 0.15f, 0.10f, 0.95f);
        private static readonly Color AiActiveColor = new Color(0.30f, 0.52f, 0.30f, 0.98f);
        private RectTransform _squadBadgeLayer;
        private RectTransform _squadBadgePrototype;
        private RectTransform _buildingBadgePrototype;
        private int _timerId;

        public RectTransform SquadBadgeLayer => _squadBadgeLayer;

        protected override void ScriptGenerator()
        {
            _resources = Required<Text>("m_rect_BottomBar/m_rect_Top/m_text_Resources");
            _clock = Required<Text>("m_rect_BottomBar/m_rect_Top/m_text_Clock");
            _pauseButton = Required<Button>("m_rect_Controls/m_btn_Pause");
            _restartButton = Required<Button>("m_rect_Controls/m_btn_Restart");
            _levelsButton = Required<Button>("m_rect_Controls/m_btn_Levels");
            _settingsButton = Required<Button>("m_rect_BottomBar/m_btn_Settings");
            _aiButton = Required<Button>("m_rect_BottomBar/m_btn_AiToggle");
            _aiLabel = Required<Text>("m_rect_BottomBar/m_btn_AiToggle/m_text_Label");
            _squadBadgeLayer = Required<RectTransform>("m_rect_SquadBadgeLayer");
            _squadBadgePrototype = Required<RectTransform>(
                "m_rect_SquadBadgeLayer/m_item_SquadBadgePrototype");
            _buildingBadgePrototype = Required<RectTransform>(
                "m_rect_SquadBadgeLayer/m_item_BuildingBadgePrototype");

            _bottomSpeedButtons.Clear();
            _bottomSpeedLabels.Clear();
            for (int i = 0; i < BottomSpeeds.Length; i++)
            {
                string path = $"m_rect_SpeedControls/m_btn_Speed{BottomSpeeds[i]:0}";
                _bottomSpeedButtons.Add(Required<Button>(path));
                _bottomSpeedLabels.Add(Required<Text>(path + "/m_text_Label"));
            }
        }

        protected override void RegisterEvent()
        {
            _pauseButton.onClick.AddListener(() => _runner?.TogglePause());
            _restartButton.onClick.AddListener(() => _runner?.NewGame());
            _levelsButton.onClick.AddListener(() => _runner?.ShowLevelSelect());
            _settingsButton.onClick.AddListener(
                () => TakeoverToastUI.Show("设置面板还没做（音量 / 画质 / 退出）", 2.5f));
            _aiButton.onClick.AddListener(() => _runner?.ToggleHumanAi());

            for (int i = 0; i < BottomSpeeds.Length; i++)
            {
                float speed = BottomSpeeds[i];
                _bottomSpeedButtons[i].onClick.AddListener(() => _runner?.SetSpeed(speed));
            }
        }

        protected override void OnCreate()
        {
            _inspector = CreateWidget<TakeoverInspectorWidget>("m_item_Inspector");
            _spellBar = CreateWidget<TakeoverSpellBarWidget>("m_item_SpellBar");
            _gmPanel = CreateWidget<TakeoverGmPanelWidget>("m_item_GmPanel");
            if (_inspector == null || _spellBar == null || _gmPanel == null)
                throw new InvalidOperationException("Battle HUD prefab is missing a child widget.");

            BindRunner((OwnerWindow as TakeoverBattleUI)?.Runner);
            _timerId = GameModule.Timer.AddTimer(OnUiTick, 0.1f, isLoop: true, isUnscaled: true);
        }

        protected override void OnDestroy()
        {
            if (_timerId != 0)
            {
                GameModule.Timer.RemoveTimer(_timerId);
                _timerId = 0;
            }
        }

        public void BindRunner(TakeoverRunner runner)
        {
            _runner = runner;
            _inspector?.SetRunner(runner);
            _spellBar?.SetRunner(runner);
            _gmPanel?.SetRunner(runner);
            RefreshHud();
        }

        public void ShowBattle()
        {
            _gmPanel?.SetGmExpanded(false);
            _inspector?.Refresh();
            _spellBar?.Refresh();
            RefreshHud();
        }

        public TakeoverSquadBadgeWidget CreateSquadBadge()
        {
            if (_squadBadgePrototype == null || _squadBadgeLayer == null)
                return null;
            return CreateWidgetByPrefab<TakeoverSquadBadgeWidget>(
                _squadBadgePrototype.gameObject, _squadBadgeLayer);
        }

        public TakeoverBuildingBadgeWidget CreateBuildingBadge()
        {
            if (_buildingBadgePrototype == null || _squadBadgeLayer == null)
                return null;
            return CreateWidgetByPrefab<TakeoverBuildingBadgeWidget>(
                _buildingBadgePrototype.gameObject, _squadBadgeLayer);
        }

        private void OnUiTick(object[] args)
        {
            if (gameObject.activeInHierarchy)
                RefreshHud();
        }

        private void RefreshHud()
        {
            BattleSimulation simulation = _runner != null ? _runner.Simulation : null;
            if (simulation == null) return;

            var player = simulation.Human;
            _resources.text =
                $"L{simulation.LevelAssetId}    " +
                $"{(simulation.IsOver ? "结束" : _runner.Paused ? "暂停" : $"×{_runner.Speed:0}")}\n" +
                $"金币 {Mathf.FloorToInt(player.Gold)}   +{player.NetGoldIncome()}/s    " +
                $"法力 {Mathf.FloorToInt(player.Mana)}/500\n" +
                $"补给 {player.SupplyUsed}/{player.SupplyCap}   " +
                $"部队 {player.Squads.Count(squad => !squad.IsDead)}   " +
                $"敌城 {CountEnemyKeyBuildings(simulation)}";
            _clock.text = $"{FormatTime(simulation.Time)}  ·  三星 < {FormatTime(330f)}";

            _gmPanel.RefreshSpeed(_runner.Speed);
            for (int i = 0; i < BottomSpeeds.Length; i++)
            {
                bool active = Mathf.Approximately(_runner.Speed, BottomSpeeds[i]);
                _bottomSpeedLabels[i].text = active
                    ? $"×{BottomSpeeds[i]:0}*"
                    : $"×{BottomSpeeds[i]:0}";
                Graphic graphic = _bottomSpeedButtons[i].targetGraphic;
                if (graphic != null)
                    graphic.color = active ? SpeedActiveColor : SpeedIdleColor;
            }

            _inspector.Refresh();
            _spellBar.Refresh();

            bool ai = _runner.HumanAiControlled;
            if (_aiLabel != null) _aiLabel.text = ai ? "AI*" : "AI";
            Graphic aiGraphic = _aiButton != null ? _aiButton.targetGraphic : null;
            if (aiGraphic != null) aiGraphic.color = ai ? AiActiveColor : AiIdleColor;
        }

        private static int CountEnemyKeyBuildings(BattleSimulation simulation)
        {
            int count = 0;
            foreach (var player in simulation.Players)
            {
                if (player.IsHuman) continue;
                foreach (MapNode node in player.OwnedNodes)
                {
                    if (BattleSim.IsKeyBuilding(node.Building))
                        count++;
                }
            }
            return count;
        }

        private static string FormatTime(float seconds)
        {
            int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return $"{total / 60:00}:{total % 60:00}";
        }

        private T Required<T>(string path) where T : Component
        {
            T component = FindChildComponent<T>(path);
            if (component == null)
                throw new InvalidOperationException(
                    $"TakeoverBattleHudWidget prefab is missing {typeof(T).Name}: {path}");
            return component;
        }
    }
}
