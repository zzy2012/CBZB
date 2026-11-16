using System;
using System.Collections.Generic;

namespace GameLogic.Takeover
{
    public enum BattleEntityType
    {
        MapNode,
        Player,
        Squad,
        Unit,
        SpellEffect,
    }

    public abstract class BattleScene
    {
        private readonly Dictionary<BattleEntityType, List<BattleEntity>> _entities =
            new Dictionary<BattleEntityType, List<BattleEntity>>();
        private readonly List<BattleEntity> _updateOrder = new List<BattleEntity>();
        private readonly List<BattleEntity> _iterationBuffer = new List<BattleEntity>();
        private bool _isInitialized;
        private bool _isDestroyed;

        public IReadOnlyList<BattleEntity> GetEntities(BattleEntityType type)
        {
            return _entities.TryGetValue(type, out List<BattleEntity> entities)
                ? (IReadOnlyList<BattleEntity>)entities
                : Array.Empty<BattleEntity>();
        }

        internal void AddEntity(BattleEntityType type, BattleEntity entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            if (_isDestroyed) throw new InvalidOperationException("Cannot add entities to a destroyed battle scene.");

            if (!_entities.TryGetValue(type, out List<BattleEntity> entities))
            {
                entities = new List<BattleEntity>();
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

        internal void RemoveEntity(BattleEntityType type, BattleEntity entity)
        {
            if (entity == null || !_entities.TryGetValue(type, out List<BattleEntity> entities) ||
                !entities.Remove(entity))
                return;
            _updateOrder.Remove(entity);
            entity.Shutdown();
        }

        public virtual void OnInit()
        {
            if (_isInitialized || _isDestroyed) return;
            _isInitialized = true;
            CopyUpdateOrder();
            for (int i = 0; i < _iterationBuffer.Count; i++)
                _iterationBuffer[i].Initialize();
        }

        public virtual void OnUpdate(float deltaSeconds)
        {
            if (_isDestroyed || deltaSeconds <= 0f) return;
            if (!_isInitialized) OnInit();
            CopyUpdateOrder();
            for (int i = 0; i < _iterationBuffer.Count; i++)
                _iterationBuffer[i].Update(deltaSeconds);
        }

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

        private void CopyUpdateOrder()
        {
            _iterationBuffer.Clear();
            _iterationBuffer.AddRange(_updateOrder);
        }
    }
}
