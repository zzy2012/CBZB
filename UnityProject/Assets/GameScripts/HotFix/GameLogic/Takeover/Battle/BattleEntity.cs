using System;
using System.Collections.Generic;
using System.Linq;

namespace GameLogic.Takeover
{
    public abstract class BattleEntity
    {
        private readonly List<BattleComponent> _components = new List<BattleComponent>();
        private bool _isInitialized;
        private bool _isDestroyed;

        public BattleScene Scene { get; private set; }
        internal bool IsInitialized => _isInitialized;

        protected T AddComponent<T>(T component) where T : BattleComponent
        {
            if (component == null) throw new ArgumentNullException(nameof(component));
            if (_isDestroyed) throw new InvalidOperationException("Components cannot be added to a destroyed entity.");
            component.Attach(this);
            _components.Add(component);
            return component;
        }

        protected T GetComponent<T>() where T : BattleComponent =>
            _components.OfType<T>().FirstOrDefault();

        protected void RemoveComponent(BattleComponent component)
        {
            if (component == null || !_components.Remove(component)) return;
            component.Detach();
        }

        internal void SetScene(BattleScene scene)
        {
            if (_isDestroyed)
                throw new InvalidOperationException("A destroyed entity cannot rejoin a battle scene.");
            if (Scene != null && Scene != scene)
                throw new InvalidOperationException("An entity cannot belong to multiple battle scenes.");
            Scene = scene;
        }

        internal void Initialize()
        {
            if (_isInitialized || _isDestroyed) return;
            _isInitialized = true;
            OnInit();
            foreach (BattleComponent component in _components)
                component.Initialize();
        }

        internal void Update(float deltaSeconds)
        {
            if (_isDestroyed) return;
            if (!_isInitialized) Initialize();
            OnUpdate(deltaSeconds);
            foreach (BattleComponent component in _components)
                component.Update(deltaSeconds);
        }

        internal void Shutdown()
        {
            if (_isDestroyed) return;
            _isDestroyed = true;
            for (int i = _components.Count - 1; i >= 0; i--)
                _components[i].Detach();
            OnDestroy();
            Scene = null;
        }

        public virtual void OnInit() { }
        public virtual void OnUpdate(float deltaSeconds) { }
        public virtual void OnDestroy() { }
    }
}
