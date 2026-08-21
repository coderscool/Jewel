using System.Collections.Generic;
using UnityEngine;

namespace JewelPainter.Gameplay.Board
{
    /// Kho hệ hạt dùng lại: ai cần loé một phát ở toạ độ nào thì gọi Play, không phải
    /// tự lo tạo, thu hồi hay đếm xem hiệu ứng chạy xong chưa.
    ///
    /// Vì sao hiệu ứng KHÔNG được gắn thẳng vào prefab viên ngọc: JewelLayer thu ngọc
    /// về kho khi ô trôi ra ngoài khung hình rồi lấy ra dùng lại khi ô trở vào. Hệ hạt
    /// nằm trong đó, bật `Play On Awake`, sẽ chạy lại mỗi lần viên được bật — kéo camera
    /// qua lại là cả bảng loé sáng như mới tô. Hiệu ứng có vòng đời riêng thì một sự
    /// kiện mới phát đúng một lần.
    ///
    /// Kèm theo đó là lợi ích về hiệu năng: số hệ hạt sống cùng lúc phụ thuộc số sự
    /// kiện vừa xảy ra, không phụ thuộc số ngọc đang hiện trên màn.
    public class ParticleBurstPool : MonoBehaviour
    {
        [Tooltip("Prefab Particle System. Phải TẮT Play On Awake — kho tự gọi Play().")]
        [SerializeField] private ParticleSystem _prefab;

        [Tooltip("Cha của các hệ hạt lấy ra dùng. Thường là chính object này.")]
        [SerializeField] private Transform _root;

        [SerializeField] private int _prewarmCount = 32;

        [Tooltip("Số hiệu ứng sống cùng lúc tối đa. Vượt quá thì BỎ QUA lần gọi mới — " +
                 "hiệu ứng đó mất hẳn, không xếp hàng lại.\n\n" +
                 "Đây là trần cứng cuối cùng. Muốn cả một màu cùng loé thì phải đặt LỚN " +
                 "HƠN số ô của màu nhiều ô nhất trong màn, không thì phần vượt bị nuốt " +
                 "dù bên gọi đã rải đều ra nhiều frame.\n\n" +
                 "Để 0 là không giới hạn.")]
        [SerializeField] private int _maxConcurrent = 400;

        [Tooltip("Chờ ít nhất ngần này giây rồi mới tin IsAlive để thu về. Đặt LỚN HƠN " +
                 "Start Delay lớn nhất trong prefab, không thì hệ hạt bị thu ngay trước " +
                 "khi kịp bắn hạt đầu tiên.")]
        [SerializeField] private float _minAliveSeconds = 0.3f;

        private struct ActiveBurst
        {
            public ParticleSystem System;
            public float Elapsed;
        }

        private readonly List<ActiveBurst> _active = new();
        private readonly Stack<ParticleSystem> _pool = new();

        public bool HasPrefab => _prefab != null;

        /// false khi đã chạm trần đồng thời, hoặc thiếu prefab.
        ///
        /// Trả về kết quả thay vì im lặng bỏ qua: bên gọi cần biết để XẾP LẠI HÀNG. Nuốt
        /// lặng lẽ nghĩa là hiệu ứng mất hẳn, mà thứ duy nhất người dùng thấy là "sao nó
        /// không loé hết".
        public bool Play(Vector2 world)
        {
            if (_maxConcurrent > 0 && _active.Count >= _maxConcurrent) return false;

            var system = Rent();
            if (system == null) return false;

            system.transform.position = world;

            // Clear trước Play: hệ hạt lấy từ kho có thể còn hạt đông cứng từ lần trước,
            // và chúng sẽ hiện ra ngay frame đầu ở đúng chỗ mới.
            system.Clear(true);
            system.Play(true);

            _active.Add(new ActiveBurst { System = system, Elapsed = 0f });
            return true;
        }

        /// Dựng sẵn lúc vào màn. Instantiate cả trăm hệ hạt đúng vào frame cần dùng là
        /// cách chắc chắn nhất để khoảnh khắc đáng lẽ đã mắt biến thành cú khựng.
        public void Prewarm()
        {
            if (_prefab == null) return;

            while (_pool.Count < _prewarmCount)
            {
                var system = Instantiate(_prefab, _root);
                system.gameObject.SetActive(false);
                _pool.Push(system);
            }
        }

        public void ReleaseAll()
        {
            for (var i = _active.Count - 1; i >= 0; i--) Release(i);
        }

        private void LateUpdate()
        {
            if (_active.Count == 0) return;

            var deltaTime = Time.deltaTime;

            // Chạy ngược vì Release() xoá phần tử ngay dưới chân.
            for (var i = _active.Count - 1; i >= 0; i--)
            {
                var item = _active[i];
                item.Elapsed += deltaTime;

                if (item.Elapsed >= _minAliveSeconds && !item.System.IsAlive(true))
                {
                    Release(i);
                    continue;
                }

                // Ghi lại vì ActiveBurst là struct: sửa bản sao không đụng tới List.
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

            // Kéo phần tử cuối vào chỗ trống thay vì RemoveAt giữa danh sách. Vòng lặp
            // gọi hàm này chạy ngược nên phần tử vừa kéo về đã duyệt rồi.
            var last = _active.Count - 1;
            _active[index] = _active[last];
            _active.RemoveAt(last);
        }
    }
}
