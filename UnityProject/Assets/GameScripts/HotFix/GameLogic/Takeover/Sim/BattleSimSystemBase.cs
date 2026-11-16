namespace GameLogic.Takeover
{
    /// <summary>持有战斗模拟上下文的系统基类。</summary>
    internal abstract class BattleSimSystemBase
    {
        /// <summary>使用指定战斗模拟初始化系统基类。</summary>
        protected BattleSimSystemBase(BattleSim sim)
        {
            Sim = sim;
        }

        /// <summary>当前模拟系统所属的战斗模拟。</summary>
        protected BattleSim Sim { get; }
    }
}
