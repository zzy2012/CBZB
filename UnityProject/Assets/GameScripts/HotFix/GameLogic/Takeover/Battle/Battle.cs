namespace GameLogic.Takeover
{
    public abstract class Battle
    {
        public BattleScene CurScene { get; protected set; }

        public virtual void OnInit()
        {
            CurScene?.OnInit();
        }

        public virtual void OnUpdate(float deltaSeconds)
        {
            CurScene?.OnUpdate(deltaSeconds);
        }

        public virtual void OnDestroy()
        {
            CurScene?.OnDestroy();
            CurScene = null;
        }
    }
}
