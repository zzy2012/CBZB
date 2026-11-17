using System;
using System.Collections.Generic;
using System.Linq;
using TEngine;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic.Takeover
{
    /// <summary>管理战斗 HUD 及场景标记组件。</summary>
    public sealed class TakeoverBattleHudWidget : UIWidget
    {
        /// <summary>底部速度按钮可选的模拟速度。</summary>
        private static readonly float[] BottomSpeeds = { 1f, 4f, 16f, 64f, 256f };
        /// <summary>未选中速度按钮的显示颜色。</summary>
        private static readonly Color SpeedIdleColor = new Color(0.18f, 0.15f, 0.10f, 0.95f);
        /// <summary>当前速度按钮的显示颜色。</summary>
        private static readonly Color SpeedActiveColor = new Color(0.38f, 0.31f, 0.15f, 0.98f);

        /// <summary>底部速度控制按钮集合。</summary>
        private readonly List<Button> _bottomSpeedButtons = new List<Button>(3);
        /// <summary>底部速度按钮的文本集合。</summary>
        private readonly List<Text> _bottomSpeedLabels = new List<Text>(3);
        /// <summary>驱动当前战斗的运行器。</summary>
        private TakeoverRunner _runner;
        /// <summary>战斗对象详情面板。</summary>
        private TakeoverInspectorWidget _inspector;
        /// <summary>玩家可用法术栏。</summary>
        private TakeoverSpellBarWidget _spellBar;
        /// <summary>战斗调试面板。</summary>
        private TakeoverGmPanelWidget _gmPanel;
        /// <summary>显示玩家资源的文本组件。</summary>
        private Text _resources;
        /// <summary>显示战斗时间的文本组件。</summary>
        private Text _clock;
        /// <summary>暂停或继续战斗的按钮。</summary>
        private Button _pauseButton;
        /// <summary>重新开始当前关卡的按钮。</summary>
        private Button _restartButton;
        /// <summary>打开关卡选择界面的按钮。</summary>
        private Button _levelsButton;
        /// <summary>打开设置界面的按钮。</summary>
        private Button _settingsButton;
        /// <summary>切换人类玩家 AI 托管的按钮。</summary>
        private Button _aiButton;
        /// <summary>显示 AI 托管状态的文本组件。</summary>
        private Text _aiLabel;
        /// <summary>AI 未托管时按钮的显示颜色。</summary>
        private static readonly Color AiIdleColor = new Color(0.18f, 0.15f, 0.10f, 0.95f);
        /// <summary>AI 托管时按钮的显示颜色。</summary>
        private static readonly Color AiActiveColor = new Color(0.30f, 0.52f, 0.30f, 0.98f);
        /// <summary>放置部队标记的界面层。</summary>
        private RectTransform _squadBadgeLayer;
        /// <summary>部队标记对应的预制体挂点。</summary>
        private readonly List<TakeoverSquadBadgeWidget> _squadSlots = new List<TakeoverSquadBadgeWidget>();
        /// <summary>建筑标记对应的预制体挂点。</summary>
        private readonly List<TakeoverBuildingBadgeWidget> _buildingSlots = new List<TakeoverBuildingBadgeWidget>();
        /// <summary>HUD 定时刷新的计时器标识。</summary>
        private int _timerId;

        /// <summary>用于放置部队标记的界面层。</summary>
        public RectTransform SquadBadgeLayer => _squadBadgeLayer;

        /// <summary>生成战斗 HUD 所需的界面组件。</summary>
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


            _bottomSpeedButtons.Clear();
            _bottomSpeedLabels.Clear();
            for (int i = 0; i < BottomSpeeds.Length; i++)
            {
                string path = $"m_rect_SpeedControls/m_btn_Speed{BottomSpeeds[i]:0}";
                _bottomSpeedButtons.Add(Required<Button>(path));
                _bottomSpeedLabels.Add(Required<Text>(path + "/m_text_Label"));
            }
        }

        /// <summary>注册 HUD 控件的按钮和操作事件。</summary>
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

        /// <summary>初始化 HUD 控件并启动界面刷新。</summary>
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

        /// <summary>停止 HUD 定时刷新并清理界面状态。</summary>
        protected override void OnDestroy()
        {
            if (_timerId != 0)
            {
                GameModule.Timer.RemoveTimer(_timerId);
                _timerId = 0;
            }
        }

        /// <summary>将战斗运行器绑定到 HUD。</summary>
        public void BindRunner(TakeoverRunner runner)
        {
            _runner = runner;
            _inspector?.SetRunner(runner);
            _spellBar?.SetRunner(runner);
            _gmPanel?.SetRunner(runner);
            RefreshHud();
        }

        /// <summary>显示战斗 HUD。</summary>
        public void ShowBattle()
        {
            _gmPanel?.SetGmExpanded(false);
            _inspector?.Refresh();
            _spellBar?.Refresh();
            RefreshHud();
        }

        /// <summary>创建部队标记组件。</summary>
        public TakeoverSquadBadgeWidget CreateSquadBadge()
        {
            return AcquireBadge(_squadSlots, "SquadBadgeSlot");
        }

        /// <summary>创建建筑标记组件。</summary>
        public TakeoverBuildingBadgeWidget CreateBuildingBadge()
        {
            return AcquireBadge(_buildingSlots, "BuildingBadgeSlot");
        }

        /// <summary>获取指定类型的战场标记界面实例。</summary>
        private T AcquireBadge<T>(List<T> slots, string prefix) where T : UIWidget, new()
        {
            if (slots.Count == 0)
            {
                foreach (Transform child in _squadBadgeLayer)
                    if (child.name.StartsWith(prefix, StringComparison.Ordinal))
                        slots.Add(CreateWidget<T>(child.gameObject, false));
            }

            foreach (T slot in slots)
            {
                if (slot.Visible) continue;
                slot.Visible = true;
                return slot;
            }

            return null;
        }
        /// <summary>按界面计时器刷新战斗 HUD。</summary>
        private void OnUiTick(object[] args)
        {
            if (gameObject.activeInHierarchy)
                RefreshHud();
        }

        /// <summary>同步资源、时间、战斗速度及按钮状态。</summary>
        private void RefreshHud()
        {
            BattleSimulation simulation = _runner != null ? _runner.Simulation : null;
            if (simulation == null) return;

            var player = simulation.State.Human;
            _resources.text =
                $"L{simulation.State.LevelAssetId}    " +
                $"{(simulation.State.IsOver ? "结束" : _runner.Paused ? "暂停" : $"×{_runner.Speed:0}")}\n" +
                $"金币 {Mathf.FloorToInt(player.Gold)}   +{player.NetGoldIncome()}/s    " +
                $"法力 {Mathf.FloorToInt(player.Mana)}/500\n" +
                $"补给 {player.SupplyUsed}/{player.SupplyCap}   " +
                $"部队 {player.Squads.Count(squad => !squad.IsDead)}   " +
                $"敌城 {CountEnemyKeyBuildings(simulation)}";
            _clock.text = $"{FormatTime(simulation.State.Time)}  ·  三星 < {FormatTime(330f)}";

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

        /// <summary>统计敌方仍控制的关键建筑数量。</summary>
        private static int CountEnemyKeyBuildings(BattleSimulation simulation)
        {
            int count = 0;
            foreach (var player in simulation.State.Players)
            {
                if (player.IsHuman) continue;
                foreach (MapNodeEntity node in player.OwnedNodes)
                {
                    if (TakeoverScene.IsKeyBuilding(node.Building))
                        count++;
                }
            }
            return count;
        }

        /// <summary>将战斗时间格式化为界面显示文本。</summary>
        private static string FormatTime(float seconds)
        {
            int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return $"{total / 60:00}:{total % 60:00}";
        }

        /// <summary>查找并返回指定的必需界面组件。</summary>
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
