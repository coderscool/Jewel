using System.Collections.Generic;
using JewelPainter.Gameplay.Config;
using UnityEngine;

namespace JewelPainter.Gameplay.Board
{
    /// Kho hiệu ứng loé chạy bằng dãy sprite bake sẵn.
    public class FlipbookBurstPool : BurstEffectPool
    {
        [Tooltip("Asset Flipbook Clip dùng để phát.")]
        [SerializeField] private FlipbookClip _clip;

        [Tooltip("Prefab có một SpriteRenderer trống.")]
        [SerializeField] private SpriteRenderer _prefab;

        [Tooltip("Cha của các hiệu ứng lấy ra dùng.")]
        [SerializeField] private Transform _root;

        [SerializeField] private int _prewarmCount = 64;

        [Tooltip("Số hiệu ứng sống cùng lúc tối đa.")]
        [SerializeField] private int _maxConcurrent = 400;

        [Tooltip("Nhân vào tốc độ phát.")]
        [SerializeField] private float _speed = 1f;

        [Tooltip("Xoay ngẫu nhiên quanh trục Z mỗi lần loé.")]
        [SerializeField] private bool _randomRotation;

        private struct ActiveBurst
        {
            public SpriteRenderer Renderer;

            public float FramePosition;

            public int Frame;
        }

        private readonly List<ActiveBurst> _active = new();
        private readonly Stack<SpriteRenderer> _pool = new();

        public override bool HasPrefab => _prefab != null && _clip != null && _clip.IsUsable;

        public override int ActiveCount => _active.Count;

        public override bool Play(Vector2 world)
        {
            if (!HasPrefab) return false;
            if (_maxConcurrent > 0 && _active.Count >= _maxConcurrent) return false;

            var renderer = Rent();
            if (renderer == null) return false;

            var tr = renderer.transform;
            tr.position = world;
            tr.localRotation = _randomRotation
                ? Quaternion.Euler(0f, 0f, Random.Range(0f, 360f))
                : Quaternion.identity;

            renderer.sprite = _clip.Frame(0);

            _active.Add(new ActiveBurst { Renderer = renderer, FramePosition = 0f, Frame = 0 });
            return true;
        }

        /// Dựng sẵn hiệu ứng lúc vào màn.
        public override void Prewarm()
        {
            if (_prefab == null) return;

            while (_pool.Count < _prewarmCount)
            {
                var renderer = Instantiate(_prefab, _root != null ? _root : transform);
                renderer.sprite = null;
                renderer.gameObject.SetActive(false);
                _pool.Push(renderer);
            }
        }

        public override void ReleaseAll()
        {
            for (var i = _active.Count - 1; i >= 0; i--) Release(i);
        }

        private void LateUpdate()
        {
            if (_active.Count == 0) return;

            var frameCount = _clip != null ? _clip.FrameCount : 0;
            if (frameCount == 0)
            {
                ReleaseAll();
                return;
            }

            var advance = Time.deltaTime * _clip.Fps * Mathf.Max(0f, _speed);

            for (var i = _active.Count - 1; i >= 0; i--)
            {
                var item = _active[i];
                item.FramePosition += advance;

                var frame = (int)item.FramePosition;

                if (frame >= frameCount)
                {
                    Release(i);
                    continue;
                }

                if (frame != item.Frame)
                {
                    item.Renderer.sprite = _clip.Frame(frame);
                    item.Frame = frame;
                }

                _active[i] = item;
            }
        }

        private SpriteRenderer Rent()
        {
            if (_pool.Count > 0)
            {
                var pooled = _pool.Pop();
                pooled.gameObject.SetActive(true);
                return pooled;
            }

            if (_prefab == null) return null;

            return Instantiate(_prefab, _root != null ? _root : transform);
        }

        private void Release(int index)
        {
            var item = _active[index];

            if (item.Renderer != null)
            {
                item.Renderer.sprite = null;
                item.Renderer.gameObject.SetActive(false);
                _pool.Push(item.Renderer);
            }

            var last = _active.Count - 1;
            _active[index] = _active[last];
            _active.RemoveAt(last);
        }
    }
}
