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
        private BattleSimulation _sim;
        private Camera _cam;
        private Transform _root;
        private Sprite _square;
        private readonly Dictionary<MapNode, SpriteRenderer> _nodeViews = new Dictionary<MapNode, SpriteRenderer>();
        private readonly Dictionary<int, SpriteRenderer> _squadViews = new Dictionary<int, SpriteRenderer>();
        private readonly HashSet<int> _boundBuildingNodes = new HashSet<int>();
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

        public int LevelAssetId = 1;
        public Faction HumanFaction = Faction.Westaria;
        public Faction EnemyFaction = Faction.Empire;

        private void Awake()
        {
            Application.targetFrameRate = 60;
            // 关掉 TEngine 自带的 DEBUGGER 面板，避免遮住玩法 HUD
            try { GameModule.Debugger.ActiveWindow = false; } catch { }
            _square = MakeSprite();
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
        }

        private static Sprite MakeSprite()
        {
            var tex = new Texture2D(8, 8, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var px = new Color32[64];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 8f);
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
            _boundBuildingNodes.Clear();
            _selected = null;
            _selectedSquad = null;
            _paused = false;
            _reported = false;
            _pendingSpell = null;

            // 关卡配置决定双方阵营：章节 0/1/2 对应 西利亚/卡若米森/艾斯得雷
            var lv = Cfg.TbLevel.DataList.FirstOrDefault(l => l.AssetId == LevelAssetId);
            if (lv != null)
            {
                HumanFaction = (Faction)lv.Chapter;
                EnemyFaction = (Faction)lv.EnemyFaction;
            }

            _sim = new BattleSimulation(LevelAssetId, HumanFaction, EnemyFaction);
            PlayMusic("takeover_sfx_2910_musicGameplayClass_musicGameplayClass");
            BuildNodeViews();
            BuildEdgeViews();
            BuildMapBackground();
            FocusCamera();
            StartCoroutine(BindLevelVisuals());
            _ui?.ShowBattle();

            Debug.Log($"[Takeover] 启动 L{LevelAssetId} 节点={_sim.Nodes.Count} 建筑={_sim.Nodes.Count(n => n.HasBuilding)}");
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
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            foreach (var n in _sim.Nodes)
            {
                var display = DisplayPosition(n.Pos);
                min = Vector2.Min(min, display);
                max = Vector2.Max(max, display);
            }
            var center = (min + max) * 0.5f;
            _cam.transform.position = new Vector3(center.x, center.y, -10f);
            float halfH = Mathf.Max(1f, (max.y - min.y) * 0.55f);
            float halfW = Mathf.Max(1f, (max.x - min.x) * 0.55f);
            _cam.orthographicSize = Mathf.Max(halfH, halfW / Mathf.Max(0.1f, _cam.aspect));
        }

        private static Vector2 DisplayPosition(Vector2 logical)
        {
            return new Vector2(logical.x,
                OriginalRules.NativeStageHeight - logical.y / OriginalRules.WorldYScale);
        }

        private void BuildNodeViews()
        {
            foreach (var n in _sim.Nodes)
            {
                var go = new GameObject($"node_{n.Key}");
                go.transform.SetParent(_root, false);
                go.transform.position = DisplayPosition(n.Pos);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = _square;
                float size = n.HasBuilding ? 22f : 10f;
                go.transform.localScale = new Vector3(size, size, 1f);
                sr.color = NodeColor(n);
                sr.sortingOrder = n.HasBuilding ? 2 : 1;
                _nodeViews[n] = sr;
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
                if (pair.Key.Building == null || !_boundBuildingNodes.Add(pair.Key.Id)) continue;
                TakeoverVisualCatalog.Bind(pair.Value, pair.Key);
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

        private void BuildEdgeViews()
        {
            var done = new HashSet<(int, int)>();
            foreach (var n in _sim.Nodes)
            {
                foreach (var m in n.Neighbours)
                {
                    int a = Mathf.Min(n.Id, m.Id), b = Mathf.Max(n.Id, m.Id);
                    if (!done.Add((a, b))) continue;
                    var go = new GameObject("edge");
                    go.transform.SetParent(_root, false);
                    var lr = go.AddComponent<LineRenderer>();
                    lr.useWorldSpace = true;
                    lr.positionCount = 2;
                    lr.SetPosition(0, DisplayPosition(n.Pos));
                    lr.SetPosition(1, DisplayPosition(m.Pos));
                    lr.startWidth = 2.5f;
                    lr.endWidth = 2.5f;
                    lr.material = new Material(Shader.Find("Sprites/Default"));
                    lr.startColor = new Color(0.35f, 0.35f, 0.30f);
                    lr.endColor = new Color(0.35f, 0.35f, 0.30f);
                    lr.sortingOrder = 0;
                }
            }
        }

        private void Update()
        {
            if (_sim == null || _levelSelectOpen) return;
            UpdateCamera();
            if (!_paused && !_sim.IsOver)
            {
                float dt = Mathf.Min(0.1f, Time.deltaTime) * _speed;
                _sim.Tick(dt);
            }

            if (_sim.IsOver && _sim.HumanVictory && !_reported)
            {
                _reported = true;
                var lvCfg = Cfg.TbLevel.DataList.FirstOrDefault(l => l.AssetId == LevelAssetId);
                Campaign.Complete(LevelAssetId, _sim.Time, lvCfg?.Xp ?? 0);
                _ui?.ShowResult(_sim);
            }

            SyncSquads();
            SyncNodes();

            if (_toastTimer > 0f) _toastTimer -= Time.deltaTime;

            HandleInput();
        }

        private void SyncSquads()
        {
            foreach (var s in _sim.Squads)
            {
                if (!_squadViews.TryGetValue(s.Id, out var sr))
                {
                    var go = new GameObject($"squad_{s.Id}");
                    go.transform.SetParent(_root, false);
                    sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = _square;
                    sr.sortingOrder = 5;
                    var animator = go.AddComponent<TakeoverUnitAnimator>();
                    animator.Initialize(sr, s.Def, s.Id);
                    _squadViews[s.Id] = sr;
                }
                sr.transform.position = DisplayPosition(s.Pos);
                float k = Mathf.Lerp(6f, 12f, s.AliveSoldiers / (float)Mathf.Max(1, s.MaxSoldiers));
                sr.transform.localScale = new Vector3(k, k, 1f);
                bool picked = s == _selectedSquad;
                sr.color = picked ? new Color(1f, 0.94f, 0.58f) : Color.white;
                if (picked) sr.transform.localScale *= 1.35f;
            }
            foreach (var id in _squadViews.Keys.ToList())
            {
                if (_sim.Squads.All(s => s.Id != id))
                {
                    Destroy(_squadViews[id].gameObject);
                    _squadViews.Remove(id);
                }
            }
        }

        private void SyncNodes()
        {
            foreach (var kv in _nodeViews)
            {
                var n = kv.Key;
                kv.Value.color = n.Building != null
                    ? (n == _selected ? new Color(1f, 0.94f, 0.58f) : Color.white)
                    : NodeColor(n);
                if (n.Building != null && _boundBuildingNodes.Add(n.Id))
                    TakeoverVisualCatalog.Bind(kv.Value, n);
                float baseSize = n.HasBuilding ? 22f : 10f;
                float size = n == _selected ? baseSize * 1.3f : baseSize;
                kv.Value.transform.localScale = new Vector3(size, size, 1f);
            }
        }

        private void HandleInput()
        {
            if (Input.GetKeyDown(KeyCode.Space)) _paused = !_paused;
            if (Input.GetKeyDown(KeyCode.R)) NewGame();
            if (Input.GetKeyDown(KeyCode.Escape)) ShowLevelSelect();
            if (UnityEngine.EventSystems.EventSystem.current != null
                && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                return;

            if (Input.GetKeyDown(KeyCode.H) && _selectedSquad != null)
            {
                _sim.Enqueue(BattleCommand.Retreat(_selectedSquad.Id));
                Toast($"小队 {_selectedSquad.Id} 撤退");
            }

            if (Input.GetMouseButtonDown(0))
            {
                var world = _cam.ScreenToWorldPoint(Input.mousePosition);

                // 先选小队
                Squad pick = null;
                float sd = 16f;
                foreach (var sq in _sim.Squads)
                {
                    if (sq.IsDead || sq.Faction != HumanFaction) continue;
                    float d = Vector2.Distance(DisplayPosition(sq.Pos), world);
                    if (d < sd) { sd = d; pick = sq; }
                }
                if (pick != null)
                {
                    _selectedSquad = pick;
                    _selected = null;
                    Toast($"选中 {pick.Def.CnName}  {pick.AliveSoldiers}/{pick.MaxSoldiers} 人");
                    return;
                }

                MapNode best = null;
                float bestD = 18f;
                foreach (var n in _sim.Nodes)
                {
                    float d = Vector2.Distance(DisplayPosition(n.Pos), world);
                    if (d < bestD) { bestD = d; best = n; }
                }
                if (best != null)
                {
                    if (_pendingSpell != null)
                    {
                        bool cast = _sim.ExecuteNow(BattleCommand.CastSpell(_pendingSpell.Id, best.Id));
                        Toast(cast ? $"已施放 {_pendingSpell.CnName}" : "法术施放失败");
                        _pendingSpell = null;
                        return;
                    }
                    if (best.Building != null && best.Building.Owner == HumanFaction)
                    {
                        _selected = best;
                        Toast($"选中 {best.Building.Node.Key}，等级 {best.Building.Level}");
                    }
                    else if (best.Kind == NodeKind.BuildSpot && best.Building == null
                             && _sim.State.IsLocControlledBy(_sim.Human, best))
                    {
                        _selected = best;
                        Toast($"空地 {best.Key}：可以建造附属建筑");
                    }
                    else
                    {
                        Toast("右键下令进攻该节点");
                    }
                }
            }
        }

        private void IssueOrderAtMouse()
        {
            var world = _cam.ScreenToWorldPoint(Input.mousePosition);
            MapNode best = null;
            float bestD = 22f;
            foreach (var n in _sim.Nodes)
            {
                float d = Vector2.Distance(DisplayPosition(n.Pos), world);
                if (d < bestD) { bestD = d; best = n; }
            }
            if (best == null) return;
            if (_selectedSquad != null && !_selectedSquad.IsDead)
            {
                _sim.Enqueue(BattleCommand.Move(_selectedSquad.Id, best.Id));
                Toast($"小队 {_selectedSquad.Id} → {best.Key}");
            }
            else
            {
                foreach (var squad in _sim.Human.Squads.Where(s => !s.IsDead && !s.IsGarrison && s.Path == null))
                    _sim.Enqueue(BattleCommand.Attack(squad.Id, best.Id));
                Toast($"全军进攻 {best.Key}");
            }
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

            if (Input.GetMouseButtonDown(1)) IssueOrderAtMouse();
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
        public bool Paused => _paused;
        public float Speed => _speed;
        public bool LevelSelectOpen => _levelSelectOpen;

        public void TogglePause() => _paused = !_paused;
        public void SetSpeed(float speed) => _speed = Mathf.Clamp(speed, 1f, 2f);

        public bool CanBuy(GameConfig.takeover.Unit unit, MapNode node)
        {
            return _sim != null && _sim.State.CanBuy(_sim.Human, unit, node, out _);
        }

        public List<GameConfig.takeover.Unit> UnitsAt(MapNode node)
        {
            if (_sim == null || node?.Building == null) return new List<GameConfig.takeover.Unit>();
            return Cfg.TbUnit.DataList
                .Where(u => u.Faction == (int)HumanFaction && BattleSim.AllowedAt(_sim.Human, node.Building, u))
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
            bool ok = _sim.ExecuteNow(BattleCommand.Build(buildingId, node.Id));
            _ui?.ShowToast(ok ? "建造完成" : "建造失败");
        }

        public void RetreatSelected()
        {
            if (_sim == null || _selectedSquad == null) return;
            _sim.Enqueue(BattleCommand.Retreat(_selectedSquad.Id));
            _ui?.ShowToast("小队开始撤退");
        }

        public void BeginSpellTarget(GameConfig.takeover.Spell spell)
        {
            _pendingSpell = spell;
            _ui?.ShowToast($"选择 {spell.CnName} 的目标位置");
        }
    }
}






