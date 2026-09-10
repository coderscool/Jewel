using System.Collections.Generic;
using JewelPainter.Gameplay.Config;
using UnityEngine;

namespace JewelPainter.Gameplay.Board
{
    /// Kho hiệu ứng loé chạy bằng dãy sprite đã bake sẵn — bản thay thế ParticleBurstPool.
    ///
    /// Đây là cách giữ đúng hoạt ảnh artist làm bằng Spine mà không phải trả giá hiệu
    /// năng của Spine. Bake xong thì mỗi hiệu ứng đang sống chỉ còn là MỘT SpriteRenderer
    /// và mỗi frame chỉ tốn đúng một phép gán sprite khi số khung nhảy. Không skeleton,
    /// không dựng lại mesh, không đụng tới runtime spine-unity — lớp này thậm chí không
    /// tham chiếu Spine, nên asmdef Gameplay giữ nguyên.
    ///
    /// Và vì mọi khung nằm chung MỘT tấm sheet, các SpriteRenderer dùng chung material
    /// nên gộp draw call được. Đó là lý do trần đồng thời ở đây để 400 thoải mái, ngang
    /// với bản Particle System.
    ///
    /// Đổi qua lại giữa hai kho là đổi component gán vào ô Burst Pool của
    /// JewelLandSparkle / ColorCompleteSparkle, không sửa code.
    public class FlipbookBurstPool : BurstEffectPool
    {
        [Tooltip("Asset Flipbook Clip do công cụ Tools → JewelPainter → Bake Spine sang " +
                 "Flipbook sinh ra.")]
        [SerializeField] private FlipbookClip _clip;

        [Tooltip("Prefab chỉ cần một SpriteRenderer trống. Sorting Layer, Order In Layer " +
                 "và Material lấy nguyên từ prefab.\n\n" +
                 "Hiệu ứng loé sáng thì thường muốn material additive; để material sprite " +
                 "mặc định cũng chạy, chỉ là ánh sáng trông phẳng hơn.")]
        [SerializeField] private SpriteRenderer _prefab;

        [Tooltip("Cha của các hiệu ứng lấy ra dùng. Thường là chính object này.")]
        [SerializeField] private Transform _root;

        [SerializeField] private int _prewarmCount = 64;

        [Tooltip("Số hiệu ứng sống cùng lúc tối đa. Vượt quá thì BỎ QUA lần gọi mới.\n\n" +
                 "Muốn cả một màu cùng loé thì phải đặt LỚN HƠN số ô của màu nhiều ô nhất " +
                 "trong màn, không thì phần vượt bị nuốt dù bên gọi đã rải đều ra nhiều " +
                 "frame.\n\n" +
                 "Để 0 là không giới hạn.")]
        [SerializeField] private int _maxConcurrent = 400;

        [Tooltip("Nhân vào tốc độ phát. 1 là đúng nhịp lúc bake, 2 là nhanh gấp đôi.")]
        [SerializeField] private float _speed = 1f;

        [Tooltip("Xoay ngẫu nhiên quanh trục Z mỗi lần loé.\n\n" +
                 "Cả trăm ô loé cùng một khung hình, cùng một góc, thì mắt bắt ngay ra là " +
                 "một con dấu đóng lặp lại. Xoay ngẫu nhiên phá được cảm giác đó mà không " +
                 "tốn thêm gì.")]
        [SerializeField] private bool _randomRotation;

        private struct ActiveBurst
        {
            public SpriteRenderer Renderer;

            /// Vị trí phát tính bằng KHUNG HÌNH chứ không phải giây — cộng dồn
            /// deltaTime * fps mỗi frame. Đổi đơn vị ngay tại đây để vòng lặp không
            /// phải nhân lại cho từng hiệu ứng đang sống.
            public float FramePosition;

            /// Khung đang hiện. Giữ riêng để biết lúc nào cần chạm vào sprite.
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

            // Đặt khung ĐẦU ngay tại đây. Để LateUpdate lo thì frame này con vừa lấy ra
            // vẫn đang đeo khung cuối của lần loé trước, ở đúng toạ độ mới — một cú nháy.
            renderer.sprite = _clip.Frame(0);

            _active.Add(new ActiveBurst { Renderer = renderer, FramePosition = 0f, Frame = 0 });
            return true;
        }

        /// Dựng sẵn lúc vào màn. Instantiate cả trăm object đúng vào frame cần dùng là
        /// cách chắc chắn nhất để khoảnh khắc đáng lẽ đã mắt biến thành cú khựng.
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

            // Chạy ngược vì Release() xoá phần tử ngay dưới chân.
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

                // Chỉ chạm vào sprite khi số khung THẬT SỰ nhảy. Gán lại đúng cái sprite
                // cũ vẫn khiến SpriteRenderer đánh dấu bẩn và dựng lại quad của nó, mà ở
                // 30 khung/giây thì phần lớn frame là gán thừa.
                if (frame != item.Frame)
                {
                    item.Renderer.sprite = _clip.Frame(frame);
                    item.Frame = frame;
                }

                // Ghi lại vì ActiveBurst là struct: sửa bản sao không đụng tới List.
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

            // Kéo phần tử cuối vào chỗ trống thay vì RemoveAt giữa danh sách. Vòng lặp
            // gọi hàm này chạy ngược nên phần tử vừa kéo về đã duyệt rồi.
            var last = _active.Count - 1;
            _active[index] = _active[last];
            _active.RemoveAt(last);
        }
    }
}
