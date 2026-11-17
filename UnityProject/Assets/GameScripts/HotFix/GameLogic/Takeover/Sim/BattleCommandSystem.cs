using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GameLogic.Takeover
{
    /// <summary>分发玩家命令并调用对应的战斗模拟系统。</summary>
    internal sealed class BattleCommandSystem : BattleSimSystemBase
    {
        /// <summary>创建并关联当前战斗模拟的命令系统。</summary>
        internal BattleCommandSystem(TakeoverScene sim) : base(sim) { }

        /// <summary>以指定参与方身份执行战斗命令并返回执行结果。</summary>
        internal bool ExecuteCommand(PlayerStateEntity player, BattleCommand command)
        {
            if (player == null || Sim.IsOver) return false;
            MapNodeEntity node = command.NodeId > 0 ? Sim.FindNode(command.NodeId) : null;
            switch (command.Type)
            {
                case BattleCommandType.Move:
                {
                    int[] ids = command.SquadIds != null && command.SquadIds.Length > 0
                        ? command.SquadIds
                        : new[] { command.SquadId };
                    List<SquadEntity> ordered = ids.Select(id =>
                            player.Squads.FirstOrDefault(value => value.Id == id && !value.IsDead))
                        .Where(value => value != null)
                        .ToList();
                    if (ordered.Count == 0) return false;

                    var groups = new List<List<SquadEntity>>();
                    foreach (SquadEntity squad in ordered)
                    {
                        List<SquadEntity> group = groups.FirstOrDefault(value =>
                            Vector2.Distance(value[0].Pos, squad.Pos) < 3f);
                        if (group == null)
                        {
                            group = new List<SquadEntity>();
                            groups.Add(group);
                        }
                        group.Add(squad);
                    }
                    foreach (List<SquadEntity> group in groups)
                    {
                        for (int i = 0; i < group.Count; i++)
                        {
                            Sim.MapSystem.OrderMove(group[i], command.Position, node);
                            group[i].MoveDelaySeconds = i * 4f;
                        }
                    }
                    return true;
                }
                case BattleCommandType.BuyUnit:
                {
                    GameConfig.takeover.Unit unit =
                        Cfg.TbUnit.DataList.FirstOrDefault(value => value.Id == command.ConfigId);
                    return unit != null && Sim.EconomySystem.BuyUnit(player, unit, node, out _) != null;
                }
                case BattleCommandType.Upgrade:
                    return Sim.EconomySystem.TryUpgrade(player, node);
                case BattleCommandType.Build:
                    return Sim.EconomySystem.TryBuild(player, node, command.ConfigId, out _);
                case BattleCommandType.CastSpell:
                {
                    GameConfig.takeover.Spell spell =
                        Cfg.TbSpell.DataList.FirstOrDefault(value => value.Id == command.ConfigId);
                    return spell != null &&
                           Sim.SpellSystem.TryCastSpell(player, spell, command.Position, node, out _);
                }
                case BattleCommandType.GrantResources:
                    player.Gold = Mathf.Max(0f, player.Gold + command.GoldDelta);
                    player.Mana = Mathf.Clamp(player.Mana + command.ManaDelta, 0f, TakeoverScene.ManaCap);
                    return true;
                default:
                    return false;
            }
        }
    }
}
