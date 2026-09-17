using System.Collections.Generic;
using UnityEngine;

namespace JewelPainter.Gameplay.Board
{
    /// Bản chạy bằng Particle System của BurstEffectPool.
    public class ParticleBurstPool : BurstEffectPool
    {
        [Tooltip("Prefab Particle System.")]
        [SerializeField] private ParticleSystem _prefab;

        [Tooltip("Cha của các hệ hạt lấy ra dùng.")]
        [SerializeField] private Transform _root;

        [SerializeField] private int _prewarmCount = 32;

        [Tooltip("Số hiệu ứng sống cùng lúc tối đa.")]
        [SerializeField] private int _maxConcurrent = 400;

        [Tooltip("Chờ ít nhất ngần này giây rồi mới tin IsAlive để thu về.")]
        [SerializeField] private float _minAliveSeconds = 0.3f;

        private struct ActiveBurst
        {
            public ParticleSystem System;
            public float Elapsed;
        }

        private readonly List<ActiveBurst> _active = new();
        private readonly Stack<ParticleSystem> _pool = new();

        public override bool HasPrefab => _prefab != null;

        public override int ActiveCount => _active.Count;

        public override bool Play(Vector2 world)
        {
            if (_maxConcurrent > 0 && _active.Count >= _maxConcurrent) return false;

            var system = Rent();
            if (system == null) return false;

            system.transform.position = world;

            system.Clear(true);
            system.Play(true);

            _active.Add(new ActiveBurst { System = system, Elapsed = 0f });
            return true;
        }

        public override void Prewarm()
        {
            if (_prefab == null) return;

            while (_pool.Count < _prewarmCount)
            {
                var system = Instantiate(_prefab, _root);
                system.gameObject.SetActive(false);
                _pool.Push(system);
            }
        }

        public override void ReleaseAll()
        {
            for (var i = _active.Count - 1; i >= 0; i--) Release(i);
        }

        private void LateUpdate()
        {
            if (_active.Count == 0) return;

            var deltaTime = Time.deltaTime;

            for (var i = _active.Count - 1; i >= 0; i--)
            {
                var item = _active[i];
                item.Elapsed += deltaTime;

                if (item.Elapsed >= _minAliveSeconds && !item.System.IsAlive(true))
                {
                    Release(i);
                    continue;
                }

                _active[i] = item;
            }
        }

        private ParticleSystem Rent()
        {
            if (_pool.Count > 0)
            {
                var pooled = _pool.Pop();
                pooled.gameObject.SetActive(true);
                return pooled;
            }

            if (_prefab == null) return null;

            return Instantiate(_prefab, _root);
        }

        private void Release(int index)
        {
            var item = _active[index];

            item.System.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            item.System.gameObject.SetActive(false);
            _pool.Push(item.System);

            var last = _active.Count - 1;
            _active[index] = _active[last];
            _active.RemoveAt(last);
        }
    }
}
