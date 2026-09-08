using System.Collections.Generic;
using JewelPainter.Gameplay.Domain;
using JewelPainter.Gameplay.Interfaces;
using TMPro;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

namespace JewelPainter.Gameplay.Board
{
    /// Hiện chỉ số bảng màu bằng MỘT mesh duy nhất cho cả bảng.
    ///
    /// Bản thay thế cho BoardNumberLayer. Cùng kết quả trên màn hình, khác hẳn ở cái giá:
    ///
    ///   BoardNumberLayer — một GameObject mỗi ô trong tầm nhìn. Bảng 101x105 ở mức zoom
    ///                      mà số bật lên là hơn 10.000 renderer, và mỗi frame engine đều
    ///                      phải cull, sắp xếp theo chiều sâu rồi dựng lệnh vẽ cho từng
    ///                      cái. Cái giá đó phải trả kể cả khi người chơi đứng yên.
    ///   BoardNumberMesh  — 1 renderer, 1 draw call, bất kể bảng bao nhiêu ô. Kéo và zoom
    ///                      không tốn một phép tính nào: không có object nào để cull, và
    ///                      mesh không phải dựng lại vì nó vốn đã chứa CẢ bảng.
    ///
    /// Đổi lại: mỗi lần nội dung đổi là dựng lại toàn bộ mesh. Nên việc đổi nội dung được
    /// gom lại và giãn ra bằng Rebuild Cooldown — xem tooltip của ô đó.
    ///
    /// KHÔNG tự dựng hình học chữ. Hình học được CHÉP từ chính TextMeshPro: lúc vào màn,
    /// lớp này dựng một TextMeshPro tạm, bảo nó viết từng con số rồi lấy nguyên mesh nó
    /// sinh ra làm khuôn. Nhờ vậy mọi kênh đỉnh mà shader của TMP cần đều đúng, không
    /// phải đoán — mà mấy kênh đó thì khác nhau giữa các phiên bản TMP và không có tài
    /// liệu nào chốt.
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class BoardNumberMesh : MonoBehaviour, IBoardNumbers
    {
        [SerializeField] private Camera _camera;

        [Tooltip("Prefab TextMeshPro dùng làm KHUÔN chữ. Dùng chung prefab với " +
                 "BoardNumberLayer được — lớp này chỉ mượn nó để lấy hình học và vật liệu, " +
                 "rồi huỷ ngay. Phải là TextMeshPro (bản 3D), không phải TextMeshProUGUI.\n\n" +
                 "Cỡ chữ, font, canh giữa, Order in Layer đều đọc từ prefab này, nên đổi " +
                 "prefab là đổi được cả hai bản cài đặt cùng lúc.")]
        [SerializeField] private TextMeshPro _numberPrefab;

        [Tooltip("Số hiện ra ở đâu trong dải zoom CỦA MÀN ĐÓ: 0 là ngay khi vào màn " +
                 "(Camera Max Size), 1 là mãi tới lúc lớp màu tan hết (Fade Switch Size). " +
                 "HẠ xuống thì số hiện sớm hơn. Chỉ có tác dụng khi LevelConfig có điền " +
                 "Fade Switch Size.\n\n" +
                 "Ở bản mesh này, đặt thấp KHÔNG còn đắt như bản cũ — số object không đổi. " +
                 "Nhưng số vẫn nên hiện đúng lúc ĐỌC ĐƯỢC, nếu không nó chỉ là nhiễu.")]
        [Range(0f, 1f)]
        [SerializeField] private float _showAtZoomProgress = 0.2f;

        [Tooltip("Đường lui khi LevelConfig để trống Fade Switch Size: ô chiếu lên màn hình " +
                 "nhỏ hơn ngần này pixel thì không hiện số.")]
        [SerializeField] private float _minCellScreenPixels = 32f;

        [Tooltip("Dải trễ quanh ngưỡng hiện số. Ở bản mesh, việc bật tắt chỉ là gạt một cờ " +
                 "renderer nên rẻ hơn hẳn bản cũ, nhưng dải trễ vẫn nên giữ: nhấp nháy " +
                 "quanh ngưỡng là thứ khó chịu về mặt nhìn, không chỉ về mặt hiệu năng.")]
        [Range(0f, 0.5f)]
        [SerializeField] private float _showHysteresis = 0.08f;

        [Tooltip("Màu chữ số. Đọc MỘT LẦN lúc dựng khuôn, nên đổi lúc đang chạy phải vào " +
                 "lại màn mới thấy.")]
        [SerializeField] private Color _numberColor = Color.black;

        [Tooltip("Gỡ số ở ô đã tô.\n\n" +
                 "Bỏ tick thì số nằm lại dưới viên ngọc. Ở bản mesh việc đó gần như KHÔNG " +
                 "tốn gì — vài đỉnh nằm khuất trong một mesh sẵn có — nên bỏ tick là cách " +
                 "tắt hẳn mọi lần dựng lại giữa lúc chơi, đổi lấy việc số vẫn còn đó nếu " +
                 "viên ngọc của bạn không phủ kín ô.")]
        [SerializeField] private bool _hideOnPainted = true;

        [Tooltip("Gom các lần dựng lại trong ngần này giây thành một.\n\n" +
                 "Mỗi ô tô xong là một lần nội dung đổi. Dựng lại ngay từng lần thì kéo tay " +
                 "tô liên tục — hoặc booster tô hết màu, 24 ô mỗi frame — sẽ dựng lại cả " +
                 "mesh mỗi frame, và đó là cách nhanh nhất để bản này còn chậm hơn bản cũ.\n\n" +
                 "0.12 giây là chờ chừng 7 frame. Con số dưới viên ngọc chậm biến mất ngần " +
                 "ấy thì không ai thấy, vì viên ngọc đã che nó rồi.\n\n" +
                 "Để 0 là dựng lại ngay mỗi lần đổi.")]
        [SerializeField] private float _rebuildCooldown = 0.12f;

        /// Hình học của MỘT con số, chép nguyên từ mesh mà TextMeshPro sinh ra.
        /// Toạ độ đỉnh đã nhân sẵn scale của prefab và lấy tâm ô làm gốc.
        private struct Stamp
        {
            public Vector3[] Vertices;
            public Vector3[] Normals;
            public Vector4[] Tangents;
            public Vector2[] Uv0;
            public Vector2[] Uv2;
            public Color32[] Colors;
            public int[] Triangles;
        }

        private readonly Dictionary<int, Stamp> _stamps = new();

        /// Bộ đệm dựng mesh, dùng lại qua mọi lần dựng. Cấp phát mới mỗi lần là vài trăm
        /// KB rác cho mỗi cú tô.
        private readonly List<Vector3> _vertices = new();
        private readonly List<Vector3> _normals = new();
        private readonly List<Vector4> _tangents = new();
        private readonly List<Vector2> _uv0 = new();
        private readonly List<Vector2> _uv2 = new();
        private readonly List<Color32> _colors = new();
        private readonly List<int> _triangles = new();

        private MeshFilter _filter;
        private MeshRenderer _renderer;
        private Mesh _mesh;

        private BoardView _boardView;
        private IPaintService _paintService;
        private JewelFlyEffect _flyEffect;

        private float _baseSize = -1f;
        private bool _needsBaseCapture = true;
        private bool _numbersShown;

        /// Nội dung đã đổi, chờ dựng lại.
        private bool _dirty;
        private float _nextRebuildTime;

        /// Khuôn đã dựng xong cho màn này. Chưa có thì không vẽ gì.
        private bool _hasStamps;

        private bool _prefabRejected;

        private static readonly ProfilerMarker RebuildMarker = new("JewelPainter.NumberMesh.Rebuild");

        public void Init(BoardView boardView, IPaintService paintService, JewelFlyEffect flyEffect)
        {
            _boardView = boardView;
            _paintService = paintService;
            _flyEffect = flyEffect;

            _filter = GetComponent<MeshFilter>();
            _renderer = GetComponent<MeshRenderer>();

            // Mesh sinh lúc chạy, KHÔNG phải asset. Unity không dọn giúp — phải tự huỷ ở
            // OnDestroy, không thì mỗi lần vào lại scene là bộ nhớ lớn thêm một nấc.
            _mesh = new Mesh { name = "BoardNumbers" };
            _mesh.MarkDynamic();
            _filter.sharedMesh = _mesh;

            _renderer.enabled = false;
            _renderer.shadowCastingMode = ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.lightProbeUsage = LightProbeUsage.Off;
            _renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

            _boardView.OnBoardRebuilt += HandleBoardRebuilt;
            _boardView.OnCoverChanged += HandleCoverChanged;

            if (_flyEffect != null) _flyEffect.OnJewelLanded += HandleJewelLanded;

            WarnIfTransformed();
        }

        private void OnDestroy()
        {
            if (_flyEffect != null) _flyEffect.OnJewelLanded -= HandleJewelLanded;

            if (_boardView != null)
            {
                _boardView.OnBoardRebuilt -= HandleBoardRebuilt;
                _boardView.OnCoverChanged -= HandleCoverChanged;
            }

            if (_mesh != null) Destroy(_mesh);
        }

        /// Đỉnh được ghi thẳng bằng toạ độ WORLD mà BoardLayout trả về, nên transform của
        /// chính object này phải là đơn vị. Lệch một chút là cả bảng số lệch theo, mà
        /// triệu chứng — số không nằm đúng ô — trông y hệt lỗi tính toạ độ.
        private void WarnIfTransformed()
        {
            var t = transform;

            var identity = t.position == Vector3.zero
                           && t.rotation == Quaternion.identity
                           && t.localScale == Vector3.one;

            if (identity) return;

            Debug.LogWarning(
                $"{nameof(BoardNumberMesh)} đang nằm trên một object có transform khác đơn vị " +
                $"(pos {t.position}, rot {t.rotation.eulerAngles}, scale {t.localScale}). " +
                "Lớp này ghi đỉnh bằng toạ độ world nên số sẽ lệch khỏi ô. " +
                "Đặt object về Position 0, Rotation 0, Scale 1.", this);
        }

        private void HandleCoverChanged()
        {
            // Không dựng lại gì — chỉ cần tắt renderer, và LateUpdate lo việc đó.
        }

        private void HandleJewelLanded(Vector2Int cell, int paletteIndex)
        {
            if (_hideOnPainted) _dirty = true;
        }

        private void HandleBoardRebuilt()
        {
            _hasStamps = false;
            _numbersShown = false;
            _needsBaseCapture = true;

            _mesh.Clear();
            _renderer.enabled = false;

            if (!BuildStamps()) return;

            _hasStamps = true;
            _dirty = true;
            _nextRebuildTime = 0f;
        }

        /// Dựng khuôn cho mọi con số màn này dùng, bằng cách nhờ chính TextMeshPro sinh
        /// hình học rồi chép lại.
        ///
        /// Chạy một lần mỗi màn, lúc màn hình chờ đang che. Mỗi con số là một lần SetText
        /// cộng ForceMeshUpdate — với 28 màu là 28 lần, không đáng kể so với hàng nghìn
        /// lần mà bản cũ phải làm.
        private bool BuildStamps()
        {
            _stamps.Clear();

            if (_prefabRejected) return false;

            if (_numberPrefab == null)
            {
                Debug.LogError($"{nameof(BoardNumberMesh)}: chưa gán Number Prefab.", this);
                _prefabRejected = true;
                return false;
            }

            var grid = _boardView.Grid;
            var colors = _boardView.Colors;
            if (grid == null || colors == null) return false;

            var source = Instantiate(_numberPrefab, transform);

            // HideInHierarchy thôi, KHÔNG phải HideAndDontSave: cờ DontSave nằm trong đó
            // làm object khó huỷ đúng cách, mà thứ duy nhất cần ở đây là nó đừng bày ra
            // giữa cây scene trong lúc sống chưa tới một frame.
            source.gameObject.hideFlags = HideFlags.HideInHierarchy;

            var sourceRenderer = source.GetComponent<MeshRenderer>();

            if (sourceRenderer == null)
            {
                Debug.LogError($"{nameof(BoardNumberMesh)}: Number Prefab không có MeshRenderer. " +
                               "Phải là TextMeshPro (bản 3D đặt thẳng trong world), không phải " +
                               "TextMeshProUGUI (bản dành cho Canvas).", this);

                _prefabRejected = true;
                Destroy(source.gameObject);
                return false;
            }

            source.color = _numberColor;

            // Scale của prefab KHÔNG nằm trong mesh mà TMP sinh ra — nó nằm ở transform.
            // Nướng sẵn vào khuôn, vì transform của mesh gộp phải là đơn vị.
            var scale = _numberPrefab.transform.localScale;

            var used = new HashSet<int>();

            for (var y = 0; y < grid.Height; y++)
            {
                for (var x = 0; x < grid.Width; x++)
                {
                    var index = grid.GetCell(x, y);
                    if (index == PixelGrid.EmptyCell) continue;
                    if (index < 0 || index >= colors.Count) continue;

                    used.Add(index + 1);
                }
            }

            foreach (var number in used)
            {
                source.SetText("{0}", number);
                source.ForceMeshUpdate();

                _stamps[number] = CaptureStamp(source.mesh, scale);
            }

            // Vật liệu và thứ tự vẽ đọc SAU vòng trên: TextMeshPro có thể đổi vật liệu ở
            // lần dựng lưới chữ đầu tiên, nên hỏi trước là hỏi phải bản cũ.
            //
            // Một font atlas là một vật liệu, nên cả bảng số gộp lại vẫn đúng một draw
            // call. Thiếu sortingOrder thì mesh này vẽ sai lớp so với bảng — kiểu sai nhìn
            // ra ngay nhưng khó đoán vì sao.
            _renderer.sharedMaterial = sourceRenderer.sharedMaterial;
            _renderer.sortingLayerID = sourceRenderer.sortingLayerID;
            _renderer.sortingOrder = sourceRenderer.sortingOrder;

            // Tắt renderer TRƯỚC khi huỷ. Destroy chỉ có hiệu lực ở cuối frame, nên không
            // tắt thì con số cuối cùng vừa viết sẽ loé lên một frame ngay giữa bảng.
            sourceRenderer.enabled = false;

            Destroy(source.gameObject);

            return _stamps.Count > 0;
        }

        /// Chép nguyên mọi kênh đỉnh, kể cả normal và tangent.
        ///
        /// Chép hết chứ không chọn lọc: các biến thể shader của TMP dùng những kênh khác
        /// nhau — bản có vát cạnh đọc normal và tangent, bản mobile thì không — và đoán
        /// sai một kênh cho ra chữ đen sì hoặc mất hẳn, không kèm lỗi nào.
        private static Stamp CaptureStamp(Mesh mesh, Vector3 scale)
        {
            var source = mesh.vertices;
            var vertices = new Vector3[source.Length];

            for (var i = 0; i < source.Length; i++)
            {
                vertices[i] = new Vector3(
                    source[i].x * scale.x,
                    source[i].y * scale.y,
                    source[i].z * scale.z);
            }

            return new Stamp
            {
                Vertices = vertices,
                Normals = mesh.normals,
                Tangents = mesh.tangents,
                Uv0 = mesh.uv,
                Uv2 = mesh.uv2,
                Colors = mesh.colors32,
                Triangles = mesh.triangles,
            };
        }

        private void LateUpdate()
        {
            if (!_hasStamps || _boardView == null || _boardView.Layout == null) return;

            if (_needsBaseCapture)
            {
                // Lấy ở LateUpdate chứ không trong handler, để BoardCamera kịp đặt lại mức
                // zoom cho bảng mới. Không phụ thuộc thứ tự đăng ký event.
                _baseSize = _camera.orthographicSize;
                _needsBaseCapture = false;
            }

            var show = !_boardView.IsCovered && ShouldShowNumbers();

            if (_renderer.enabled != show) _renderer.enabled = show;

            // Đang không hiện thì KHÔNG dựng lại, nhưng vẫn giữ cờ bẩn. Tô cả màn ở mức
            // zoom xa vì thế không tốn một lần dựng nào; tới lúc zoom vào mới dựng đúng
            // một lần.
            if (!show || !_dirty) return;

            if (_rebuildCooldown > 0f && Time.unscaledTime < _nextRebuildTime) return;

            _dirty = false;
            _nextRebuildTime = Time.unscaledTime + Mathf.Max(0f, _rebuildCooldown);

            Rebuild();
        }

        private void Rebuild()
        {
            using var _ = RebuildMarker.Auto();

            var grid = _boardView.Grid;
            var layout = _boardView.Layout;
            var colors = _boardView.Colors;

            if (grid == null || layout == null || colors == null) return;

            _vertices.Clear();
            _normals.Clear();
            _tangents.Clear();
            _uv0.Clear();
            _uv2.Clear();
            _colors.Clear();
            _triangles.Clear();

            for (var y = 0; y < grid.Height; y++)
            {
                for (var x = 0; x < grid.Width; x++)
                {
                    var index = grid.GetCell(x, y);
                    if (index == PixelGrid.EmptyCell) continue;
                    if (index < 0 || index >= colors.Count) continue;

                    if (_hideOnPainted && IsDone(x, y)) continue;

                    if (!_stamps.TryGetValue(index + 1, out var stamp)) continue;

                    Append(stamp, layout.CellToWorldCenter(x, y));
                }
            }

            _mesh.Clear();

            // 16 bit chỉ đánh số được 65.535 đỉnh — bảng 101x105 với số hai chữ là hơn
            // 80.000. Đặt TRƯỚC khi ghi đỉnh, không thì Unity chửi và cắt cụt mesh.
            _mesh.indexFormat = IndexFormat.UInt32;

            _mesh.SetVertices(_vertices);

            if (_normals.Count == _vertices.Count) _mesh.SetNormals(_normals);
            if (_tangents.Count == _vertices.Count) _mesh.SetTangents(_tangents);
            if (_uv0.Count == _vertices.Count) _mesh.SetUVs(0, _uv0);
            if (_uv2.Count == _vertices.Count) _mesh.SetUVs(1, _uv2);
            if (_colors.Count == _vertices.Count) _mesh.SetColors(_colors);

            // calculateBounds: false rồi đặt tay bằng khung bảng. Tự tính là quét lại cả
            // tám vạn đỉnh chỉ để ra đúng cái hình chữ nhật mình đã biết sẵn.
            _mesh.SetTriangles(_triangles, 0, false);
            _mesh.bounds = layout.WorldBounds;
        }

        private void Append(Stamp stamp, Vector2 center)
        {
            var start = _vertices.Count;
            var count = stamp.Vertices.Length;

            for (var i = 0; i < count; i++)
            {
                var v = stamp.Vertices[i];

                _vertices.Add(new Vector3(center.x + v.x, center.y + v.y, v.z));
            }

            if (stamp.Normals != null && stamp.Normals.Length == count)
            {
                for (var i = 0; i < count; i++) _normals.Add(stamp.Normals[i]);
            }

            if (stamp.Tangents != null && stamp.Tangents.Length == count)
            {
                for (var i = 0; i < count; i++) _tangents.Add(stamp.Tangents[i]);
            }

            if (stamp.Uv0 != null && stamp.Uv0.Length == count)
            {
                for (var i = 0; i < count; i++) _uv0.Add(stamp.Uv0[i]);
            }

            if (stamp.Uv2 != null && stamp.Uv2.Length == count)
            {
                for (var i = 0; i < count; i++) _uv2.Add(stamp.Uv2[i]);
            }

            if (stamp.Colors != null && stamp.Colors.Length == count)
            {
                for (var i = 0; i < count; i++) _colors.Add(stamp.Colors[i]);
            }

            var triangles = stamp.Triangles;

            for (var i = 0; i < triangles.Length; i++) _triangles.Add(start + triangles[i]);
        }

        /// Xem chú thích cùng tên ở BoardNumberLayer.
        private bool IsDone(int x, int y)
        {
            if (_paintService == null || !_paintService.IsPainted(x, y)) return false;

            return _flyEffect == null || !_flyEffect.IsInFlight(new Vector2Int(x, y));
        }

        /// Xem chú thích cùng tên ở BoardNumberLayer — luật giống hệt, chỉ khác là ở đây
        /// nó chỉ gạt một cờ renderer thay vì thu về cả nghìn object.
        private bool ShouldShowNumbers()
        {
            var threshold = ResolveShowSize();
            if (threshold <= 0f) return false;

            var ceiling = Mathf.Max(threshold, _baseSize);
            var limit = _numbersShown
                ? Mathf.Lerp(threshold, ceiling, Mathf.Clamp01(_showHysteresis))
                : threshold;

            _numbersShown = _camera.orthographicSize <= limit;

            return _numbersShown;
        }

        /// Xem chú thích cùng tên ở BoardNumberLayer.
        private float ResolveShowSize()
        {
            var config = _boardView.Config;
            var fadeSwitchSize = config != null ? config.FadeSwitchSize : 0f;

            if (fadeSwitchSize > 0f && _baseSize > 0f)
            {
                return Mathf.Lerp(_baseSize, fadeSwitchSize, _showAtZoomProgress);
            }

            if (_minCellScreenPixels <= 0f) return 0f;

            return Screen.height / (2f * _minCellScreenPixels);
        }
    }
}
