using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using TEngine;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace GameLogic.Takeover
{
    /// <summary>
    /// 首章战斗窗口，通过同名预制体绑定界面节点。
    /// </summary>
    [Window(UILayer.UI, location: "TakeoverUIWindow", fullScreen: true)]
    public sealed class TakeoverUIWindow : UIWindow
    {
        private const string ActionButtonPrefabLocation = "TakeoverUIActionButton";

        private TakeoverRunner _runner;
        private Font _font;
        private GameObject _actionButtonTemplate;
        private GameObject _menu;
        private GameObject _hud;
        private GameObject _result;
        private GameObject _inspector;
        private GameObject _spellBar;
        private Text _resources;
        private Text _clock;
        private Text _toast;
        private GameObject _toastPanel;
        private Text _resultTitle;
        private Text _resultBody;
        private Text _inspectorTitle;
        private Text _inspectorBody;
        private Text _resetLabel;
        private Transform _inspectorActions;
        private readonly List<Button> _levelButtons = new List<Button>();
        private readonly List<Text> _levelLabels = new List<Text>();
        private readonly List<Text> _gmSpeedLabels = new List<Text>();
        private static readonly float[] GmSpeeds = { 1f, 2f, 4f, 8f };
        private string _lastInspectorKey;
        private string _lastSpellState;
        private float _toastUntil;
        private bool _resetArmed;
        private Font _originalFont;

        protected override void OnCreate()
        {
            _runner = UserData as TakeoverRunner;
            InitializeAsync(gameObject.GetCancellationTokenOnDestroy()).Forget();
        }

        private async UniTask InitializeAsync(CancellationToken cancellationToken)
        {
            var templateText = gameObject.GetComponentInChildren<Text>(true);
            var fallbackFont = templateText != null ? templateText.font : null;
            Font loadedFont = null;
            try
            {
                loadedFont = await GameModule.Resource.LoadAssetAsync<Font>(
                    "takeover_font_328_Chaparral_Pro", cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception exception)
            {
                Log.Warning($"[Takeover] 字体加载失败，使用界面默认字体: {exception.Message}");
            }

            if (cancellationToken.IsCancellationRequested)
            {
                if (loadedFont != null) GameModule.Resource.UnloadAsset(loadedFont);
                return;
            }

            _originalFont = loadedFont;
            _font = _originalFont != null ? _originalFont : fallbackFont;
            var scaler = gameObject.GetComponentInParent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.referenceResolution = new Vector2(
                    OriginalRules.NativeStageWidth, OriginalRules.NativeStageHeight);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
            }

            foreach (var text in gameObject.GetComponentsInChildren<Text>(true))
                text.font = _font;

            BindStaticUi();
            _actionButtonTemplate = GameModule.Resource.LoadGameObject(ActionButtonPrefabLocation, transform);
            if (_actionButtonTemplate == null)
                throw new InvalidOperationException($"未能加载 UI 列表项预制体：{ActionButtonPrefabLocation}");
            _actionButtonTemplate.name = "ActionButtonTemplate";
            if (_actionButtonTemplate.GetComponent<Button>() == null ||
                _actionButtonTemplate.GetComponentInChildren<Text>(true) == null)
                throw new InvalidOperationException($"UI 列表项预制体缺少 Button 或 Text：{ActionButtonPrefabLocation}");
            _actionButtonTemplate.SetActive(false);

            _runner?.RegisterUI(this);
        }

        protected override void OnDestroy()
        {
            if (_originalFont != null)
            {
                GameModule.Resource.UnloadAsset(_originalFont);
                _originalFont = null;
            }
        }

        protected override void OnRefresh()
        {
            _runner = UserData as TakeoverRunner ?? _runner;
            _runner?.RegisterUI(this);
            RefreshLevelButtons();
        }

        protected override void OnUpdate()
        {
            if (_runner == null || _hud == null || _inspector == null || _spellBar == null) return;
            if (_runner.Simulation != null && _runner.Simulation.IsOver)
            {
                _inspector.SetActive(false);
                if (_toastPanel != null && Time.unscaledTime >= _toastUntil) _toastPanel.SetActive(false);
                return;
            }
            if (_hud.activeSelf && _runner.Simulation != null)
            {
                RefreshHud();
                string spellState =
                    $"{_runner.Simulation.Human.SpellsEnabled}|{_runner.Simulation.Human.Mana:F0}|" +
                    $"{_runner.Simulation.Human.Ultimate:F0}|" +
                    $"{_runner.Simulation.State.AvailableSpells(_runner.Simulation.Human).Count}";
                if (spellState != _lastSpellState)
                {
                    _lastSpellState = spellState;
                    RefreshSpellBar();
                }
            }
            RefreshInspector();
            if (_toastPanel != null && Time.unscaledTime >= _toastUntil && _toastPanel.activeSelf)
                _toastPanel.SetActive(false);
        }

        public void ShowLevelSelect()
        {
            _resetArmed = false;
            _menu?.SetActive(true);
            _hud?.SetActive(false);
            _result?.SetActive(false);
            _inspector?.SetActive(false);
            _spellBar?.SetActive(false);
            RefreshLevelButtons();
            if (Campaign.LastLoadWarning != null)
                ShowToast(Campaign.LastLoadWarning, 5f);
        }

        public void ShowBattle()
        {
            _menu?.SetActive(false);
            _hud?.SetActive(true);
            _result?.SetActive(false);
            _inspector?.SetActive(false);
            _spellBar?.SetActive(true);
            _lastInspectorKey = null;
            _lastSpellState = null;
            RefreshSpellBar();
        }

        public void ShowResult(BattleSimulation simulation)
        {
            if (_result == null || simulation == null) return;
            _result.transform.SetAsLastSibling();
            _result.SetActive(true);
            _hud?.SetActive(false);
            _inspector?.SetActive(false);
            _spellBar?.SetActive(false);
            _resultTitle.text = simulation.HumanVictory ? "胜利" : "战败";
            if (simulation.HumanVictory)
            {
                _resultBody.text =
                    $"L{simulation.LevelAssetId}  用时 {simulation.Time:F0}s   {new string('★', simulation.Stars)}\n" +
                    $"累计经验 {Campaign.Xp}";
            }
            else
            {
                _resultBody.text = $"L{simulation.LevelAssetId}  用时 {simulation.Time:F0}s\n重新部署后再次挑战";
            }
        }

        public void ShowToast(string message, float seconds = 2.5f)
        {
            if (_toast == null || _toastPanel == null || string.IsNullOrEmpty(message)) return;
            _toast.text = message;
            _toastPanel.SetActive(true);
            _toastPanel.transform.SetAsLastSibling();
            _toastUntil = Time.unscaledTime + seconds;
        }

        private void BindStaticUi()
        {
            _menu = FindRequired<RectTransform>("TakeoverRuntimeUI/LevelSelect").gameObject;
            _hud = FindRequired<RectTransform>("TakeoverRuntimeUI/BattleHUD").gameObject;
            _result = FindRequired<RectTransform>("TakeoverRuntimeUI/Result").gameObject;
            _inspector = FindRequired<RectTransform>("TakeoverRuntimeUI/BattleHUD/Inspector").gameObject;
            _spellBar = FindRequired<RectTransform>("TakeoverRuntimeUI/BattleHUD/Spells").gameObject;
            _toastPanel = FindRequired<RectTransform>("TakeoverRuntimeUI/ToastPanel").gameObject;
            _toast = FindRequired<Text>("TakeoverRuntimeUI/ToastPanel/ToastText");
            _resources = FindRequired<Text>("TakeoverRuntimeUI/BattleHUD/Top/Resources");
            _clock = FindRequired<Text>("TakeoverRuntimeUI/BattleHUD/Top/Clock");
            _inspectorTitle = FindRequired<Text>("TakeoverRuntimeUI/BattleHUD/Inspector/InspectorTitle");
            _inspectorBody = FindRequired<Text>("TakeoverRuntimeUI/BattleHUD/Inspector/InspectorBody");
            _inspectorActions = FindRequired<RectTransform>("TakeoverRuntimeUI/BattleHUD/Inspector/Actions");
            _resultTitle = FindRequired<Text>("TakeoverRuntimeUI/Result/ResultPanel/Title");
            _resultBody = FindRequired<Text>("TakeoverRuntimeUI/Result/ResultPanel/Body");

            for (int i = 0; i < 8; i++)
            {
                int levelId = i + 1;
                var button = FindRequired<Button>(
                    $"TakeoverRuntimeUI/LevelSelect/ChapterPanel/Level{levelId}");
                _levelButtons.Add(button);
                _levelLabels.Add(FindRequired<Text>(
                    $"TakeoverRuntimeUI/LevelSelect/ChapterPanel/Level{levelId}/Label"));
                button.onClick.AddListener(delegate { _runner?.StartLevel(levelId); });
            }

            var resetButton = FindRequired<Button>(
                "TakeoverRuntimeUI/LevelSelect/ChapterPanel/ResetSave");
            _resetLabel = FindRequired<Text>(
                "TakeoverRuntimeUI/LevelSelect/ChapterPanel/ResetSave/Label");
            resetButton.onClick.AddListener(HandleResetSave);

            FindRequired<Button>("TakeoverRuntimeUI/BattleHUD/Controls/Pause")
                .onClick.AddListener(delegate { _runner?.TogglePause(); });
            FindRequired<Button>("TakeoverRuntimeUI/BattleHUD/Controls/Restart")
                .onClick.AddListener(delegate { _runner?.NewGame(); });
            FindRequired<Button>("TakeoverRuntimeUI/BattleHUD/Controls/Levels")
                .onClick.AddListener(delegate { _runner?.ShowLevelSelect(); });
            FindRequired<Button>("TakeoverRuntimeUI/BattleHUD/GmControls/GmGold")
                .onClick.AddListener(delegate { _runner?.GrantResources(1000, 0); });
            FindRequired<Button>("TakeoverRuntimeUI/BattleHUD/GmControls/GmMana")
                .onClick.AddListener(delegate { _runner?.GrantResources(0, 500); });

            _gmSpeedLabels.Clear();
            for (int i = 0; i < GmSpeeds.Length; i++)
            {
                float speed = GmSpeeds[i];
                var button = FindRequired<Button>(
                    $"TakeoverRuntimeUI/BattleHUD/GmControls/GmSpeed{speed:0}");
                _gmSpeedLabels.Add(FindRequired<Text>(
                    $"TakeoverRuntimeUI/BattleHUD/GmControls/GmSpeed{speed:0}/Label"));
                button.onClick.AddListener(delegate { _runner?.SetSpeed(speed); });
            }

            FindRequired<Button>("TakeoverRuntimeUI/Result/ResultPanel/Replay")
                .onClick.AddListener(delegate { _runner?.NewGame(); });
            FindRequired<Button>("TakeoverRuntimeUI/Result/ResultPanel/Next")
                .onClick.AddListener(delegate
                {
                    if (_runner == null) return;
                    _runner.StartLevel(Campaign.NextLevel(_runner.LevelAssetId));
                });
            FindRequired<Button>("TakeoverRuntimeUI/Result/ResultPanel/Menu")
                .onClick.AddListener(delegate { _runner?.ShowLevelSelect(); });
        }

        private T FindRequired<T>(string path) where T : Component
        {
            var component = FindChildComponent<T>(path);
            if (component == null)
                throw new InvalidOperationException($"TakeoverUIWindow 预制体缺少节点或组件：{path}");
            return component;
        }

        private void HandleResetSave()
        {
            if (!_resetArmed)
            {
                _resetArmed = true;
                _resetLabel.text = "再次点击确认";
                ShowToast("再次点击将清除全部首章进度", 4f);
                return;
            }

            Campaign.Reset();
            _resetArmed = false;
            _resetLabel.text = "清除进度";
            RefreshLevelButtons();
            ShowToast("进度已重置");
        }

        private void RefreshLevelButtons()
        {
            for (int i = 0; i < _levelButtons.Count; i++)
            {
                int levelId = i + 1;
                bool unlocked = Campaign.IsUnlocked(levelId);
                var record = Campaign.Record(levelId);
                _levelButtons[i].interactable = unlocked;
                _levelLabels[i].text = unlocked
                    ? $"L{levelId}\n{new string('★', record.BestStars)}" +
                      (record.BestSeconds >= 0f ? $"  {record.BestSeconds:F0}s" : string.Empty)
                    : $"L{levelId}\n未解锁";
            }
        }

        private void RefreshHud()
        {
            var sim = _runner.Simulation;
            var player = sim.Human;
            _resources.text =
                $"L{sim.LevelAssetId}    {(sim.IsOver ? "结束" : _runner.Paused ? "暂停" : $"×{_runner.Speed:0}")}\n" +
                $"金币 {Mathf.FloorToInt(player.Gold)}   +{player.NetGoldIncome()}/s    " +
                $"法力 {Mathf.FloorToInt(player.Mana)}/500\n" +
                $"补给 {player.SupplyUsed}/{player.SupplyCap}   " +
                $"部队 {player.Squads.Count(s => !s.IsDead)}   " +
                $"敌城 {sim.Players.Where(p => !p.IsHuman).Sum(p => p.OwnedNodes.Count(n => BattleSim.IsKeyBuilding(n.Building)))}";
            _clock.text = $"{FormatTime(sim.Time)}  ·  三星 < {FormatTime(330f)}";
            for (int i = 0; i < _gmSpeedLabels.Count; i++)
            {
                string speed = $"×{GmSpeeds[i]:0}";
                _gmSpeedLabels[i].text = Mathf.Approximately(_runner.Speed, GmSpeeds[i])
                    ? $"{speed}*"
                    : speed;
            }
        }

        private void RefreshInspector()
        {
            if (_runner == null || _runner.Simulation == null || _runner.LevelSelectOpen) return;
            string targetKey = _runner.SelectedSquad != null
                ? $"squad:{_runner.SelectedSquad.Id}"
                : _runner.SelectedNode != null ? $"node:{_runner.SelectedNode.Id}" : "none";
            string inspectorKey = $"{targetKey}|{Mathf.FloorToInt(_runner.Simulation.Human.Gold)}|" +
                                  $"{_runner.Simulation.Human.FreeSupply}|" +
                                  $"{(_runner.SelectedNode?.Building?.Level ?? -1)}";
            if (inspectorKey == _lastInspectorKey) return;
            _lastInspectorKey = inspectorKey;
            ClearChildren(_inspectorActions);

            if (_runner.SelectedSquad != null)
            {
                var squad = _runner.SelectedSquad;
                _inspector.SetActive(true);
                _inspectorTitle.text = $"{squad.Def.CnName}  {squad.AliveSoldiers}/{squad.MaxSoldiers}";
                _inspectorBody.text =
                    $"HP {squad.Hp:F0}/{squad.HpPerSoldier * squad.MaxSoldiers:F0}\n" +
                    $"等级 {squad.LevelIndex}   经验 {squad.Xp:F0}\n" +
                    $"伤害基础 {squad.Def.DmgMin}-{squad.Def.DmgMax} × {squad.AliveSoldiers}\n" +
                    $"射程 {squad.Def.Range}   移速 {squad.Speed:F1}\n" +
                    $"{(squad.TargetNode != null ? $"目标 {squad.TargetNode.Key}" : "待命")}";
                return;
            }

            var node = _runner.SelectedNode;
            if (node == null)
            {
                _inspector.SetActive(false);
                return;
            }

            _inspector.SetActive(true);
            if (node.Building != null && node.Building.Owner == _runner.HumanFaction &&
                BattleSim.IsKeyBuilding(node.Building))
            {
                var building = node.Building;
                _inspectorTitle.text = $"{node.Key}  等级 {building.Level}/2";
                _inspectorBody.text =
                    $"HP {building.Hp}/{building.MaxHp}\n金币 +{building.GoldPerSec}/s   " +
                    $"法力 +{building.ManaPerSec}/s\n补给 +{building.Supply}";
                var units = _runner.UnitsAt(node);
                for (int i = 0; i < units.Count; i++)
                {
                    var unit = units[i];
                    bool canBuy = _runner.CanBuy(unit, node);
                    var button = InstantiateActionButton($"Buy{unit.Id}", _inspectorActions,
                        new Vector2(0f, -34f - i * 40f), new Vector2(250f, 32f),
                        $"{unit.CnName}  {unit.Cost}金  补给{unit.Upkeep}",
                        delegate { _runner.Buy(unit, node); });
                    button.interactable = canBuy;
                }
                var buildingDef = Cfg.BuildingById(building.Kind);
                int upgradeCost = building.Level == 0
                    ? buildingDef?.UpgradeCost1 ?? 350
                    : buildingDef?.UpgradeCost2 ?? 500;
                var upgrade = InstantiateActionButton("Upgrade", _inspectorActions,
                    new Vector2(0f, -46f - units.Count * 40f), new Vector2(250f, 32f),
                    building.Level >= 2 ? "已满级" : $"升级 {upgradeCost} 金",
                    delegate { _runner.Upgrade(node); });
                upgrade.interactable = building.Level < 2 &&
                                       _runner.Simulation.Human.Gold >= upgradeCost;
            }
            else if (node.Building != null)
            {
                _inspectorTitle.text = $"{node.Building.Node.Key}  {node.Building.Owner}";
                _inspectorBody.text =
                    $"HP {node.Building.Hp}/{node.Building.MaxHp}\n" +
                    $"等级 {node.Building.Level}/2\n" +
                    "拖动己方部队到此处进行攻击";
            }
            else if (node.Kind == NodeKind.BuildSpot && node.Building == null)
            {
                _inspectorTitle.text = $"空地 {node.Key}";
                bool controlled = _runner.Simulation.State.IsLocControlledBy(_runner.Simulation.Human, node);
                _inspectorBody.text = controlled ? "在己方控制区段建造附属建筑" : "不在己方控制区";
                if (!controlled) return;
                for (int i = 0; i < BattleSim.SupplyBuildingIds.Length; i++)
                {
                    int id = BattleSim.SupplyBuildingIds[i];
                    var def = Cfg.BuildingById(id);
                    if (def == null) continue;
                    string effect = def.Supply > 0 ? "补给 +1" : def.Gold > 0 ? "金币 +1/s" : "法力 +1/s";
                    var button = InstantiateActionButton($"Build{id}", _inspectorActions,
                        new Vector2(0f, -40f - i * 44f),
                        new Vector2(250f, 32f), $"{def.CnName}  {def.BuildCost}金  {effect}",
                        delegate { _runner.Build(node, id); });
                    button.interactable = _runner.CanBuild(node, id);
                }
            }
            else
            {
                _inspector.SetActive(false);
            }
        }

        private void RefreshSpellBar()
        {
            if (_spellBar == null || _runner == null) return;
            ClearChildren(_spellBar.transform);
            if (_runner.Simulation == null) return;
            var spells = _runner.Simulation.State.AvailableSpells(_runner.Simulation.Human);
            for (int i = 0; i < spells.Count; i++)
            {
                var spell = spells[i];
                var button = InstantiateActionButton($"Spell{spell.Id}", _spellBar.transform,
                    new Vector2(-126f + i * 82f, 0f), new Vector2(76f, 40f),
                    $"{spell.CnName}\n{(spell.SpellType == 2 ? "终极" : spell.Cost.ToString())}",
                    delegate { _runner.BeginSpellTarget(spell); });
                button.GetComponentInChildren<Text>().fontSize = 11;
                button.interactable = spell.SpellType == 2
                    ? _runner.Simulation.Human.Ultimate + 0.0001f >= _runner.Simulation.Human.UltimateMax
                    : _runner.Simulation.Human.Mana >=
                      Mathf.RoundToInt(spell.Cost * _runner.Simulation.Human.ModMul(35));
            }
        }

        private static string FormatTime(float seconds)
        {
            int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return $"{total / 60:00}:{total % 60:00}";
        }

        private Button InstantiateActionButton(string name, Transform parent, Vector2 position, Vector2 size,
            string label, UnityEngine.Events.UnityAction action)
        {
            var instance = Object.Instantiate(_actionButtonTemplate, parent, false);
            instance.name = name;
            var rect = instance.transform as RectTransform;
            if (rect == null)
                throw new InvalidOperationException($"UI 列表项预制体根节点缺少 RectTransform：{ActionButtonPrefabLocation}");
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            var button = instance.GetComponent<Button>();
            var text = instance.GetComponentInChildren<Text>(true);
            if (button == null || text == null)
                throw new InvalidOperationException($"UI 列表项预制体缺少 Button 或 Text：{ActionButtonPrefabLocation}");
            instance.SetActive(true);
            text.text = label;
            text.font = _font;
            button.onClick.AddListener(action);
            return button;
        }

        private static void ClearChildren(Transform parent)
        {
            if (parent == null) return;
            for (int i = parent.childCount - 1; i >= 0; i--)
                Object.Destroy(parent.GetChild(i).gameObject);
        }
    }
}
