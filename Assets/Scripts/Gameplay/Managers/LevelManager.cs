using System;
using System.Collections;
using System.Collections.Generic;
using JewelPainter.Gameplay.Config;
using JewelPainter.Gameplay.Data;
using JewelPainter.Gameplay.Domain;
using JewelPainter.Gameplay.Interfaces;
using UnityEngine;

namespace JewelPainter.Gameplay.Managers
{
    /// MonoBehaviour mỏng: điều phối vòng đời màn chơi.
    /// Toàn bộ luật tiến trình nằm ở PlayerProgress (thuần C#).
    public class LevelManager : MonoBehaviour, ILevelService
    {
        [SerializeField] private LevelConfig[] _levels = Array.Empty<LevelConfig>();

        [Tooltip("Phép chỉnh từ màu đất sang màu viên ngọc, dùng chung cho mọi màn. " +
                 "Bỏ trống thì ngọc mang đúng màu đất.\n\n" +
                 "Dò bộ số bằng JewelPainter > Chỉnh màu viên ngọc.")]
        [SerializeField] private JewelTintConfig _jewelTint;

        private PlayerProgress _progress;
        private LevelConfig _currentConfig;
        private IReadOnlyList<Color32> _jewelColors = Array.Empty<Color32>();

        /// Con số THÔ trong tiến trình — "đã xong tới đâu". Vượt qua màn cuối được, và
        /// phải vượt: IsCompleted đọc nó, mà màn cuối chỉ tính là xong khi có thứ gì đó
        /// đứng sau nó.
        ///
        /// Không lộ ra ngoài. Mọi thứ bên ngoài đọc CurrentLevel đã kẹp.
        private int RawLevel => _progress?.Level ?? 0;

        public int CurrentLevel => HasLevel(RawLevel) ? RawLevel : HighestLevelId();
        public LevelConfig CurrentConfig => _currentConfig;

        // Không dùng `_currentConfig?.GridData` — LevelConfig là UnityEngine.Object,
        // toán tử ?. bỏ qua phép so sánh null của Unity nên object đã huỷ vẫn lọt qua.
        public LevelGridData CurrentGrid => _currentConfig != null ? _currentConfig.GridData : null;

        public IReadOnlyList<LevelConfig> Levels => _levels;

        public IReadOnlyList<Color32> CurrentJewelColors => _jewelColors;

        /// Mở khoá theo tiến trình, không lưu riêng cờ cho từng màn: game chỉ chơi tuần
        /// tự nên "đã tới màn 5" đã nói đủ rằng 1–4 xong rồi.
        public bool IsUnlocked(int levelId) => levelId <= RawLevel;

        /// Tiến trình chỉ nhích khi một màn tô xong, nên "đứng trước màn hiện tại" đã đủ
        /// nghĩa là "đã hoàn thành" — không cần lưu riêng cờ cho từng màn.
        ///
        /// Đọc con số THÔ, không đọc CurrentLevel đã kẹp: kẹp rồi thì màn cuối đứng ngang
        /// bằng chứ không đứng trước, và nó vĩnh viễn không được tính là đã hoàn thành.
        public bool IsCompleted(int levelId) => levelId < RawLevel;

        public event Action<int> OnLevelLoadStarted;
        public event Action<int> OnLevelStarted;
        public event Action<int> OnLevelCompleted;

        /// Lượt nạp đang chạy. Bấm nạp màn mới giữa chừng thì huỷ lượt cũ — không huỷ
        /// thì hai lượt cùng bắn OnLevelStarted và bàn chơi dựng hai lần.
        private Coroutine _loadRoutine;

        /// Bootstrap đưa phụ thuộc xuống — không tự đi tìm.
        public void Init(PlayerProgress progress)
        {
            _progress = progress;
        }

        public bool HasLevel(int levelId) => FindConfig(levelId) != null;

        /// Level Id LỚN NHẤT đang khai, không phải phần tử cuối mảng: thứ tự trong
        /// Inspector không có gì bắt phải trùng với thứ tự id.
        private int HighestLevelId()
        {
            var highest = 0;

            foreach (var config in _levels)
            {
                if (config != null && config.LevelId > highest) highest = config.LevelId;
            }

            return highest;
        }

        /// KHÔNG dựng bàn ngay trong lời gọi này.
        ///
        /// Dựng bàn là việc nặng nhất của cả game — texture, hàng nghìn object dựng sẵn,
        /// mười một lớp cùng dựng lại. Làm hết trong frame của cú bấm thì người chơi thấy
        /// game đứng hình ngay dưới ngón tay mình, và màn hình chờ có tồn tại cũng vô ích
        /// vì nó chưa kịp được vẽ lần nào.
        ///
        /// Nên: bắn OnLevelLoadStarted để màn chờ hiện lên, nhường vài frame cho nó thật
        /// sự lên màn hình, rồi mới dựng. Mọi nơi gọi LoadLevel — Home, chơi lại, cheat,
        /// lúc khởi động — đều được che mà không phải tự lo gì.
        ///
        /// Cái giá: LoadLevel KHÔNG còn đồng bộ. Đọc CurrentConfig ngay dòng sau lời gọi
        /// sẽ ra màn CŨ. Muốn chạy việc gì sau khi bàn dựng xong thì nghe OnLevelStarted.
        public void LoadLevel(int levelId)
        {
            if (_loadRoutine != null) StopCoroutine(_loadRoutine);

            // Object tắt thì không chạy được coroutine. Hiếm, nhưng nếu xảy ra thì thà
            // dựng thẳng còn hơn im lặng không nạp màn nào.
            if (!isActiveAndEnabled)
            {
                Build(levelId);
                return;
            }

            _loadRoutine = StartCoroutine(LoadRoutine(levelId));
        }

        private IEnumerator LoadRoutine(int levelId)
        {
            OnLevelLoadStarted?.Invoke(levelId);

            // HAI frame, không phải một: frame đầu Canvas dựng lại layout của màn chờ,
            // frame sau nó mới thật sự được vẽ ra. Một frame đủ trên máy khoẻ, và không
            // đủ đúng trên những máy mà cú khựng này khó chịu nhất.
            yield return null;
            yield return null;

            _loadRoutine = null;

            Build(levelId);
        }

        private void Build(int levelId)
        {
            _currentConfig = FindConfig(levelId);

            // Dựng TRƯỚC khi bắn event: BoardView và ColorPaletteBar đều đọc bảng này
            // ngay trong lượt xử lý OnLevelStarted.
            _jewelColors = BuildJewelColors();

            OnLevelStarted?.Invoke(levelId);
        }

        /// Trả thẳng bảng màu đất khi không có gì để chỉnh — không copy một mảng chỉ để
        /// nó giống hệt mảng gốc.
        private IReadOnlyList<Color32> BuildJewelColors()
        {
            var grid = CurrentGrid;
            if (grid == null) return Array.Empty<Color32>();

            var ground = grid.Colors;
            if (_jewelTint == null || _jewelTint.Tint.IsNone) return ground;

            var jewel = new Color32[ground.Count];
            for (var i = 0; i < jewel.Length; i++) jewel[i] = _jewelTint.Tint.Apply(ground[i]);

            return jewel;
        }

        public void CompleteCurrentLevel()
        {
            var finishedLevel = _progress.Level;

            _progress.Advance();

            MarkLevelFinished(finishedLevel);
        }

        /// Chỉ bắn tín hiệu "màn này đã tô xong", không đụng tới tiến trình.
        ///
        /// CompleteCurrentLevel đi qua đây thay vì tự bắn: hai đường phải phát ra CÙNG
        /// một sự kiện, không thì người nghe chỉ dọn dẹp được cho một nửa số lượt chơi.
        public void MarkLevelFinished(int levelId) => OnLevelCompleted?.Invoke(levelId);

        private LevelConfig FindConfig(int levelId)
        {
            foreach (var config in _levels)
            {
                if (config != null && config.LevelId == levelId) return config;
            }

            return null;
        }
    }
}
