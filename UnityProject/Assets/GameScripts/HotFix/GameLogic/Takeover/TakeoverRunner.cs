using System.Collections.Generic;
using System.Linq;
using System.Collections;
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
        private const float NativeSpriteScale = 100f;
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
        private readonly HashSet<Squad> _selectedSquads = new HashSet<Squad>();
        private SpriteRenderer _mapBackground;
        private MapNode _selected;
        private Squad _selectedSquad;
        private bool _dragging;
        private Vector3 _lastMouse;
        private bool _paused;
        private float _speed = 1f;
        private string _toast = "";
        private bool _reported;
        private float _toastTimer;
        private bool _levelSelectOpen = true;
        private GameConfig.takeover.Spell _pendingSpell;
        private TakeoverUIWindow _ui;
        private Squad _dragSquad;
        private Vector2 _selectionStart;
        private Vector2 _selectionEnd;
        private bool _selecting;
        private LineRenderer _selectionBox;
        private LineRenderer _orderPreview;
        private Material _clickableAreaMaterial;

        public int LevelAssetId = 1;
        public Faction HumanFaction = Faction.Westaria;
        public Faction EnemyFaction = Faction.Empire;

        private void Awake()
        {
            Application.targetFrameRate = 60;
            // 关掉 TEngine 自带的 DEBUGGER 面板，避免遮住玩法 HUD
            try { GameModule.Debugger.ActiveWindow = false; } catch { }
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
            GameModule.UI.ShowUI<TakeoverUIWindow>(this);
        }

        private void OnDestroy()
        {
            TakeoverSpriteCache.Clear();
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

        public void ShowLevelSelect()
        {
            PlayMusic("takeover_sfx_8126_musicMenuClass_musicMenuClass");
            _levelSelectOpen = true;
            _paused = true;
            _selected = null;
            _selectedSquad = null;
            _selectedSquads.Clear();
            _pendingSpell = null;
            if (_root != null) _root.gameObject.SetActive(false);
            _ui?.ShowLevelSelect();
        }

        public void StartLevel(int levelAssetId)
        {
            if (!Campaign.IsUnlocked(levelAssetId))
            {
                Toast($"L{levelAssetId} 尚未解锁");
                return;
            }

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
            _selected = null;
            _selectedSquad = null;
            _selectedSquads.Clear();
            _paused = false;
            _reported = false;
            _pendingSpell = null;

            // The editable tables kept the migration UI order. Convert once into
            // the raw 1.0.4 faction indexes used by the simulation.
            var lv = Cfg.TbLevel.DataList.FirstOrDefault(l => l.AssetId == LevelAssetId);
            if (lv != null)
            {
                HumanFaction = OriginalRules.FromConfigFaction(lv.Chapter);
                EnemyFaction = OriginalRules.EnemyPlayers(LevelAssetId).FirstOrDefault()?.Faction ??
                               OriginalRules.FromConfigFaction(lv.EnemyFaction);
            }

            _sim = new BattleSimulation(LevelAssetId, HumanFaction, EnemyFaction);
            PlayMusic("takeover_sfx_2910_musicGameplayClass_musicGameplayClass");
            BuildSelectionVisuals();
            BuildNodeViews();
            BuildEdgeViews();
            BuildMapBackground();
            FocusCamera();
            StartCoroutine(BindLevelVisuals());
            _ui?.ShowBattle();
            ShowScenarioHint();

            Debug.Log($"[Takeover] 启动 L{LevelAssetId} 节点={_sim.Nodes.Count} 建筑={_sim.Nodes.Count(n => n.HasBuilding)}");
        }

        private void ShowScenarioHint()
        {
            if (_ui == null) return;
            if (LevelAssetId == 1)
                _ui.ShowToast("点击己方城堡招募，再按住部队拖到目标；拖框可多选", 6f);
            else if (LevelAssetId == 2)
                _ui.ShowToast("先建造农场增加补给；左键拖动部队下令，拖框多选", 6f);
            else
                _ui.ShowToast("左键按住部队拖动下令；空地拖框多选；左键点击建筑打开面板", 6f);
        }

        public void RegisterUI(TakeoverUIWindow ui)
        {
            _ui = ui;
            if (_levelSelectOpen) _ui.ShowLevelSelect();
            else _ui.ShowBattle();
        }

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
            go.transform.position = new Vector2(
                OriginalRules.NativeStageWidth * 0.5f,
                OriginalRules.NativeStageHeight * 0.5f);
            _mapBackground = go.AddComponent<SpriteRenderer>();
            _mapBackground.sortingOrder = -10;
        }

        private IEnumerator BindLevelVisuals()
        {
            // ResourceExtComponent creates its sprite cache after its first frame.
            yield return null;
            yield return new WaitForEndOfFrame();
            if (_sim == null) yield break;

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
            _selectionBox = CreateOverlayLine("SelectionBox", new Color(0.35f, 1f, 0.35f, 0.9f));
            _selectionBox.loop = true;
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
                _ui?.ShowResult(_sim);
            }

            SyncSquads();
            SyncNodes();

            if (_toastTimer > 0f) _toastTimer -= Time.deltaTime;

            _selectedSquads.RemoveWhere(squad => squad == null || squad.IsDead);
            if (_selectedSquad != null && (!_selectedSquads.Contains(_selectedSquad) || _selectedSquad.IsDead))
                _selectedSquad = _selectedSquads.FirstOrDefault();
            if (_sim.IsOver)
            {
                _selected = null;
                _selectedSquads.Clear();
                _selectedSquad = null;
                return;
            }
            HandleInput();
        }

        private void ProcessBattleEvents()
        {
            foreach (BattleEvent battleEvent in _sim.DrainEvents())
            {
                switch (battleEvent.Kind)
                {
                    case "SpellCast":
                    case "UltimateCast":
                        Toast(battleEvent.Kind == "UltimateCast" ? "终极技能发动" : "法术发动");
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
                    bool picked = _selectedSquads.Contains(squad);
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
                    : usesOriginalArt ? Color.white : NodeColor(n);
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
            if (Input.GetKeyDown(KeyCode.Escape)) ShowLevelSelect();
            bool pointerOverUI = UnityEngine.EventSystems.EventSystem.current != null
                                 && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
            if (pointerOverUI && _dragSquad == null && !_selecting)
                return;

            if (Input.GetMouseButtonDown(0))
            {
                _selectionStart = Input.mousePosition;
                _selectionEnd = _selectionStart;
                _dragSquad = FindHumanSquadAtScreen(_selectionStart);
                if (_dragSquad != null)
                {
                    if (_selectedSquads.Count == 0 || !_selectedSquads.Contains(_dragSquad))
                    {
                        _selectedSquads.Clear();
                        _selectedSquads.Add(_dragSquad);
                    }
                    _selectedSquad = _dragSquad;
                    _selected = null;
                    MapNode hover = FindNodeAtScreen(Input.mousePosition, 34f);
                    UpdateOrderPreview(DisplayPosition(_dragSquad.Pos),
                        hover?.Pos ?? MouseWorldPosition(),
                        hover?.Building != null && hover.Building.Owner != HumanFaction);
                }
                else
                {
                    _selecting = true;
                    ClearSelection();
                    UpdateSelectionBox();
                }
            }

            if (Input.GetMouseButton(0))
            {
                _selectionEnd = Input.mousePosition;
                if (_dragSquad != null)
                {
                    MapNode hover = FindNodeAtScreen(Input.mousePosition, 34f);
                    UpdateOrderPreview(DisplayPosition(_dragSquad.Pos),
                        hover?.Pos ?? MouseWorldPosition(),
                        hover?.Building != null && hover.Building.Owner != HumanFaction);
                }
                else if (_selecting)
                {
                    UpdateSelectionBox();
                    SelectSquadsInBox();
                }
            }

            if (Input.GetMouseButtonUp(0))
            {
                float dragDistance = Vector2.Distance(_selectionStart, Input.mousePosition);
                if (_dragSquad != null)
                {
                    if (dragDistance >= 8f)
                        IssueGroupOrderAtMouse();
                    else
                        Toast($"选中 {_dragSquad.Def.CnName}  {_dragSquad.AliveSoldiers}/{_dragSquad.MaxSoldiers} 人");
                }
                else if (_selecting && dragDistance < 8f)
                {
                    HandleLeftClickAtMouse();
                }
                _dragSquad = null;
                _selecting = false;
                if (_selectionBox != null) _selectionBox.enabled = false;
                if (_orderPreview != null) _orderPreview.enabled = false;
            }
        }

        private void IssueGroupOrderAtMouse()
        {
            if (_selectedSquads.Count == 0) return;
            MapNode node = FindNodeAtScreen(Input.mousePosition, 34f);
            Vector2 target = node?.Pos ?? MouseWorldPosition();
            int[] ids = _selectedSquads.Where(squad => !squad.IsDead).Select(squad => squad.Id).ToArray();
            if (ids.Length == 0) return;
            _sim.Enqueue(BattleCommand.Move(ids, target, node?.Id ?? 0));
            if (node?.Building != null && node.Building.Owner != HumanFaction)
                Toast($"部队前往 {node.Key}");
            else
                Toast($"已命令 {ids.Length} 支部队移动");
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
                Toast(cast ? $"已施放 {_pendingSpell.CnName}" : "法术施放失败");
                _pendingSpell = null;
                return;
            }

            MapNode node = FindNodeAtScreen(mouse, 30f);
            if (node?.Building != null)
            {
                _selected = node;
                Toast(node.Building.Owner == HumanFaction
                    ? $"选中 {node.Key}，等级 {node.Building.Level}"
                    : $"{node.Key} 属于 {node.Building.Owner}");
            }
            else if (node?.Kind == NodeKind.BuildSpot && node.Building == null)
            {
                _selected = node;
                Toast(_sim.State.IsLocControlledBy(_sim.Human, node)
                    ? $"空地 {node.Key}：可以建造"
                    : $"空地 {node.Key}：不在己方控制区");
            }
            _selectedSquads.Clear();
            _selectedSquad = null;
        }

        private Squad FindHumanSquadAtScreen(Vector2 screen)
        {
            Squad best = null;
            float bestDistance = 24f;
            foreach (Squad squad in _sim.Human.Squads.Where(value => !value.IsDead))
            {
                foreach (CombatUnit unit in squad.Units.Where(value => !value.IsDead))
                {
                    Vector3 unitScreen = _cam.WorldToScreenPoint(DisplayPosition(unit.Pos));
                    float distance = Vector2.Distance(screen, unitScreen);
                    if (distance >= bestDistance) continue;
                    bestDistance = distance;
                    best = squad;
                }
            }
            return best;
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
            _selectedSquads.Clear();
            _selectedSquad = null;
            _selected = null;
        }

        private void SelectSquadsInBox()
        {
            Rect rect = ScreenRect(_selectionStart, _selectionEnd);
            _selectedSquads.Clear();
            foreach (Squad squad in _sim.Human.Squads.Where(value => !value.IsDead))
            {
                Vector3 screen = _cam.WorldToScreenPoint(DisplayPosition(squad.Pos));
                if (rect.Contains(screen)) _selectedSquads.Add(squad);
            }
            _selectedSquad = _selectedSquads.FirstOrDefault();
        }

        private void UpdateSelectionBox()
        {
            if (_selectionBox == null) return;
            Vector2 a = ScreenToDisplayWorld(_selectionStart);
            Vector2 b = ScreenToDisplayWorld(_selectionEnd);
            Vector2 c = new Vector2(a.x, b.y);
            Vector2 d = new Vector2(b.x, a.y);
            _selectionBox.positionCount = 4;
            _selectionBox.SetPositions(new[]
            {
                new Vector3(a.x, a.y, 0f),
                new Vector3(c.x, c.y, 0f),
                new Vector3(b.x, b.y, 0f),
                new Vector3(d.x, d.y, 0f),
            });
            _selectionBox.enabled = Vector2.Distance(_selectionStart, _selectionEnd) >= 3f;
        }

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

        private Vector2 ScreenToDisplayWorld(Vector2 screen)
        {
            Vector3 world = _cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -_cam.transform.position.z));
            return new Vector2(world.x, world.y);
        }

        private static Rect ScreenRect(Vector2 a, Vector2 b)
        {
            return new Rect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y),
                Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
        }

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

        private void Toast(string msg)
        {
            _toast = msg;
            _toastTimer = 2.5f;
            _ui?.ShowToast(msg);
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
                    Debug.Log($"[Takeover] 播放原版音乐 {resolved}");
                    GameModule.Audio.Play(TEngine.AudioType.Music, resolved, bLoop: true, bAsync: true);
                }
            }
            catch (System.Exception)
            {
                // Audio is optional in headless validation.
            }
        }

        public BattleSimulation Simulation => _sim;
        public MapNode SelectedNode => _selected;
        public Squad SelectedSquad => _selectedSquad;
        public IReadOnlyCollection<Squad> SelectedSquads => _selectedSquads;
        public bool Paused => _paused;
        public float Speed => _speed;
        public bool LevelSelectOpen => _levelSelectOpen;

        public void TogglePause() => _paused = !_paused;
        public void SetSpeed(float speed) => _speed = Mathf.Clamp(speed, 1f, 8f);

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
            _ui?.ShowToast(ok ? $"已招募 {unit.CnName}" : reason ?? "招募失败");
        }

        public void Upgrade(MapNode node)
        {
            if (_sim == null) return;
            bool ok = _sim.ExecuteNow(BattleCommand.Upgrade(node.Id));
            _ui?.ShowToast(ok ? "升级成功" : "升级失败");
        }

        public void Build(MapNode node, int buildingId)
        {
            if (_sim == null) return;
            _sim.State.CanPlace(_sim.Human, node, buildingId, out string reason);
            bool ok = _sim.ExecuteNow(BattleCommand.Build(buildingId, node.Id));
            _ui?.ShowToast(ok ? "建造完成" : reason ?? "建造失败");
        }

        public bool CanBuild(MapNode node, int buildingId)
        {
            return _sim != null && _sim.State.CanPlace(_sim.Human, node, buildingId, out _);
        }

        public void BeginSpellTarget(GameConfig.takeover.Spell spell)
        {
            _pendingSpell = spell;
            _ui?.ShowToast($"选择 {spell.CnName} 的目标位置");
        }
    }
}






