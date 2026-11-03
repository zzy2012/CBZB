using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameLogic.Takeover.UI;
using GameLogic.UI;
using TEngine;
using UnityEngine;

namespace GameLogic.Takeover
{
    /// <summary>
    /// 首章表现控制器：运行时生成地图与单位，界面由 UI Toolkit 的 UIManager 驱动。
    /// 逻辑全部在 BattleSim 中，Runner 只负责世界表现、输入与界面流程。
    /// </summary>
    public sealed class TakeoverRunner : MonoBehaviour
    {
        private const float NativeSpriteScale = 100f;

        /// <summary>建筑底图按阵营染色的强度（0 = 原图，1 = 完全用阵营色）。</summary>
        private const float FactionTintBlend = 0.55f;

        private BattleSimulation _sim;
        private Camera _cam;
        private Transform _root;
        private readonly Dictionary<MapNode, SpriteRenderer> _nodeViews = new Dictionary<MapNode, SpriteRenderer>();
        private readonly Dictionary<int, SpriteRenderer> _squadViews = new Dictionary<int, SpriteRenderer>();
        private readonly Dictionary<int, string> _boundNodeVisuals = new Dictionary<int, string>();
        private readonly Dictionary<MapNode, LineRenderer> _nodeClickableAreas =
            new Dictionary<MapNode, LineRenderer>();
        private readonly Dictionary<int, LineRenderer> _unitClickableAreas =
            new Dictionary<int, LineRenderer>();
        private SpriteRenderer _mapBackground;
        private MapNode _selected;
        private Squad _selectedSquad;
        private bool _dragging;
        private Vector3 _lastMouse;
        private bool _paused;
        private float _speed = 1f;
        private bool _reported;
        private bool _battleActive;
        private bool _resultShown;
        private bool _pressOverUI;
        private CampaignReward _lastReward;
        private int _levelRequestVersion;
        private string _uiFatal;
        private GameConfig.takeover.Spell _pendingSpell;
        private Squad _dragSquad;
        private Vector2 _pressScreen;
        private LineRenderer _orderPreview;
        private Material _clickableAreaMaterial;
        private CancellationTokenSource _visualBindingCancellationTokenSource;

        private const float BadgeWorldOffsetY = 34f;
        private const float BuildingBadgeWorldOffsetY = 42f;

        /// <summary>敌方普通法术播报的最小间隔（秒），避免 AI 互丢法术刷屏。</summary>
        private const float EnemySpellToastInterval = 4f;
        private float _lastEnemySpellToast = -100f;

        public int LevelAssetId = 1;
        public Faction HumanFaction = Faction.Westaria;
        public Faction EnemyFaction = Faction.Empire;

        /// <summary>调试开关：关掉后不再创建 squad 头顶徽标（排查卡顿/不显示时用）。</summary>
        public bool ShowSquadBadges = true;

        /// <summary>调试开关：关掉后不再创建建筑头顶徽标。</summary>
        public bool ShowBuildingBadges = true;

        private void Awake()
        {
            Application.targetFrameRate = 60;
            // 关掉 TEngine 自带的 DEBUGGER 面板，避免遮住玩法 HUD
            if (GameModule.Debugger != null)
                GameModule.Debugger.ActiveWindow = false;
            _cam = Camera.main;
            if (_cam == null)
            {
                var go = new GameObject("TakeoverCamera");
                _cam = go.AddComponent<Camera>();
                go.tag = "MainCamera";
            }
            _cam.orthographic = true;
            _cam.backgroundColor = new Color(0.10f, 0.08f, 0.06f);
            _cam.clearFlags = CameraClearFlags.SolidColor;

            _root = new GameObject("TakeoverWorld").transform;
            _root.gameObject.SetActive(false);

            UIAudioSettings.Load();
            BootAsync().Forget();
        }

        private async UniTaskVoid BootAsync()
        {
            await UIManager.InitializeAsync();
            if (!UIManager.IsInitialized)
            {
                SetUiFatal("[TakeoverUI] UIManager 初始化失败，标题界面无法打开；请查看 Console。");
                return;
            }

            AudioControlsView audio = await UIManager.OpenPersistentAsync<AudioControlsView>();
            if (audio == null)
                Log.Error("[TakeoverUI] 音频开关加载失败: TakeoverAudioControlsView");

            if (!_battleActive) ShowTitle();
        }

        private void SetUiFatal(string message)
        {
            _uiFatal = message;
            Log.Error(message);
        }

        private void OnGUI()
        {
            if (string.IsNullOrEmpty(_uiFatal)) return;
            GUI.Box(new Rect(20f, 20f, Screen.width - 40f, 120f), "UI Toolkit");
            GUI.Label(new Rect(36f, 56f, Screen.width - 72f, 72f), _uiFatal);
        }

        private void OnDestroy()
        {
            UIManager.CloseAll();
            ClearSpellFx();
            CancelVisualBinding();
            if (_root != null) Destroy(_root.gameObject);
            if (_clickableAreaMaterial != null)
            {
                Destroy(_clickableAreaMaterial);
                _clickableAreaMaterial = null;
            }
        }

        public void NewGame()
        {
            StartLevel(LevelAssetId);
        }

        #region 界面流程

        public void ShowTitle()
        {
            ShowTitleAsync().Forget();
        }

        private async UniTaskVoid ShowTitleAsync()
        {
            await UIManager.InitializeAsync();
            if (!UIManager.IsInitialized) return;
            if (_battleActive) return;
            LeaveBattleWorld();
            UIManager.ClosePopupAndOverlay();
            TitleView title = await UIManager.ShowScreenAsync<TitleView>(this);
            if (title == null)
            {
                SetUiFatal("[TakeoverUI] 标题界面打开失败: TakeoverTitleView；请查看 Console。");
                return;
            }
            PlayMusic("takeover_sfx_8126_musicMenuClass_musicMenuClass");
        }

        public void OpenSaveSlots()
        {
            UIManager.OpenPopupAsync<SaveSlotsView>(this).Forget();
        }

        public void EnterCampaignSlot(int slot)
        {
            bool hadSave = Campaign.HasSave(slot);
            Campaign.SelectProfile(slot);
            // 触发一次读取，损坏档的警告会记录在 LastLoadWarning。
            _ = Campaign.Data;
            if (!string.IsNullOrEmpty(Campaign.LastLoadWarning))
                Toast(Campaign.LastLoadWarning, 5f);

            if (hadSave) ShowCampaignMap();
            else ShowFactionSelect();
        }

        public void ShowFactionSelect()
        {
            ShowFactionSelectAsync().Forget();
        }

        private async UniTaskVoid ShowFactionSelectAsync()
        {
            await UIManager.InitializeAsync();
            LeaveBattleWorld();
            UIManager.ClosePopupAndOverlay();
            await UIManager.ShowScreenAsync<FactionSelectView>(this);
        }

        public void ContinueFromFactionSelect()
        {
            if (!Campaign.FactionPlayable(Campaign.SelectedFaction))
            {
                Toast("第 2/3 章内容尚未导入，暂不可挑战。");
                return;
            }
            Campaign.Save();
            ShowCampaignMap();
        }

        public void ShowCampaignMap()
        {
            ShowCampaignMapAsync().Forget();
        }

        private async UniTaskVoid ShowCampaignMapAsync()
        {
            await UIManager.InitializeAsync();
            LeaveBattleWorld();
            UIManager.ClosePopupAndOverlay();
            await UIManager.ShowScreenAsync<CampaignMapView>(this);
            PlayMusic("takeover_sfx_8126_musicMenuClass_musicMenuClass");
        }

        public void ShowSkillTree()
        {
            ShowSkillTreeAsync().Forget();
        }

        private async UniTaskVoid ShowSkillTreeAsync()
        {
            await UIManager.InitializeAsync();
            await UIManager.ShowScreenAsync<SkillTreeView>(this);
        }

        public void OpenMissionDetail(int levelAssetId)
        {
            UIManager.OpenPopupAsync<MissionDetailView>(new MissionDetailArgs
            {
                Runner = this,
                LevelId = levelAssetId,
            }).Forget();
        }

        public void ReturnToCampaignMap()
        {
            ShowCampaignMap();
        }

        public void SetPaused(bool paused)
        {
            _paused = paused;
        }

        public void CycleSpeed()
        {
            float[] speeds = { 1f, 4f, 16f, 64f, 256f };
            int current = 0;
            for (int i = 0; i < speeds.Length; i++)
            {
                if (Mathf.Approximately(_speed, speeds[i])) { current = i; break; }
            }
            SetSpeed(speeds[(current + 1) % speeds.Length]);
        }

        private void LeaveBattleWorld()
        {
            _levelRequestVersion++;
            _battleActive = false;
            _paused = false;
            _speed = 1f;
            _reported = false;
            _resultShown = false;
            _selected = null;
            _selectedSquad = null;
            _dragSquad = null;
            _pendingSpell = null;
            _sim = null;
            CancelVisualBinding();
            ClearSpellFx();
            if (_root != null) _root.gameObject.SetActive(false);
        }

        #endregion

        public void StartLevel(int levelAssetId)
        {
            // 先占住“战斗中”状态，避免启动引导异步打开标题把 HUD 覆盖掉。
            _battleActive = true;
            int version = ++_levelRequestVersion;
            StartLevelAsync(levelAssetId, version).Forget();
        }

        private async UniTaskVoid StartLevelAsync(int levelAssetId, int requestVersion)
        {
            await UIManager.InitializeAsync();
            if (requestVersion != _levelRequestVersion) return;
            if (!Campaign.IsUnlocked(levelAssetId))
            {
                _battleActive = false;
                Toast($"L{levelAssetId} 尚未解锁");
                return;
            }

            UIManager.ClosePopupAndOverlay();
            CancelVisualBinding();
            LevelAssetId = levelAssetId;
            Campaign.SetLastLevel(levelAssetId);
            _battleActive = true;
            _root.gameObject.SetActive(true);
            foreach (Transform c in _root) Destroy(c.gameObject);
            _nodeViews.Clear();
            _squadViews.Clear();
            _boundNodeVisuals.Clear();
            _nodeClickableAreas.Clear();
            _unitClickableAreas.Clear();
            ClearSpellFx();
            _selected = null;
            _selectedSquad = null;
            _paused = false;
            _speed = 1f;
            _reported = false;
            _resultShown = false;
            _lastReward = default;
            _pendingSpell = null;
            _dragSquad = null;

            // 玩家阵营来自档案选择；第一章固定西利亚。
            HumanFaction = Campaign.SelectedFaction;
            var lv = Cfg.TbLevel.DataList.FirstOrDefault(l => l.AssetId == LevelAssetId);
            if (lv != null)
            {
                EnemyFaction = OriginalRules.EnemyPlayers(LevelAssetId).FirstOrDefault()?.Faction ??
                               OriginalRules.FromConfigFaction(lv.EnemyFaction);
            }

            // 已购教令在进关时换算成修正器数组交给模拟器（建筑 HP 在建图时按 mods 缩放）
            _sim = new BattleSimulation(LevelAssetId, HumanFaction, EnemyFaction, Campaign.BuildHumanMods());
            PlayMusic("takeover_sfx_2910_musicGameplayClass_musicGameplayClass");
            BuildSelectionVisuals();
            BuildNodeViews();
            BuildEdgeViews();
            BuildMapBackground();
            FocusCamera();
            BindLevelVisuals();

            await UIManager.ShowScreenAsync<BattleHudView>(this);
            if (_sim == null) return;

            Log.Info($"[Takeover] 启动 L{LevelAssetId} 节点={_sim.Nodes.Count} 建筑={_sim.Nodes.Count(n => n.HasBuilding)}");        }

        private void FocusCamera()
        {
            if (_sim.Nodes.Count == 0) return;
            var min = Vector2.zero;
            var max = new Vector2(OriginalRules.NativeStageWidth, OriginalRules.NativeStageHeight);
            foreach (var n in _sim.Nodes)
            {
                var display = DisplayPosition(n.Pos);
                min = Vector2.Min(min, display);
                max = Vector2.Max(max, display);
            }
            var center = (min + max) * 0.5f;
            _cam.transform.position = new Vector3(center.x, center.y, -10f);
            float halfH = Mathf.Max(1f, (max.y - min.y) * 0.525f);
            float halfW = Mathf.Max(1f, (max.x - min.x) * 0.525f);
            _cam.orthographicSize = Mathf.Max(halfH, halfW / Mathf.Max(0.1f, _cam.aspect));
        }

        private static Vector2 DisplayPosition(Vector2 logical)
        {
            return TakeoverCoordinates.LogicalToDisplay(logical);
        }

        private void BuildNodeViews()
        {
            foreach (var n in _sim.Nodes)
            {
                var go = new GameObject($"node_{n.Key}");
                go.transform.SetParent(_root, false);
                go.transform.position = DisplayPosition(n.Pos);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = null;
                sr.enabled = false;
                float size = NativeSpriteScale;
                go.transform.localScale = new Vector3(size, size, 1f);
                sr.color = NodeColor(n);
                sr.sortingOrder = n.HasBuilding ? 2 : 1;
                _nodeViews[n] = sr;
            }

            BuildNodeClickableAreas();
        }

        private void BuildNodeClickableAreas()
        {
            foreach (MapNode node in _sim.Nodes)
            {
                if (!IsNodeClickable(node)) continue;
                _nodeClickableAreas[node] =
                    CreateClickableArea($"clickable_{node.Key}", 1);
            }
        }

        private void BuildMapBackground()
        {
            if (_sim.Nodes.Count == 0) return;
            var go = new GameObject("TakeoverMapBackground");
            go.transform.SetParent(_root, false);
            // 地图 PNG 与舞台坐标差一个平移（居中裁切导致），按表里的修正量把贴图挪回去：
            // 贴图特征在 贴图坐标 (x+ox, y+oy)，渲染后世界坐标 y = 600 - y_pixel，所以 x 取反、y 同号。
            Vector2 mapOffset = TakeoverVisualCatalog.MapOffset(LevelAssetId);
            go.transform.position = new Vector2(
                OriginalRules.NativeStageWidth * 0.5f - mapOffset.x,
                OriginalRules.NativeStageHeight * 0.5f + mapOffset.y);
            _mapBackground = go.AddComponent<SpriteRenderer>();
            _mapBackground.sortingOrder = -10;
        }

        private void BindLevelVisuals()
        {
            _visualBindingCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                this.GetCancellationTokenOnDestroy());
            BindLevelVisualsAsync(_visualBindingCancellationTokenSource.Token).Forget();
        }

        private async UniTask BindLevelVisualsAsync(CancellationToken cancellationToken)
        {
            // ResourceExtComponent 会在首帧之后创建精灵缓存。
            await UniTask.Yield(cancellationToken);
            await UniTask.WaitForEndOfFrame(this, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (_sim == null) return;

            foreach (var pair in _nodeViews)
            {
                BindNodeVisual(pair.Key, pair.Value);
            }

            if (_mapBackground != null && _sim.Nodes.Count > 0)
            {
                TakeoverVisualCatalog.BindMapBackground(
                    _mapBackground, LevelAssetId,
                    new Vector2(OriginalRules.NativeStageWidth, OriginalRules.NativeStageHeight));
            }
        }

        private void CancelVisualBinding()
        {
            if (_visualBindingCancellationTokenSource == null) return;
            _visualBindingCancellationTokenSource.Cancel();
            _visualBindingCancellationTokenSource.Dispose();
            _visualBindingCancellationTokenSource = null;
        }

        private static Color NodeColor(MapNode n)
        {
            if (n.Building != null)
            {
                switch (n.Building.Owner)
                {
                    case Faction.Westaria: return new Color(0.30f, 0.75f, 0.35f);
                    case Faction.Khaganate: return new Color(0.85f, 0.30f, 0.30f);
                    case Faction.Cult: return new Color(0.30f, 0.45f, 0.90f);
                    case Faction.Empire: return new Color(0.65f, 0.40f, 0.80f);
                    default: return Color.gray;
                }
            }
            if (n.Kind == NodeKind.BuildSpot) return new Color(0.95f, 0.70f, 0.25f);
            return new Color(0.55f, 0.55f, 0.55f);
        }

        /// <summary>
        /// 建筑贴图各阵营共用一张底图（building.xlsx 的 sprite 列），
        /// 用 SpriteRenderer.color 把底图朝所属阵营色叠加，靠颜色区分国家。
        /// </summary>
        private static Color FactionTint(MapNode node)
        {
            if (node.Building == null) return Color.white;
            return Color.Lerp(Color.white, NodeColor(node), FactionTintBlend);
        }

        private void BindNodeVisual(MapNode node, SpriteRenderer renderer)
        {
            if (node.Kind == NodeKind.Empty)
            {
                _boundNodeVisuals.Remove(node.Id);
                renderer.enabled = false;
                return;
            }
            string location = TakeoverVisualCatalog.LocationFor(node);
            if (string.IsNullOrEmpty(location)) return;
            if (_boundNodeVisuals.TryGetValue(node.Id, out string previous)
                && previous == location)
                return;
            if (TakeoverVisualCatalog.Bind(renderer, location))
            {
                _boundNodeVisuals[node.Id] = location;
                renderer.enabled = true;
            }
        }

        private void BuildEdgeViews()
        {
            foreach (MapEdge edge in _sim.Edges)
            {
                var go = new GameObject($"edge_{edge.Id}");
                go.transform.SetParent(_root, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.useWorldSpace = true;
                lr.positionCount = edge.Waypoints.Count;
                for (int i = 0; i < edge.Waypoints.Count; i++)
                    lr.SetPosition(i, DisplayPosition(edge.Waypoints[i]));
                lr.startWidth = 1.5f;
                lr.endWidth = 1.5f;
                lr.material = new Material(Shader.Find("Sprites/Default"));
                lr.startColor = new Color(0.35f, 0.35f, 0.30f, 0.35f);
                lr.endColor = new Color(0.35f, 0.35f, 0.30f, 0.35f);
                lr.sortingOrder = 0;
            }
        }

        private void BuildSelectionVisuals()
        {
            if (_clickableAreaMaterial != null)
                Destroy(_clickableAreaMaterial);
            _clickableAreaMaterial = new Material(Shader.Find("Sprites/Default"));
            _orderPreview = CreateOverlayLine("OrderPreview", new Color(1f, 0.85f, 0.35f, 0.9f));
        }

        private LineRenderer CreateClickableArea(string objectName, int sortingOrder)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(_root, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = true;
            line.positionCount = 32;
            line.material = _clickableAreaMaterial;
            line.sortingOrder = sortingOrder;
            return line;
        }

        private LineRenderer CreateOverlayLine(string objectName, Color color)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(_root, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 0;
            line.startWidth = 2f;
            line.endWidth = 2f;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.startColor = color;
            line.endColor = color;
            line.sortingOrder = 20;
            line.enabled = false;
            return line;
        }

        private void Update()
        {
            if (_sim == null || !_battleActive) return;
            UpdateCamera();
            if (!_paused && !_sim.IsOver)
            {
                float dt = Mathf.Min(0.1f, Time.deltaTime) * _speed;
                _sim.Tick(dt);
                ProcessBattleEvents();
            }

            if (_sim.IsOver && !_reported)
            {
                _reported = true;
                _lastReward = default;
                if (_sim.HumanVictory)
                {
                    var lvCfg = Cfg.TbLevel.DataList.FirstOrDefault(l => l.AssetId == LevelAssetId);
                    _lastReward = Campaign.CompleteBattle(LevelAssetId, _sim.Time, lvCfg?.Xp ?? 0);
                }
                ShowResultOverlay();
            }

            SyncSquads();
            SyncNodes();
            if (!_paused) UpdateSpellFx(Time.deltaTime);

            if (_selectedSquad != null && _selectedSquad.IsDead) _selectedSquad = null;
            if (_sim.IsOver)
            {
                _selected = null;
                _selectedSquad = null;
                return;
            }
            HandleInput();
        }

        private void ShowResultOverlay()
        {
            if (_sim == null || _resultShown) return;
            _resultShown = true;
            _paused = true;
            var args = new ResultOverlayArgs
            {
                Runner = this,
                Victory = _sim.HumanVictory,
                Seconds = _sim.Time,
                Stars = _sim.Stars,
                Reward = _lastReward,
            };
            UIManager.OpenOverlayAsync<ResultOverlayView>(args).Forget();
        }

        private void ProcessBattleEvents()
        {
            foreach (BattleEvent battleEvent in _sim.DrainEvents())
            {
                switch (battleEvent.Kind)
                {
                    case "SpellCast":
                    case "UltimateCast":
                        HandleSpellCastEvent(battleEvent);
                        break;
                    case "BuildingCaptured":
                        Toast("建筑已占领");
                        break;
                    case "ChestCollected":
                        Toast("宝箱 +500 金币");
                        break;
                    case "OgreFightStarted":
                        Toast("与食人魔交战，法术已解锁");
                        break;
                    case "OgreDefeated":
                        Toast("食人魔已击败，敌方开始行动");
                        break;
                    case "TutorialEnemyReinforced":
                        Toast("敌军得到增援");
                        break;
                    case "TutorialTownCaptured":
                        Toast("已占领敌方城镇");
                        break;
                    case "UltimateUnlocked":
                        Toast("占领魔法塔，终极技能已充满");
                        break;
                }
            }
        }

        /// <summary>
        /// 法术播报 + 表现：谁放的、什么法术、具体效果，并在落点画一圈范围提示。
        /// 敌方普通法术 4 秒内只播一次，避免 AI 互丢法术刷屏；终极技能每次播。
        /// </summary>
        private void HandleSpellCastEvent(BattleEvent battleEvent)
        {
            var spell = Cfg.TbSpell.DataList.FirstOrDefault(value =>
                value.Id == Mathf.RoundToInt(battleEvent.Value));
            PlayerState caster = _sim != null
                ? _sim.Players.FirstOrDefault(value => value.Id == battleEvent.ActorId)
                : null;
            int originalId = spell != null ? OriginalRules.OriginalSpellIndex(spell.Id) : -1;
            SpawnSpellFx(battleEvent.Position, spell != null ? spell.Radius : 0f, originalId);

            bool ultimate = battleEvent.Kind == "UltimateCast";
            bool byHuman = caster != null && _sim != null && _sim.Human != null && caster == _sim.Human;
            if (!byHuman && !ultimate &&
                Time.unscaledTime - _lastEnemySpellToast < EnemySpellToastInterval)
            {
                return;
            }
            _lastEnemySpellToast = Time.unscaledTime;

            string who = caster != null ? FactionDisplayName(caster.Faction) : "未知阵营";
            string name = spell != null ? spell.CnName : "法术";
            string effect = spell != null ? BattleSpellSystem.DescribeSpellText(originalId, spell) : "";
            Toast($"{who} 施放 {name}：{effect}");
        }

        private static string FactionDisplayName(Faction faction)
        {
            switch (faction)
            {
                case Faction.Westaria: return "维斯特利亚";
                case Faction.Khaganate: return "游牧汗国";
                case Faction.Cult: return "暗影教团";
                case Faction.Empire: return "帝国";
                default: return faction.ToString();
            }
        }

        private struct SpellFx
        {
            public LineRenderer Line;
            public float Life;
            public float Total;
            public Color Color;
        }

        private const float SpellFxLife = 1.2f;
        private readonly List<SpellFx> _spellFx = new List<SpellFx>();

        /// <summary>法术落点表现：一圈会淡出的范围指示；全屏类法术（半径 0）画满舞台。</summary>
        private void SpawnSpellFx(Vector2 center, float radius, int originalId)
        {
            if (_root == null) return;
            Color color = SpellFxColor(originalId);
            bool global = radius <= 1f;
            float drawRadius = global ? 300f : radius;
            Vector2 worldCenter = global
                ? new Vector2(OriginalRules.NativeStageWidth * 0.5f,
                    OriginalRules.NativeStageHeight * 0.5f)
                : DisplayPosition(center);

            LineRenderer line = CreateOverlayLine("SpellFx", color);
            const int segments = 48;
            line.positionCount = segments + 1;
            for (int i = 0; i <= segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2f;
                line.SetPosition(i, new Vector3(worldCenter.x + Mathf.Cos(angle) * drawRadius,
                    worldCenter.y + Mathf.Sin(angle) * drawRadius, 0f));
            }
            line.startWidth = 3f;
            line.endWidth = 3f;
            line.enabled = true;
            _spellFx.Add(new SpellFx { Line = line, Life = SpellFxLife, Total = SpellFxLife, Color = color });
        }

        private static Color SpellFxColor(int originalId)
        {
            switch (originalId)
            {
                case 0:
                case 4:
                case 7:
                case 12:
                case 14:
                    return new Color(1f, 0.38f, 0.28f, 0.9f);   // 伤害 / 控制
                case 1:
                case 2:
                case 6:
                case 9:
                case 10:
                    return new Color(0.45f, 1f, 0.55f, 0.9f);   // 治疗 / 增益
                case 8:
                    return new Color(0.55f, 0.85f, 1f, 0.9f);   // 冰冻
                default:
                    return new Color(0.85f, 0.75f, 1f, 0.9f);
            }
        }

        private void UpdateSpellFx(float dt)
        {
            for (int i = _spellFx.Count - 1; i >= 0; i--)
            {
                SpellFx fx = _spellFx[i];
                fx.Life -= dt;
                if (fx.Line == null || fx.Life <= 0f)
                {
                    if (fx.Line != null) Destroy(fx.Line.gameObject);
                    _spellFx.RemoveAt(i);
                    continue;
                }
                float k = Mathf.Clamp01(fx.Life / fx.Total);
                fx.Line.startColor = new Color(fx.Color.r, fx.Color.g, fx.Color.b, fx.Color.a * k);
                fx.Line.endColor = fx.Line.startColor;
                _spellFx[i] = fx;
            }
        }

        private void ClearSpellFx()
        {
            for (int i = 0; i < _spellFx.Count; i++)
            {
                if (_spellFx[i].Line != null) Destroy(_spellFx[i].Line.gameObject);
            }
            _spellFx.Clear();
        }

        private void SyncSquads()
        {
            var liveUnitIds = new HashSet<int>();
            foreach (Squad squad in _sim.Squads)
            {
                foreach (CombatUnit unit in squad.Units.Where(value => !value.IsDead))
                {
                    liveUnitIds.Add(unit.Id);
                    if (!_squadViews.TryGetValue(unit.Id, out SpriteRenderer renderer))
                    {
                        var go = new GameObject($"unit_{unit.Id}");
                        go.transform.SetParent(_root, false);
                        renderer = go.AddComponent<SpriteRenderer>();
                        renderer.sprite = null;
                        renderer.enabled = false;
                        renderer.sortingOrder = 5;
                        var animator = go.AddComponent<TakeoverUnitAnimator>();
                        animator.Initialize(renderer, squad.Def, unit.Id);
                        _squadViews[unit.Id] = renderer;
                    }
                    renderer.transform.position = DisplayPosition(unit.Pos);
                    renderer.transform.localScale = new Vector3(NativeSpriteScale, NativeSpriteScale, 1f);
                    bool picked = squad == _selectedSquad;
                    renderer.color = unit.FrozenSeconds > 0f ? new Color(0.65f, 0.85f, 1f) :
                        unit.PoisonSeconds > 0f ? new Color(0.70f, 1f, 0.55f) :
                        picked ? new Color(1f, 0.94f, 0.58f) : Color.white;
                    if (squad.Owner.IsHuman)
                    {
                        UpdateClickableArea(UnitClickableArea(unit.Id),
                            unit.Pos, 24f,
                            picked
                                ? new Color(1f, 0.88f, 0.48f, 0.32f)
                                : new Color(0.58f, 1f, 0.72f, 0.13f));
                    }
                }
            }
            foreach (var id in _squadViews.Keys.ToList())
            {
                if (!liveUnitIds.Contains(id))
                {
                    Destroy(_squadViews[id].gameObject);
                    _squadViews.Remove(id);
                }
            }
            foreach (int id in _unitClickableAreas.Keys.ToList())
            {
                if (liveUnitIds.Contains(id)) continue;
                Destroy(_unitClickableAreas[id].gameObject);
                _unitClickableAreas.Remove(id);
            }
        }

        private void SyncNodes()
        {
            foreach (var kv in _nodeViews)
            {
                var n = kv.Key;
                BindNodeVisual(n, kv.Value);
                bool usesOriginalArt = kv.Value.enabled && _boundNodeVisuals.ContainsKey(n.Id);
                kv.Value.color = n == _selected
                    ? new Color(1f, 0.94f, 0.58f)
                    : usesOriginalArt ? FactionTint(n) : NodeColor(n);
                float baseSize = NativeSpriteScale;
                float size = n == _selected ? baseSize * 1.12f : baseSize;
                kv.Value.transform.localScale = new Vector3(size, size, 1f);
                if (_nodeClickableAreas.TryGetValue(n, out LineRenderer clickableArea))
                {
                    UpdateClickableArea(clickableArea, n.Pos, 30f, ClickableNodeColor(n));
                }
            }
        }

        private LineRenderer UnitClickableArea(int unitId)
        {
            if (_unitClickableAreas.TryGetValue(unitId, out LineRenderer area))
                return area;
            area = CreateClickableArea($"clickable_unit_{unitId}", 4);
            _unitClickableAreas[unitId] = area;
            return area;
        }

        private static bool IsNodeClickable(MapNode node)
        {
            return node != null && (node.Building != null || node.Kind == NodeKind.BuildSpot);
        }

        private Color ClickableNodeColor(MapNode node)
        {
            if (node == _selected) return new Color(1f, 0.91f, 0.52f, 0.48f);
            if (node.Building != null)
            {
                return node.Building.Owner == HumanFaction
                    ? new Color(0.55f, 1f, 0.67f, 0.27f)
                    : new Color(1f, 0.70f, 0.52f, 0.23f);
            }
            if (node.Kind == NodeKind.BuildSpot)
            {
                bool controlled = _sim != null &&
                                  _sim.State.IsLocControlledBy(_sim.Human, node);
                return controlled
                    ? new Color(1f, 0.88f, 0.52f, 0.25f)
                    : new Color(0.72f, 0.72f, 0.68f, 0.13f);
            }
            return Color.clear;
        }

        private void UpdateClickableArea(LineRenderer area, Vector2 logicalCenter,
            float radiusPixels, Color color)
        {
            if (area == null) return;
            Vector2 center = DisplayPosition(logicalCenter);
            float radius = ScreenPixelsToWorld(radiusPixels);
            for (int i = 0; i < area.positionCount; i++)
            {
                float angle = Mathf.PI * 2f * i / area.positionCount;
                Vector2 point = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                area.SetPosition(i, new Vector3(point.x, point.y, 0f));
            }
            float width = Mathf.Max(0.6f, ScreenPixelsToWorld(1.2f));
            area.startWidth = width;
            area.endWidth = width;
            area.startColor = color;
            area.endColor = color;
            area.enabled = color.a > 0f;
        }

        private float ScreenPixelsToWorld(float pixels)
        {
            return 2f * _cam.orthographicSize / Mathf.Max(1f, Screen.height) * pixels;
        }

        private void HandleInput()
        {
            if (Input.GetKeyDown(KeyCode.Space)) _paused = !_paused;
            if (Input.GetKeyDown(KeyCode.R)) NewGame();
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (!UIManager.IsOpen<PauseOverlayView>() && !UIManager.IsOpen<SettingsOverlayView>() &&
                    !UIManager.IsOpen<ResultOverlayView>())
                {
                    _paused = true;
                    UIManager.OpenOverlayAsync<PauseOverlayView>(this).Forget();
                }
                return;
            }

            bool pointerOverUI = UIManager.IsPointerOverUI();
            if (pointerOverUI && _dragSquad == null)
            {
                if (Input.GetMouseButtonDown(0)) _pressOverUI = true;
                return;
            }

            // 原版没有框选：左键按住头顶徽标拖动下令（徽标 PointerDown 调 BeginBadgeDrag），
            // 左键单击建筑/空地选中，单击空地取消选择。
            if (Input.GetMouseButtonDown(0))
            {
                _pressScreen = Input.mousePosition;
                _pressOverUI = pointerOverUI;
            }

            if (Input.GetMouseButton(0) && _dragSquad != null)
            {
                MapNode hover = FindNodeAtScreen(Input.mousePosition, 34f);
                // UpdateOrderPreview 内部自己做 逻辑->显示 转换，这里必须传逻辑坐标。
                UpdateOrderPreview(_dragSquad.Pos,
                    hover?.Pos ?? MouseWorldPosition(),
                    hover?.Building != null && hover.Building.Owner != HumanFaction);
            }

            if (Input.GetMouseButtonUp(0))
            {
                float dragDistance = Vector2.Distance(_pressScreen, Input.mousePosition);
                if (_dragSquad != null)
                {
                    if (dragDistance >= 8f)
                        IssueOrderAtMouse(_dragSquad);
                    else
                        Toast($"选中 {_dragSquad.Def.CnName}  {_dragSquad.AliveSoldiers}/{_dragSquad.MaxSoldiers} 人");
                }
                else if (!_pressOverUI && dragDistance < 8f)
                {
                    HandleLeftClickAtMouse();
                }
                _dragSquad = null;
                _pressOverUI = false;
                if (_orderPreview != null) _orderPreview.enabled = false;
            }
        }

        /// <summary>由 UI Toolkit 的小队徽标在 PointerDown 时调用。</summary>
        public void BeginBadgeDrag(Squad squad)
        {
            if (squad == null || squad.IsDead || _sim == null || !squad.Owner.IsHuman) return;
            _dragSquad = squad;
            _selectedSquad = squad;
            _selected = null;
            _pressScreen = Input.mousePosition;
            _pressOverUI = false;

            MapNode hover = FindNodeAtScreen(Input.mousePosition, 34f);
            UpdateOrderPreview(squad.Pos,
                hover?.Pos ?? MouseWorldPosition(),
                hover?.Building != null && hover.Building.Owner != HumanFaction);
        }

        private void IssueOrderAtMouse(Squad squad)
        {
            if (squad == null || squad.IsDead) return;
            MapNode node = FindNodeAtScreen(Input.mousePosition, 34f);
            Vector2 target = node?.Pos ?? MouseWorldPosition();
            _sim.Enqueue(BattleCommand.Move(new[] { squad.Id }, target, node?.Id ?? 0));
            if (node?.Building != null && node.Building.Owner != HumanFaction)
                Toast($"部队前往 {node.Key}");
            else
                Toast($"已命令 {squad.Def.CnName} 移动");
        }

        private void HandleLeftClickAtMouse()
        {
            Vector2 mouse = Input.mousePosition;
            if (_pendingSpell != null)
            {
                MapNode spellNode = FindNodeAtScreen(mouse, 34f);
                Vector2 target = spellNode?.Pos ?? MouseWorldPosition();
                bool cast = _sim.ExecuteNow(BattleCommand.CastSpell(
                    _pendingSpell.Id, target, spellNode?.Id ?? 0));
                // 成功时的播报交给 SpellCast 事件（带施法者和效果说明），这里只报失败
                if (!cast) Toast("法术施放失败");
                _pendingSpell = null;
                return;
            }

            MapNode node = FindNodeAtScreen(mouse, 30f);
            if (node?.Building != null)
            {
                _selected = node;
                Toast(node.Building.Owner == HumanFaction
                    ? $"选中 {node.Key}，等级 {node.Building.Level + 1}"
                    : $"{node.Key} 属于 {node.Building.Owner}");
            }
            else if (node?.Kind == NodeKind.BuildSpot && node.Building == null)
            {
                _selected = node;
                Toast(_sim.State.IsLocControlledBy(_sim.Human, node)
                    ? $"空地 {node.Key}：可以建造"
                    : $"空地 {node.Key}：不在己方控制区");
            }
            else
            {
                // 点空地：取消选择
                ClearSelection();
            }
            _selectedSquad = null;
        }

        private MapNode FindNodeAtScreen(Vector2 screen, float maxDistance)
        {
            MapNode best = null;
            float bestDistance = maxDistance;
            foreach (MapNode node in _sim.Nodes)
            {
                Vector3 nodeScreen = _cam.WorldToScreenPoint(DisplayPosition(node.Pos));
                float distance = Vector2.Distance(screen, nodeScreen);
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = node;
            }
            return best;
        }

        private Vector2 MouseWorldPosition()
        {
            Vector3 world = _cam.ScreenToWorldPoint(Input.mousePosition);
            return new Vector2(world.x, (OriginalRules.NativeStageHeight - world.y) * OriginalRules.WorldYScale);
        }

        private void ClearSelection()
        {
            _selectedSquad = null;
            _selected = null;
        }

        /// <summary>绘制拖拽下令的预览曲线。from / to 都是模拟逻辑坐标（不是显示坐标）。</summary>
        private void UpdateOrderPreview(Vector2 from, Vector2 to, bool attack)
        {
            if (_orderPreview == null) return;
            Vector2 start = DisplayPosition(from);
            Vector2 end = DisplayPosition(to);
            Vector2 control = (start + end) * 0.5f +
                              Vector2.up * (Vector2.Distance(start, end) * 0.7f);
            const int segments = 16;
            _orderPreview.positionCount = segments + 1;
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                Vector2 point = (1f - t) * (1f - t) * start +
                                2f * (1f - t) * t * control +
                                t * t * end;
                _orderPreview.SetPosition(i, new Vector3(point.x, point.y, 0f));
            }
            Color color = attack
                ? new Color(1f, 0.25f, 0.20f, 0.95f)
                : new Color(0.35f, 1f, 0.40f, 0.95f);
            _orderPreview.startColor = color;
            _orderPreview.endColor = color;
            _orderPreview.enabled = true;
        }

        private void UpdateCamera()
        {
            if (UIManager.IsPointerOverUI()) return;
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.01f)
                _cam.orthographicSize = Mathf.Clamp(_cam.orthographicSize - scroll * 40f, 60f, 700f);

            float pan = 420f * Time.unscaledDeltaTime * (_cam.orthographicSize / 300f);
            var move = Vector3.zero;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) move.x -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) move.x += 1f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) move.y += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) move.y -= 1f;
            _cam.transform.position += move * pan;

            if (Input.GetMouseButtonDown(2)) { _dragging = true; _lastMouse = Input.mousePosition; }
            if (Input.GetMouseButtonUp(2)) _dragging = false;
            if (_dragging)
            {
                var delta = Input.mousePosition - _lastMouse;
                float k = _cam.orthographicSize * 2f / Mathf.Max(1f, Screen.height);
                _cam.transform.position -= new Vector3(delta.x * k, delta.y * k, 0f);
                _lastMouse = Input.mousePosition;
            }

        }

        private void Toast(string msg, float seconds = 2.5f)
        {
            UIManager.Toast(msg, seconds);
        }

        private static void PlayMusic(string location)
        {
            try
            {
                GameModule.Audio.Stop(TEngine.AudioType.Music, false);
                string resolved = null;
                foreach (string candidate in new[] { location, location + ".wav" })
                {
                    if (!GameModule.Resource.CheckLocationValid(candidate)) continue;
                    resolved = candidate;
                    break;
                }
                if (resolved != null)
                {
                    Log.Info($"[Takeover] 播放原版音乐 {resolved}");
                    GameModule.Audio.Play(TEngine.AudioType.Music, resolved, bLoop: true, bAsync: true);
                }
            }
            catch (System.Exception)
            {
                // 无头验证时音频不是必需项。
            }
        }

        public BattleSimulation Simulation => _sim;
        public MapNode SelectedNode => _selected;
        public Squad SelectedSquad => _selectedSquad;
        public bool Paused => _paused;
        public float Speed => _speed;
        public bool LevelSelectOpen => !_battleActive;
        public Camera WorldCamera => _cam;

        /// <summary>小队徽标的世界坐标（含头顶偏移）。</summary>
        public Vector3 GetSquadBadgeWorldPosition(Squad squad)
        {
            return DisplayPosition(squad.Pos) + new Vector2(0f, BadgeWorldOffsetY);
        }

        /// <summary>建筑徽标的世界坐标（含头顶偏移）。</summary>
        public Vector3 GetBuildingBadgeWorldPosition(MapNode node)
        {
            return DisplayPosition(node.Pos) + new Vector2(0f, BuildingBadgeWorldOffsetY);
        }

        public void TogglePause() => _paused = !_paused;
        public void SetSpeed(float speed) => _speed = Mathf.Clamp(speed, 1f, MaxSpeed);

        /// <summary>GM 面板最高 64 倍速。</summary>
        public const float MaxSpeed = 256f;

        /// <summary>玩家是否已被 AI 托管（底栏 AI 开关）。</summary>
        public bool HumanAiControlled => _sim != null && _sim.Human != null && _sim.Human.AiControlled;

        public void ToggleHumanAi()
        {
            if (_sim == null || _sim.Human == null) return;
            SetHumanAi(!_sim.Human.AiControlled);
        }

        public void SetHumanAi(bool enabled)
        {
            if (_sim == null || _sim.Human == null) return;
            _sim.Human.AiControlled = enabled;
            if (enabled)
            {
                // 立刻跑一拍，不用等下一个逻辑秒，开关点下去就能看到 AI 开始行动
                _sim.ForceAiTick(_sim.Human);
            }
            Toast(enabled ? "AI 已接管我方（再点一次收回）" : "已收回 AI，恢复手动操作");
        }

        public void GrantResources(int gold, int mana)
        {
            if (_sim == null) return;
            float oldGold = _sim.Human.Gold;
            float oldMana = _sim.Human.Mana;
            bool ok = _sim.ExecuteNow(BattleCommand.GrantResources(gold, mana));
            if (!ok) return;
            Toast($"GM：金币 +{_sim.Human.Gold - oldGold:F0}    " +
                  $"法力 +{_sim.Human.Mana - oldMana:F0}");
        }

        /// <summary>
        /// GM：强制结束本局。victory=true 走正常胜利结算（会解锁下一关），
        /// false 走战败结算；结算流程复用 Update 里 IsOver 分支，不另开代码路径。
        /// </summary>
        public void GmEndBattle(bool victory)
        {
            if (_sim == null || _sim.IsOver) return;
            _sim.State.IsOver = true;
            _sim.State.HumanVictory = victory;
            Toast(victory ? "GM：判定本关胜利" : "GM：判定本关失败");
        }

        public bool CanBuy(GameConfig.takeover.Unit unit, MapNode node)
        {
            return _sim != null && _sim.State.CanBuy(_sim.Human, unit, node, out _);
        }

        public List<GameConfig.takeover.Unit> UnitsAt(MapNode node)
        {
            if (_sim == null || node?.Building == null) return new List<GameConfig.takeover.Unit>();
            return Cfg.TbUnit.DataList
                .Where(u => OriginalRules.FromConfigFaction(u.Faction) == HumanFaction &&
                            BattleSim.AllowedAt(_sim.Human, node.Building, u))
                .OrderBy(u => u.UnitType).ThenBy(u => u.Tier).ToList();
        }

        public void Buy(GameConfig.takeover.Unit unit, MapNode node)
        {
            if (_sim == null) return;
            bool ok = _sim.ExecuteNow(BattleCommand.BuyUnit(unit.Id, node.Id));
            _sim.State.CanBuy(_sim.Human, unit, node, out string reason);
            Toast(ok ? $"已招募 {unit.CnName}" : reason ?? "招募失败");
        }

        public void Upgrade(MapNode node)
        {
            if (_sim == null) return;
            bool ok = _sim.ExecuteNow(BattleCommand.Upgrade(node.Id));
            if (ok) Campaign.AddHonor(25);
            Toast(ok ? "升级成功　荣誉 +25" : "升级失败");
        }

        public void Build(MapNode node, int buildingId)
        {
            if (_sim == null) return;
            _sim.State.CanPlace(_sim.Human, node, buildingId, out string reason);
            bool ok = _sim.ExecuteNow(BattleCommand.Build(buildingId, node.Id));
            Toast(ok ? "建造完成" : reason ?? "建造失败");
        }

        public bool CanBuild(MapNode node, int buildingId)
        {
            return _sim != null && _sim.State.CanPlace(_sim.Human, node, buildingId, out _);
        }

        public void BeginSpellTarget(GameConfig.takeover.Spell spell)
        {
            _pendingSpell = spell;
            Toast($"选择 {spell.CnName} 的目标位置");
        }
    }
}