using System;
using System.Collections.Generic;
using System.Linq;

namespace GameLogic.Takeover
{
    /// <summary>战斗场景中实体的基类，管理组件及其生命周期。</summary>
    public abstract class EntityBase
    {
        /// <summary>附加到该实体上的战斗组件集合。</summary>
        private readonly List<BattleComponent> _components = new List<BattleComponent>();
        /// <summary>实体是否已完成初始化。</summary>
        private bool _isInitialized;
        /// <summary>实体是否已完成销毁。</summary>
        private bool _isDestroyed;

        /// <summary>实体所属的战斗场景。</summary>
        public BattleScene Scene { get; private set; }
        /// <summary>实体是否已完成初始化。</summary>
        internal bool IsInitialized => _isInitialized;

        /// <summary>将组件附加到实体并返回该组件。</summary>
        protected T AddComponent<T>(T component) where T : BattleComponent
        {
            if (component == null) throw new ArgumentNullException(nameof(component));
            if (_isDestroyed) throw new InvalidOperationException("Components cannot be added to a destroyed entity.");
            component.Attach(this);
            _components.Add(component);
            return component;
        }

        /// <summary>获取实体上第一个指定类型的组件。</summary>
        protected T GetComponent<T>() where T : BattleComponent =>
            _components.OfType<T>().FirstOrDefault();

        /// <summary>移除指定组件并执行其分离生命周期。</summary>
        protected void RemoveComponent(BattleComponent component)
        {
            if (component == null || !_components.Remove(component)) return;
            component.Detach();
        }

        /// <summary>设置实体所属场景。</summary>
        internal void SetScene(BattleScene scene)
        {
            if (_isDestroyed)
                throw new InvalidOperationException("A destroyed entity cannot rejoin a battle scene.");
            if (Scene != null && Scene != scene)
                throw new InvalidOperationException("An entity cannot belong to multiple battle scenes.");
            Scene = scene;
        }

        /// <summary>按顺序初始化实体上的所有组件。</summary>
        internal void Initialize()
        {
            if (_isInitialized || _isDestroyed) return;
            _isInitialized = true;
            OnInit();
            foreach (BattleComponent component in _components)
                component.Initialize();
        }

        /// <summary>按顺序更新实体上的所有组件。</summary>
        internal void Update(float deltaSeconds)
        {
            if (_isDestroyed) return;
            if (!_isInitialized) Initialize();
            OnUpdate(deltaSeconds);
            foreach (BattleComponent component in _components)
                component.Update(deltaSeconds);
        }

        /// <summary>按逆序销毁实体上的组件并清理生命周期状态。</summary>
        internal void Shutdown()
        {
            if (_isDestroyed) return;
            _isDestroyed = true;
            for (int i = _components.Count - 1; i >= 0; i--)
                _components[i].Detach();
            OnDestroy();
            Scene = null;
        }

        /// <summary>实体初始化时调用的生命周期钩子。</summary>
        public virtual void OnInit() { }
        /// <summary>实体更新时调用的生命周期钩子。</summary>
        public virtual void OnUpdate(float deltaSeconds) { }
        /// <summary>实体销毁时调用的生命周期钩子。</summary>
        public virtual void OnDestroy() { }
    }
}
