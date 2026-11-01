using System;
using System.Collections.Generic;
using System.Linq;
using TEngine;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace GameLogic.Takeover
{
    /// <summary>
    /// Runtime UGUI shell for the first-chapter build. The existing
    /// BattleMainUI prefab supplies the framework Canvas and font; all nodes are
    /// rebuilt here so this remains functional until the extracted SWF atlas and
    /// prefabs are supplied by the external asset pipeline.
    /// </summary>
    [Window(UILayer.UI, location: "BattleMainUI", fullScreen: true)]
    public sealed class TakeoverUIWindow : UIWindow
    {
        private TakeoverRunner _runner;
        private Font _font;
        private GameObject _menu;
        private GameObject _hud;
        private GameObject _result;
        private GameObject _inspector;
        private GameObject _spellBar;
        private Text _resources;
        private Text _clock;
        private Text _toast;
        private Text _resultTitle;
        private Text _resultBody;
        private Text _inspectorTitle;
        private Text _inspectorBody;
        private Transform _inspectorActions;
        private readonly List<Button> _levelButtons = new List<Button>();
        private readonly List<Text> _levelLabels = new List<Text>();
        private string _lastInspectorKey;
        private float _toastUntil;
        private bool _resetArmed;
        private Font _originalFont;

        protected override async void OnCreate()
        {
            _runner = UserData as TakeoverRunner;
            var templateText = gameObject.GetComponentInChildren<Text>(true);
            var fallbackFont = templateText != null ? templateText.font : null;
            try
            {
                _originalFont = await GameModule.Resource.LoadAssetAsync<Font>(
                    "takeover_font_328_Chaparral_Pro");
            }
            catch (System.Exception)
            {
                _originalFont = null;
            }
            _font = _originalFont != null ? _originalFont : fallbackFont;
            var scaler = gameObject.GetComponentInParent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.referenceResolution = new Vector2(
                    OriginalRules.NativeStageWidth, OriginalRules.NativeStageHeight);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
            }
            for (int i = 0; i < transform.childCount; i++)
                transform.GetChild(i).gameObject.SetActive(false);

            BuildStaticShell();
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
            if (_hud.activeSelf && _runner.Simulation != null)
                RefreshHud();
            RefreshInspector();
            if (_toast != null && Time.unscaledTime >= _toastUntil && _toast.gameObject.activeSelf)
                _toast.gameObject.SetActive(false);
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
            RefreshSpellBar();
        }

        public void ShowResult(BattleSimulation simulation)
        {
            if (_result == null || simulation == null) return;
            _result.SetActive(true);
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
            if (_toast == null || string.IsNullOrEmpty(message)) return;
            _toast.text = message;
            _toast.gameObject.SetActive(true);
            _toastUntil = Time.unscaledTime + seconds;
        }

        private void BuildStaticShell()
        {
            var root = CreateRect("TakeoverRuntimeUI", transform, Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero, Vector2.zero);
            root.SetAsLastSibling();

            _menu = CreateRect("LevelSelect", root, Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero, Vector2.zero).gameObject;
            AddImage(_menu, new Color(0.055f, 0.07f, 0.065f, 1f));
            BuildMenu(_menu.transform);

            _hud = CreateRect("BattleHUD", root, Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero, Vector2.zero).gameObject;
            BuildHud(_hud.transform);

            _result = CreateRect("Result", root, Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero, Vector2.zero).gameObject;
            AddImage(_result, new Color(0f, 0f, 0f, 0.62f));
            BuildResult(_result.transform);

            _toast = CreateText("Toast", root, 18, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 110f), new Vector2(760f, 42f));
            _toast.color = new Color(1f, 0.91f, 0.53f);
            _toast.gameObject.SetActive(false);
        }

        private void BuildMenu(Transform root)
        {
            var box = MakePanel("ChapterPanel", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 470f),
                new Color(0.12f, 0.15f, 0.13f, 0.98f));
            var title = CreateText("Title", box, 34, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -60f), new Vector2(840f, 56f));
            title.text = "城邦争霸 · 西利亚公国";
            title.color = new Color(0.96f, 0.84f, 0.48f);

            var hint = CreateText("Hint", box, 17, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -105f), new Vector2(840f, 34f));
            hint.text = "第一章 · 仅显示已解锁关卡";
            hint.color = new Color(0.74f, 0.78f, 0.72f);

            for (int i = 0; i < 8; i++)
            {
                int levelId = i + 1;
                float x = -330f + (i % 4) * 220f;
                float y = 55f - (i / 4) * 130f;
                var button = CreateButton($"Level{levelId}", box, new Vector2(x, y), new Vector2(190f, 92f),
                    $"L{levelId}", delegate { _runner?.StartLevel(levelId); });
                _levelButtons.Add(button);
                _levelLabels.Add(button.GetComponentInChildren<Text>());
            }

            var reset = CreateButton("ResetSave", box, new Vector2(335f, -170f), new Vector2(150f, 44f),
                "清除进度", delegate { });
            reset.onClick.AddListener(delegate
                {
                    var label = reset.GetComponentInChildren<Text>();
                    if (!_resetArmed)
                    {
                        _resetArmed = true;
                        label.text = "再次点击确认";
                        ShowToast("再次点击将清除全部首章进度", 4f);
                        return;
                    }
                    Campaign.Reset();
                    _resetArmed = false;
                    label.text = "清除进度";
                    RefreshLevelButtons();
                    ShowToast("进度已重置");
                });
            reset.GetComponentInChildren<Text>().fontSize = 16;
        }

        private void BuildHud(Transform root)
        {
            var top = MakePanel("Top", root, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(24f, -20f), new Vector2(560f, 140f),
                new Color(0.08f, 0.10f, 0.09f, 0.88f));
            _resources = CreateText("Resources", top, 19, TextAnchor.UpperLeft,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                new Vector2(16f, -12f), new Vector2(-32f, 110f));
            _clock = CreateText("Clock", top, 16, TextAnchor.UpperRight,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-16f, -104f), new Vector2(210f, 28f));

            var controls = MakePanel("Controls", root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(650f, 58f),
                new Color(0.08f, 0.10f, 0.09f, 0.90f));
            CreateButton("Pause", controls, new Vector2(-270f, 0f), new Vector2(100f, 38f), "暂停",
                delegate { _runner?.TogglePause(); });
            CreateButton("Speed1", controls, new Vector2(-155f, 0f), new Vector2(72f, 38f), "×1",
                delegate { _runner?.SetSpeed(1f); });
            CreateButton("Speed2", controls, new Vector2(-72f, 0f), new Vector2(72f, 38f), "×2",
                delegate { _runner?.SetSpeed(2f); });
            CreateButton("Restart", controls, new Vector2(25f, 0f), new Vector2(100f, 38f), "重开",
                delegate { _runner?.NewGame(); });
            CreateButton("Levels", controls, new Vector2(145f, 0f), new Vector2(100f, 38f), "关卡",
                delegate { _runner?.ShowLevelSelect(); });

            _inspector = MakePanel("Inspector", root, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(1f, 1f), new Vector2(-24f, -20f), new Vector2(360f, 500f),
                new Color(0.08f, 0.10f, 0.09f, 0.92f)).gameObject;
            _inspectorTitle = CreateText("InspectorTitle", _inspector.transform, 21, TextAnchor.UpperLeft,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                new Vector2(16f, -14f), new Vector2(-32f, 36f));
            _inspectorBody = CreateText("InspectorBody", _inspector.transform, 15, TextAnchor.UpperLeft,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                new Vector2(16f, -56f), new Vector2(-32f, 92f));
            var actions = CreateRect("Actions", _inspector.transform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, 1f), new Vector2(16f, -154f), new Vector2(-32f, 330f));
            _inspectorActions = actions;
            _inspector.SetActive(false);

            _spellBar = MakePanel("Spells", root, new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(0f, 0f), new Vector2(24f, 92f), new Vector2(430f, 72f),
                new Color(0.08f, 0.10f, 0.09f, 0.90f)).gameObject;
        }

        private void BuildResult(Transform root)
        {
            var box = MakePanel("ResultPanel", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520f, 300f),
                new Color(0.10f, 0.13f, 0.11f, 0.98f));
            _resultTitle = CreateText("Title", box, 34, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -54f), new Vector2(460f, 50f));
            _resultBody = CreateText("Body", box, 20, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 15f), new Vector2(460f, 100f));
            CreateButton("Replay", box, new Vector2(-130f, -105f), new Vector2(150f, 44f), "重玩本关",
                delegate { _runner?.NewGame(); });
            CreateButton("Next", box, new Vector2(130f, -105f), new Vector2(150f, 44f), "下一关",
                delegate
                {
                    if (_runner == null) return;
                    _runner.StartLevel(Campaign.NextLevel(_runner.LevelAssetId));
                });
            CreateButton("Menu", box, new Vector2(0f, -105f), new Vector2(90f, 44f), "关卡",
                delegate { _runner?.ShowLevelSelect(); });
            _result.SetActive(false);
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
                $"敌城 {sim.Enemy.OwnedNodes.Count}";
            _clock.text = $"{FormatTime(sim.Time)}\n三星 < {FormatTime(330f)}";
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
                    $"伤害 {squad.Def.DmgMin}-{squad.Def.DmgMax} × {squad.AliveSoldiers}\n" +
                    $"射程 {squad.Def.Range}   移速 {squad.Speed:F0}\n" +
                    $"{(squad.TargetNode != null ? $"目标 {squad.TargetNode.Key}" : "待命")}";
                CreateButton("Retreat", _inspectorActions, new Vector2(0f, -20f), new Vector2(150f, 40f),
                    "撤退回城", delegate { _runner.RetreatSelected(); });
                return;
            }

            var node = _runner.SelectedNode;
            if (node == null)
            {
                _inspector.SetActive(false);
                return;
            }

            _inspector.SetActive(true);
            if (node.Building != null && node.Building.Owner == _runner.HumanFaction)
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
                    var button = CreateButton($"Buy{unit.Id}", _inspectorActions,
                        new Vector2(0f, -40f - i * 44f), new Vector2(320f, 38f),
                        $"{unit.CnName}  {unit.Cost}金  补给{unit.Upkeep}",
                        delegate { _runner.Buy(unit, node); });
                    button.interactable = canBuy;
                }
                var upgrade = CreateButton("Upgrade", _inspectorActions,
                    new Vector2(0f, -52f - units.Count * 44f), new Vector2(320f, 38f),
                    building.Level >= 2 ? "已满级" : $"升级 { (building.Level == 0 ? 350 : 500) } 金",
                    delegate { _runner.Upgrade(node); });
                upgrade.interactable = building.Level < 2;
            }
            else if (node.Kind == NodeKind.BuildSpot && node.Building == null)
            {
                _inspectorTitle.text = $"空地 {node.Key}";
                _inspectorBody.text = "在己方控制区段建造附属建筑";
                for (int i = 0; i < BattleSim.SupplyBuildingIds.Length; i++)
                {
                    int id = BattleSim.SupplyBuildingIds[i];
                    var def = Cfg.BuildingById(id);
                    if (def == null) continue;
                    string effect = def.Supply > 0 ? "补给 +1" : def.Gold > 0 ? "金币 +1/s" : "法力 +1/s";
                    CreateButton($"Build{id}", _inspectorActions, new Vector2(0f, -40f - i * 44f),
                        new Vector2(320f, 38f), $"{def.CnName}  {def.BuildCost}金  {effect}",
                        delegate { _runner.Build(node, id); });
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
            var spells = Cfg.TbSpell.DataList
                .Where(s => s.Faction == (int)_runner.HumanFaction)
                .OrderBy(s => s.SpellType).ThenBy(s => s.Id).ToList();
            for (int i = 0; i < spells.Count; i++)
            {
                var spell = spells[i];
                var button = CreateButton($"Spell{spell.Id}", _spellBar.transform,
                    new Vector2(-155f + i * 102f, 0f), new Vector2(94f, 50f),
                    $"{spell.CnName}\n{(spell.SpellType == 2 ? "终极" : spell.Cost.ToString())}",
                    delegate { _runner.BeginSpellTarget(spell); });
                button.GetComponentInChildren<Text>().fontSize = 13;
            }
        }

        private static string FormatTime(float seconds)
        {
            int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return $"{total / 60:00}:{total % 60:00}";
        }

        private RectTransform MakePanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 pivot, Vector2 position, Vector2 size, Color color)
        {
            var rect = CreateRect(name, parent, anchorMin, anchorMax, pivot, position, size);
            AddImage(rect.gameObject, color);
            return rect;
        }

        private RectTransform CreateRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 pivot, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private Text CreateText(string name, Transform parent, int size, TextAnchor alignment,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 dimensions)
        {
            var rect = CreateRect(name, parent, anchorMin, anchorMax, pivot, position, dimensions);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = _font;
            text.fontSize = size;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        private Button CreateButton(string name, Transform parent, Vector2 position, Vector2 size,
            string label, UnityAction action)
        {
            var rect = CreateRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), position, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.20f, 0.25f, 0.21f, 0.98f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
            colors.disabledColor = new Color(0.42f, 0.42f, 0.42f, 0.65f);
            button.colors = colors;

            var text = CreateText("Label", rect, 17, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            text.text = label;
            text.color = new Color(0.96f, 0.93f, 0.81f);
            return button;
        }

        private static void AddImage(GameObject go, Color color)
        {
            var image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = true;
        }

        private static void ClearChildren(Transform parent)
        {
            if (parent == null) return;
            for (int i = parent.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(parent.GetChild(i).gameObject);
        }
    }
}
