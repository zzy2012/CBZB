using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GameLogic.Takeover
{
    /// <summary>
    /// 垂直切片的表现层：运行时生成地图（节点/道路/小队）与 IMGUI HUD。
    /// 逻辑全部在 BattleSim 中，View 只负责渲染与输入。
    /// </summary>
    public sealed class TakeoverRunner : MonoBehaviour
    {
        private BattleSim _sim;
        private Camera _cam;
        private Transform _root;
        private Sprite _square;
        private readonly Dictionary<MapNode, SpriteRenderer> _nodeViews = new Dictionary<MapNode, SpriteRenderer>();
        private readonly Dictionary<int, SpriteRenderer> _squadViews = new Dictionary<int, SpriteRenderer>();
        private MapNode _selected;
        private Squad _selectedSquad;
        private bool _dragging;
        private Vector3 _lastMouse;
        private bool _paused;
        private float _speed = 1f;
        private string _toast = "";
        private bool _reported;
        private float _toastTimer;

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
            NewGame();
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
            foreach (Transform c in _root) Destroy(c.gameObject);
            _nodeViews.Clear();
            _squadViews.Clear();
            _selected = null;
            _selectedSquad = null;
            _paused = false;
            _reported = false;

            // 关卡配置决定双方阵营：章节 0/1/2 对应 西利亚/卡若米森/艾斯得雷
            var lv = Cfg.TbLevel.DataList.FirstOrDefault(l => l.AssetId == LevelAssetId);
            if (lv != null)
            {
                HumanFaction = (Faction)lv.Chapter;
                EnemyFaction = (Faction)lv.EnemyFaction;
            }

            _sim = new BattleSim(LevelAssetId, HumanFaction, EnemyFaction);
            BuildNodeViews();
            BuildEdgeViews();
            FocusCamera();

            Debug.Log($"[Takeover] 启动 L{LevelAssetId} 节点={_sim.Nodes.Count} 建筑={_sim.Nodes.Count(n => n.HasBuilding)}");
        }

        private void FocusCamera()
        {
            if (_sim.Nodes.Count == 0) return;
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            foreach (var n in _sim.Nodes)
            {
                min = Vector2.Min(min, n.Pos);
                max = Vector2.Max(max, n.Pos);
            }
            var center = (min + max) * 0.5f;
            _cam.transform.position = new Vector3(center.x, center.y, -10f);
            float halfH = Mathf.Max(1f, (max.y - min.y) * 0.55f);
            float halfW = Mathf.Max(1f, (max.x - min.x) * 0.55f);
            _cam.orthographicSize = Mathf.Max(halfH, halfW / Mathf.Max(0.1f, _cam.aspect));
        }

        private void BuildNodeViews()
        {
            foreach (var n in _sim.Nodes)
            {
                var go = new GameObject($"node_{n.Key}");
                go.transform.SetParent(_root, false);
                go.transform.position = n.Pos;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = _square;
                float size = n.HasBuilding ? 22f : 10f;
                go.transform.localScale = new Vector3(size, size, 1f);
                sr.color = NodeColor(n);
                sr.sortingOrder = n.HasBuilding ? 2 : 1;
                _nodeViews[n] = sr;
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
                    lr.SetPosition(0, n.Pos);
                    lr.SetPosition(1, m.Pos);
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
            if (_sim == null) return;
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
                    _squadViews[s.Id] = sr;
                }
                sr.transform.position = s.Pos;
                float k = Mathf.Lerp(6f, 12f, s.AliveSoldiers / (float)Mathf.Max(1, s.MaxSoldiers));
                sr.transform.localScale = new Vector3(k, k, 1f);
                bool picked = s == _selectedSquad;
                sr.color = picked
                    ? Color.white
                    : (s.Faction == HumanFaction ? new Color(0.95f, 0.95f, 0.55f) : new Color(0.95f, 0.55f, 0.35f));
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
                kv.Value.color = NodeColor(n);
                float baseSize = n.HasBuilding ? 22f : 10f;
                float size = n == _selected ? baseSize * 1.3f : baseSize;
                kv.Value.transform.localScale = new Vector3(size, size, 1f);
            }
        }

        private void HandleInput()
        {
            if (Input.GetKeyDown(KeyCode.Space)) _paused = !_paused;
            if (Input.GetKeyDown(KeyCode.R)) NewGame();

            if (Input.GetKeyDown(KeyCode.H) && _selectedSquad != null)
            {
                _sim.OrderRetreat(_selectedSquad);
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
                    float d = Vector2.Distance(sq.Pos, world);
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
                    float d = Vector2.Distance(n.Pos, world);
                    if (d < bestD) { bestD = d; best = n; }
                }
                if (best != null)
                {
                    if (best.Building != null && best.Building.Owner == HumanFaction)
                    {
                        _selected = best;
                        Toast($"选中 {best.Building.Node.Key}，等级 {best.Building.Level}");
                    }
                    else if (best.Kind == NodeKind.BuildSpot && best.Building == null
                             && _sim.IsLocControlledBy(_sim.Human, best))
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
                float d = Vector2.Distance(n.Pos, world);
                if (d < bestD) { bestD = d; best = n; }
            }
            if (best == null) return;
            if (_selectedSquad != null && !_selectedSquad.IsDead)
            {
                _sim.OrderMove(_selectedSquad, best);
                Toast($"小队 {_selectedSquad.Id} → {best.Key}");
            }
            else
            {
                _sim.PlayerOrderAllAttack(best);
                Toast($"全军进攻 {best.Key}");
            }
        }

        private void UpdateCamera()
        {
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
        }

        /// <summary>按资源 ID 顺序切换关卡（1-8 / 11-18 / 21-28，最大 28）。</summary>
        private void StepLevel(int dir)
        {
            var ids = Cfg.TbLevel.DataList.Select(l => l.AssetId).OrderBy(x => x).ToList();
            if (ids.Count == 0) return;
            int idx = ids.IndexOf(LevelAssetId);
            if (idx < 0) idx = 0;
            idx = Mathf.Clamp(idx + dir, 0, ids.Count - 1);
            if (ids[idx] == LevelAssetId) return;
            LevelAssetId = ids[idx];
            NewGame();
            Toast($"切换到 L{LevelAssetId}");
        }

        private void OnGUI()
        {
            if (_sim == null) return;
            GUI.skin.label.fontSize = 14;
            GUI.skin.button.fontSize = 14;

            var h = _sim.Human;
            GUILayout.BeginArea(new Rect(10, 10, 420, 210), GUI.skin.box);
            GUILayout.Label($"<b>城邦争霸 · 复刻切片</b>  L{_sim.LevelAssetId}   {(_paused ? "已暂停" : $"×{_speed}")}", RichLabel());
            GUILayout.Label($"金币 {Mathf.FloorToInt(h.Gold)}   (+{h.NetGoldIncome()}/s，基础 {h.BaseGoldIncome})", RichLabel());
            GUILayout.Label($"法力 {Mathf.FloorToInt(h.Mana)} / 500   (+{h.ManaIncome}/s)", RichLabel());
            GUILayout.Label($"补给 {h.SupplyUsed} / {h.SupplyCap}   部队 {h.Squads.Count(s => !s.IsDead)} 队", RichLabel());
            GUILayout.Label($"敌方建筑 {_sim.Enemy.OwnedNodes.Count} 座   玩家建筑 {h.OwnedNodes.Count} 座", RichLabel());
            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(_paused ? "继续" : "暂停")) _paused = !_paused;
            if (GUILayout.Button("×1")) _speed = 1f;
            if (GUILayout.Button("×2")) _speed = 2f;
            if (GUILayout.Button("重开")) NewGame();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("上一关")) StepLevel(-1);
            if (GUILayout.Button("下一关")) StepLevel(1);
            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            if (_selectedSquad != null && _selectedSquad.IsDead) _selectedSquad = null;
            if (_selectedSquad != null) DrawSquadPanel();

            if (_selected != null && _selected.Building != null)
            {
                DrawBuildingPanel(_selected);
            }
            else if (_selected != null && _selected.Kind == NodeKind.BuildSpot && _selected.Building == null)
            {
                DrawBuildSpotPanel(_selected);
            }

            if (_toastTimer > 0f)
            {
                GUI.Label(new Rect(Screen.width / 2f - 150, Screen.height - 90, 300, 30), _toast);
            }

            if (_sim.IsOver)
            {
                var rect = new Rect(Screen.width / 2f - 180, Screen.height / 2f - 80, 360, 160);
                GUI.Box(rect, "");
                GUI.Label(new Rect(rect.x + 20, rect.y + 16, 320, 26), _sim.HumanVictory ? "胜利！" : "失败…");
                if (_sim.HumanVictory)
                {
                    int stars = _sim.Stars;
                    GUI.Label(new Rect(rect.x + 20, rect.y + 46, 320, 26), $"用时 {_sim.Time:F0}s   评价 {new string('★', stars)}");
                    GUI.Label(new Rect(rect.x + 20, rect.y + 72, 320, 26), $"累计经验 {Campaign.Xp}   教令点 {Campaign.EdictPoints}");
                }
                if (GUI.Button(new Rect(rect.x + 30, rect.y + 110, 130, 32), "重玩本关")) NewGame();
                if (GUI.Button(new Rect(rect.x + 200, rect.y + 110, 130, 32), "下一关"))
                {
                    LevelAssetId = Campaign.NextLevel(LevelAssetId);
                    NewGame();
                }
            }
        }

        private static GUIStyle RichLabel()
        {
            var s = new GUIStyle(GUI.skin.label) { richText = true };
            return s;
        }


        private void DrawSquadPanel()
        {
            var sq = _selectedSquad;
            GUILayout.BeginArea(new Rect(Screen.width - 330, 10, 320, 170), GUI.skin.box);
            GUILayout.Label($"<b>{sq.Def.CnName}</b>   {sq.AliveSoldiers}/{sq.MaxSoldiers} 人", RichLabel());
            GUILayout.Label($"HP {sq.Hp:F0} / {sq.HpPerSoldier * sq.MaxSoldiers:F0}   攻速 {sq.Def.AttackSpeed * 0.1f:F1}s   射程 {sq.Def.Range}", RichLabel());
            GUILayout.Label(sq.TargetNode != null ? $"目标：{sq.TargetNode.Key}" : "待命", RichLabel());
            if (GUILayout.Button("撤退回城")) _sim.OrderRetreat(sq);
            if (GUILayout.Button("取消选择")) _selectedSquad = null;
            GUILayout.EndArea();
        }

        /// <summary>空地建造面板：农场 +1 补给 / 矿场 +1 金/秒 / 英雄碑 +1 法力/秒。</summary>
        private void DrawBuildSpotPanel(MapNode spot)
        {
            GUILayout.BeginArea(new Rect(Screen.width - 330, 10, 320, 250), GUI.skin.box);
            GUILayout.Label($"<b>空地 {spot.Key}</b>   区段 {spot.Loc}", RichLabel());
            GUILayout.Label("附属建筑只能建在己方控制区内的空地", RichLabel());
            GUILayout.Space(4);
            foreach (var id in BattleSim.SupplyBuildingIds)
            {
                var def = Cfg.BuildingById(id);
                if (def == null) continue;
                string effect = def.Supply > 0 ? "补给 +1" : def.Gold > 0 ? "金币 +1/秒" : "法力 +1/秒";
                bool ok = _sim.CanPlace(_sim.Human, spot, id, out var why);
                GUI.enabled = ok;
                if (GUILayout.Button($"{def.CnName}  {def.BuildCost}金   {effect}"))
                {
                    Toast(_sim.TryBuild(_sim.Human, spot, id, out var fail) ? $"已建造 {def.CnName}" : (fail ?? "建造失败"));
                }
                GUI.enabled = true;
            }
            GUILayout.Space(6);
            GUILayout.Label("左键选小队/建筑 · 右键下令 · H 撤退 · 滚轮缩放 · WASD 平移", RichLabel());
            GUILayout.EndArea();
        }
        private void DrawBuildingPanel(MapNode node)
        {
            var b = node.Building;
            var list = Cfg.TbUnit.DataList
                .Where(u => u.Faction == (int)HumanFaction && BattleSim.AllowedAt(b, u))
                .OrderBy(u => u.UnitType).ThenBy(u => u.Tier).ToList();

            GUILayout.BeginArea(new Rect(Screen.width - 330, 10, 320, 420), GUI.skin.box);
            GUILayout.Label($"<b>{b.Node.Key}</b>  等级 {b.Level}/2  HP {b.Hp}/{b.MaxHp}", RichLabel());
            GUILayout.Label($"金币 +{b.GoldPerSec}/s  法力 +{b.ManaPerSec}/s  补给 +{b.Supply}", RichLabel());
            GUILayout.Space(4);
            for (int i = 0; i < list.Count; i++)
            {
                var u = list[i];
                bool ok = _sim.CanBuy(_sim.Human, u, node, out var why);
                GUI.enabled = ok;
                if (GUILayout.Button($"{u.CnName}  {u.Cost}金  补给{u.Upkeep}  HP{u.Hp}×{u.UnitsCount}  伤害{u.DmgMin}-{u.DmgMax}"))
                {
                    _sim.BuyUnit(_sim.Human, u, node, out var fail);
                    Toast(fail ?? $"已招募 {u.CnName}");
                }
                GUI.enabled = true;
            }
            GUILayout.Space(6);
            bool canUp = b.Level < 2 && _sim.Human.Gold >= (b.Level == 0 ? 350 : 500);
            GUI.enabled = canUp;
            if (GUILayout.Button(b.Level >= 2 ? "已满级" : $"升级（{(b.Level == 0 ? 350 : 500)}金）"))
            {
                Toast(_sim.TryUpgrade(_sim.Human, node) ? "升级成功" : "升级失败");
            }
            GUI.enabled = true;
            GUILayout.Label("左键选小队/建筑 · 右键下令 · H 撤退 · 滚轮缩放 · WASD 平移", RichLabel());
            GUILayout.EndArea();
        }
    }
}






