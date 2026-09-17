using JewelPainter.Gameplay.Data;
using UnityEngine;

namespace JewelPainter.Gameplay.Config
{
    /// Dữ liệu tĩnh của một màn chơi.
    [CreateAssetMenu(fileName = "LevelConfig", menuName = "JewelPainter/Gameplay/Level Config")]
    public class LevelConfig : ScriptableObject
    {
        [SerializeField] private int _levelId = 1;
        [SerializeField] private Sprite _targetImage;
        [SerializeField] private LevelGridData _gridData;

        [Tooltip("Số tiền thưởng khi tô xong màn này.")]
        [SerializeField] private int _rewardCoins = 10;

        [Tooltip("Tranh của màn này nằm thế nào trong ô bộ sưu tập.")]
        [SerializeField] private CollectionArtworkFit _collectionFit = CollectionArtworkFit.Inset;

        [Header("Camera")]
        [Tooltip("Mức phóng sát nhất, tính bằng orthographicSize.")]
        [SerializeField] private float _cameraMinSize;

        [Tooltip("Mức kéo xa nhất, cũng là mức lúc mới vào màn.")]
        [SerializeField] private float _cameraMaxSize;

        [Tooltip("orthographicSize mà tại đó lớp màu tan hết và viền ô hiện đủ.")]
        [SerializeField] private float _fadeSwitchSize;

        public int LevelId => _levelId;
        public Sprite TargetImage => _targetImage;
        public LevelGridData GridData => _gridData;
        public int RewardCoins => _rewardCoins;

        public CollectionArtworkFit CollectionFit => _collectionFit;

        public float CameraMinSize => _cameraMinSize;

        public float CameraMaxSize => _cameraMaxSize;

        public float FadeSwitchSize => _fadeSwitchSize;
    }
}
