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
    /// pixelsPerUnit = 1 nên một pixel là một ô là một world unit.
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

        [Header("Chẩn đoán")]
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

                _unpaintedTexture.SetPixels32(_unpaintedPixels);
                _unpaintedTexture.Apply(false);

                _paintedTexture.SetPixels32(_paintedPixels);
                _paintedTexture.Apply(false);

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

            for (var y = 0; y < Grid.Height; y++)
            {
                for (var x = 0; x < Grid.Width; x++)
                {
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

            // Hoán đổi hai tập thay vì chép nội dung: tập cũ thành chỗ chứa cho lượt sau.
            (_previousMismatches, _currentMismatches) = (_currentMismatches, _previousMismatches);
        }

        private bool IsMismatched(int x, int y)
        {
            if (_paintService == null || _paintedPixels == null || _paintedTexture == null) return false;

            var index = (Grid.Height - 1 - y) * Grid.Width + x;

            // Texture2D dựng bằng code nên luôn đọc lại được, không cần bật Read/Write.
            var cpuHasColor = _paintedPixels[index].a > 0;
            var gpuHasColor = _paintedTexture.GetPixel(x, Grid.Height - 1 - y).a > 0f;

            return _paintService.IsPainted(x, y) != cpuHasColor || cpuHasColor != gpuHasColor;
        }

        /// In đủ ba nguồn để không phải đoán tầng nào hỏng.
        private void Report(int x, int y, string context)
        {
            var index = (Grid.Height - 1 - y) * Grid.Width + x;

            var isPainted = _paintService.IsPainted(x, y);
            var cpu = _paintedPixels[index];
            var gpu = (Color32)_paintedTexture.GetPixel(x, Grid.Height - 1 - y);

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

            WarnOnLayerMisalignment();

            BuildPixels();

            _unpaintedTexture = CreateTexture(grid.Width, grid.Height, _unpaintedPixels);
            _paintedTexture = CreateTexture(grid.Width, grid.Height, _paintedPixels);

            _unpaintedSprite = CreateSprite(_unpaintedTexture, grid.Width, grid.Height);
            _paintedSprite = CreateSprite(_paintedTexture, grid.Width, grid.Height);

            _unpaintedRenderer.sprite = _unpaintedSprite;
            _paintedRenderer.sprite = _paintedSprite;

            PushCellCountToMaterial();

            _isTextureDirty = false;

            OnBoardRebuilt?.Invoke();
        }

        private void BuildPixels()
        {
            var count = Grid.Width * Grid.Height;

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

        /// Hai lớp texture phải nằm CHỒNG KHÍT lên nhau.
        ///
        /// Chúng là hai SpriteRenderer riêng, dựng từ cùng một cách nên cùng kích thước
        /// world. Nhưng nếu object con `Painted` bị lệch vị trí hoặc khác scale thì màu
        /// đã tô vẽ trượt khỏi ô của nó: có ô hiện màu của hàng xóm, có ô trông như
        /// không được tô, và mép ô nào cũng chỉ phủ một phần.
        ///
        /// Triệu chứng đó rất khó lần ra bằng mắt vì mọi thứ khác vẫn đúng — ngọc vẫn
        /// hiện, số vẫn hiện, chỉ riêng lớp màu lệch đi. Nên báo thẳng ra Console.
        private void WarnOnLayerMisalignment()
        {
            if (_unpaintedRenderer == null || _paintedRenderer == null) return;

            var a = _unpaintedRenderer.transform;
            var b = _paintedRenderer.transform;

            var offset = b.position - a.position;
            var scaleRatio = b.lossyScale - a.lossyScale;

            // Bỏ qua sai lệch cỡ một phần nghìn ô — đó là sai số dấu phẩy động, không
            // phải người ta đặt nhầm.
            if (offset.sqrMagnitude < 0.0001f && scaleRatio.sqrMagnitude < 0.0001f) return;

            Debug.LogWarning(
                $"Hai lớp bảng KHÔNG chồng khít: '{b.name}' lệch {offset} và chênh scale " +
                $"{scaleRatio} so với '{a.name}'. Màu đã tô sẽ vẽ trượt khỏi ô của nó. " +
                "Đặt Position của object lớp đã tô về (0, 0, 0) và Scale về (1, 1, 1).",
                _paintedRenderer);
        }

        /// Đưa kích thước lưới sang shader BoardGloss, để nó biết một ô rộng bao nhiêu
        /// phần của bảng mà đặt điểm sáng vào giữa ô.
        ///
        /// Shader tự suy được con số này từ _MainTex_TexelSize — texture bảng đúng một
        /// pixel một ô. Nhưng SpriteRenderer gán texture qua đường riêng của nó, và Unity
        /// không đảm bảo đổ _TexelSize theo đường đó. Đẩy tường minh thì hết cửa hỏng.
        ///
        /// Đi qua MaterialPropertyBlock chứ không đụng `.material`: đọc `.material` sinh
        /// một bản sao material cho riêng renderer này, và bản sao đó rò ra mỗi lần vào màn.
        ///
        /// Material không dùng shader BoardGloss thì thuộc tính này bị bỏ qua, không lỗi.
        private void PushCellCountToMaterial()
        {
            if (_paintedRenderer == null || Grid == null) return;

            // Tạo ở LẦN GỌI ĐẦU, không phải ở khởi tạo trường.
            //
            // Trường static có giá trị khởi tạo sẵn sẽ chạy trong type initializer, mà
            // Unity kích hoạt nó từ ngữ cảnh constructor của MonoBehaviour — nơi cấm gọi
            // mọi API dựng object của engine. Kết quả là TypeInitializationException nuốt
            // trọn cả class: không chỉ dòng này hỏng, mà BoardView không dùng được nữa.
            _materialProperties ??= new MaterialPropertyBlock();

            _paintedRenderer.GetPropertyBlock(_materialProperties);
            _materialProperties.SetVector(CellCountId, new Vector4(Grid.Width, Grid.Height, 0f, 0f));
            _paintedRenderer.SetPropertyBlock(_materialProperties);
        }

        private static readonly int CellCountId = Shader.PropertyToID("_CellCount");

        /// Dùng chung một khối cho mọi lần gọi — cấp phát mới mỗi lần là rác không cần thiết.
        private static MaterialPropertyBlock _materialProperties;

        private static Texture2D CreateTexture(int width, int height, Color32[] pixels)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };

            texture.SetPixels32(pixels);
            texture.Apply(false);

            return texture;
        }

        /// Hai lớp phải dùng đúng cùng một cách dựng sprite, không thì chúng lệch nhau
        /// nửa ô và mọi thứ trông như bị nhoè viền.
        private static Sprite CreateSprite(Texture2D texture, int width, int height)
        {
            return Sprite.Create(
                texture,
                new Rect(0f, 0f, width, height),
                new Vector2(0.5f, 0.5f),
                1f);
        }

        /// PixelGrid có y = 0 ở trên, Texture2D có y = 0 ở dưới.
        private void WritePixel(Color32[] pixels, int x, int y, Color32 color)
        {
            pixels[(Grid.Height - 1 - y) * Grid.Width + x] = color;
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
