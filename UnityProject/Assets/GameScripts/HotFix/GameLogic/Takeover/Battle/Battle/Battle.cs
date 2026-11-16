namespace GameLogic.Takeover
{
    /// <summary>管理当前战斗场景生命周期的战斗基类。</summary>
    public abstract class Battle
    {
        /// <summary>当前战斗场景。</summary>
        public BattleScene CurScene { get; protected set; }

        /// <summary>初始化当前战斗场景。</summary>
        public virtual void OnInit()
        {
            CurScene?.OnInit();
        }

        /// <summary>按指定时间增量更新当前战斗场景。</summary>
        public virtual void OnUpdate(float deltaSeconds)
        {
            CurScene?.OnUpdate(deltaSeconds);
        }

        /// <summary>销毁当前战斗场景。</summary>
        public virtual void OnDestroy()
        {
            CurScene?.OnDestroy();
            CurScene = null;
        }
    }
}
