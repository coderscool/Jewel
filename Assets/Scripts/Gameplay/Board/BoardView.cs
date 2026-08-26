using System;
using System.Collections.Generic;
using JewelPainter.Gameplay.Config;
using JewelPainter.Gameplay.Domain;
using JewelPainter.Gameplay.Interfaces;
using UnityEngine;
using UnityEngine.Serialization;

namespace JewelPainter.Gameplay.Board
{
    /// Dựng toàn bộ ô màu thành texture rồi gắn lên SpriteRenderer.
    ///
    /// Mỗi ô chiếm một KHỐI texel vuông (xem Texels Per Cell), và pixelsPerUnit của sprite
    /// đúng bằng cạnh khối đó — nên bảng vẫn rộng đúng Width x Height world unit. Mọi lớp
    /// khác đi qua BoardLayout nên không phải biết gì về độ mịn của texture.
    ///
    /// Ô tô xong cũng chỉ là ghi lại một pixel — chi phí KHÔNG phụ thuộc số ô,
    /// nên lưới 64x64 (4096 ô) tốn đúng bằng lưới 16x16.
    ///
    /// HAI LỚP chứ không phải một:
    ///   - lớp CHƯA TÔ giữ các ô xám, và bị BoardColorFade làm mờ dần khi phóng to
    ///     để lộ số nằm dưới;
    ///   - lớp ĐÃ TÔ giữ màu thật, KHÔNG ai làm mờ nó.
    ///
    /// Gộp chung một texture thì không tách được: alpha là của cả SpriteRenderer, nên
    /// làm mờ để hiện số cũng đồng thời xoá màu của những ô người chơi vừa tô xong.
    /// Hai ô không bao giờ cùng nằm trên cả hai lớp — tô tới đâu, pixel bên lớp chưa
    /// tô bị xoá tới đó.
    [RequireComponent(typeof(SpriteRenderer))]
    public class BoardView : MonoBehaviour
    {
        private static readonly Color32 Transparent = new Color32(0, 0, 0, 0);

        [Tooltip("Lớp ô CHƯA TÔ (xám). Đây là lớp bị BoardColorFade làm mờ theo mức zoom.")]
        [FormerlySerializedAs("_renderer")]
        [SerializeField] private SpriteRenderer _unpaintedRenderer;

        [Tooltip("Lớp ô ĐÃ TÔ (màu thật). KHÔNG gắn BoardColorFade lên đây — chính việc " +
                 "nó không bị làm mờ là lý do lớp này tồn tại. Order in Layer phải LỚN HƠN " +
                 "của lớp chưa tô.")]
        [SerializeField] private SpriteRenderer _paintedRenderer;

        [Tooltip("Ô chưa tô hiện dạng xám. Ô tô đúng sẽ hiện màu thật. " +
                 "Tắt để đối chiếu với ảnh gốc.")]
        [SerializeField] private bool _grayscale = true;

        [Tooltip("Mỗi ô chiếm bao nhiêu texel vuông trong texture bảng.\n\n" +
                 "1 là bản cũ, và nó không sống nổi ở hai đầu mức zoom. Zoom xa: lọc Point " +
                 "chỉ lấy một texel cho mỗi pixel màn hình nên có ô bị bỏ sót hẳn. Bật " +
                 "mipmap để chữa thì mip lại trộn ô đã tô với hàng xóm alpha 0, và ô nhạt " +
                 "dần đi — đó chính là cảnh 'ô mất màu không cố định' và 'ngọc chìm dưới nền'.\n\n" +
                 "4 là mặc định: thu nhỏ có dữ liệu thật để trung bình, phóng to vẫn sắc " +
                 "cạnh vì lọc vẫn là Point. Bộ nhớ gấp 16 lần nhưng bảng 72x72 cũng chỉ " +
                 "331 KB cho cả hai lớp.\n\n" +
                 "Đổi số này phải VÀO LẠI MÀN mới có tác dụng.")]
        [Range(1, 8)]
        [SerializeField] private int _texelsPerCell = 4;

        [Tooltip("Sinh mipmap cho hai texture bảng.\n\n" +
                 "Từ khi mỗi ô có 4x4 texel, mipmap không còn là thứ bắt buộc — mức mip đầu " +
                 "tiên vẫn còn nguyên hình ô. Bật thì zoom rất xa mượt hơn, tắt thì cạnh ô " +
                 "sắc hơn. Thử cả hai rồi chọn.\n\n" +
                 "Cái giá: thêm một phần ba bộ nhớ texture, và mỗi lần upload phải dựng lại " +
                 "mipmap.")]
        [SerializeField] private bool _generateMipmaps = true;

        /// Cách đẩy thay đổi lên màn hình. Chỉ để CHIA ĐÔI phạm vi lỗi, không phải để phát hành.
        private enum UploadMode
        {
            /// SetPixels32 + Apply. Đây là đường thật.
            Normal = 0,

            /// Thêm bước gán lại sprite cho renderer sau mỗi lần Apply.
            ReassignSprite = 1,

            /// Dựng lại texture và sprite từ đầu sau mỗi lần đổi — đúng bằng những gì xảy
            /// ra khi vào lại màn. RẤT nặng, chỉ bật vài giây để xem có hết lỗi không.
            RecreateTexture = 2,
        }

        [Header("Chẩn đoán")]
        [Tooltip("Chia đôi phạm vi lỗi 'tô rồi mà không lên màu, vào lại màn thì đúng'.\n\n" +
                 "Vào lại màn khác cú upload thường ở ba việc: tạo texture mới, tạo sprite " +
                 "mới, gán lại sprite. Chạy thử từng nấc để biết việc nào mới là thứ chữa " +
                 "được lỗi.\n\n" +
                 "Normal — đường thật, để so sánh.\n" +
                 "Reassign Sprite — hết lỗi thì thủ phạm là renderer đang giữ bản cũ.\n" +
                 "Recreate Texture — chỉ nấc này mới hết thì thủ phạm là chính texture.\n\n" +
                 "Trả về Normal khi đo xong: nấc thứ ba dựng lại texture mỗi lần tô.")]
        [SerializeField] private UploadMode _uploadMode = UploadMode.Normal;
        [Tooltip("Sau mỗi lần upload, đọc NGƯỢC từ texture ra để kiểm những ô vừa lộ màu.\n\n" +
                 "Dùng khi gặp cảnh 'ô đã tô mà không có màu'. Ba nguồn được đối chiếu: " +
                 "trạng thái luật chơi, mảng pixel trên CPU, và texture đã nằm trên GPU. " +
                 "Chỗ nào lệch chính là chỗ hỏng.\n\n" +
                 "Tốn một lần đọc pixel cho mỗi ô vừa tô. Tắt khi phát hành.")]
        [SerializeField] private bool _verifyReveals;

        [Tooltip("Cứ ngần này giây thì quét lại TOÀN BỘ bảng một lượt, đối chiếu ba nguồn " +
                 "như trên. Để 0 là tắt.\n\n" +
                 "Khác với ô trên ở chỗ nó bắt được cả những ô ghi ĐÚNG lúc tô rồi mới hỏng " +
                 "về sau. Nếu ô trên báo sạch mà ô này báo lệch, thì thủ phạm nằm ở đoạn " +
                 "sau — có thứ gì đó ghi đè lên texture.")]
        [SerializeField] private float _auditIntervalSeconds;

        private ILevelService _levelService;
        private IPaintService _paintService;

        private Texture2D _unpaintedTexture;
        private Texture2D _paintedTexture;
        private Sprite _unpaintedSprite;
        private Sprite _paintedSprite;
        private Color32[] _unpaintedPixels;
        private Color32[] _paintedPixels;

        private bool _isTextureDirty;

        /// Những ô vừa lộ màu, chờ được đối chiếu sau lần upload kế tiếp.
        private readonly List<Vector2Int> _revealedSinceUpload = new();

        /// Hai lượt quét gần nhất. Không readonly vì hai tập được hoán đổi cho nhau.
        private HashSet<Vector2Int> _previousMismatches = new();
        private HashSet<Vector2Int> _currentMismatches = new();

        private float _nextAuditTime;

        public BoardLayout Layout { get; private set; }
        public PixelGrid Grid { get; private set; }
        public IReadOnlyList<Color32> Colors { get; private set; }

        /// Cấu hình màn đang chơi. BoardColorFade đọc mốc mờ từ đây — hai instance của
        /// nó không đi qua DI được (RegisterComponentInHierarchy chỉ tìm thấy một), nên
        /// lấy chung qua BoardView vốn đã có sẵn tham chiếu.
        public LevelConfig Config { get; private set; }

        public event Action OnBoardRebuilt;

        /// Bootstrap gọi. Chỉ đăng ký lắng nghe — bảng dựng khi màn chơi được nạp.
        ///
        /// Cần IPaintService để dựng đúng những ô đã tô từ phiên trước. PaintManager
        /// phải Init TRƯỚC lớp này, vì nó nạp lại tiến độ trong chính handler của
        /// OnLevelStarted — Init sau thì bảng dựng khi trạng thái còn trống.
        public void Init(ILevelService levelService, IPaintService paintService)
        {
            _levelService = levelService;
            _paintService = paintService;

            _levelService.OnLevelStarted += HandleLevelStarted;
        }

        private void OnDestroy()
        {
            if (_levelService != null) _levelService.OnLevelStarted -= HandleLevelStarted;

            ReleaseTextures();
        }

        /// Gộp mọi thay đổi trong một frame thành MỘT lần Apply cho mỗi lớp.
        /// Kéo tay tô mười ô trong một frame vẫn chỉ upload hai texture một lần.
        private void LateUpdate()
        {
            if (_isTextureDirty)
            {
                _isTextureDirty = false;

                // Apply phải dựng LẠI mipmap, không thì mức thu nhỏ vẫn mang nội dung của
                // lúc vào màn và ô vừa tô không hiện ra khi zoom xa.
                _unpaintedTexture.SetPixels32(_unpaintedPixels);
                _unpaintedTexture.Apply(_generateMipmaps);

                _paintedTexture.SetPixels32(_paintedPixels);
                _paintedTexture.Apply(_generateMipmaps);

                ApplyUploadMode();

                VerifyReveals();
            }

            AuditBoard();
        }

        private void HandleLevelStarted(int levelId) => Rebuild();

        /// Chuyển một ô từ lớp chưa tô sang lớp đã tô.
        ///
        /// KHÔNG tự nghe OnCellPainted: ô phải đợi viên ngọc bay tới rồi mới đổi màu,
        /// nếu không màu hiện trước lúc hiệu ứng kết thúc và cú bay thành vô nghĩa.
        /// JewelFlyEffect gọi hàm này khi viên đáp xuống.
        public void RevealCell(Vector2Int cell, int paletteIndex)
        {
            if (Grid == null || Colors == null || _paintedPixels == null) return;
            if (paletteIndex < 0 || paletteIndex >= Colors.Count) return;
            if (cell.x < 0 || cell.x >= Grid.Width || cell.y < 0 || cell.y >= Grid.Height) return;

            WritePixel(_paintedPixels, cell.x, cell.y, Colors[paletteIndex]);

            // Xoá bên lớp chưa tô. Lớp đã tô đục và nằm trên nên không xoá cũng không
            // ai thấy, nhưng để lại thì hai lớp cùng mô tả một ô — sau này sửa gì cũng
            // phải nhớ giữ chúng khớp nhau.
            WritePixel(_unpaintedPixels, cell.x, cell.y, Transparent);

            _isTextureDirty = true;

            if (_verifyReveals) _revealedSinceUpload.Add(cell);
        }

        /// Hai nấc chẩn đoán bổ sung sau mỗi lần upload. Normal thì không làm gì.
        private void ApplyUploadMode()
        {
            if (_uploadMode == UploadMode.Normal) return;

            if (_uploadMode == UploadMode.RecreateTexture)
            {
                // Dựng lại đúng như lúc vào màn. Giữ lại tham chiếu cũ để huỷ SAU khi đã
                // gán cái mới — huỷ trước thì có một nhịp renderer trỏ vào sprite đã chết.
                var oldUnpaintedSprite = _unpaintedSprite;
                var oldPaintedSprite = _paintedSprite;
                var oldUnpaintedTexture = _unpaintedTexture;
                var oldPaintedTexture = _paintedTexture;

                _unpaintedTexture = CreateTexture(TextureWidth, Grid.Height * _texelsPerCell, _unpaintedPixels, _generateMipmaps);
                _paintedTexture = CreateTexture(TextureWidth, Grid.Height * _texelsPerCell, _paintedPixels, _generateMipmaps);

                _unpaintedSprite = CreateSprite(_unpaintedTexture);
                _paintedSprite = CreateSprite(_paintedTexture);

                DestroyIfAlive(ref oldUnpaintedSprite);
                DestroyIfAlive(ref oldPaintedSprite);
                DestroyIfAlive(ref oldUnpaintedTexture);
                DestroyIfAlive(ref oldPaintedTexture);
            }

            // Gán null trước rồi gán lại: gán đúng cái sprite đang có sẵn thì Unity thấy
            // giá trị không đổi và bỏ qua, nên bước này sẽ chẳng chứng minh được gì.
            if (_unpaintedRenderer != null)
            {
                _unpaintedRenderer.sprite = null;
                _unpaintedRenderer.sprite = _unpaintedSprite;
            }

            if (_paintedRenderer != null)
            {
                _paintedRenderer.sprite = null;
                _paintedRenderer.sprite = _paintedSprite;
            }
        }

        /// Đọc ngược từ TEXTURE ra để xem những ô vừa lộ màu có thật sự tới nơi không.
        ///
        /// Ba nguồn phải khớp nhau, và chỗ lệch chỉ thẳng ra tầng hỏng:
        ///   - luật chơi bảo đã tô, mảng CPU vẫn trong suốt  → cú ghi không xảy ra;
        ///   - mảng CPU có màu, texture vẫn trong suốt        → cú upload bị mất;
        ///   - cả ba đều đúng mà mắt vẫn thấy ô trống         → hỏng ở khâu VẼ, không
        ///     phải ở dữ liệu: hai lớp lệch nhau, hoặc có lớp khác đè lên.
        ///
        /// Nhánh thứ ba là nhánh dễ bị bỏ sót nhất, vì nó khiến người ta đi sửa mãi phần
        /// dữ liệu vốn đang đúng.
        private void VerifyReveals()
        {
            if (!_verifyReveals || _revealedSinceUpload.Count == 0) return;

            foreach (var cell in _revealedSinceUpload)
            {
                if (IsMismatched(cell.x, cell.y)) Report(cell.x, cell.y, "vừa tô");
            }

            _revealedSinceUpload.Clear();
        }

        /// Quét lại cả bảng theo chu kỳ.
        ///
        /// Bắt được loại hỏng mà VerifyReveals không thấy: ô ghi ĐÚNG lúc tô rồi mới bị
        /// làm hỏng ở một frame nào đó về sau.
        ///
        /// Chỉ báo ô lệch ở HAI LƯỢT QUÉT LIÊN TIẾP. Ô đang có viên ngọc bay tới thì luật
        /// chơi đã ghi "đã tô" trong khi màu còn chưa được lộ — đó là trạng thái BÌNH
        /// THƯỜNG, chỉ kéo dài chưa tới một giây. Báo ngay lượt đầu thì mỗi lần kéo tay
        /// tô là hàng chục dòng cảnh báo giả, và cái lệch thật chìm nghỉm trong đó.
        private void AuditBoard()
        {
            if (_auditIntervalSeconds <= 0f || Grid == null || _paintService == null) return;
            if (Time.unscaledTime < _nextAuditTime) return;

            _nextAuditTime = Time.unscaledTime + _auditIntervalSeconds;

            _currentMismatches.Clear();

            var confirmed = 0;

            // Đếm riêng từng nguồn. Ba con số này là thứ biến sự IM LẶNG thành bằng chứng:
            // im lặng mà không có số thì không phân biệt được "mọi thứ khớp" với "máy đo
            // không hề chạy" — mà hai kết luận đó dẫn đi hai hướng ngược nhau.
            var stateCount = 0;
            var cpuCount = 0;
            var gpuCount = 0;

            for (var y = 0; y < Grid.Height; y++)
            {
                for (var x = 0; x < Grid.Width; x++)
                {
                    var index = TexelIndexOf(x, y);

                    if (_paintService.IsPainted(x, y)) stateCount++;
                    if (_paintedPixels[index].a > 0) cpuCount++;
                    if (_paintedTexture.GetPixel(x * _texelsPerCell, (Grid.Height - 1 - y) * _texelsPerCell).a > 0f) gpuCount++;

                    if (!IsMismatched(x, y)) continue;

                    var cell = new Vector2Int(x, y);
                    _currentMismatches.Add(cell);

                    if (!_previousMismatches.Contains(cell)) continue;

                    Report(x, y, "lệch dai dẳng");
                    confirmed++;
                }
            }

            if (confirmed > 0)
            {
                Debug.LogWarning($"[BoardAudit] {confirmed} ô lệch qua hai lượt quét, " +
                                 $"trên tổng {Grid.Width * Grid.Height} ô.", this);
            }
            else
            {
                // In cả khi sạch. Ba số bằng nhau mà mắt vẫn thấy ô thiếu màu thì kết luận
                // đã chắc: DỮ LIỆU ĐÚNG, hỏng nằm ở khâu VẼ.
                Debug.Log($"[BoardAudit] quét {Grid.Width}x{Grid.Height} ô, khớp hết. " +
                          $"Đã tô: luật chơi {stateCount}, mảng CPU {cpuCount}, " +
                          $"texture {gpuCount}.", this);
            }

            // Hoán đổi hai tập thay vì chép nội dung: tập cũ thành chỗ chứa cho lượt sau.
            (_previousMismatches, _currentMismatches) = (_currentMismatches, _previousMismatches);
        }

        private bool IsMismatched(int x, int y)
        {
            if (_paintService == null || _paintedPixels == null || _paintedTexture == null) return false;

            var index = TexelIndexOf(x, y);

            // Texture2D dựng bằng code nên luôn đọc lại được, không cần bật Read/Write.
            var cpuHasColor = _paintedPixels[index].a > 0;
            var gpuHasColor = _paintedTexture.GetPixel(x * _texelsPerCell, (Grid.Height - 1 - y) * _texelsPerCell).a > 0f;

            return _paintService.IsPainted(x, y) != cpuHasColor || cpuHasColor != gpuHasColor;
        }

        /// In đủ ba nguồn để không phải đoán tầng nào hỏng.
        private void Report(int x, int y, string context)
        {
            var index = TexelIndexOf(x, y);

            var isPainted = _paintService.IsPainted(x, y);
            var cpu = _paintedPixels[index];
            var gpu = (Color32)_paintedTexture.GetPixel(x * _texelsPerCell, (Grid.Height - 1 - y) * _texelsPerCell);

            Debug.LogWarning(
                $"[BoardAudit/{context}] ô ({x},{y}): luật chơi={(isPainted ? "ĐÃ TÔ" : "chưa tô")}, " +
                $"mảng CPU={(cpu.a > 0 ? $"có màu {cpu}" : "TRONG SUỐT")}, " +
                $"texture={(gpu.a > 0 ? $"có màu {gpu}" : "TRONG SUỐT")}.", this);
        }

        private void Rebuild()
        {
            ReleaseTextures();

            // Gán trước mọi nhánh thoát: kể cả khi lưới hỏng, các lớp khác vẫn cần biết
            // cấu hình của màn hiện tại.
            Config = _levelService.CurrentConfig;

            // Hai nguyên nhân rất khác nhau, phải tách: "không tìm thấy màn" là hỏng ở
            // LevelManager hoặc ở tiến trình đã lưu, còn "thiếu GridData" là hỏng ở
            // chính asset LevelConfig. Gộp chung một câu là bắt người đọc đi mò.
            if (Config == null)
            {
                ClearBoard($"không có LevelConfig nào mang Level Id = {_levelService.CurrentLevel}. " +
                           "Kiểm tra mảng Levels của LevelManager, hoặc tiến trình đã lưu đang " +
                           "trỏ tới một màn chưa tồn tại.");
                return;
            }

            var data = _levelService.CurrentGrid;
            if (data == null)
            {
                ClearBoard($"'{Config.name}' (Level Id = {Config.LevelId}) chưa gán Grid Data");
                return;
            }

            var grid = data.ToGrid();
            if (grid == null)
            {
                ClearBoard($"'{data.name}' chưa được tool sinh dữ liệu lưới");
                return;
            }

            var colors = data.Colors;
            if (colors.Count == 0)
            {
                ClearBoard($"'{data.name}' không có màu nào — sinh lại bằng tool");
                return;
            }

            if (_paintedRenderer == null)
            {
                ClearBoard($"{nameof(BoardView)} chưa gán Painted Renderer");
                return;
            }

            Grid = grid;
            Colors = colors;
            Layout = new BoardLayout(grid.Width, grid.Height);

            BuildPixels();

            _unpaintedTexture = CreateTexture(TextureWidth, Grid.Height * _texelsPerCell, _unpaintedPixels, _generateMipmaps);
            _paintedTexture = CreateTexture(TextureWidth, Grid.Height * _texelsPerCell, _paintedPixels, _generateMipmaps);

            _unpaintedSprite = CreateSprite(_unpaintedTexture);
            _paintedSprite = CreateSprite(_paintedTexture);

            _unpaintedRenderer.sprite = _unpaintedSprite;
            _paintedRenderer.sprite = _paintedSprite;

            // Gọi SAU khi đã gán sprite: phép kiểm đo renderer.bounds, mà bounds chỉ có
            // nghĩa khi renderer đã có sprite.
            WarnOnLayerMisalignment();

            _isTextureDirty = false;

            OnBoardRebuilt?.Invoke();
        }

        private void BuildPixels()
        {
            var count = Grid.Width * Grid.Height * _texelsPerCell * _texelsPerCell;

            _unpaintedPixels = new Color32[count];
            _paintedPixels = new Color32[count];

            var reportedOutOfRange = false;

            for (var y = 0; y < Grid.Height; y++)
            {
                for (var x = 0; x < Grid.Width; x++)
                {
                    var index = Grid.GetCell(x, y);

                    var unpaintedColor = Transparent;
                    var paintedColor = Transparent;

                    if (index != PixelGrid.EmptyCell)
                    {
                        if (index >= 0 && index < Colors.Count)
                        {
                            // Ô đã tô từ phiên trước đi thẳng sang lớp màu thật. Không
                            // đợi RevealCell vì đâu có viên ngọc nào bay tới — người
                            // chơi chỉ mở lại game và thấy tranh đúng như lúc rời đi.
                            if (_paintService != null && _paintService.IsPainted(x, y))
                            {
                                paintedColor = Colors[index];
                            }
                            else
                            {
                                unpaintedColor = _grayscale
                                    ? BoardColors.ToGrayscale(Colors[index])
                                    : Colors[index];
                            }
                        }
                        else if (!reportedOutOfRange)
                        {
                            Debug.LogWarning(
                                $"Lưới có chỉ số màu {index} nhưng bảng màu chỉ có {Colors.Count} màu. " +
                                "Những ô đó vẽ trong suốt. Sinh lại lưới bằng tool để hết cảnh báo này.");
                            reportedOutOfRange = true;
                        }
                    }

                    WritePixel(_unpaintedPixels, x, y, unpaintedColor);
                    WritePixel(_paintedPixels, x, y, paintedColor);
                }
            }
        }

        /// Mỗi lớp texture phải phủ ĐÚNG vùng world mà BoardLayout mô tả.
        ///
        /// BoardLayout quy ước bảng căn giữa gốc toạ độ, mỗi ô rộng đúng một world unit —
        /// và mọi lớp khác đều tin vào quy ước đó: ngọc, gợi ý, số, viền ô đều đặt mình
        /// bằng CellToWorldCenter. Sprite của bảng chỉ khớp khi renderer nằm ở gốc với
        /// scale 1.
        ///
        /// So với TOẠ ĐỘ TUYỆT ĐỐI chứ không so hai lớp với nhau. Bản trước chỉ so tương
        /// đối nên bỏ lọt đúng trường hợp hay gặp nhất: object CHA bị dời hoặc bị scale,
        /// kéo cả hai lớp lệch đi cùng một lượng. Lúc đó hai lớp vẫn khít nhau hoàn hảo,
        /// phép kiểm im lặng, mà màu thì trượt khỏi ô — mỗi ô chỉ được phủ một phần, phần
        /// còn lại là màu của hàng xóm hoặc để trống.
        ///
        /// Triệu chứng rất khó lần bằng mắt vì mọi thứ khác vẫn đúng chỗ. Nên báo thẳng
        /// ra Console, kèm số đo.
        private void WarnOnLayerMisalignment()
        {
            var expected = Layout.WorldBounds;

            WarnIfBoundsWrong(_unpaintedRenderer, expected, "chưa tô");
            WarnIfBoundsWrong(_paintedRenderer, expected, "đã tô");
        }

        private static void WarnIfBoundsWrong(SpriteRenderer renderer, Bounds expected, string label)
        {
            if (renderer == null || renderer.sprite == null) return;

            var actual = renderer.bounds;

            var offset = (Vector2)(actual.center - expected.center);
            var sizeError = (Vector2)(actual.size - expected.size);

            // Bỏ qua sai lệch cỡ một phần trăm ô — đó là sai số dấu phẩy động, không phải
            // người ta đặt nhầm.
            if (offset.sqrMagnitude < 0.0001f && sizeError.sqrMagnitude < 0.0001f) return;

            Debug.LogWarning(
                $"Lớp bảng '{label}' ({renderer.name}) KHÔNG trùng lưới ô: lệch tâm {offset}, " +
                $"chênh kích thước {sizeError}. Bảng phải phủ đúng {expected.size.x} x " +
                $"{expected.size.y} world unit và căn giữa gốc toạ độ.\n" +
                "Kiểm theo thứ tự: Position của chính nó, rồi Position và Scale của MỌI " +
                "object cha — một cái cha lệch thôi là cả hai lớp cùng trượt, và phép so " +
                "hai lớp với nhau sẽ không phát hiện ra.",
                renderer);
        }

        /// Giữ lọc Point kể cả khi có mipmap: mipmap lo phần zoom XA, còn Point lo phần
        /// zoom GẦN. Đổi sang Bilinear để chữa cái thứ nhất sẽ làm hỏng cái thứ hai —
        /// ô mất cạnh sắc và nhoè sang nhau.
        private static Texture2D CreateTexture(int width, int height, Color32[] pixels, bool mipChain)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, mipChain)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };

            texture.SetPixels32(pixels);
            texture.Apply(mipChain);

            return texture;
        }

        /// Hai lớp phải dùng đúng cùng một cách dựng sprite, không thì chúng lệch nhau
        /// nửa ô và mọi thứ trông như bị nhoè viền.
        ///
        /// pixelsPerUnit = số texel mỗi ô, nên bảng vẫn rộng đúng Width x Height world
        /// unit dù texture mịn hơn. BoardLayout và mọi lớp khác không phải biết gì về
        /// chuyện độ mịn đã đổi.
        private Sprite CreateSprite(Texture2D texture)
        {
            return Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                _texelsPerCell);
        }

        /// Bề rộng texture tính bằng texel.
        private int TextureWidth => Grid.Width * _texelsPerCell;

        /// Chỉ số texel ở GÓC DƯỚI TRÁI của một ô, trong mảng pixel.
        ///
        /// PixelGrid có y = 0 ở trên, Texture2D có y = 0 ở dưới.
        private int TexelIndexOf(int x, int y)
        {
            return (Grid.Height - 1 - y) * _texelsPerCell * TextureWidth + x * _texelsPerCell;
        }

        /// Tô cả KHỐI texel của một ô, không phải một texel.
        ///
        /// Một texel một ô nghe thì gọn, nhưng nó không sống nổi ở hai đầu mức zoom. Zoom
        /// xa: lọc Point chỉ lấy một texel cho mỗi pixel màn hình nên ô bị bỏ sót hẳn; bật
        /// mipmap để chữa thì mip lại trộn ô đã tô với hàng xóm alpha 0 và ô nhạt dần đi.
        /// Cả hai đều là bệnh của việc không có dữ liệu để mà thu nhỏ.
        ///
        /// Có khối 4x4 thì thu nhỏ có cái thật để trung bình, còn phóng to vẫn sắc cạnh vì
        /// lọc vẫn là Point. Cái giá là bộ nhớ gấp 16 lần — với bảng 72x72 là 331 KB cho
        /// cả hai lớp, không đáng kể.
        private void WritePixel(Color32[] pixels, int x, int y, Color32 color)
        {
            var origin = TexelIndexOf(x, y);
            var width = TextureWidth;

            for (var row = 0; row < _texelsPerCell; row++)
            {
                var start = origin + row * width;

                for (var column = 0; column < _texelsPerCell; column++)
                {
                    pixels[start + column] = color;
                }
            }
        }

        private void ClearBoard(string reason)
        {
            Debug.LogWarning($"Không dựng được bảng: {reason}");

            _unpaintedRenderer.sprite = null;
            if (_paintedRenderer != null) _paintedRenderer.sprite = null;

            Grid = null;
            Colors = null;
            Layout = null;
            _unpaintedPixels = null;
            _paintedPixels = null;

            OnBoardRebuilt?.Invoke();
        }

        private void ReleaseTextures()
        {
            _isTextureDirty = false;

            // Toạ độ của lưới cũ không còn nghĩa gì với lưới mới.
            _revealedSinceUpload.Clear();
            _previousMismatches.Clear();
            _currentMismatches.Clear();

            DestroyIfAlive(ref _unpaintedSprite);
            DestroyIfAlive(ref _paintedSprite);
            DestroyIfAlive(ref _unpaintedTexture);
            DestroyIfAlive(ref _paintedTexture);
        }

        private void DestroyIfAlive<T>(ref T asset) where T : UnityEngine.Object
        {
            if (asset == null) return;

            Destroy(asset);
            asset = null;
        }
    }
}
