namespace GameLogic.Takeover
{
    public abstract class BattleComponent
    {
        private bool _isInitialized;
        private bool _isDestroyed;

        public BattleEntity Entity { get; private set; }

        internal void Attach(BattleEntity entity)
        {
            if (_isDestroyed || Entity != null)
                throw new System.InvalidOperationException("A battle component can only be attached once.");
            Entity = entity;
            if (entity.IsInitialized) Initialize();
        }

        internal void Initialize()
        {
            if (_isInitialized || _isDestroyed) return;
            _isInitialized = true;
            OnInit();
        }

        internal void Update(float deltaSeconds)
        {
            if (_isDestroyed) return;
            if (!_isInitialized) Initialize();
            OnUpdate(deltaSeconds);
        }

        internal void Detach()
        {
            if (Entity == null) return;
            if (_isInitialized && !_isDestroyed) OnDestroy();
            _isDestroyed = true;
            Entity = null;
        }

        public virtual void OnInit() { }
        public virtual void OnUpdate(float deltaSeconds) { }
        public virtual void OnDestroy() { }
    }
}
