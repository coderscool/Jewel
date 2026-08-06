using JewelPainter.Gameplay.Data;
using UnityEngine;

namespace JewelPainter.Gameplay.Config
{
    /// Dữ liệu tĩnh của một màn chơi. Designer chỉnh trong Inspector,
    /// không cần lập trình viên đụng code.
    [CreateAssetMenu(fileName = "LevelConfig", menuName = "JewelPainter/Gameplay/Level Config")]
    public class LevelConfig : ScriptableObject
    {
        [SerializeField] private int _levelId = 1;
        [SerializeField] private Sprite _targetImage;
        [SerializeField] private LevelGridData _gridData;
        [SerializeField] private int _timeLimitSeconds;

        [Header("Camera")]
        [Tooltip("Mức phóng sát nhất, tính bằng orthographicSize. Một ô rộng một world unit " +
                 "nên giá trị 4 là thấy 8 ô theo chiều dọc. Để 0 thì tự tính (thấy 5 ô).")]
        [SerializeField] private float _cameraMinSize;

        [Tooltip("Mức kéo xa nhất, cũng là mức lúc mới vào màn. Để 0 thì tự tính (vừa khít " +
                 "bảng cộng lề 10%). Đặt lớn hơn mức vừa khít thì kéo được ra xa hơn cả bảng.")]
        [SerializeField] private float _cameraMaxSize;

        [Tooltip("orthographicSize mà tại đó lớp màu tan hết và viền ô hiện đủ — hai lớp " +
                 "hoán đổi đúng tại mức này. Để 0 thì dùng giá trị đặt sẵn trên BoardColorFade.")]
        [SerializeField] private float _fadeSwitchSize;

        public int LevelId => _levelId;
        public Sprite TargetImage => _targetImage;
        public LevelGridData GridData => _gridData;
        public int TimeLimitSeconds => _timeLimitSeconds;

        /// 0 hoặc âm nghĩa là để BoardCamera tự tính.
        public float CameraMinSize => _cameraMinSize;

        /// 0 hoặc âm nghĩa là để BoardCamera tự tính.
        public float CameraMaxSize => _cameraMaxSize;

        /// 0 hoặc âm nghĩa là dùng giá trị đặt sẵn trên từng BoardColorFade.
        public float FadeSwitchSize => _fadeSwitchSize;
    }
}
