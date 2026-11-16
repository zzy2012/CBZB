namespace GameLogic.Takeover
{
    /// <summary>挂载在战斗实体上的组件基类，提供初始化、更新和销毁钩子。</summary>
    public abstract class BattleComponent
    {
        /// <summary>组件是否已完成初始化。</summary>
        private bool _isInitialized;
        /// <summary>组件是否已完成销毁。</summary>
        private bool _isDestroyed;

        /// <summary>组件所属的战斗实体。</summary>
        public EntityBase Entity { get; private set; }

        /// <summary>将组件附加到实体并建立反向所属关系。</summary>
        internal void Attach(EntityBase entity)
        {
            if (_isDestroyed || Entity != null)
                throw new System.InvalidOperationException("A battle component can only be attached once.");
            Entity = entity;
            if (entity.IsInitialized) Initialize();
        }

        /// <summary>初始化组件，并在首次初始化时调用生命周期钩子。</summary>
        internal void Initialize()
        {
            if (_isInitialized || _isDestroyed) return;
            _isInitialized = true;
            OnInit();
        }

        /// <summary>更新已初始化且未销毁的组件。</summary>
        internal void Update(float deltaSeconds)
        {
            if (_isDestroyed) return;
            if (!_isInitialized) Initialize();
            OnUpdate(deltaSeconds);
        }

        /// <summary>从所属实体分离组件并执行销毁钩子。</summary>
        internal void Detach()
        {
            if (Entity == null) return;
            if (_isInitialized && !_isDestroyed) OnDestroy();
            _isDestroyed = true;
            Entity = null;
        }

        /// <summary>组件初始化时调用的生命周期钩子。</summary>
        public virtual void OnInit() { }
        /// <summary>组件更新时调用的生命周期钩子。</summary>
        public virtual void OnUpdate(float deltaSeconds) { }
        /// <summary>组件销毁时调用的生命周期钩子。</summary>
        public virtual void OnDestroy() { }
    }
}
