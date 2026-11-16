namespace GameLogic.Takeover
{
    /// <summary>将 Takeover 模拟场景接入战斗生命周期。</summary>
    public sealed class TakeoverBattle : Battle
    {
        /// <summary>当前战斗模拟实例。</summary>
        public BattleSim Simulation => CurScene as BattleSim;

        /// <summary>使用指定模拟场景创建战斗实例。</summary>
        public TakeoverBattle(BattleSim simulation)
        {
            CurScene = simulation;
        }

        /// <summary>推进 Takeover 战斗及其场景。</summary>
        public override void OnUpdate(float deltaSeconds)
        {
            Simulation?.Tick(deltaSeconds);
        }
    }
}
