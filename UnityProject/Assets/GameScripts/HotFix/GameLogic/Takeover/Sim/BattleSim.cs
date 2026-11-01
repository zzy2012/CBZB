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
        public float MoraleBonus = 10f;
        public float PoisonDamage;
        public float PoisonSeconds;
        public float PoisonTickTimer;
        public float FrozenSeconds;
        public bool ChargeConsumed;
        public float DashRemaining;
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

    public sealed class ActiveSpellEffect
    {
        public GameConfig.takeover.Spell Def;
        public PlayerState Owner;
        public Faction Faction;
        public Vector2 Center;
        public float Remaining;
        public float TickTimer;
    }

    /// <summary>
    /// 战斗模拟：地图图、经济、造兵、寻路、战斗、占领、AI、胜负。
    /// 纯数据逻辑，不依赖场景，由 View 层驱动 Tick。
    /// </summary>
    public sealed class BattleSim
    {
        public const float ManaCap = 500f;
        public const float CaptureSeconds = 2f;
        public const int AiBuyThreshold = OriginalRules.AiBuyThreshold;
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
        public readonly List<ActiveSpellEffect> ActiveSpells = new List<ActiveSpellEffect>();

        public float Time;
        public int Stars => Time < 330f ? 3 : Time < 540f ? 2 : 1;
        public bool IsOver;
        public bool HumanVictory;
        public int LevelAssetId;

        private readonly System.Random _rng;
        private readonly List<BattleEvent> _events = new List<BattleEvent>();
        /// <summary>每座建筑开局的驻军人数（原版建筑有守军，不是空城）。</summary>
        public static int GarrisonPerBuilding = 1;
        private int _nextSquadId = 1;
        private float _aiTimer;

        public BattleSim(int levelAssetId, Faction humanFaction, Faction enemyFaction,
            int[] humanMods = null, int seed = 20120920)
        {
            _rng = new System.Random(seed);
            if (humanMods != null)
            {
                for (int i = 0; i < 45 && i < humanMods.Length; i++) Human.Mods[i] = humanMods[i];
            }
            LevelAssetId = levelAssetId;
            Human.Faction = humanFaction;
            Enemy.Faction = enemyFaction;
            foreach (var modifier in OriginalRules.EnemyModifiers(levelAssetId))
            {
                if (modifier.Id >= 0 && modifier.Id < Enemy.Mods.Length)
                    Enemy.Mods[modifier.Id] += modifier.Value;
            }
            Human.UltimateMax = 200f * Human.ModMul(12);
            Enemy.UltimateMax = 200f * Enemy.ModMul(12);
            BuildMap(levelAssetId);
            var lv = Cfg.TbLevel.DataList.FirstOrDefault(l => l.AssetId == levelAssetId);
            Human.Gold = (lv?.StartGold ?? OriginalRules.PlayerStartGold) + Human.Mods[8];
            Human.Mana = lv?.StartMana ?? OriginalRules.PlayerStartMana;
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
                    Pos = new Vector2(cfg.X, cfg.Y * OriginalRules.WorldYScale),
                };
                if (node.Kind >= NodeKind.Castle && node.Kind <= NodeKind.MageTower)
                {
                    var def = Cfg.BuildingByKind((int)node.Kind);
                    if (def != null)
                    {
                        var owner = node.Owner == Human.Faction ? Human
                            : node.Owner == Enemy.Faction ? Enemy : null;
                        int towerBonus = def.Id == 4 && owner != null ? owner.Mods[38] : 0;
                        node.Building = new BuildingState
                        {
                            Node = node,
                            Owner = node.Owner,
                            Kind = def.Id,
                            BaseHp = Mathf.RoundToInt(def.Hp * (node.Owner == Human.Faction ? Human.ModMul(11) : node.Owner == Enemy.Faction ? Enemy.ModMul(11) : 1f)),
                            GoldPerSec = def.Gold + towerBonus,
                            ManaPerSec = def.Mana + towerBonus,
                            Supply = def.Supply + towerBonus,
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
                        MoraleBonus = 10f * (fac == Human.Faction ? Human : Enemy).ModMul(15),
                        HpMul = UnitHpMul(fac == Human.Faction ? Human : Enemy, def),
                        SpeedMul = UnitSpeedMul(fac == Human.Faction ? Human : Enemy, def),
                    });
                    Squads[Squads.Count - 1].Hp = def.Hp * def.UnitsCount * Squads[Squads.Count - 1].HpMul;
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
            TickSpells(dt);
            TickSquads(dt);
            TickAI(dt);
            RefreshOwnership();
            CheckEnd();
        }

        public IReadOnlyList<BattleEvent> DrainEvents()
        {
            var copy = _events.ToArray();
            _events.Clear();
            return copy;
        }

        public bool ExecuteCommand(PlayerState player, BattleCommand command)
        {
            if (player == null || IsOver) return false;
            var node = FindNode(command.NodeId);
            switch (command.Type)
            {
                case BattleCommandType.Move:
                case BattleCommandType.Attack:
                {
                    var squad = player.Squads.FirstOrDefault(s => s.Id == command.SquadId && !s.IsDead);
                    if (squad == null || node == null) return false;
                    OrderMove(squad, node);
                    return true;
                }
                case BattleCommandType.Retreat:
                {
                    var squad = player.Squads.FirstOrDefault(s => s.Id == command.SquadId && !s.IsDead);
                    if (squad == null) return false;
                    OrderRetreat(squad);
                    return true;
                }
                case BattleCommandType.BuyUnit:
                {
                    var def = Cfg.TbUnit.DataList.FirstOrDefault(u => u.Id == command.ConfigId);
                    return def != null && BuyUnit(player, def, node, out _) != null;
                }
                case BattleCommandType.Upgrade:
                    return TryUpgrade(player, node);
                case BattleCommandType.Build:
                    return TryBuild(player, node, command.ConfigId, out _);
                case BattleCommandType.CastSpell:
                {
                    var spell = Cfg.TbSpell.DataList.FirstOrDefault(s => s.Id == command.ConfigId);
                    return spell != null && TryCastSpell(player, spell, node, out _);
                }
                default:
                    return false;
            }
        }

        public MapNode FindNode(int id) => Nodes.FirstOrDefault(n => n.Id == id);

        private void TickEconomy(PlayerState p, float dt)
        {
            p.Gold += p.NetGoldIncome() * dt;
            p.Mana = Mathf.Min(ManaCap, p.Mana + p.ManaIncome * dt);
            p.Ultimate = Mathf.Min(p.UltimateMax, p.Ultimate + p.UltimateRate * dt);
        }

        private void TickSpells(float dt)
        {
            for (int i = ActiveSpells.Count - 1; i >= 0; i--)
            {
                var effect = ActiveSpells[i];
                effect.Remaining -= dt;
                effect.TickTimer -= dt;
                if (effect.TickTimer <= 0f)
                {
                    effect.TickTimer = 1f;
                    ApplySpellPulse(effect);
                }
                if (effect.Remaining <= 0f) ActiveSpells.RemoveAt(i);
            }

            foreach (var squad in Squads.Where(s => !s.IsDead))
            {
                if (!OriginalRules.TryAbility(squad.Def.Abilities, "DivinePresence", out int healing)
                    || healing <= 0)
                    continue;
                foreach (var ally in Squads)
                {
                    if (ally.IsDead || ally.Faction != squad.Faction || ally == squad) continue;
                    if ((ally.Pos - squad.Pos).sqrMagnitude <= 75f * 75f)
                        ally.Hp = Mathf.Min(ally.HpPerSoldier * ally.MaxSoldiers, ally.Hp + healing * dt);
                }
            }
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
            if (p.Gold < UnitCost(p, def)) { reason = "金币不足"; return false; }
            if (p.FreeSupply < def.Upkeep) { reason = "补给不足"; return false; }
            if (at == null || at.Building == null || at.Building.Owner != p.Faction) { reason = "不是己方建筑"; return false; }
            if (!AllowedAt(p, at.Building, def)) { reason = "该建筑不能生产此兵种"; return false; }
            return true;
        }

        /// <summary>建筑等级决定可用科技阶：0级→1阶，1级→2阶，2级→3阶。</summary>
        public static bool AllowedAt(BuildingState b, GameConfig.takeover.Unit def)
            => AllowedAt(null, b, def);

        public static bool AllowedAt(PlayerState p, BuildingState b, GameConfig.takeover.Unit def)
        {
            var bd = Cfg.BuildingById(b.Kind);
            if (bd == null) return false;
            if (!bd.AllowedTypes.Contains(def.UnitType)) return false;
            int troopUpgrade = p == null ? 0 : p.Mods[TierModifier(def.UnitType)];
            return def.Tier <= Mathf.Min(3, b.Level + 1 + troopUpgrade);
        }

        private static int TierModifier(int unitType)
        {
            switch ((UnitKind)unitType)
            {
                case UnitKind.Infantry: return 0;
                case UnitKind.Cavalry: return 1;
                case UnitKind.Archers: return 2;
                case UnitKind.Siege: return 3;
                case UnitKind.Magic: return 4;
                default: return 0;
            }
        }

        public Squad BuyUnit(PlayerState p, GameConfig.takeover.Unit def, MapNode at, out string reason)
        {
            reason = null;
            if (!CanBuy(p, def, at, out reason)) return null;
            p.Gold -= UnitCost(p, def);
            var squad = new Squad
            {
                Id = _nextSquadId++,
                Faction = p.Faction,
                Def = def,
                Soldiers = def.UnitsCount,
                MaxSoldiers = def.UnitsCount,
                HpMul = UnitHpMul(p, def),
                SpeedMul = UnitSpeedMul(p, def),
                MoraleBonus = 10f * p.ModMul(15),
                Pos = at.Pos,
                AtNode = at,
            };
            squad.Hp = squad.HpPerSoldier * squad.MaxSoldiers;
            Squads.Add(squad);
            p.Squads.Add(squad);
            return squad;
        }

        private static int UnitCost(PlayerState p, GameConfig.takeover.Unit def)
        {
            float multiplier = OriginalRules.HasAbility(def.Abilities, "Undead") ? p.ModMul(28) : 1f;
            return Mathf.RoundToInt(def.Cost * multiplier);
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

        private static float UnitSpeedMul(PlayerState p, GameConfig.takeover.Unit def) =>
            p.ModMul(def.UnitType == (int)UnitKind.Siege ? 27 : 19);

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
            if (p.Gold < Mathf.RoundToInt(def.BuildCost * p.ModMul(31))) { reason = "金币不足"; return false; }
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

        public bool TryCastSpell(PlayerState p, GameConfig.takeover.Spell spell, MapNode target, out string reason)
        {
            reason = null;
            if (p == null || spell == null || target == null) { reason = "无效法术或目标"; return false; }
            if ((int)p.Faction != spell.Faction) { reason = "阵营不匹配"; return false; }

            bool ultimate = spell.SpellType == 2;
            if (ultimate)
            {
                if (p.Ultimate + 0.0001f < p.UltimateMax) { reason = "终极技能尚未充满"; return false; }
                p.Ultimate = 0f;
            }
            else
            {
                int cost = Mathf.RoundToInt(spell.Cost * p.ModMul(35));
                if (p.Mana < cost) { reason = "法力不足"; return false; }
                p.Mana -= cost;
            }

            var effect = new ActiveSpellEffect
            {
                Def = spell,
                Owner = p,
                Faction = p.Faction,
                Center = target.Pos,
                Remaining = Mathf.Max(0.05f, spell.Time * (spell.SpellType == 1 ? p.ModMul(6) : 1f)),
                TickTimer = 0f,
            };
            ActiveSpells.Add(effect);
            ApplySpellPulse(effect);
            _events.Add(new BattleEvent
            {
                Kind = ultimate ? "UltimateCast" : "SpellCast",
                ActorId = (int)p.Faction,
                TargetId = target.Id,
                Value = spell.Id,
                Position = target.Pos,
            });
            return true;
        }

        private void ApplySpellPulse(ActiveSpellEffect effect)
        {
            var name = effect.Def.Name ?? string.Empty;
            float radius = Mathf.Max(0f, effect.Def.Radius);
            float radiusSq = radius * radius;
            int power = effect.Def.Power;

            foreach (var squad in Squads.Where(s => !s.IsDead))
            {
                if ((squad.Pos - effect.Center).sqrMagnitude > radiusSq) continue;
                bool ally = squad.Faction == effect.Faction;

                if (name.Contains("LORD'S JUDGEMENT") || name.Contains("STONEWORMS")
                    || name.Contains("CATACLYSM") || name.Contains("TERROR")
                    || name.Contains("CALL TO THE GRAVE"))
                {
                    if (!ally)
                    {
                        if (name.Contains("TERROR"))
                            squad.MoraleBonus = Mathf.Max(1f, squad.MoraleBonus - power);
                        else
                        {
                            float damage = power;
                            if (name.Contains("LORD'S JUDGEMENT") && effect.Owner != null)
                                damage *= effect.Owner.ModMul(10);
                            ApplyDamage(squad, damage);
                        }
                    }
                }
                else if (name.Contains("ICE BLAST"))
                {
                    if (!ally) squad.FrozenSeconds = Mathf.Max(squad.FrozenSeconds, 5f);
                }
                else if (name.Contains("CONSECRATION") || name.Contains("LIBERATION")
                         || name.Contains("SIGN OF THE ADVENT"))
                {
                    if (ally) squad.Hp = Mathf.Min(squad.HpPerSoldier * squad.MaxSoldiers, squad.Hp + power);
                }
                else if (name.Contains("HEROISM") || name.Contains("BLOODLUST")
                         || name.Contains("DESECRATION"))
                {
                    if (ally) squad.MoraleBonus = Mathf.Min(100f, squad.MoraleBonus + power);
                }
                else if (name.Contains("GRAND TRANSFORMATION"))
                {
                    if (!ally) ApplyDamage(squad, Mathf.Max(1, power));
                }
            }

            if (name.Contains("DISENTOMB"))
            {
                var node = NearestNode(effect.Center);
                for (int i = 0; i < Mathf.Max(1, power); i++) SummonSpellUnit(effect.Faction, node);
            }
            else if (name.Contains("RAISE DEAD"))
            {
                SummonSpellUnit(effect.Faction, NearestNode(effect.Center));
            }
        }

        private void SummonSpellUnit(Faction faction, MapNode node)
        {
            var player = faction == Human.Faction ? Human : faction == Enemy.Faction ? Enemy : null;
            if (player == null || node == null) return;
            var def = Cfg.TbUnit.DataList.FirstOrDefault(u => u.Faction == (int)faction
                                                              && u.UnitType == (int)UnitKind.Infantry
                                                              && u.Tier == 1);
            if (def == null) return;
            var squad = new Squad
            {
                Id = _nextSquadId++,
                Faction = faction,
                Def = def,
                Soldiers = def.UnitsCount,
                MaxSoldiers = def.UnitsCount,
                HpMul = UnitHpMul(player, def),
                SpeedMul = UnitSpeedMul(player, def),
                MoraleBonus = 10f * player.ModMul(15),
                Pos = node.Pos,
                AtNode = node,
            };
            squad.Hp = squad.HpPerSoldier * squad.MaxSoldiers;
            Squads.Add(squad);
            player.Squads.Add(squad);
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
            if (OriginalRules.TryAbility(s.Def.Abilities, "HorrificDash", out int dash))
                s.DashRemaining = Mathf.Max(0f, dash);
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
                    var owner = s.Faction == Human.Faction ? Human : s.Faction == Enemy.Faction ? Enemy : null;
                    if (owner != null && owner.Mods[26] > 0)
                        owner.Ultimate = Mathf.Min(owner.UltimateMax,
                            owner.Ultimate + owner.UltimateMax * owner.Mods[26] / 100f);
                    Squads.RemoveAt(i);
                    Human.Squads.Remove(s);
                    Enemy.Squads.Remove(s);
                    continue;
                }

                if (s.FrozenSeconds > 0f)
                {
                    s.FrozenSeconds = Mathf.Max(0f, s.FrozenSeconds - dt);
                    s.AttackTimer = Mathf.Max(s.AttackTimer, 0f);
                    continue;
                }

                if (s.PoisonSeconds > 0f)
                {
                    s.PoisonSeconds = Mathf.Max(0f, s.PoisonSeconds - dt);
                    s.PoisonTickTimer -= dt;
                    if (s.PoisonTickTimer <= 0f)
                    {
                        s.PoisonTickTimer = 3f;
                        ApplyDamage(s, s.PoisonDamage);
                        if (s.IsDead) continue;
                    }
                }

                // 1) 交战优先：射程内发现敌军就停下开打（远程因此能站在射程外输出）
                var foe = FindEngageTarget(s);
                if (foe != null)
                {
                    AttackSquad(s, foe, dt);
                }
                else
                {
                    s.ChargeConsumed = false;
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
                var regeneratingOwner = s.Faction == Human.Faction ? Human : s.Faction == Enemy.Faction ? Enemy : null;
                if (regeneratingOwner != null && regeneratingOwner.Mods[40] > 0
                    && s.Def.Name.IndexOf("Giant", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    float maxHp = s.HpPerSoldier * s.MaxSoldiers;
                    s.Hp = Mathf.Min(maxHp, s.Hp + maxHp * regeneratingOwner.Mods[40] / 100f * dt);
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
            float step = s.Speed * dt + s.DashRemaining;
            s.DashRemaining = 0f;
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

            bool frozenDamage = target.FrozenSeconds > 0f;
            float dmg = frozenDamage ? MaxRollDamage(s) * MoraleMul(s) : RollDamage(s) * MoraleMul(s);
            if (s.Def.UnitType == (int)UnitKind.Archers || s.Def.UnitType == (int)UnitKind.Siege)
                dmg *= s.Faction == Human.Faction ? Human.ModMul(13) : Enemy.ModMul(13);

            float distance = (target.Pos - s.Pos).magnitude;
            // 远程被贴脸才吃近战惩罚
            if (s.Def.Range > 0 && distance < 40f)
                dmg *= (100 - s.Def.MeleePenalty) / 100f;

            if (OriginalRules.TryAbility(s.Def.Abilities, "Charge", out int charge) && !s.ChargeConsumed)
            {
                dmg += Mathf.Floor(dmg * charge / 10f);
                s.ChargeConsumed = true;
            }

            if (target.Def.UnitType == (int)UnitKind.Cavalry
                && OriginalRules.TryAbility(s.Def.Abilities, "LongSpear", out int spear))
                dmg *= 1f + spear / 100f;

            if (distance > 40f && OriginalRules.TryAbility(target.Def.Abilities, "ShieldWall", out int shield))
                dmg *= shield / 100f;

            var attackingPlayer = s.Faction == Human.Faction ? Human : s.Faction == Enemy.Faction ? Enemy : null;
            if (OriginalRules.HasAbility(s.Def.Abilities, "Mechanism")
                && attackingPlayer != null
                && _rng.NextDouble() < attackingPlayer.Mods[32] / 100f)
                dmg *= 2f;

            ApplyDamage(target, dmg);

            if (OriginalRules.TryAbility(s.Def.Abilities, "FreezeArrow", out int freezeArrow)
                || OriginalRules.TryAbility(s.Def.Abilities, "FreezeBlast", out freezeArrow))
            {
                float freezeChance = freezeArrow / 100f;
                if (target.Def.UnitType == (int)UnitKind.Infantry && attackingPlayer != null)
                    freezeChance += attackingPlayer.Mods[33] / 100f;
                if (_rng.NextDouble() < freezeChance)
                {
                    float modifier = attackingPlayer?.Mods[30] ?? 0;
                    target.FrozenSeconds = Mathf.Max(target.FrozenSeconds, 3f + modifier);
                }
            }

            if (OriginalRules.TryAbility(s.Def.Abilities, "Poisonous", out int poison)
                && !OriginalRules.HasAbility(target.Def.Abilities, "PoisonImmunity"))
            {
                target.PoisonDamage = Mathf.Max(target.PoisonDamage, poison);
                target.PoisonSeconds = 3f;
                target.PoisonTickTimer = Mathf.Min(target.PoisonTickTimer <= 0f ? 3f : target.PoisonTickTimer, 3f);
            }

            if (OriginalRules.TryAbility(s.Def.Abilities, "Intimidiation", out int intimidation))
                target.MoraleBonus = Mathf.Max(1f, target.MoraleBonus - intimidation);

            if (OriginalRules.TryAbility(s.Def.Abilities, "SplashDamage", out int splash))
            {
                foreach (var other in Squads)
                {
                    if (other == target || other.IsDead || other.Faction == s.Faction) continue;
                    if ((other.Pos - target.Pos).sqrMagnitude <= splash * splash)
                        ApplyDamage(other, dmg);
                }
            }

            _events.Add(new BattleEvent
            {
                Kind = target.IsDead ? "SquadKilled" : "SquadDamaged",
                ActorId = s.Id,
                TargetId = target.Id,
                Value = dmg,
                Position = target.Pos,
            });
        }

        private void AttackBuilding(Squad s, MapNode node, float dt)
        {
            if ((node.Pos - s.Pos).magnitude > Mathf.Max(60f, s.Def.Range)) return;

            s.AttackTimer -= dt;
            if (s.AttackTimer > 0f) return;
            s.AttackTimer = Mathf.Max(0.2f, s.Def.AttackSpeed * 0.1f);

            float dmg = RollDamage(s) * MoraleMul(s);
            dmg *= s.Faction == Human.Faction ? Human.ModMul(18) : Enemy.ModMul(18);
            if (OriginalRules.TryAbility(s.Def.Abilities, "SiegeWeapon", out int siege))
                dmg *= 1f + siege / 100f;
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
            return roll * alive * SpellDamageMultiplier(s);
        }

        private float MaxRollDamage(Squad s) =>
            s.Def.DmgMax * s.AliveSoldiers * SpellDamageMultiplier(s);

        private float SpellDamageMultiplier(Squad squad)
        {
            float multiplier = 1f;
            foreach (var effect in ActiveSpells)
            {
                if (effect.Faction != squad.Faction) continue;
                if ((squad.Pos - effect.Center).sqrMagnitude > effect.Def.Radius * effect.Def.Radius) continue;
                var name = effect.Def.Name ?? string.Empty;
                if (name.Contains("BLOODLUST") || name.Contains("FROST ENCHANTMENT")
                    || name.Contains("LIBERATION") || name.Contains("DESECRATION"))
                    multiplier *= 1f + effect.Def.Power / 100f;
            }
            return multiplier;
        }

        /// <summary>士气伤害倍率。原版 PRP_MORALE_BONUS 基准值是 10（=1.0x），单位表里的 morale=50 是士气池，不是倍率。</summary>
        private static float MoraleMul(Squad s) => Mathf.Clamp(s.MoraleBonus, 1f, 100f) / 10f;

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
            float hpMultiplier = by == Human.Faction ? Human.ModMul(11)
                : by == Enemy.Faction ? Enemy.ModMul(11) : 1f;
            node.Building.BaseHp = Mathf.RoundToInt((def?.Hp ?? 500) * hpMultiplier);
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
            var capturer = by == Human.Faction ? Human : by == Enemy.Faction ? Enemy : null;
            if (capturer != null && capturer.Mods[22] > 0)
                capturer.Gold += capturer.Mods[22];
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

            // 4) 法术：终极技能充满立即释放，普通法术按固定概率使用
            var spells = Cfg.TbSpell.DataList.Where(s => s.Faction == (int)Enemy.Faction).ToList();
            var spellTarget = FindThreatenedBuilding(260f)
                              ?? Human.OwnedNodes.OrderBy(n => (n.Pos - Enemy.OwnedNodes.FirstOrDefault()?.Pos ?? Vector2.zero).sqrMagnitude)
                                  .FirstOrDefault();
            var ultimate = spells.FirstOrDefault(s => s.SpellType == 2);
            if (ultimate != null && Enemy.Ultimate + 0.0001f >= Enemy.UltimateMax && spellTarget != null)
            {
                TryCastSpell(Enemy, ultimate, spellTarget, out _);
            }
            else if (spellTarget != null && _rng.NextDouble() < 0.12d)
            {
                var normalSpells = spells.Where(s => s.SpellType != 2 && Enemy.Mana >= s.Cost).ToList();
                if (normalSpells.Count > 0)
                    TryCastSpell(Enemy, normalSpells[_rng.Next(normalSpells.Count)], spellTarget, out _);
            }

            // 5) 战术：残血撤退 + 增援被围攻的建筑
            DoAITactics();

            // 6) 进攻：老家被打就先防守，不外出
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




















