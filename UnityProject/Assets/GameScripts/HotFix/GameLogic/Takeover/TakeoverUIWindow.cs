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
        private RectTransform _inspectorRect;
        private GameObject _spellBar;
        private Text _resources;
        private Text _clock;
        private Text _toast;
        private GameObject _toastPanel;
        private RectTransform _toastRect;
        private GameObject _gmPanel;
        private RectTransform _gmPanelRect;
        private Button _gmToggle;
        private bool _gmExpanded;
        private Text _resultTitle;
        private Text _resultBody;
        private Text _inspectorTitle;
        private Text _inspectorBody;
        private Text _resetLabel;
        private Transform _inspectorActions;
        private readonly List<Button> _levelButtons = new List<Button>();
        private readonly List<Text> _levelLabels = new List<Text>();
        private readonly List<Text> _gmSpeedLabels = new List<Text>();
        private RectTransform _badgeLayer;
        private RectTransform _badgePrototype;

        /// <summary>底栏的常驻调速按钮（原版右上角三个圆钮的位置改到底栏右侧）。</summary>
        private static readonly float[] BottomSpeeds = { 1f, 2f, 4f };
        private readonly List<Button> _bottomSpeedButtons = new List<Button>();
        private readonly List<Text> _bottomSpeedLabels = new List<Text>();
        private static readonly Color SpeedIdleColor = new Color(0.18f, 0.15f, 0.10f, 0.95f);
        private static readonly Color SpeedActiveColor = new Color(0.38f, 0.31f, 0.15f, 0.98f);
        private static readonly float[] GmSpeeds = { 1f, 2f, 4f, 8f };

        /// <summary>检查器动作按钮的排版：从 Actions 容器顶部（+110）往下排，避免垂到面板外。</summary>
        private const float InspectorActionTopY = 90f;
        private const float InspectorActionSpacing = 38f;

        /// <summary>检查器面板高度：无动作按钮时只包住标题+正文，有按钮时随按钮数增长。</summary>
        private const float InspectorInfoOnlyHeight = 134f;
        private const float InspectorActionBaseHeight = 174f;
        private readonly List<ActionPlan> _inspectorPlan = new List<ActionPlan>();
        private readonly List<ActionPlan> _spellPlan = new List<ActionPlan>();
        private readonly List<Button> _inspectorButtons = new List<Button>();
        private readonly List<Func<bool>> _inspectorRules = new List<Func<bool>>();
        private readonly List<Button> _spellButtons = new List<Button>();
        private readonly List<Func<bool>> _spellRules = new List<Func<bool>>();
        private static readonly System.Text.StringBuilder PlanSignatureBuilder =
            new System.Text.StringBuilder(256);
        private string _lastInspectorKey;
        private string _lastInspectorSignature;
        private string _lastSpellSignature;
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

            BuildGmPanel();

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

        /// <summary>头顶徽标的父节点（BattleHUD/SquadBadgeLayer），由 TakeoverRunner 写入 squad 位置。</summary>
        public RectTransform SquadBadgeLayer => _badgeLayer;

        /// <summary>徽标原型（预制体里的隐藏节点），运行时复制它，不按地址加载资源。</summary>
        public RectTransform SquadBadgePrototype => _badgePrototype;

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
            _gmToggle?.gameObject.SetActive(true);
            SetGmExpanded(false);
            _lastInspectorKey = null;
            _lastInspectorSignature = null;
            _lastSpellState = null;
            _lastSpellSignature = null;
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
            FitToastToText();
            _toastPanel.SetActive(true);
            _toastPanel.transform.SetAsLastSibling();
            _toastUntil = Time.unscaledTime + seconds;
        }

        /// <summary>
        /// 提示条背景按文字自适应。预制体里 ToastPanel 是固定 700×52 的深色底，
        /// 短提示（比如“选中 057，等级 0”）会横着压住左下角的 GM 按钮和底部操作栏。
        /// </summary>
        private void FitToastToText()
        {
            if (_toastRect == null || _toast == null) return;
            const float maxWidth = 700f;
            const float horizontalPadding = 40f;

            // 先按“不换行”量出真实文字宽度，再决定面板宽度
            _toast.horizontalOverflow = HorizontalWrapMode.Overflow;
            float contentWidth = _toast.preferredWidth;
            float width = Mathf.Clamp(contentWidth + horizontalPadding, 220f, maxWidth);

            // 超过上限才允许换行，并按最终宽度重新量高度
            bool needsWrap = contentWidth + horizontalPadding > maxWidth;
            _toast.horizontalOverflow = needsWrap ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
            _toastRect.sizeDelta = new Vector2(width, 52f);
            float height = Mathf.Clamp(_toast.preferredHeight + 14f, 34f, 120f);
            _toastRect.sizeDelta = new Vector2(width, height);
        }

        /// <summary>
        /// 把预制体里原来那条 250×66 的 GM 工具条改造成“左侧中间按钮控制、默认折叠”的
        /// 左半屏 GM 面板：布局都在这里集中定义，方便后续微调。
        /// </summary>
        private void BuildGmPanel()
        {
            if (_gmPanel == null || _gmPanelRect == null || _hud == null) return;

            // 面板铺满左半屏
            _gmPanelRect.anchorMin = new Vector2(0f, 0f);
            _gmPanelRect.anchorMax = new Vector2(0.5f, 1f);
            _gmPanelRect.pivot = new Vector2(0.5f, 0.5f);
            _gmPanelRect.sizeDelta = Vector2.zero;
            _gmPanelRect.anchoredPosition = Vector2.zero;
            // 画在 HUD 最上层：GM 按钮/文字浮在资源栏、法术栏、底部操作栏之上
            _gmPanelRect.SetAsLastSibling();

            var background = _gmPanel.GetComponent<Image>();
            if (background != null)
            {
                // 背景全透明，只留按钮；不接收点击，空白处仍然可以操作地图
                background.color = new Color(0f, 0f, 0f, 0f);
                background.raycastTarget = false;
            }

            CreateLabel("GmTitle", _gmPanelRect, "GM 面板",
                new Vector2(20f, 145f), new Vector2(320f, 28f), 17,
                new Color(0.95f, 0.93f, 0.80f));

            ArrangeGmButton("TakeoverRuntimeUI/BattleHUD/GmControls/GmGold",
                new Vector2(-65f, 100f), new Vector2(150f, 34f));
            ArrangeGmButton("TakeoverRuntimeUI/BattleHUD/GmControls/GmMana",
                new Vector2(105f, 100f), new Vector2(150f, 34f));

            float[] speedX = { -104f, -40f, 24f, 88f };
            for (int i = 0; i < GmSpeeds.Length && i < speedX.Length; i++)
            {
                ArrangeGmButton($"TakeoverRuntimeUI/BattleHUD/GmControls/GmSpeed{GmSpeeds[i]:0}",
                    new Vector2(speedX[i], 50f), new Vector2(56f, 30f));
            }

            InstantiateActionButton("GmWin", _gmPanelRect,
                new Vector2(-65f, -10f), new Vector2(150f, 34f),
                "胜利", delegate { _runner?.GmEndBattle(true); });
            InstantiateActionButton("GmLose", _gmPanelRect,
                new Vector2(105f, -10f), new Vector2(150f, 34f),
                "失败", delegate { _runner?.GmEndBattle(false); });
            CreateLabel("GmHint", _gmPanelRect, "胜利 / 失败会直接进入结算，本面板默认折叠",
                new Vector2(20f, -70f), new Vector2(320f, 22f), 12,
                new Color(0.72f, 0.76f, 0.68f));

            // 左侧中间的折叠/展开按钮：始终在 HUD 最上层，展开时压在面板左边
            _gmToggle = InstantiateActionButton("GmToggle", _hud.transform, Vector2.zero,
                new Vector2(36f, 104f), "GM", delegate { SetGmExpanded(!_gmExpanded); });
            var toggleRect = _gmToggle.transform as RectTransform;
            if (toggleRect != null)
            {
                toggleRect.anchorMin = new Vector2(0f, 0.5f);
                toggleRect.anchorMax = new Vector2(0f, 0.5f);
                toggleRect.pivot = new Vector2(0f, 0.5f);
                toggleRect.anchoredPosition = Vector2.zero;
            }
            _gmToggle.transform.SetAsLastSibling();

            SetGmExpanded(false);
        }

        private void ArrangeGmButton(string path, Vector2 position, Vector2 size)
        {
            var button = FindRequired<Button>(path);
            var rect = button.transform as RectTransform;
            if (rect == null) return;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private Text CreateLabel(string name, Transform parent, string content,
            Vector2 position, Vector2 size, int fontSize, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            var text = go.GetComponent<Text>();
            text.font = _font;
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.raycastTarget = false;
            text.text = content;
            return text;
        }

        private void SetGmExpanded(bool expanded)
        {
            _gmExpanded = expanded;
            if (_gmPanel != null) _gmPanel.SetActive(expanded);
        }

        private void BindStaticUi()
        {
            _menu = FindRequired<RectTransform>("TakeoverRuntimeUI/LevelSelect").gameObject;
            _hud = FindRequired<RectTransform>("TakeoverRuntimeUI/BattleHUD").gameObject;
            _result = FindRequired<RectTransform>("TakeoverRuntimeUI/Result").gameObject;
            _inspector = FindRequired<RectTransform>("TakeoverRuntimeUI/BattleHUD/Inspector").gameObject;
            _inspectorRect = (RectTransform)_inspector.transform;
            _spellBar = FindRequired<RectTransform>("TakeoverRuntimeUI/BattleHUD/Spells").gameObject;
            _gmPanel = FindRequired<RectTransform>("TakeoverRuntimeUI/BattleHUD/GmControls").gameObject;
            _gmPanelRect = (RectTransform)_gmPanel.transform;
            _toastPanel = FindRequired<RectTransform>("TakeoverRuntimeUI/ToastPanel").gameObject;
            _toastRect = _toastPanel.GetComponent<RectTransform>();
            _toast = FindRequired<Text>("TakeoverRuntimeUI/ToastPanel/ToastText");
            _resources = FindRequired<Text>("TakeoverRuntimeUI/BattleHUD/BottomBar/Top/Resources");
            _clock = FindRequired<Text>("TakeoverRuntimeUI/BattleHUD/BottomBar/Top/Clock");
            _badgeLayer = FindRequired<RectTransform>("TakeoverRuntimeUI/BattleHUD/SquadBadgeLayer");
            _badgePrototype = FindRequired<RectTransform>(
                "TakeoverRuntimeUI/BattleHUD/SquadBadgeLayer/SquadBadgePrototype");
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

            FindRequired<Button>("TakeoverRuntimeUI/BattleHUD/BottomBar/Settings")
                .onClick.AddListener(delegate { SetGmExpanded(true); });
            _bottomSpeedButtons.Clear();
            _bottomSpeedLabels.Clear();
            for (int i = 0; i < BottomSpeeds.Length; i++)
            {
                float speed = BottomSpeeds[i];
                string node = $"TakeoverRuntimeUI/BattleHUD/SpeedControls/Speed{speed:0}";
                var button = FindRequired<Button>(node);
                button.onClick.AddListener(delegate { _runner?.SetSpeed(speed); });
                _bottomSpeedButtons.Add(button);
                _bottomSpeedLabels.Add(FindRequired<Text>(node + "/Label"));
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
            for (int i = 0; i < _bottomSpeedButtons.Count; i++)
            {
                bool active = Mathf.Approximately(_runner.Speed, BottomSpeeds[i]);
                if (i < _bottomSpeedLabels.Count && _bottomSpeedLabels[i] != null)
                    _bottomSpeedLabels[i].text = active ? $"×{BottomSpeeds[i]:0}*" : $"×{BottomSpeeds[i]:0}";
                var graphic = _bottomSpeedButtons[i] != null ? _bottomSpeedButtons[i].targetGraphic : null;
                if (graphic != null) graphic.color = active ? SpeedActiveColor : SpeedIdleColor;
            }
        }

        private void RefreshInspector()
        {
            if (_runner == null || _runner.Simulation == null || _runner.LevelSelectOpen) return;
            var human = _runner.Simulation.Human;
            string targetKey = _runner.SelectedSquad != null
                ? $"squad:{_runner.SelectedSquad.Id}"
                : _runner.SelectedNode != null ? $"node:{_runner.SelectedNode.Id}" : "none";
            string inspectorKey = $"{targetKey}|{Mathf.FloorToInt(human.Gold)}|{human.FreeSupply}|" +
                                  $"{(_runner.SelectedNode?.Building?.Level ?? -1)}";
            if (inspectorKey == _lastInspectorKey) return;
            _lastInspectorKey = inspectorKey;

            _inspectorPlan.Clear();
            bool visible = BuildInspectorPlan();
            if (_inspector.activeSelf != visible) _inspector.SetActive(visible);
            if (!visible) return;

            // 面板高度按内容收缩：只有标题+正文（比如选中部队）时不再留一大块空底板
            float panelHeight = _inspectorPlan.Count == 0
                ? InspectorInfoOnlyHeight
                : InspectorActionBaseHeight + (_inspectorPlan.Count - 1) * InspectorActionSpacing;
            if (_inspectorRect != null &&
                !Mathf.Approximately(_inspectorRect.sizeDelta.y, panelHeight))
            {
                _inspectorRect.sizeDelta = new Vector2(_inspectorRect.sizeDelta.x, panelHeight);
            }

            // 结构签名只包含“按钮集合 + 文案”，金币/补给的整数变化不会让它变化，
            // 因此每秒进账只刷新 interactable，不再销毁重建按钮（原来的闪烁来源）。
            string signature = targetKey + "#" + BuildPlanSignature(_inspectorPlan);
            if (signature != _lastInspectorSignature)
            {
                _lastInspectorSignature = signature;
                ClearButtons(_inspectorActions, _inspectorButtons, _inspectorRules);
                foreach (var plan in _inspectorPlan)
                {
                    var button = InstantiateActionButton(plan.Name, _inspectorActions,
                        plan.Position, plan.Size, plan.Label, plan.Click, stateTint: true);
                    if (plan.FontSize > 0) button.GetComponentInChildren<Text>().fontSize = plan.FontSize;
                    _inspectorButtons.Add(button);
                    _inspectorRules.Add(plan.Enabled);
                }
            }
            ApplyRules(_inspectorButtons, _inspectorRules);
        }

        /// <summary>刷新检查器文案并生成按钮计划；返回检查器是否应显示。</summary>
        private bool BuildInspectorPlan()
        {
            if (_runner.SelectedSquad != null)
            {
                var squad = _runner.SelectedSquad;
                _inspectorTitle.text = $"{squad.Def.CnName}  {squad.AliveSoldiers}/{squad.MaxSoldiers}";
                _inspectorBody.text =
                    $"HP {squad.Hp:F0}/{squad.HpPerSoldier * squad.MaxSoldiers:F0}\n" +
                    $"等级 {squad.LevelIndex}   经验 {squad.Xp:F0}\n" +
                    $"伤害基础 {squad.Def.DmgMin}-{squad.Def.DmgMax} × {squad.AliveSoldiers}\n" +
                    $"射程 {squad.Def.Range}   移速 {squad.Speed:F1}\n" +
                    $"{(squad.TargetNode != null ? $"目标 {squad.TargetNode.Key}" : "待命")}";
                return true;
            }

            var node = _runner.SelectedNode;
            if (node == null) return false;

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
                    AddPlan($"Buy{unit.Id}", $"{unit.CnName}  {unit.Cost}金  补给{unit.Upkeep}",
                        new Vector2(0f, InspectorActionTopY - i * InspectorActionSpacing),
                        new Vector2(250f, 32f),
                        delegate { _runner.Buy(unit, node); },
                        () => _runner.CanBuy(unit, node));
                }
                var buildingDef = Cfg.BuildingById(building.Kind);
                int upgradeCost = building.Level == 0
                    ? buildingDef?.UpgradeCost1 ?? 350
                    : buildingDef?.UpgradeCost2 ?? 500;
                AddPlan("Upgrade", building.Level >= 2 ? "已满级" : $"升级 {upgradeCost} 金",
                    new Vector2(0f, InspectorActionTopY - units.Count * InspectorActionSpacing),
                    new Vector2(250f, 32f),
                    delegate { _runner.Upgrade(node); },
                    () => building.Level < 2 && _runner.Simulation.Human.Gold >= upgradeCost);
                return true;
            }

            if (node.Building != null)
            {
                _inspectorTitle.text = $"{node.Building.Node.Key}  {node.Building.Owner}";
                _inspectorBody.text =
                    $"HP {node.Building.Hp}/{node.Building.MaxHp}\n" +
                    $"等级 {node.Building.Level}/2\n" +
                    "拖动己方部队到此处进行攻击";
                return true;
            }

            if (node.Kind == NodeKind.BuildSpot && node.Building == null)
            {
                _inspectorTitle.text = $"空地 {node.Key}";
                bool controlled = _runner.Simulation.State.IsLocControlledBy(_runner.Simulation.Human, node);
                _inspectorBody.text = controlled ? "在己方控制区段建造附属建筑" : "不在己方控制区";
                if (!controlled) return true;
                for (int i = 0; i < BattleSim.SupplyBuildingIds.Length; i++)
                {
                    int id = BattleSim.SupplyBuildingIds[i];
                    var def = Cfg.BuildingById(id);
                    if (def == null) continue;
                    string effect = def.Supply > 0 ? "补给 +1" : def.Gold > 0 ? "金币 +1/s" : "法力 +1/s";
                    AddPlan($"Build{id}", $"{def.CnName}  {def.BuildCost}金  {effect}",
                        new Vector2(0f, InspectorActionTopY - i * InspectorActionSpacing),
                        new Vector2(250f, 32f),
                        delegate { _runner.Build(node, id); },
                        () => _runner.CanBuild(node, id));
                }
                return true;
            }

            return false;
        }

        private void RefreshSpellBar()
        {
            if (_spellBar == null || _runner == null || _runner.Simulation == null) return;
            var human = _runner.Simulation.Human;
            var spells = _runner.Simulation.State.AvailableSpells(human);
            _spellPlan.Clear();
            for (int i = 0; i < spells.Count; i++)
            {
                var spell = spells[i];
                AddPlanTo(_spellPlan, $"Spell{spell.Id}",
                    $"{spell.CnName}\n{(spell.SpellType == 2 ? "终极" : spell.Cost.ToString())}",
                    new Vector2(-126f + i * 82f, 0f), new Vector2(76f, 40f),
                    delegate { _runner.BeginSpellTarget(spell); },
                    () => spell.SpellType == 2
                        ? _runner.Simulation.Human.Ultimate + 0.0001f >= _runner.Simulation.Human.UltimateMax
                        : _runner.Simulation.Human.Mana >=
                          Mathf.RoundToInt(spell.Cost * _runner.Simulation.Human.ModMul(35)),
                    fontSize: 11);
            }

            string signature = BuildPlanSignature(_spellPlan);
            if (signature != _lastSpellSignature)
            {
                _lastSpellSignature = signature;
                ClearButtons(_spellBar.transform, _spellButtons, _spellRules);
                for (int i = 0; i < _spellPlan.Count; i++)
                {
                    var plan = _spellPlan[i];
                    var button = InstantiateActionButton(plan.Name, _spellBar.transform,
                        plan.Position, plan.Size, plan.Label, plan.Click);
                    if (plan.FontSize > 0) button.GetComponentInChildren<Text>().fontSize = plan.FontSize;
                    _spellButtons.Add(button);
                    _spellRules.Add(plan.Enabled);
                }
            }
            ApplyRules(_spellButtons, _spellRules);
        }

        private struct ActionPlan
        {
            public string Name;
            public string Label;
            public Vector2 Position;
            public Vector2 Size;
            public int FontSize;
            public UnityEngine.Events.UnityAction Click;
            public Func<bool> Enabled;
        }

        private void AddPlan(string name, string label, Vector2 position, Vector2 size,
            UnityEngine.Events.UnityAction click, Func<bool> enabled, int fontSize = 0)
        {
            AddPlanTo(_inspectorPlan, name, label, position, size, click, enabled, fontSize);
        }

        private static void AddPlanTo(List<ActionPlan> plan, string name, string label,
            Vector2 position, Vector2 size, UnityEngine.Events.UnityAction click,
            Func<bool> enabled, int fontSize = 0)
        {
            plan.Add(new ActionPlan
            {
                Name = name,
                Label = label,
                Position = position,
                Size = size,
                FontSize = fontSize,
                Click = click,
                Enabled = enabled,
            });
        }

        private static string BuildPlanSignature(List<ActionPlan> plan)
        {
            PlanSignatureBuilder.Clear();
            for (int i = 0; i < plan.Count; i++)
            {
                PlanSignatureBuilder.Append(plan[i].Name).Append('|')
                    .Append(plan[i].Label).Append('|')
                    .Append(plan[i].Position.x).Append(',').Append(plan[i].Position.y).Append('|')
                    .Append(plan[i].Enabled == null ? '0' : '1').Append(';');
            }
            return PlanSignatureBuilder.ToString();
        }

        private static void ClearButtons(Transform parent, List<Button> buttons, List<Func<bool>> rules)
        {
            ClearChildren(parent);
            buttons.Clear();
            rules.Clear();
        }

        private static void ApplyRules(List<Button> buttons, List<Func<bool>> rules)
        {
            int count = Mathf.Min(buttons.Count, rules.Count);
            for (int i = 0; i < count; i++)
            {
                if (buttons[i] == null) continue;
                buttons[i].interactable = rules[i] == null || rules[i]();
            }
        }

        /// <summary>检查器里的招募/建造/升级按钮：可点绿色、不可点灰色（走 Selectable 的 ColorTint）。</summary>
        private static readonly Color ActionEnabledColor = new Color(0.20f, 0.48f, 0.26f, 0.95f);
        private static readonly Color ActionHighlightColor = new Color(0.27f, 0.60f, 0.33f, 0.95f);
        private static readonly Color ActionPressedColor = new Color(0.15f, 0.38f, 0.21f, 0.95f);
        private static readonly Color ActionDisabledColor = new Color(0.30f, 0.30f, 0.30f, 0.85f);

        private static void ApplyActionStateColors(Button button)
        {
            button.transition = Selectable.Transition.ColorTint;
            var colors = button.colors;
            colors.normalColor = ActionEnabledColor;
            colors.highlightedColor = ActionHighlightColor;
            colors.selectedColor = ActionEnabledColor;
            colors.pressedColor = ActionPressedColor;
            colors.disabledColor = ActionDisabledColor;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.05f;
            button.colors = colors;
            // ColorTint 是把 CanvasRenderer 颜色乘到 Image 自身上，模板底图是深色
            // rgba(0.15,0.2,0.16)，直接叠绿会变成墨绿；这里把底图刷白，状态色才是最终颜色。
            if (button.targetGraphic != null) button.targetGraphic.color = Color.white;
        }

        private static string FormatTime(float seconds)
        {
            int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return $"{total / 60:00}:{total % 60:00}";
        }

        private Button InstantiateActionButton(string name, Transform parent, Vector2 position, Vector2 size,
            string label, UnityEngine.Events.UnityAction action, bool stateTint = false)
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
            text.text = label;
            text.font = _font;
            button.onClick.AddListener(action);
            // 先套好状态配色再显示，避免首帧闪一下模板默认色
            if (stateTint) ApplyActionStateColors(button);
            instance.SetActive(true);
            return button;
        }

        private static void ClearChildren(Transform parent)
        {
            if (parent == null) return;
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i).gameObject;
                // Destroy 要到帧末才真正生效，先隐藏，避免新按钮和旧按钮同帧叠在一起的闪烁。
                child.SetActive(false);
                Object.Destroy(child);
            }
        }
    }
}
