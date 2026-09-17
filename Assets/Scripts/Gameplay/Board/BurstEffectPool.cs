using UnityEngine;

namespace JewelPainter.Gameplay.Board
{
    /// Kho hiệu ứng loé dùng lại.
    public abstract class BurstEffectPool : MonoBehaviour
    {
        public abstract bool HasPrefab { get; }

        public abstract int ActiveCount { get; }

        /// Phát một hiệu ứng loé; false khi hết chỗ hoặc thiếu prefab.
        public abstract bool Play(Vector2 world);

        /// Dựng sẵn hiệu ứng lúc vào màn.
        public abstract void Prewarm();

        /// Thu mọi hiệu ứng đang chạy về kho ngay lập tức.
        public abstract void ReleaseAll();
    }
}
