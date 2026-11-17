using System;
using System.Collections.Generic;

namespace GameLogic.Takeover
{
    /// <summary>战斗场景中用于分类实体的类型。</summary>
    public enum BattleEntityType
    {
        /// <summary>地图节点实体。</summary>
        MapNode,
        /// <summary>战斗参与方实体。</summary>
        Player,
        /// <summary>部队实体。</summary>
        Squad,
        /// <summary>单个士兵实体。</summary>
        Unit,
        /// <summary>持续法术效果实体。</summary>
        SpellEffect,
    }

    /// <summary>管理战斗实体并转发生命周期调用的场景基类。</summary>
    public abstract class SceneBase
    {
        /// <summary>按实体类型登记的场景实体集合。</summary>
        private readonly Dictionary<BattleEntityType, List<EntityBase>> _entities =
            new Dictionary<BattleEntityType, List<EntityBase>>();
        /// <summary>按注册顺序更新的场景实体列表。</summary>
        private readonly List<EntityBase> _updateOrder = new List<EntityBase>();
        /// <summary>场景生命周期遍历时使用的实体快照。</summary>
        private readonly List<EntityBase> _iterationBuffer = new List<EntityBase>();
        /// <summary>场景是否已完成初始化。</summary>
        private bool _isInitialized;
        /// <summary>场景是否已完成销毁。</summary>
        private bool _isDestroyed;

        /// <summary>获取指定类型的场景实体；没有实体时返回空集合。</summary>
        public IReadOnlyList<EntityBase> GetEntities(BattleEntityType type)
        {
            return _entities.TryGetValue(type, out List<EntityBase> entities)
                ? (IReadOnlyList<EntityBase>)entities
                : Array.Empty<EntityBase>();
        }

        /// <summary>将实体按类型注册到场景并纳入生命周期更新。</summary>
        internal void AddEntity(BattleEntityType type, EntityBase entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            if (_isDestroyed) throw new InvalidOperationException("Cannot add entities to a destroyed battle scene.");

            if (!_entities.TryGetValue(type, out List<EntityBase> entities))
            {
                entities = new List<EntityBase>();
                _entities.Add(type, entities);
            }
            if (entities.Contains(entity)) return;
            if (entity.Scene == this)
                throw new InvalidOperationException("An entity can only be registered once in a battle scene.");
            entity.SetScene(this);
            entities.Add(entity);
            _updateOrder.Add(entity);
            if (_isInitialized) entity.Initialize();
        }

        /// <summary>从场景注销实体并关闭其生命周期。</summary>
        internal void RemoveEntity(BattleEntityType type, EntityBase entity)
        {
            if (entity == null || !_entities.TryGetValue(type, out List<EntityBase> entities) ||
                !entities.Remove(entity))
                return;
            _updateOrder.Remove(entity);
            entity.Shutdown();
        }

        /// <summary>初始化场景及当前实体。</summary>
        public virtual void OnInit()
        {
            if (_isInitialized || _isDestroyed) return;
            _isInitialized = true;
            CopyUpdateOrder();
            for (int i = 0; i < _iterationBuffer.Count; i++)
                _iterationBuffer[i].Initialize();
        }

        /// <summary>按时间增量更新场景中的实体。</summary>
        public virtual void OnUpdate(float deltaSeconds)
        {
            if (_isDestroyed || deltaSeconds <= 0f) return;
            if (!_isInitialized) OnInit();
            CopyUpdateOrder();
            for (int i = 0; i < _iterationBuffer.Count; i++)
                _iterationBuffer[i].Update(deltaSeconds);
        }

        /// <summary>销毁场景中的实体并清空注册状态。</summary>
        public virtual void OnDestroy()
        {
            if (_isDestroyed) return;
            _isDestroyed = true;
            CopyUpdateOrder();
            for (int i = _iterationBuffer.Count - 1; i >= 0; i--)
                _iterationBuffer[i].Shutdown();
            _updateOrder.Clear();
            _entities.Clear();
        }

        /// <summary>复制当前实体更新顺序以安全遍历。</summary>
        private void CopyUpdateOrder()
        {
            _iterationBuffer.Clear();
            _iterationBuffer.AddRange(_updateOrder);
        }
    }
}
