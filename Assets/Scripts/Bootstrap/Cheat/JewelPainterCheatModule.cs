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
        private const int FreePaintGrant = 5;
        private const int FillColorGrant = 5;

        private IJewelPainterCheatService _game;

        private Button _paintSmall;
        private Button _paintLarge;
        private Button _paintColor;
        private Button _stop;
        private Button _addHints;
        private Button _addFreePaint;
        private Button _addFillColor;
        private Button _toggleHud;
        private Text _stats;

        /// Nhãn nút giấu HUD ở lần vẽ gần nhất. Cùng lý do với mấy con số dưới đây: gán
        /// Text.text là dựng lại lưới chữ, đừng làm mỗi frame cho một chữ đứng yên.
        private bool _lastHudHidden;

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

            var creditRow = CheatUi.Row(section);
            _addHints = CheatUi.Button(creditRow, $"+{HintGrant} gợi ý", new Color(0.85f, 0.70f, 0.25f));
            _addFreePaint = CheatUi.Button(
                creditRow, $"+{FreePaintGrant} tô tự do", new Color(0.90f, 0.50f, 0.30f));

            _addFillColor = CheatUi.Button(
                section, $"+{FillColorGrant} tô hết màu", new Color(0.40f, 0.75f, 0.45f));

            _toggleHud = CheatUi.Button(section, HudLabel(false), new Color(0.45f, 0.45f, 0.50f));

            _paintSmall.onClick.AddListener(OnPaintSmall);
            _paintLarge.onClick.AddListener(OnPaintLarge);
            _paintColor.onClick.AddListener(OnPaintColor);
            _stop.onClick.AddListener(OnStop);
            _addHints.onClick.AddListener(OnAddHints);
            _addFreePaint.onClick.AddListener(OnAddFreePaint);
            _addFillColor.onClick.AddListener(OnAddFillColor);
            _toggleHud.onClick.AddListener(OnToggleHud);

            _stats = CheatUi.Label(section, "Còn — ô · Gợi ý —", 24);
        }

        public void Bind(CheatServices services)
        {
            _game = services?.Get<IJewelPainterCheatService>();

            ApplyInteractable();
            RefreshHudLabel();
            ForceRefreshText();
        }

        protected override void OnCleanup()
        {
            if (_paintSmall != null) _paintSmall.onClick.RemoveListener(OnPaintSmall);
            if (_paintLarge != null) _paintLarge.onClick.RemoveListener(OnPaintLarge);
            if (_paintColor != null) _paintColor.onClick.RemoveListener(OnPaintColor);
            if (_stop != null) _stop.onClick.RemoveListener(OnStop);
            if (_addHints != null) _addHints.onClick.RemoveListener(OnAddHints);
            if (_addFreePaint != null) _addFreePaint.onClick.RemoveListener(OnAddFreePaint);
            if (_addFillColor != null) _addFillColor.onClick.RemoveListener(OnAddFillColor);
            if (_toggleHud != null) _toggleHud.onClick.RemoveListener(OnToggleHud);
        }

        public override void OnUpdate()
        {
            if (_game == null || _stats == null) return;

            if (_game.IsFilling != _lastFilling)
            {
                _lastFilling = _game.IsFilling;
                ApplyInteractable();
            }

            // Theo dõi cả chiều NGƯỢC LẠI, không chỉ cập nhật lúc bấm: game tự trả HUD về
            // hiện sau mỗi màn, và nếu nhãn chỉ đổi lúc bấm thì nó sẽ nói "Hiện HUD" trong
            // khi HUD đang hiện sẵn — bấm một phát nữa mới về đúng.
            if (_game.IsHudHidden != _lastHudHidden) RefreshHudLabel();

            if (_game.RemainingCells == _lastRemaining && _game.HintCredits == _lastHints) return;

            ForceRefreshText();
        }

        private void OnPaintSmall() => _game?.PaintCells(SmallBatch);
        private void OnPaintLarge() => _game?.PaintCells(LargeBatch);
        private void OnPaintColor() => _game?.PaintOneColor();
        private void OnStop() => _game?.StopFilling();
        private void OnAddHints() => _game?.AddHintCredits(HintGrant);
        private void OnAddFreePaint() => _game?.AddFreePaintCredits(FreePaintGrant);
        private void OnAddFillColor() => _game?.AddFillColorCredits(FillColorGrant);

        /// Giấu HUD đi mà vẫn bấm được nó — xem IJewelPainterCheatService.SetHudHidden.
        ///
        /// Panel cheat nằm trên canvas riêng của kit nên KHÔNG bị giấu theo. Muốn khuôn
        /// hình sạch hẳn thì đóng panel lại; nút của HUD vẫn ở đúng chỗ cũ và vẫn ăn chạm,
        /// chỉ là phải bấm bằng trí nhớ.
        private void OnToggleHud()
        {
            if (_game == null) return;

            _game.SetHudHidden(!_game.IsHudHidden);

            RefreshHudLabel();
        }

        private void RefreshHudLabel()
        {
            _lastHudHidden = _game != null && _game.IsHudHidden;

            if (_toggleHud == null) return;

            var label = _toggleHud.GetComponentInChildren<Text>();
            if (label != null) label.text = HudLabel(_lastHudHidden);
        }

        private static string HudLabel(bool hidden) => hidden ? "Hiện HUD" : "Giấu HUD";

        /// Nút "Dừng tô" chỉ bấm được khi thật sự đang tô — nút bấm được mà không làm gì
        /// là lời nói dối nhỏ mà người test phải mất một lúc mới nhận ra.
        private void ApplyInteractable()
        {
            var has = _game != null;

            if (_paintSmall != null) _paintSmall.interactable = has;
            if (_paintLarge != null) _paintLarge.interactable = has;
            if (_paintColor != null) _paintColor.interactable = has;
            if (_addHints != null) _addHints.interactable = has;
            if (_addFreePaint != null) _addFreePaint.interactable = has;
            if (_addFillColor != null) _addFillColor.interactable = has;
            if (_toggleHud != null) _toggleHud.interactable = has;
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
