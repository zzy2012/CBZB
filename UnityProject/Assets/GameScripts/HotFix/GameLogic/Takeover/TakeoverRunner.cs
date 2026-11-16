using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using TEngine;
using UnityEngine;

namespace GameLogic.Takeover
{
    /// <summary>
    /// 首章表现控制器：运行时生成地图、单位和正式 UGUI HUD。
    /// 逻辑全部在 BattleSim 中，View 只负责渲染与输入。
    /// </summary>
    public sealed class TakeoverRunner : MonoBehaviour
    {
        /// <summary>原版舞台坐标到精灵显示尺寸的缩放系数。</summary>
        private const float NativeSpriteScale = 100f;

        /// <summary>建筑底图按阵营染色的强度（0 = 原图，1 = 完全用阵营色）。</summary>
        private const float FactionTintBlend = 0.55f;

        /// <summary>当前运行中的战斗模拟门面。</summary>
        private BattleSimulation _sim;
        /// <summary>用于显示战场的摄像机。</summary>
        private Camera _cam;
        /// <summary>战场表现对象的根变换。</summary>
        private Transform _root;
        /// <summary>地图节点标识到场景显示对象的映射。</summary>
        private readonly Dictionary<MapNodeEntity, SpriteRenderer> _nodeViews = new Dictionary<MapNodeEntity, SpriteRenderer>();
        /// <summary>部队标识到场景显示对象的映射。</summary>
        private readonly Dictionary<int, SpriteRenderer> _squadViews = new Dictionary<int, SpriteRenderer>();
        /// <summary>已绑定节点精灵的显示对象集合。</summary>
        private readonly Dictionary<int, string> _boundNodeVisuals = new Dictionary<int, string>();
        /// <summary>各地图节点对应的可点击区域。</summary>
        private readonly Dictionary<MapNodeEntity, LineRenderer> _nodeClickableAreas =
            new Dictionary<MapNodeEntity, LineRenderer>();
        /// <summary>各部队士兵对应的可点击区域。</summary>
        private readonly Dictionary<int, LineRenderer> _unitClickableAreas =
            new Dictionary<int, LineRenderer>();
        /// <summary>当前关卡的地图背景渲染器。</summary>
        private SpriteRenderer _mapBackground;
        /// <summary>当前选中的地图节点。</summary>
        private MapNodeEntity _selected;
        /// <summary>当前选中的部队。</summary>
        private SquadEntity _selectedSquad;
        /// <summary>是否正在拖动鼠标选择单位。</summary>
        private bool _dragging;
        /// <summary>上一次记录的鼠标屏幕位置。</summary>
        private Vector3 _lastMouse;
        /// <summary>战斗是否暂停。</summary>
        private bool _paused;
        /// <summary>当前模拟速度倍率。</summary>
        private float _speed = 1f;
        /// <summary>战斗结算结果是否已处理。</summary>
        private bool _reported;
        /// <summary>关卡选择界面是否打开。</summary>
        private bool _levelSelectOpen = true;
        /// <summary>当前等待玩家选择目标的法术。</summary>
        private GameConfig.takeover.Spell _pendingSpell;
        /// <summary>当前注册的战斗界面。</summary>
        private TakeoverBattleUI _battleUI;
        /// <summary>鼠标拖动操作选中的部队。</summary>
        private SquadEntity _dragSquad;
        /// <summary>鼠标按下时的屏幕坐标。</summary>
        private Vector2 _pressScreen;
        /// <summary>显示部队移动命令预览的线条渲染器。</summary>
        private LineRenderer _orderPreview;
        /// <summary>用于绘制可点击区域的材质实例。</summary>
        private Material _clickableAreaMaterial;
        /// <summary>取消当前异步关卡视觉资源绑定操作的令牌源。</summary>
        private CancellationTokenSource _visualBindingCancellationTokenSource;

        /// <summary>是否已记录部队标记创建失败日志。</summary>
        private bool _badgeCreateErrorLogged;
        /// <summary>是否已记录部队标记预制体探测日志。</summary>
        private bool _badgeProbeLogged;
        /// <summary>部队标记是否因创建失败而停用。</summary>
        private bool _badgeDisabled;
        /// <summary>按部队标识缓存的部队界面标记。</summary>
        private readonly Dictionary<int, TakeoverSquadBadgeWidget> _squadBadges =
            new Dictionary<int, TakeoverSquadBadgeWidget>();
        /// <summary>按节点标识缓存的建筑界面标记。</summary>
        private readonly Dictionary<int, TakeoverBuildingBadgeWidget> _buildingBadges =
            new Dictionary<int, TakeoverBuildingBadgeWidget>();
        /// <summary>建筑标记是否因创建失败而停用。</summary>
        private bool _buildingBadgeDisabled;
        /// <summary>是否已记录建筑标记创建失败日志。</summary>
        private bool _buildingBadgeErrorLogged;

        /// <summary>部队标记相对战场位置的纵向偏移。</summary>
        private const float BadgeWorldOffsetY = 34f;
        /// <summary>建筑标记相对战场位置的纵向偏移。</summary>
        private const float BuildingBadgeWorldOffsetY = 42f;

        /// <summary>敌方普通法术播报的最小间隔（秒），避免 AI 互丢法术刷屏。</summary>
        private const float EnemySpellToastInterval = 4f;
        /// <summary>最近一次敌方施法提示的时间。</summary>
        private float _lastEnemySpellToast = -100f;

        /// <summary>待开始或当前关卡的资源标识。</summary>
        public int LevelAssetId = 1;
        /// <summary>用于初始化人类玩家的阵营。</summary>
        public Faction HumanFaction = Faction.Westaria;
        /// <summary>用于初始化敌方的阵营。</summary>
        public Faction EnemyFaction = Faction.Empire;

        /// <summary>调试开关：关掉后不再创建 squad 头顶徽标（排查卡顿/不显示时用）。</summary>
        public bool ShowSquadBadges = true;

        /// <summary>调试开关：关掉后不再创建建筑头顶徽标。</summary>
        public bool ShowBuildingBadges = true;

        /// <summary>初始化战场对象、摄像机和界面运行状态。</summary>
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
            _cam.backgroundColor = new Color(0.10f, 0.12f, 0.10f);
            _cam.clearFlags = CameraClearFlags.SolidColor;

            _root = new GameObject("TakeoverWorld").transform;
            _root.gameObject.SetActive(false);
            GameModule.UI.ShowUIAsync<TakeoverLevelSelectUI>(this);
            if (Campaign.LastLoadWarning != null)
                TakeoverToastUI.Show(Campaign.LastLoadWarning, 5f);
        }

        /// <summary>停止战场资源加载并清理表现对象及标记。</summary>
        private void OnDestroy()
        {
            ClearSquadBadges();
            ClearBuildingBadges();
            ClearSpellFx();
            CancelVisualBinding();
            _sim?.Dispose();
            _sim = null;
            if (_clickableAreaMaterial != null)
            {
                Destroy(_clickableAreaMaterial);
                _clickableAreaMaterial = null;
            }
        }

        /// <summary>按当前关卡资源标识开始战斗。</summary>
        public void NewGame()
        {
            StartLevel(LevelAssetId);
        }

        /// <summary>打开关卡选择界面。</summary>
        public void ShowLevelSelect()
        {
            PlayMusic("takeover_sfx_8126_musicMenuClass_musicMenuClass");
            _levelSelectOpen = true;
            _paused = true;
            _selected = null;
            _selectedSquad = null;
            _pendingSpell = null;
            ClearSquadBadges();
            ClearBuildingBadges();
            ClearSpellFx();
            if (_root != null) _root.gameObject.SetActive(false);
            GameModule.UI.CloseUI<TakeoverBattleUI>();
            GameModule.UI.CloseUI<TakeoverResultUI>();
            GameModule.UI.ShowUIAsync<TakeoverLevelSelectUI>(this);
            if (Campaign.LastLoadWarning != null)
                TakeoverToastUI.Show(Campaign.LastLoadWarning, 5f);
        }

        /// <summary>按关卡资源标识开始战斗。</summary>
        public void StartLevel(int levelAssetId)
        {
            if (!Campaign.IsUnlocked(levelAssetId))
            {
                Toast($"L{levelAssetId} 尚未解锁");
                return;
            }

            GameModule.UI.CloseUI<TakeoverLevelSelectUI>();
            GameModule.UI.CloseUI<TakeoverResultUI>();
            CancelVisualBinding();
            LevelAssetId = levelAssetId;
            Campaign.SetLastLevel(levelAssetId);
            _levelSelectOpen = false;
            _root.gameObject.SetActive(true);
            foreach (Transform c in _root) Destroy(c.gameObject);
            _nodeViews.Clear();
            _squadViews.Clear();
            _boundNodeVisuals.Clear();
            _nodeClickableAreas.Clear();
            _unitClickableAreas.Clear();
            ClearSquadBadges();
            ClearBuildingBadges();
            ClearSpellFx();
            _badgeCreateErrorLogged = false;
            _badgeProbeLogged = false;
            _badgeDisabled = false;
            _selected = null;
            _selectedSquad = null;
            _paused = false;
            _speed = 1f;              // 每次重开/进关都回到 ×1
            _reported = false;
            _pendingSpell = null;

            // 可编辑配置表沿用迁移期间的界面阵营顺序，此处转换为模拟器使用的原版 1.0.4 阵营索引。
            var lv = Cfg.TbLevel.DataList.FirstOrDefault(l => l.AssetId == LevelAssetId);
            if (lv != null)
            {
                HumanFaction = OriginalRules.FromConfigFaction(lv.Chapter);
                EnemyFaction = OriginalRules.EnemyPlayers(LevelAssetId).FirstOrDefault()?.Faction ??
                               OriginalRules.FromConfigFaction(lv.EnemyFaction);
            }

            // 已购教令在进关时换算成修正器数组交给模拟器（建筑 HP 在建图时按 mods 缩放）
            _sim?.Dispose();
            _sim = new BattleSimulation(LevelAssetId, HumanFaction, EnemyFaction, Campaign.BuildHumanMods());
            PlayMusic("takeover_sfx_2910_musicGameplayClass_musicGameplayClass");
            BuildSelectionVisuals();
            BuildNodeViews();
            BuildEdgeViews();
            BuildMapBackground();
            FocusCamera();
            BindLevelVisuals();
            GameModule.UI.ShowUIAsync<TakeoverBattleUI>(this);

            Log.Info($"[Takeover] 启动 L{LevelAssetId} 节点={_sim.Nodes.Count} 建筑={_sim.Nodes.Count(n => n.HasBuilding)}");
        }

        /// <summary>注册当前战斗界面。</summary>
        public void RegisterBattleUI(TakeoverBattleUI ui)
        {
            _battleUI = ui;
            if (!_levelSelectOpen && _sim != null)
                _battleUI.ShowBattle();
        }

        /// <summary>注销指定战斗界面。</summary>
        public void UnregisterBattleUI(TakeoverBattleUI ui)
        {
            if (_battleUI != ui) return;
            _battleUI = null;
            _squadBadges.Clear();
            _badgeCreateErrorLogged = false;
            _badgeProbeLogged = false;
            _badgeDisabled = false;
        }

        /// <summary>将摄像机调整到当前战场的合适位置。</summary>
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

        /// <summary>将模拟坐标转换为战场显示坐标。</summary>
        private static Vector2 DisplayPosition(Vector2 logical)
        {
            return TakeoverCoordinates.LogicalToDisplay(logical);
        }

        /// <summary>为地图节点创建并绑定场景显示对象。</summary>
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

        /// <summary>为可交互地图节点创建点击区域。</summary>
        private void BuildNodeClickableAreas()
        {
            foreach (MapNodeEntity node in _sim.Nodes)
            {
                if (!IsNodeClickable(node)) continue;
                _nodeClickableAreas[node] =
                    CreateClickableArea($"clickable_{node.Key}", 1);
            }
        }

        /// <summary>加载并布置当前关卡的地图背景。</summary>
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

        /// <summary>开始绑定当前关卡的地图视觉资源。</summary>
        private void BindLevelVisuals()
        {
            _visualBindingCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                this.GetCancellationTokenOnDestroy());
            BindLevelVisualsAsync(_visualBindingCancellationTokenSource.Token).Forget();
        }

        /// <summary>异步加载并绑定当前关卡视觉资源。</summary>
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

        /// <summary>取消尚未完成的关卡视觉资源绑定。</summary>
        private void CancelVisualBinding()
        {
            if (_visualBindingCancellationTokenSource == null) return;
            _visualBindingCancellationTokenSource.Cancel();
            _visualBindingCancellationTokenSource.Dispose();
            _visualBindingCancellationTokenSource = null;
        }

        /// <summary>根据节点状态计算节点显示颜色。</summary>
        private static Color NodeColor(MapNodeEntity n)
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
        private static Color FactionTint(MapNodeEntity node)
        {
            if (node.Building == null) return Color.white;
            return Color.Lerp(Color.white, NodeColor(node), FactionTintBlend);
        }

        /// <summary>将地图节点外观绑定到对应渲染对象。</summary>
        private void BindNodeVisual(MapNodeEntity node, SpriteRenderer renderer)
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

        /// <summary>创建地图连接边和行军路径的显示对象。</summary>
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

        /// <summary>创建节点与部队选中状态的显示对象。</summary>
        private void BuildSelectionVisuals()
        {
            if (_clickableAreaMaterial != null)
                Destroy(_clickableAreaMaterial);
            _clickableAreaMaterial = new Material(Shader.Find("Sprites/Default"));
            _orderPreview = CreateOverlayLine("OrderPreview", new Color(1f, 0.85f, 0.35f, 0.9f));
        }

        /// <summary>创建指定形状和位置的可点击区域。</summary>
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

        /// <summary>创建用于覆盖层显示的线条渲染器。</summary>
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

        /// <summary>推进战斗模拟并同步战场表现。</summary>
        private void Update()
        {
            if (_sim == null || _levelSelectOpen) return;
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
                if (_sim.HumanVictory)
                {
                    var lvCfg = Cfg.TbLevel.DataList.FirstOrDefault(l => l.AssetId == LevelAssetId);
                    Campaign.Complete(LevelAssetId, _sim.Time, lvCfg?.Xp ?? 0);
                }
                GameModule.UI.CloseUI<TakeoverBattleUI>();
                GameModule.UI.ShowUIAsync<TakeoverResultUI>(this);
            }

            SyncSquads();
            SyncNodes();
            SyncBuildingBadges();
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

        /// <summary>处理模拟器事件并触发对应界面反馈。</summary>
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
            PlayerStateEntity caster = _sim != null
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

        /// <summary>获取阵营对应的界面显示名称。</summary>
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

        /// <summary>保存短暂法术线条特效的显示状态。</summary>
        private struct SpellFx
        {
            /// <summary>绘制特效的线条渲染器。</summary>
            public LineRenderer Line;
            /// <summary>特效的剩余显示时间。</summary>
            public float Life;
            /// <summary>特效的总显示时间。</summary>
            public float Total;
            /// <summary>特效使用的颜色。</summary>
            public Color Color;
        }

        /// <summary>法术线条特效的显示时长，单位为秒。</summary>
        private const float SpellFxLife = 1.2f;
        /// <summary>当前显示的法术线条特效集合。</summary>
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

        /// <summary>根据法术事件类型确定特效颜色。</summary>
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

        /// <summary>更新法术线条特效的位置和剩余显示时间。</summary>
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

        /// <summary>清理当前显示的法术线条特效。</summary>
        private void ClearSpellFx()
        {
            for (int i = 0; i < _spellFx.Count; i++)
            {
                if (_spellFx[i].Line != null) Destroy(_spellFx[i].Line.gameObject);
            }
            _spellFx.Clear();
        }

        /// <summary>同步部队模拟状态与战场显示对象。</summary>
        private void SyncSquads()
        {
            var liveUnitIds = new HashSet<int>();
            foreach (SquadEntity squad in _sim.Squads)
            {
                foreach (CombatUnitEntity unit in squad.Units.Where(value => !value.IsDead))
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
            SyncSquadBadges();
        }

        /// <summary>头顶徽标：位置按屏幕坐标换算到 HUD 画布，尺寸不随镜头缩放（和原版一致）。</summary>
        private void SyncSquadBadges()
        {
            if (!ShowSquadBadges || _badgeDisabled)
            {
                if (_squadBadges.Count > 0) ClearSquadBadges();
                return;
            }
            if (_battleUI == null || _sim == null || _cam == null) return;
            RectTransform layer = _battleUI.SquadBadgeLayer;
            if (layer == null) return;

            // TEngine 的 UICanvas 是 Screen Space - Camera（带 UICamera），
            // 用 null 相机换算会把徽标全推到画布角落，所以这里按根画布的实际渲染模式取相机。
            Canvas canvas = layer.GetComponentInParent<Canvas>();
            Canvas rootCanvas = canvas != null ? canvas.rootCanvas : null;
            Camera uiCamera = rootCanvas != null &&
                              rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? rootCanvas.worldCamera
                : null;
            float badgeScale = ResolveBadgeScale(rootCanvas);
            if (!_badgeProbeLogged)
            {
                _badgeProbeLogged = true;
                Log.Info($"[Takeover] 头顶 UI: squads={_sim.Squads.Count} " +
                         $"renderMode={rootCanvas?.renderMode} " +
                         $"scaleFactor={rootCanvas?.scaleFactor:F2} badgeScale={badgeScale:F2} " +
                         $"camera={(uiCamera != null ? uiCamera.name : "null")}");
            }

            var live = new HashSet<int>();
            foreach (SquadEntity squad in _sim.Squads)
            {
                if (squad.IsDead) continue;
                live.Add(squad.Id);
                if (!_squadBadges.TryGetValue(squad.Id, out TakeoverSquadBadgeWidget badge))
                {
                    badge = _battleUI.CreateSquadBadgeWidget();
                    if (badge == null) continue;
                    if (!badge.IsValid)
                    {
                        badge.Visible = false;
                        _badgeDisabled = true;
                        if (!_badgeCreateErrorLogged)
                        {
                            _badgeCreateErrorLogged = true;
                            Log.Error("[Takeover] Squad badge widget could not be created; badges are disabled.");
                        }
                        ClearSquadBadges();
                        ClearBuildingBadges();
                        return;
                    }
                    _squadBadges[squad.Id] = badge;
                    badge.Bind(squad);
                }

                Vector3 screen = _cam.WorldToScreenPoint(
                    DisplayPosition(squad.Pos) + new Vector2(0f, BadgeWorldOffsetY));
                Vector2 local;
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, screen, uiCamera, out local))
                    continue;
                badge.SetPosition(local);
                badge.RefreshVisuals(squad == _selectedSquad, badgeScale);
            }

            foreach (int id in _squadBadges.Keys.ToList())
            {
                if (live.Contains(id)) continue;
                DestroySquadBadge(id);
            }
        }

        /// <summary>让徽标的 1 个画布单位等于 1 个舞台像素（画布参考分辨率和舞台不同，需要换算）。</summary>
        private float ResolveBadgeScale(Canvas canvas)
        {
            if (canvas == null || canvas.scaleFactor <= 0.0001f) return 1f;
            float pixelsPerStagePixel = Mathf.Abs(
                _cam.WorldToScreenPoint(new Vector3(0f, 1f, 0f)).y -
                _cam.WorldToScreenPoint(Vector3.zero).y);
            if (pixelsPerStagePixel <= 0.0001f) return 1f;
            return pixelsPerStagePixel / canvas.scaleFactor;
        }

        /// <summary>销毁指定部队的界面标记。</summary>
        private void DestroySquadBadge(int squadId)
        {
            if (!_squadBadges.TryGetValue(squadId, out TakeoverSquadBadgeWidget badge)) return;
            badge.Visible = false;
            _squadBadges.Remove(squadId);
        }

        /// <summary>清理所有部队界面标记。</summary>
        private void ClearSquadBadges()
        {
            foreach (int id in _squadBadges.Keys.ToList()) DestroySquadBadge(id);
            _squadBadges.Clear();
        }

        /// <summary>建筑头顶徽标：等级 + 血条 + 阵营色环，跟随建筑位置（只对城镇/要塞/营地/魔法塔）。</summary>
        private void SyncBuildingBadges()
        {
            if (!ShowBuildingBadges || _buildingBadgeDisabled)
            {
                if (_buildingBadges.Count > 0) ClearBuildingBadges();
                return;
            }
            if (_sim == null) return;
            if (!TryResolveBadgeContext(out RectTransform layer, out Camera uiCamera, out float badgeScale))
                return;

            var live = new HashSet<int>();
            foreach (MapNodeEntity node in _sim.Nodes)
            {
                BuildingState building = node.Building;
                if (building == null || !BattleSim.IsKeyBuilding(building)) continue;
                live.Add(node.Id);
                if (!_buildingBadges.TryGetValue(node.Id, out TakeoverBuildingBadgeWidget badge))
                {
                    badge = _battleUI.CreateBuildingBadgeWidget();
                    if (badge == null) continue;
                    if (!badge.IsValid)
                    {
                        if (badge != null) badge.Visible = false;
                        _buildingBadgeDisabled = true;
                        if (!_buildingBadgeErrorLogged)
                        {
                            _buildingBadgeErrorLogged = true;
                            Log.Error("[Takeover] Building badge widget could not be created; " +
                                      "building badges are disabled.");
                        }
                        ClearBuildingBadges();
                        return;
                    }
                    _buildingBadges[node.Id] = badge;
                    badge.Bind(node);
                }

                Vector3 screen = _cam.WorldToScreenPoint(
                    DisplayPosition(node.Pos) + new Vector2(0f, BuildingBadgeWorldOffsetY));
                Vector2 local;
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, screen, uiCamera, out local))
                    continue;
                badge.SetPosition(local);
                badge.RefreshVisuals(node == _selected, building.Owner == _sim.Human.Faction, badgeScale);
            }

            foreach (int id in _buildingBadges.Keys.ToList())
            {
                if (live.Contains(id)) continue;
                DestroyBuildingBadge(id);
            }
        }

        /// <summary>徽标层、UI 相机、尺寸换算，squad 与建筑徽标共用。</summary>
        private bool TryResolveBadgeContext(out RectTransform layer, out Camera uiCamera, out float badgeScale)
        {
            layer = null;
            uiCamera = null;
            badgeScale = 1f;
            if (_battleUI == null || _cam == null) return false;
            layer = _battleUI.SquadBadgeLayer;
            if (layer == null) return false;
            Canvas canvas = layer.GetComponentInParent<Canvas>();
            Canvas rootCanvas = canvas != null ? canvas.rootCanvas : null;
            uiCamera = rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? rootCanvas.worldCamera
                : null;
            badgeScale = ResolveBadgeScale(rootCanvas);
            return true;
        }

        /// <summary>销毁指定建筑的界面标记。</summary>
        private void DestroyBuildingBadge(int nodeId)
        {
            if (!_buildingBadges.TryGetValue(nodeId, out TakeoverBuildingBadgeWidget badge)) return;
            badge.Visible = false;
            _buildingBadges.Remove(nodeId);
        }

        /// <summary>清理所有建筑界面标记。</summary>
        private void ClearBuildingBadges()
        {
            foreach (int id in _buildingBadges.Keys.ToList()) DestroyBuildingBadge(id);
            _buildingBadges.Clear();
        }

        /// <summary>同步地图节点模拟状态与战场显示对象。</summary>
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

        /// <summary>获取指定单位对应的可点击区域。</summary>
        private LineRenderer UnitClickableArea(int unitId)
        {
            if (_unitClickableAreas.TryGetValue(unitId, out LineRenderer area))
                return area;
            area = CreateClickableArea($"clickable_unit_{unitId}", 4);
            _unitClickableAreas[unitId] = area;
            return area;
        }

        /// <summary>判断地图节点当前是否可响应玩家点击。</summary>
        private static bool IsNodeClickable(MapNodeEntity node)
        {
            return node != null && (node.Building != null || node.Kind == NodeKind.BuildSpot);
        }

        /// <summary>根据节点交互状态计算点击区域颜色。</summary>
        private Color ClickableNodeColor(MapNodeEntity node)
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

        /// <summary>更新节点和单位可点击区域的显示状态。</summary>
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

        /// <summary>将屏幕像素坐标转换为世界坐标。</summary>
        private float ScreenPixelsToWorld(float pixels)
        {
            return 2f * _cam.orthographicSize / Mathf.Max(1f, Screen.height) * pixels;
        }

        /// <summary>处理战场鼠标输入、选中和命令操作。</summary>
        private void HandleInput()
        {
            if (Input.GetKeyDown(KeyCode.Space)) _paused = !_paused;
            if (Input.GetKeyDown(KeyCode.R)) NewGame();
            if (Input.GetKeyDown(KeyCode.Escape)) ShowLevelSelect();
            bool pointerOverUI = UnityEngine.EventSystems.EventSystem.current != null
                                 && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
            if (pointerOverUI && _dragSquad == null)
                return;

            // 原版没有框选：左键按住头顶徽标拖动下令，左键单击建筑/空地选中，单击空地取消选择。
            if (Input.GetMouseButtonDown(0))
            {
                _pressScreen = Input.mousePosition;
                _dragSquad = PickHumanSquadByBadge(_pressScreen);
                if (_dragSquad != null)
                {
                    _selectedSquad = _dragSquad;
                    _selected = null;
                    MapNodeEntity hover = FindNodeAtScreen(Input.mousePosition, 34f);
                    // UpdateOrderPreview 内部自己做 逻辑->显示 转换，这里必须传逻辑坐标，
                    // 否则起点会被转换两次（第二次是 600-y/2 的镜像折叠），曲线从错误的位置画出去。
                    UpdateOrderPreview(_dragSquad.Pos,
                        hover?.Pos ?? MouseWorldPosition(),
                        hover?.Building != null && hover.Building.Owner != HumanFaction);
                }
            }

            if (Input.GetMouseButton(0))
            {
                if (_dragSquad != null)
                {
                    MapNodeEntity hover = FindNodeAtScreen(Input.mousePosition, 34f);
                    UpdateOrderPreview(_dragSquad.Pos,
                        hover?.Pos ?? MouseWorldPosition(),
                        hover?.Building != null && hover.Building.Owner != HumanFaction);
                }
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
                else if (dragDistance < 8f)
                {
                    HandleLeftClickAtMouse();
                }
                _dragSquad = null;
                if (_orderPreview != null) _orderPreview.enabled = false;
            }
        }

        /// <summary>在鼠标位置为选中部队下达移动命令。</summary>
        private void IssueOrderAtMouse(SquadEntity squad)
        {
            if (squad == null || squad.IsDead) return;
            MapNodeEntity node = FindNodeAtScreen(Input.mousePosition, 34f);
            Vector2 target = node?.Pos ?? MouseWorldPosition();
            _sim.Enqueue(BattleCommand.Move(new[] { squad.Id }, target, node?.Id ?? 0));
            if (node?.Building != null && node.Building.Owner != HumanFaction)
                Toast($"部队前往 {node.Key}");
            else
                Toast($"已命令 {squad.Def.CnName} 移动");
        }

        /// <summary>处理鼠标左键在战场上的点击操作。</summary>
        private void HandleLeftClickAtMouse()
        {
            Vector2 mouse = Input.mousePosition;
            if (_pendingSpell != null)
            {
                MapNodeEntity spellNode = FindNodeAtScreen(mouse, 34f);
                Vector2 target = spellNode?.Pos ?? MouseWorldPosition();
                bool cast = _sim.ExecuteNow(BattleCommand.CastSpell(
                    _pendingSpell.Id, target, spellNode?.Id ?? 0));
                // 成功时的播报交给 SpellCast 事件（带施法者和效果说明），这里只报失败
                if (!cast) Toast("法术施放失败");
                _pendingSpell = null;
                return;
            }

            MapNodeEntity node = FindNodeAtScreen(mouse, 30f);
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

        /// <summary>选中/下令都走头顶徽标：点士兵本体不再生效（原版也是这样操作的）。</summary>
        private SquadEntity PickHumanSquadByBadge(Vector2 screen)
        {
            RectTransform layer = _battleUI != null ? _battleUI.SquadBadgeLayer : null;
            if (layer == null) return null;
            Canvas canvas = layer.GetComponentInParent<Canvas>();
            Canvas root = canvas != null ? canvas.rootCanvas : null;
            Camera uiCamera = root != null && root.renderMode != RenderMode.ScreenSpaceOverlay
                ? root.worldCamera
                : null;

            SquadEntity best = null;
            foreach (TakeoverSquadBadgeWidget badge in _squadBadges.Values)
            {
                if (badge == null || badge.RectTransform == null || badge.Squad == null) continue;
                if (!badge.Squad.Owner.IsHuman || badge.Squad.IsDead) continue;
                if (badge.ContainsScreenPoint(screen, uiCamera))
                    best = badge.Squad;
            }
            return best;
        }

        /// <summary>查找屏幕坐标下的地图节点。</summary>
        private MapNodeEntity FindNodeAtScreen(Vector2 screen, float maxDistance)
        {
            MapNodeEntity best = null;
            float bestDistance = maxDistance;
            foreach (MapNodeEntity node in _sim.Nodes)
            {
                Vector3 nodeScreen = _cam.WorldToScreenPoint(DisplayPosition(node.Pos));
                float distance = Vector2.Distance(screen, nodeScreen);
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = node;
            }
            return best;
        }

        /// <summary>获取鼠标指针对应的战场世界坐标。</summary>
        private Vector2 MouseWorldPosition()
        {
            Vector3 world = _cam.ScreenToWorldPoint(Input.mousePosition);
            return new Vector2(world.x, (OriginalRules.NativeStageHeight - world.y) * OriginalRules.WorldYScale);
        }

        /// <summary>清除当前地图节点和部队的选中状态。</summary>
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

        /// <summary>根据用户输入更新战场摄像机。</summary>
        private void UpdateCamera()
        {
            bool pointerOverUI = UnityEngine.EventSystems.EventSystem.current != null
                                 && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
            if (pointerOverUI) return;
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

        /// <summary>显示战斗界面提示消息。</summary>
        private void Toast(string msg)
        {
            TakeoverToastUI.Show(msg);
        }

        /// <summary>播放指定的 Takeover 音乐资源。</summary>
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

        /// <summary>当前战斗模拟实例。</summary>
        public BattleSimulation Simulation => _sim;
        /// <summary>当前选中的地图节点。</summary>
        public MapNodeEntity SelectedNode => _selected;
        /// <summary>当前选中的部队。</summary>
        public SquadEntity SelectedSquad => _selectedSquad;
        /// <summary>战斗是否处于暂停状态。</summary>
        public bool Paused => _paused;
        /// <summary>当前战斗推进速度。</summary>
        public float Speed => _speed;
        /// <summary>关卡选择界面是否打开。</summary>
        public bool LevelSelectOpen => _levelSelectOpen;

        /// <summary>切换战斗暂停状态。</summary>
        public void TogglePause() => _paused = !_paused;
        /// <summary>设置战斗推进速度并限制到允许范围内。</summary>
        public void SetSpeed(float speed) => _speed = Mathf.Clamp(speed, 1f, MaxSpeed);

        /// <summary>GM 面板最高 64 倍速。</summary>
        public const float MaxSpeed = 256f;

        /// <summary>玩家是否已被 AI 托管（底栏 AI 开关）。</summary>
        public bool HumanAiControlled => _sim != null && _sim.Human != null && _sim.Human.AiControlled;

        /// <summary>切换人类玩家的 AI 托管状态。</summary>
        public void ToggleHumanAi()
        {
            if (_sim == null || _sim.Human == null) return;
            SetHumanAi(!_sim.Human.AiControlled);
        }

        /// <summary>设置是否由 AI 托管人类玩家。</summary>
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

        /// <summary>通过调试命令为人类玩家增减金币和魔法。</summary>
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

        /// <summary>检查当前玩家能否在节点招募指定单位。</summary>
        public bool CanBuy(GameConfig.takeover.Unit unit, MapNodeEntity node)
        {
            return _sim != null && _sim.State.CanBuy(_sim.Human, unit, node, out _);
        }

        /// <summary>获取当前玩家在指定节点可招募的单位配置。</summary>
        public List<GameConfig.takeover.Unit> UnitsAt(MapNodeEntity node)
        {
            if (_sim == null || node?.Building == null) return new List<GameConfig.takeover.Unit>();
            return Cfg.TbUnit.DataList
                .Where(u => OriginalRules.FromConfigFaction(u.Faction) == HumanFaction &&
                            BattleSim.AllowedAt(_sim.Human, node.Building, u))
                .OrderBy(u => u.UnitType).ThenBy(u => u.Tier).ToList();
        }

        /// <summary>提交在指定节点招募单位的命令。</summary>
        public void Buy(GameConfig.takeover.Unit unit, MapNodeEntity node)
        {
            if (_sim == null) return;
            bool ok = _sim.ExecuteNow(BattleCommand.BuyUnit(unit.Id, node.Id));
            _sim.State.CanBuy(_sim.Human, unit, node, out string reason);
            Toast(ok ? $"已招募 {unit.CnName}" : reason ?? "招募失败");
        }

        /// <summary>提交升级指定节点建筑的命令。</summary>
        public void Upgrade(MapNodeEntity node)
        {
            if (_sim == null) return;
            bool ok = _sim.ExecuteNow(BattleCommand.Upgrade(node.Id));
            Toast(ok ? "升级成功" : "升级失败");
        }

        /// <summary>提交在指定节点建造建筑的命令。</summary>
        public void Build(MapNodeEntity node, int buildingId)
        {
            if (_sim == null) return;
            _sim.State.CanPlace(_sim.Human, node, buildingId, out string reason);
            bool ok = _sim.ExecuteNow(BattleCommand.Build(buildingId, node.Id));
            Toast(ok ? "建造完成" : reason ?? "建造失败");
        }

        /// <summary>检查当前玩家能否在节点建造指定建筑。</summary>
        public bool CanBuild(MapNodeEntity node, int buildingId)
        {
            return _sim != null && _sim.State.CanPlace(_sim.Human, node, buildingId, out _);
        }

        /// <summary>进入指定法术的目标选择状态。</summary>
        public void BeginSpellTarget(GameConfig.takeover.Spell spell)
        {
            _pendingSpell = spell;
            Toast($"选择 {spell.CnName} 的目标位置");
        }
    }
}