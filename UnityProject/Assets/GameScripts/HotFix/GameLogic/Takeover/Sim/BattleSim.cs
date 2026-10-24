using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GameLogic.Takeover
{
    /// <summary>阵营，数值与原版一致：0 西利亚 1 卡若米森 2 艾斯得雷 3 帝国 4 强盗。</summary>
    public enum Faction
    {
        Westaria = 0,
        Khaganate = 1,
        Cult = 2,
        Empire = 3,
        Bandits = 4,
    }

    /// <summary>兵种。</summary>
    public enum UnitKind
    {
        Infantry = 0,
        Archers = 1,
        Cavalry = 2,
        Magic = 3,
        Siege = 4,
    }

    /// <summary>节点类型。</summary>
    public enum NodeKind
    {
        Empty = 0,
        BuildSpot = 1,
        Chest = 2,
        Ogre = 3,
        Castle = 4,
        Town = 5,
        Camp = 6,
        MageTower = 7,
    }

    /// <summary>地图节点。</summary>
    public sealed class MapNode
    {
        public int Id;
        public string Key;
        public NodeKind Kind;
        public int Loc;           // 区段编号：建造点按区段归属，而不是道路相邻
        public Faction Owner;
        public Vector2 Pos;
        public BuildingState Building;
        public readonly List<MapNode> Neighbours = new List<MapNode>();

        public bool HasBuilding => Building != null;
        public bool IsOwnedBy(Faction f) => Building != null && Building.Owner == f;
    }

    /// <summary>建筑状态。HP 曲线与原版一致：baseHp × (1 + 0.5 × 等级)。</summary>
    public sealed class BuildingState
    {
        public MapNode Node;
        public Faction Owner;
        public int Kind;          // 对应 building.id
        public int Level;         // 0..2
        public int BaseHp;
        public int Hp;
        public int GoldPerSec;
        public int ManaPerSec;
        public int Supply;
        public int UltimatePerSec;
        public float CaptureProgress;

        public int MaxHp => Mathf.RoundToInt(BaseHp * (1f + 0.5f * Level));
        public bool IsCaptured => Hp <= 0;
    }

    /// <summary>小队。</summary>
    public sealed class Squad
    {
        public int Id;
        public Faction Faction;
        public GameConfig.takeover.Unit Def;
        public int Soldiers;
        public int MaxSoldiers;
        public float Hp;          // 当前总血量 = 单兵血量 × 存活人数
        public Vector2 Pos;
        public MapNode AtNode;
        public MapNode TargetNode;
                public MapNode HomeNode;   // 驻军的家（原建筑节点）
        public List<MapNode> Path;
        public int PathIndex;
        public float AttackTimer;
        public float IdleTimer;
        /// <summary>驻军：开局就在建筑里的守军，不占补给、不计入收入惩罚、不主动出击。</summary>
        public bool IsGarrison;
        /// <summary>AI 专用：残血撤退中，撤退途中不再主动进攻。</summary>
        public bool Retreating;
        /// <summary>教令带来的生命/移速倍率（步兵 +HP、骑兵 +HP、全军 +移速）。</summary>
        public float HpMul = 1f;
        public float SpeedMul = 1f;
        public bool IsDead => Soldiers <= 0 || Hp <= 0f;

        public float HpPerSoldier => Def.Hp * HpMul;
        public int AliveSoldiers => Mathf.Clamp(Mathf.CeilToInt(Hp / Mathf.Max(1f, HpPerSoldier)), 0, MaxSoldiers);
        public float Speed => Mathf.Max(8f, Def.Speed * SpeedMul);
    }

    /// <summary>玩家（含 AI）经济状态。</summary>
    public sealed class PlayerState
    {
        public Faction Faction;
        public float Gold;
        public float Mana;
        public float Ultimate;
        public float UltimateMax = 200f;

        /// <summary>45 个修正器（对应原版 §_-NE§）：值是百分比，最终乘数 = (100+v)/100。</summary>
        public readonly int[] Mods = new int[45];
        public float ModMul(int id) => (100 + Mods[id]) / 100f;

        public List<MapNode> OwnedNodes = new List<MapNode>();
        public List<Squad> Squads = new List<Squad>();

        public int SupplyCap => OwnedNodes.Where(n => n.Building != null).Sum(n => n.Building.Supply);
        public int SupplyUsed => Squads.Where(s => !s.IsDead && !s.IsGarrison).Sum(s => s.Def.Upkeep);
        public int FreeSupply => SupplyCap - SupplyUsed;

        public int BaseGoldIncome => OwnedNodes.Where(n => n.Building != null).Sum(n => n.Building.GoldPerSec);
        public int ManaIncome => OwnedNodes.Where(n => n.Building != null).Sum(n => n.Building.ManaPerSec);
        public int UltimateRate => OwnedNodes.Where(n => n.Building != null).Sum(n => n.Building.UltimatePerSec);

        /// <summary>原版收入惩罚：round(基础收入 × (100 − (25 + 5×min(队数,10))) / 100)，最低 1。</summary>
        public int NetGoldIncome()
        {
            if (BaseGoldIncome <= 0) return 0;
            int squads = Squads.Count(s => !s.IsDead && !s.IsGarrison);
            int modifier = 25 + 5 * Mathf.Min(squads, 10);
            return Mathf.Max(1, Mathf.RoundToInt(BaseGoldIncome * (100 - modifier) / 100f));
        }
    }

    /// <summary>
    /// 战斗模拟：地图图、经济、造兵、寻路、战斗、占领、AI、胜负。
    /// 纯数据逻辑，不依赖场景，由 View 层驱动 Tick。
    /// </summary>
    public sealed class BattleSim
    {
        public const float ManaCap = 500f;
        public const float CaptureSeconds = 2f;
        public const int AiBuyThreshold = 180;
        public const float AiThinkInterval = 1.2f;
        /// <summary>AI 首次出兵前的宽限期（秒）。原版 AI 前期偏被动，给玩家铺农场的时间。</summary>
        public const float AiFirstAttackDelay = 180f;

        /// <summary>空闲小队是否自动寻找下一个敌方建筑（原版需要玩家指挥，这里给 AI 与自动演示用）。</summary>
        public bool AutoAdvance = true;

        public readonly List<MapNode> Nodes = new List<MapNode>();
        public readonly Dictionary<string, MapNode> NodeByKey = new Dictionary<string, MapNode>();
        public readonly List<Squad> Squads = new List<Squad>();
        public readonly PlayerState Human = new PlayerState();
        public readonly PlayerState Enemy = new PlayerState();

        public float Time;
        public int Stars => Time < 330f ? 3 : Time < 540f ? 2 : 1;
        public bool IsOver;
        public bool HumanVictory;
        public int LevelAssetId;

        private readonly System.Random _rng = new System.Random(20120920);
        /// <summary>每座建筑开局的驻军人数（原版建筑有守军，不是空城）。</summary>
        public static int GarrisonPerBuilding = 1;
        private int _nextSquadId = 1;
        private float _aiTimer;

        public BattleSim(int levelAssetId, Faction humanFaction, Faction enemyFaction, int[] humanMods = null)
        {
            if (humanMods != null)
            {
                for (int i = 0; i < 45 && i < humanMods.Length; i++) Human.Mods[i] = humanMods[i];
            }
            LevelAssetId = levelAssetId;
            Human.Faction = humanFaction;
            Enemy.Faction = enemyFaction;
            BuildMap(levelAssetId);
            var lv = Cfg.TbLevel.DataList.FirstOrDefault(l => l.AssetId == levelAssetId);
            Human.Gold = (lv?.StartGold ?? 250) + Human.Mods[8];   // 教令：开局 +N 金
            Human.Mana = lv?.StartMana ?? 100;
            Enemy.Gold = 0;
            Enemy.Mana = 0;
            SpawnGarrisons();
        }

        // ------------------------------------------------------------------ 地图
        private void BuildMap(int levelAssetId)
        {
            var nodeCfg = Cfg.TbMapNode.DataList.Where(n => n.LevelId == levelAssetId);
            foreach (var cfg in nodeCfg)
            {
                var node = new MapNode
                {
                    Id = cfg.Id,
                    Key = cfg.NodeKey,
                    Kind = (NodeKind)cfg.Kind,
                    Loc = cfg.Loc,
                    Owner = (Faction)cfg.Owner,
                    Pos = new Vector2(cfg.X, cfg.Y),
                };
                if (node.Kind >= NodeKind.Castle && node.Kind <= NodeKind.MageTower)
                {
                    var def = Cfg.BuildingByKind((int)node.Kind);
                    if (def != null)
                    {
                        node.Building = new BuildingState
                        {
                            Node = node,
                            Owner = node.Owner,
                            Kind = def.Id,
                            BaseHp = Mathf.RoundToInt(def.Hp * (node.Owner == Human.Faction ? Human.ModMul(11) : node.Owner == Enemy.Faction ? Enemy.ModMul(11) : 1f)),
                            GoldPerSec = def.Gold,
                            ManaPerSec = def.Mana,
                            Supply = def.Supply,
                            UltimatePerSec = def.Ultimate,
                        };
                        node.Building.Hp = node.Building.MaxHp;
                    }
                }
                Nodes.Add(node);
                NodeByKey[node.Key] = node;
            }

            foreach (var e in Cfg.TbMapEdge.DataList.Where(e => e.LevelId == levelAssetId))
            {
                if (NodeByKey.TryGetValue(e.NodeA, out var a) && NodeByKey.TryGetValue(e.NodeB, out var b))
                {
                    a.Neighbours.Add(b);
                    b.Neighbours.Add(a);
                }
            }

            foreach (var n in Nodes)
            {
                if (n.Building != null)
                {
                    if (n.Building.Owner == Human.Faction) Human.OwnedNodes.Add(n);
                    else if (n.Building.Owner == Enemy.Faction) Enemy.OwnedNodes.Add(n);
                }
            }
        }

        /// <summary>给每座建筑放一队驻军（原版建筑不是空城，进攻方要先打掉守军）。</summary>
        private void SpawnGarrisons()
        {
            if (GarrisonPerBuilding <= 0) return;
            foreach (var n in Nodes)
            {
                if (n.Building == null || n.Building.Owner == Faction.Bandits) continue;
                var fac = n.Building.Owner;
                var def = Cfg.TbUnit.DataList.FirstOrDefault(u => u.Faction == (int)fac
                                                                  && u.UnitType == (int)UnitKind.Infantry
                                                                  && u.Tier == 1);
                if (def == null) continue;
                for (int i = 0; i < GarrisonPerBuilding; i++)
                {
                    Squads.Add(new Squad
                    {
                        Id = _nextSquadId++,
                        Faction = fac,
                        Def = def,
                        Soldiers = def.UnitsCount,
                        MaxSoldiers = def.UnitsCount,
                        Hp = def.Hp * def.UnitsCount,
                        Pos = n.Pos,
                        AtNode = n,
                        HomeNode = n,
                        IsGarrison = true,
                    });
                }
            }
        }
        // ------------------------------------------------------------------ 主循环
        public void Tick(float dt)
        {
            if (IsOver) return;
            Time += dt;
            TickEconomy(Human, dt);
            TickEconomy(Enemy, dt);
            TickSquads(dt);
            TickAI(dt);
            RefreshOwnership();
            CheckEnd();
        }

        private void TickEconomy(PlayerState p, float dt)
        {
            p.Gold += p.NetGoldIncome() * dt;
            p.Mana = Mathf.Min(ManaCap, p.Mana + p.ManaIncome * dt);
            p.Ultimate = Mathf.Min(p.UltimateMax, p.Ultimate + p.UltimateRate * dt);
        }

        private void RefreshOwnership()
        {
            Human.OwnedNodes.Clear();
            Enemy.OwnedNodes.Clear();
            foreach (var n in Nodes)
            {
                if (n.Building == null) continue;
                if (n.Building.Owner == Human.Faction) Human.OwnedNodes.Add(n);
                else if (n.Building.Owner == Enemy.Faction) Enemy.OwnedNodes.Add(n);
            }
        }

        private void CheckEnd()
        {
            bool humanHas = Nodes.Any(n => IsKeyBuilding(n.Building) && n.Building.Owner == Human.Faction);
            bool enemyHas = Nodes.Any(n => IsKeyBuilding(n.Building) && n.Building.Owner == Enemy.Faction);
            if (!enemyHas)
            {
                IsOver = true;
                HumanVictory = true;
            }
            else if (!humanHas)
            {
                IsOver = true;
                HumanVictory = false;
            }
        }

        // ------------------------------------------------------------------ 造兵
        public bool CanBuy(PlayerState p, GameConfig.takeover.Unit def, MapNode at, out string reason)
        {
            reason = null;
            if (p.Gold < def.Cost) { reason = "金币不足"; return false; }
            if (p.FreeSupply < def.Upkeep) { reason = "补给不足"; return false; }
            if (at == null || at.Building == null || at.Building.Owner != p.Faction) { reason = "不是己方建筑"; return false; }
            if (!AllowedAt(at.Building, def)) { reason = "该建筑不能生产此兵种"; return false; }
            return true;
        }

        /// <summary>建筑等级决定可用科技阶：0级→1阶，1级→2阶，2级→3阶。</summary>
        public static bool AllowedAt(BuildingState b, GameConfig.takeover.Unit def)
        {
            var bd = Cfg.BuildingById(b.Kind);
            if (bd == null) return false;
            if (!bd.AllowedTypes.Contains(def.UnitType)) return false;
            return def.Tier <= b.Level + 1;
        }

        public Squad BuyUnit(PlayerState p, GameConfig.takeover.Unit def, MapNode at, out string reason)
        {
            reason = null;
            if (!CanBuy(p, def, at, out reason)) return null;
            p.Gold -= Mathf.RoundToInt(def.Cost * (def.Abilities.Contains("Undead") ? p.ModMul(28) : 1f));
            var squad = new Squad
            {
                Id = _nextSquadId++,
                Faction = p.Faction,
                Def = def,
                Soldiers = def.UnitsCount,
                MaxSoldiers = def.UnitsCount,
                HpMul = UnitHpMul(p, def),
                SpeedMul = p.ModMul(19),
                Pos = at.Pos,
                AtNode = at,
            };
            squad.Hp = squad.HpPerSoldier * squad.MaxSoldiers;
            Squads.Add(squad);
            p.Squads.Add(squad);
            return squad;
        }

        /// <summary>教令：解锁某教令的某一档，把修正值叠加到该玩家的修正器上。</summary>
        public static void ApplyEdict(PlayerState p, int edictId, int tier)
        {
            var row = Cfg.TbEdict.DataList.FirstOrDefault(e => e.EdictId == edictId && e.Tier == tier);
            if (row == null) return;
            p.Mods[row.ModifierId] += row.Value;
        }

        private static float UnitHpMul(PlayerState p, GameConfig.takeover.Unit def)
        {
            if (def.UnitType == (int)UnitKind.Infantry) return p.ModMul(16);
            if (def.UnitType == (int)UnitKind.Cavalry) return p.ModMul(39);
            return 1f;
        }

        public bool TryUpgrade(PlayerState p, MapNode at)
        {
            if (at?.Building == null || at.Building.Owner != p.Faction) return false;
            var b = at.Building;
            if (b.Level >= 2) return false;
            int cost = b.Level == 0 ? 350 : 500;
            if (p.Gold < cost) return false;
            p.Gold -= cost;
            int before = b.MaxHp;
            b.Level++;
            b.Hp += Mathf.Max(0, b.MaxHp - before);
            return true;
        }

        // ------------------------------------------------------------------ 附属建筑（农场/矿场/英雄碑）
        public static readonly int[] SupplyBuildingIds = { 5, 6, 7 };

        /// <summary>主建筑（要塞/城镇/营地/魔法塔）。胜负只算主建筑，农场矿场不算。</summary>
        public static bool IsKeyBuilding(BuildingState b) => b != null && b.Kind >= 1 && b.Kind <= 4;

        /// <summary>空位是否可建造：必须是建造点、且紧邻己方建筑（原版“地图空地牌子”规则）。</summary>
        public bool CanPlace(PlayerState p, MapNode spot, int buildingId, out string reason)
        {
            reason = null;
            if (spot == null || spot.Kind != NodeKind.BuildSpot || spot.Building != null) { reason = "不是可建造空位"; return false; }
            var def = Cfg.BuildingById(buildingId);
            if (def == null || def.BuildCost <= 0) { reason = "不可建造"; return false; }
            if (p.Gold < def.BuildCost) { reason = "金币不足"; return false; }
            if (!IsLocControlled(p, spot.Loc)) { reason = "不在己方控制范围"; return false; }
            return true;
        }

        public bool TryBuild(PlayerState p, MapNode spot, int buildingId, out string reason)
        {
            reason = null;
            if (!CanPlace(p, spot, buildingId, out reason)) return false;
            var def = Cfg.BuildingById(buildingId);
            p.Gold -= Mathf.RoundToInt(def.BuildCost * p.ModMul(31));
            var b = new BuildingState
            {
                Node = spot,
                Owner = p.Faction,
                Kind = def.Id,
                BaseHp = def.Hp,
                GoldPerSec = def.Gold,
                ManaPerSec = def.Mana,
                Supply = def.Supply,
                UltimatePerSec = def.Ultimate,
            };
            b.Hp = b.MaxHp;
            spot.Building = b;
            if (!p.OwnedNodes.Contains(spot)) p.OwnedNodes.Add(spot);
            return true;
        }

        private MapNode FindBuildSpot(PlayerState p)
        {
            return Nodes.FirstOrDefault(n => n.Kind == NodeKind.BuildSpot && n.Building == null && IsLocControlled(p, n.Loc));
        }

        /// <summary>供表现层判断某空位是否属于自己可建造范围。</summary>
        public bool IsLocControlledBy(PlayerState p, MapNode spot) => spot != null && IsLocControlled(p, spot.Loc);

        /// <summary>区段控制权：该区段内有己方建筑即可建造。</summary>
        private bool IsLocControlled(PlayerState p, int loc)
        {
            return Nodes.Any(n => n.Loc == loc && n.Building != null && n.Building.Owner == p.Faction);
        }

        // ------------------------------------------------------------------ 移动与战斗
        public void OrderMove(Squad s, MapNode target)
        {
            if (s == null || s.IsDead || target == null) return;
            s.TargetNode = target;
            s.Path = FindPath(s.AtNode ?? NearestNode(s.Pos), target);
            if (s.Path == null)
            {
                // 起点落在孤立节点（例如建造点）时，退回到最近的连通节点重新寻路
                var alt = Nodes.Where(n => n.Neighbours.Count > 0)
                               .OrderBy(n => (n.Pos - s.Pos).sqrMagnitude)
                               .FirstOrDefault();
                if (alt != null) s.Path = FindPath(alt, target);
            }
            s.PathIndex = 0;
            s.IdleTimer = 0f;
        }

        /// <summary>最近的敌方建筑。</summary>
        private MapNode FindEnemyBuilding(Squad s)
        {
            MapNode best = null;
            float bestD = float.MaxValue;
            foreach (var n in Nodes)
            {
                if (!IsKeyBuilding(n.Building) || n.Building.Owner == s.Faction) continue;
                float d = (n.Pos - s.Pos).sqrMagnitude;
                if (d < bestD) { bestD = d; best = n; }
            }
            return best;
        }

        public MapNode NearestNode(Vector2 p)
        {
            MapNode best = null;
            float bestD = float.MaxValue;
            foreach (var n in Nodes)
            {
                float d = (n.Pos - p).sqrMagnitude;
                if (d < bestD) { bestD = d; best = n; }
            }
            return best;
        }

        public List<MapNode> FindPath(MapNode from, MapNode to)
        {
            if (from == null || to == null) return null;
            if (from == to) return new List<MapNode> { to };
            var prev = new Dictionary<MapNode, MapNode>();
            var q = new Queue<MapNode>();
            q.Enqueue(from);
            prev[from] = null;
            while (q.Count > 0)
            {
                var cur = q.Dequeue();
                if (cur == to) break;
                foreach (var nb in cur.Neighbours)
                {
                    if (prev.ContainsKey(nb)) continue;
                    prev[nb] = cur;
                    q.Enqueue(nb);
                }
            }
            if (!prev.ContainsKey(to)) return null;
            var path = new List<MapNode>();
            for (var n = to; n != null; n = prev[n]) path.Add(n);
            path.Reverse();
            return path;
        }

        private void TickSquads(float dt)
        {
            for (int i = Squads.Count - 1; i >= 0; i--)
            {
                var s = Squads[i];
                if (s.IsDead)
                {
                    Squads.RemoveAt(i);
                    Human.Squads.Remove(s);
                    Enemy.Squads.Remove(s);
                    continue;
                }

                // 1) 交战优先：射程内发现敌军就停下开打（远程因此能站在射程外输出）
                var foe = FindEngageTarget(s);
                if (foe != null)
                {
                    AttackSquad(s, foe, dt);
                }
                else
                {
                    MoveAlongPath(s, dt);
                    var node = s.AtNode;
                    if (node != null && node.Building != null && node.Building.Owner != s.Faction)
                    {
                        if (node.Building.Hp > 0)
                        {
                            AttackBuilding(s, node, dt);
                        }
                        else if ((node.Pos - s.Pos).magnitude <= 60f)
                        {
                            node.Building.CaptureProgress += dt;
                            if (node.Building.CaptureProgress >= CaptureSeconds) Capture(node, s.Faction);
                        }
                    }
                }

                // 驻军：附近有敌人就出城迎战，敌人走远就回防
                if (s.IsGarrison) UpdateGarrison(s);

                // 回血（仅在己方建筑内）；回血会把减员补回来
                if (s.Path == null && s.AtNode?.Building != null && s.AtNode.Building.Owner == s.Faction)
                {
                    s.Hp = Mathf.Min(s.HpPerSoldier * s.MaxSoldiers, s.Hp + s.HpPerSoldier * 0.15f * dt);
                }

                // 自动推进：闲置 3 秒去找下一个敌方主建筑（人类只剩 1 队时不自作主张）
                int aliveSameFaction = Squads.Count(o => !o.IsDead && o.Faction == s.Faction);
                bool mayAdvance = AutoAdvance && !s.IsGarrison
                                  && (s.Faction != Human.Faction ? Time >= AiFirstAttackDelay : aliveSameFaction > 1);
                if (mayAdvance && s.Path == null && s.AtNode != null)
                {
                    if (FindEngageTarget(s) != null)
                    {
                        s.IdleTimer = 0f;
                    }
                    else
                    {
                        s.IdleTimer += dt;
                        if (s.IdleTimer >= 3f)
                        {
                            s.IdleTimer = 0f;
                            var next = FindEnemyBuilding(s);
                            if (next != null) OrderMove(s, next);
                        }
                    }
                }
            }
        }

        private void MoveAlongPath(Squad s, float dt)
        {
            if (s.Path == null || s.PathIndex >= s.Path.Count) return;
            var target = s.Path[s.PathIndex];
            var delta = target.Pos - s.Pos;
            float dist = delta.magnitude;
            float step = s.Speed * dt;
            if (dist <= step)
            {
                s.Pos = target.Pos;
                s.AtNode = target;
                s.PathIndex++;
                if (s.PathIndex >= s.Path.Count) { s.Path = null; s.PathIndex = 0; }
            }
            else
            {
                s.Pos += delta / dist * step;
                s.AtNode = null;
            }
        }

        /// <summary>射程内最近的敌军。近战有效距离 40，远程用单位自己的射程。</summary>
        private Squad FindEngageTarget(Squad s)
        {
            float reach = Mathf.Max(40f, s.Def.Range);
            Squad best = null;
            float bestD = reach;
            foreach (var o in Squads)
            {
                if (o == s || o.IsDead || o.Faction == s.Faction) continue;
                float d = (o.Pos - s.Pos).magnitude;
                if (d <= bestD) { bestD = d; best = o; }
            }
            return best;
        }

        private void AttackSquad(Squad s, Squad target, float dt)
        {
            s.AttackTimer -= dt;
            if (s.AttackTimer > 0f) return;
            s.AttackTimer = Mathf.Max(0.2f, s.Def.AttackSpeed * 0.1f);

            float dmg = RollDamage(s) * MoraleMul(s);
            if (s.Def.UnitType == (int)UnitKind.Archers || s.Def.UnitType == (int)UnitKind.Siege)
                dmg *= s.Faction == Human.Faction ? Human.ModMul(13) : Enemy.ModMul(13);

            // 远程被贴脸才吃近战惩罚
            if (s.Def.Range > 0 && (target.Pos - s.Pos).magnitude < 40f)
                dmg *= (100 - s.Def.MeleePenalty) / 100f;

            ApplyDamage(target, dmg);
        }

        private void AttackBuilding(Squad s, MapNode node, float dt)
        {
            if ((node.Pos - s.Pos).magnitude > Mathf.Max(60f, s.Def.Range)) return;

            s.AttackTimer -= dt;
            if (s.AttackTimer > 0f) return;
            s.AttackTimer = Mathf.Max(0.2f, s.Def.AttackSpeed * 0.1f);

            float dmg = RollDamage(s) * MoraleMul(s);
            dmg *= s.Faction == Human.Faction ? Human.ModMul(18) : Enemy.ModMul(18);
            if (s.Def.Abilities.Contains("SiegeWeapon")) dmg *= 1.5f;
            if (s.Def.Abilities.Contains("SplashDamage")) dmg *= 1.1f;
            node.Building.Hp = Mathf.Max(0, node.Building.Hp - Mathf.RoundToInt(dmg));
            node.Building.CaptureProgress = 0f;
        }

        /// <summary>驻军行为：260 像素内有敌人就出城，否则回防。</summary>
        private void UpdateGarrison(Squad s)
        {
            if (s.HomeNode == null) return;
            Squad nearest = null;
            float bestD = 140f;   // 只有贴脸才出城，避免弃城送死
            foreach (var o in Squads)
            {
                if (o.IsDead || o.Faction == s.Faction) continue;
                float d = (o.Pos - s.Pos).magnitude;
                if (d < bestD) { bestD = d; nearest = o; }
            }
            if (s.Path != null) return;
            if (nearest != null)
            {
                var node = nearest.AtNode ?? NearestNode(nearest.Pos);
                if (node != s.AtNode) OrderMove(s, node);
            }
            else if (s.HomeNode != s.AtNode)
            {
                OrderMove(s, s.HomeNode);
            }
        }

        private float RollDamage(Squad s)
        {
            int alive = s.AliveSoldiers;
            float roll = Mathf.Lerp(s.Def.DmgMin, s.Def.DmgMax, (float)_rng.NextDouble());
            return roll * alive;
        }

        /// <summary>士气伤害倍率。原版 PRP_MORALE_BONUS 基准值是 10（=1.0x），单位表里的 morale=50 是士气池，不是倍率。</summary>
        private static float MoraleMul(Squad s) => 1f;

        private void ApplyDamage(Squad target, float dmg)
        {
            target.Hp -= dmg;
            if (target.Hp <= 0f)
            {
                target.Hp = 0f;
                target.Soldiers = 0;
            }
            else
            {
                target.Soldiers = target.AliveSoldiers;
            }
        }

        private void Capture(MapNode node, Faction by)
        {
            if (node.Building == null) return;
            int kind = node.Building.Kind;
            var def = Cfg.BuildingById(kind);
            node.Building.Owner = by;
            node.Building.CaptureProgress = 0f;
            node.Building.Level = 0;
            node.Building.BaseHp = def?.Hp ?? 500;
            node.Building.Hp = Mathf.RoundToInt(node.Building.MaxHp * 0.3f);
            node.Building.GoldPerSec = def?.Gold ?? 0;
            node.Building.ManaPerSec = def?.Mana ?? 0;
            node.Building.Supply = def?.Supply ?? 0;
            node.Building.UltimatePerSec = def?.Ultimate ?? 0;

            if (by == Human.Faction)
            {
                if (!Human.OwnedNodes.Contains(node)) Human.OwnedNodes.Add(node);
                Enemy.OwnedNodes.Remove(node);
            }
            else if (by == Enemy.Faction)
            {
                if (!Enemy.OwnedNodes.Contains(node)) Enemy.OwnedNodes.Add(node);
                Human.OwnedNodes.Remove(node);
            }
        }

        // ------------------------------------------------------------------ AI
        private void TickAI(float dt)
        {
            _aiTimer -= dt;
            if (_aiTimer > 0f) return;
            _aiTimer = AiThinkInterval;

            // 1) 买兵：优先补到被围攻的建筑，其次随机
            if (Enemy.Gold >= AiBuyThreshold && Enemy.FreeSupply > 0 && Enemy.OwnedNodes.Count > 0)
            {
                var buyNode = FindThreatenedBuilding(220f) ?? Enemy.OwnedNodes[_rng.Next(Enemy.OwnedNodes.Count)];
                if (buyNode?.Building != null)
                {
                    var candidates = Cfg.TbUnit.DataList
                        .Where(u => u.Faction == (int)Enemy.Faction && AllowedAt(buyNode.Building, u))
                        .ToList();
                    if (candidates.Count > 0)
                    {
                        var pick = candidates[_rng.Next(candidates.Count)];
                        BuyUnit(Enemy, pick, buyNode, out _);
                    }
                }
            }

            // 2) 升级：留出买兵余量后再升级，优先升等级低的
            var upgradable = Enemy.OwnedNodes
                .Where(n => n.Building != null && n.Building.Level < 2)
                .OrderBy(n => n.Building.Level)
                .FirstOrDefault();
            if (upgradable != null)
            {
                int upCost = upgradable.Building.Level == 0 ? 350 : 500;
                if (Enemy.Gold >= upCost + 120) TryUpgrade(Enemy, upgradable);
            }

            // 3) 附属建筑：先补补给（农场），钱多再铺矿场
            if (Enemy.FreeSupply <= 0 && Enemy.Gold >= 150)
            {
                var spot = FindBuildSpot(Enemy);
                if (spot != null) TryBuild(Enemy, spot, 5, out _);
            }
            else if (Enemy.Gold >= 420)
            {
                var spot = FindBuildSpot(Enemy);
                if (spot != null) TryBuild(Enemy, spot, 6, out _);
            }

            // 4) 战术：残血撤退 + 增援被围攻的建筑
            DoAITactics();

            // 5) 进攻：老家被打就先防守，不外出
            if (Time < AiFirstAttackDelay) return;
            if (FindThreatenedBuilding(220f) != null) return;

            var targets = Human.OwnedNodes;
            if (targets.Count == 0) return;
            var idle = Enemy.Squads.Where(s => !s.IsDead && !s.IsGarrison && !s.Retreating && s.Path == null).ToList();
            int send = idle.Count - 1;   // 留 1 队守家
            foreach (var s in idle)
            {
                if (send <= 0) break;
                var nearest = targets.OrderBy(t => (t.Pos - s.Pos).sqrMagnitude).First();
                if (s.AtNode != nearest)
                {
                    OrderMove(s, nearest);
                    send--;
                }
            }
        }

        /// <summary>AI 战术：血线低于 32% 撤退回城，血量回到 75% 再出动；己方建筑被围攻就派最近的空闲小队增援。</summary>
        private void DoAITactics()
        {
            foreach (var s in Enemy.Squads)
            {
                if (s.IsDead || s.IsGarrison) continue;
                float max = s.HpPerSoldier * s.MaxSoldiers;
                float ratio = max > 0f ? s.Hp / max : 0f;
                if (ratio < 0.32f) s.Retreating = true;
                else if (ratio > 0.75f) s.Retreating = false;

                if (!s.Retreating) continue;
                var home = Enemy.OwnedNodes.Where(n => n.Building != null)
                                           .OrderBy(n => (n.Pos - s.Pos).sqrMagnitude)
                                           .FirstOrDefault();
                if (home != null && s.AtNode != home) OrderMove(s, home);
            }

            foreach (var node in Enemy.OwnedNodes)
            {
                if (node.Building == null || node.Building.Hp <= 0) continue;
                bool threatened = Squads.Any(o => !o.IsDead && o.Faction != Enemy.Faction
                                                  && (o.Pos - node.Pos).magnitude < 220f);
                if (!threatened) continue;

                int defenders = Squads.Count(o => !o.IsDead && o.Faction == Enemy.Faction && !o.IsGarrison
                                                  && (o.Pos - node.Pos).magnitude < 140f);
                if (defenders >= 2) continue;

                var helper = Enemy.Squads
                    .Where(s => !s.IsDead && !s.IsGarrison && !s.Retreating && s.Path == null)
                    .OrderBy(s => (s.Pos - node.Pos).sqrMagnitude)
                    .FirstOrDefault();
                if (helper != null && helper.AtNode != node) OrderMove(helper, node);
            }
        }

        /// <summary>被敌方小队逼近的己方建筑（按威胁数量），没有则返回 null。</summary>
        private MapNode FindThreatenedBuilding(float radius)
        {
            MapNode best = null;
            int bestCount = 0;
            foreach (var n in Enemy.OwnedNodes)
            {
                if (n.Building == null || n.Building.Hp <= 0) continue;
                int count = Squads.Count(o => !o.IsDead && o.Faction != Enemy.Faction
                                              && (o.Pos - n.Pos).magnitude < radius);
                if (count > bestCount)
                {
                    bestCount = count;
                    best = n;
                }
            }
            return best;
        }

        // ------------------------------------------------------------------ 玩家指令
        /// <summary>撤退：回到最近的己方建筑。</summary>
        public void OrderRetreat(Squad s)
        {
            if (s == null || s.IsDead) return;
            var home = Nodes.Where(n => n.Building != null && n.Building.Owner == s.Faction)
                            .OrderBy(n => (n.Pos - s.Pos).sqrMagnitude)
                            .FirstOrDefault();
            if (home != null) OrderMove(s, home);
        }
        public void PlayerOrderAllAttack(MapNode target)
        {
            foreach (var s in Human.Squads)
            {
                if (s.IsDead) continue;
                if (s.Path == null && !s.IsGarrison)
                {
                    // “全军进攻”就是全部出击；守家由玩家自己留
                    OrderMove(s, target);
                }
            }
        }
    }
}




















