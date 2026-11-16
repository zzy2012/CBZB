namespace GameLogic.Takeover
{
    public sealed class TakeoverBattle : Battle
    {
        public BattleSim Simulation => CurScene as BattleSim;

        public TakeoverBattle(BattleSim simulation)
        {
            CurScene = simulation;
        }

        public override void OnUpdate(float deltaSeconds)
        {
            Simulation?.Tick(deltaSeconds);
        }
    }
}
