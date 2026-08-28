#if CHEAT_ENABLED
using CoreModules.CheatKit;
using CoreModules.CheatKit.Ports;
using UnityEngine;
using UnityEngine.UI;

namespace JewelPainter.Bootstrap.Cheat
{
    /// MODULE ĐẶC THÙ — phần cheat chỉ JewelPainter mới có, gắn vào panel qua
    /// CheatPanelBuilder.AddModule mà KHÔNG sửa lấy một dòng của kit (OCP).
    ///
    /// Phụ thuộc IJewelPainterCheatService qua Bind, không phụ thuộc bridge cụ thể (DIP).
    /// Mọi thứ đều null-safe: chưa Bind, hoặc bind bằng một CheatServices không có port
    /// này, thì nút xám đi chứ không ném lỗi.
    public sealed class JewelPainterCheatModule : UITestModuleBase, ICheatBindable, ICheatModuleUi
    {
        private const int SmallBatch = 10;
        private const int LargeBatch = 200;
        private const int HintGrant = 5;

        private IJewelPainterCheatService _game;

        private Button _paintSmall;
        private Button _paintLarge;
        private Button _paintColor;
        private Button _stop;
        private Button _addHints;
        private Text _stats;

        /// Giá trị đã VẼ lần gần nhất. Có nó thì OnUpdate chỉ chạm Text.text khi số thật
        /// sự đổi — gán text mỗi frame là dựng lại lưới chữ mỗi frame, đúng thứ làm tụt
        /// khung hình trên máy yếu mà lại chỉ để hiện một con số đứng yên.
        private int _lastRemaining = int.MinValue;
        private int _lastHints = int.MinValue;
        private bool _lastFilling;

        protected override void OnInitialize() => _moduleName = "JewelPainter";

        public void BuildDefaultUI(RectTransform section)
        {
            CheatUi.Label(section, "▼ JEWEL PAINTER", 26);

            var batchRow = CheatUi.Row(section);
            _paintSmall = CheatUi.Button(batchRow, $"Tô {SmallBatch} ô", new Color(0.30f, 0.65f, 0.55f));
            _paintLarge = CheatUi.Button(batchRow, $"Tô {LargeBatch} ô", new Color(0.30f, 0.58f, 0.75f));

            var colorRow = CheatUi.Row(section);
            _paintColor = CheatUi.Button(colorRow, "Xong 1 màu", new Color(0.55f, 0.45f, 0.80f));
            _stop = CheatUi.Button(colorRow, "Dừng tô", new Color(0.75f, 0.35f, 0.35f));

            _addHints = CheatUi.Button(section, $"+{HintGrant} lượt gợi ý", new Color(0.85f, 0.70f, 0.25f));

            _paintSmall.onClick.AddListener(OnPaintSmall);
            _paintLarge.onClick.AddListener(OnPaintLarge);
            _paintColor.onClick.AddListener(OnPaintColor);
            _stop.onClick.AddListener(OnStop);
            _addHints.onClick.AddListener(OnAddHints);

            _stats = CheatUi.Label(section, "Còn — ô · Gợi ý —", 24);
        }

        public void Bind(CheatServices services)
        {
            _game = services?.Get<IJewelPainterCheatService>();

            ApplyInteractable();
            ForceRefreshText();
        }

        protected override void OnCleanup()
        {
            if (_paintSmall != null) _paintSmall.onClick.RemoveListener(OnPaintSmall);
            if (_paintLarge != null) _paintLarge.onClick.RemoveListener(OnPaintLarge);
            if (_paintColor != null) _paintColor.onClick.RemoveListener(OnPaintColor);
            if (_stop != null) _stop.onClick.RemoveListener(OnStop);
            if (_addHints != null) _addHints.onClick.RemoveListener(OnAddHints);
        }

        public override void OnUpdate()
        {
            if (_game == null || _stats == null) return;

            if (_game.IsFilling != _lastFilling)
            {
                _lastFilling = _game.IsFilling;
                ApplyInteractable();
            }

            if (_game.RemainingCells == _lastRemaining && _game.HintCredits == _lastHints) return;

            ForceRefreshText();
        }

        private void OnPaintSmall() => _game?.PaintCells(SmallBatch);
        private void OnPaintLarge() => _game?.PaintCells(LargeBatch);
        private void OnPaintColor() => _game?.PaintOneColor();
        private void OnStop() => _game?.StopFilling();
        private void OnAddHints() => _game?.AddHintCredits(HintGrant);

        /// Nút "Dừng tô" chỉ bấm được khi thật sự đang tô — nút bấm được mà không làm gì
        /// là lời nói dối nhỏ mà người test phải mất một lúc mới nhận ra.
        private void ApplyInteractable()
        {
            var has = _game != null;

            if (_paintSmall != null) _paintSmall.interactable = has;
            if (_paintLarge != null) _paintLarge.interactable = has;
            if (_paintColor != null) _paintColor.interactable = has;
            if (_addHints != null) _addHints.interactable = has;
            if (_stop != null) _stop.interactable = has && _game.IsFilling;
        }

        private void ForceRefreshText()
        {
            if (_stats == null) return;

            if (_game == null)
            {
                _stats.text = "Còn — ô · Gợi ý —";
                return;
            }

            _lastRemaining = _game.RemainingCells;
            _lastHints = _game.HintCredits;

            var remaining = _lastRemaining < 0 ? "—" : _lastRemaining.ToString();
            var hints = _lastHints < 0 ? "—" : _lastHints.ToString();

            _stats.text = $"Còn {remaining} ô · Gợi ý {hints}";
        }
    }
}
#endif
