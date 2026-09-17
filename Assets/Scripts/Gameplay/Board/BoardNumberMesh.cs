using System.Collections.Generic;
using JewelPainter.Gameplay.Domain;
using JewelPainter.Gameplay.Interfaces;
using TMPro;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

namespace JewelPainter.Gameplay.Board
{
    /// Hiện chỉ số bảng màu bằng một mesh duy nhất cho cả bảng.
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class BoardNumberMesh : MonoBehaviour, IBoardNumbers
    {
        [SerializeField] private Camera _camera;

        [Tooltip("Prefab TextMeshPro dùng làm khuôn chữ.")]
        [SerializeField] private TextMeshPro _numberPrefab;

        [Tooltip("Vị trí trong dải zoom của màn mà số bắt đầu hiện (0 = lúc vào màn, 1 = lúc lớp màu tan hết).")]
        [Range(0f, 1f)]
        [SerializeField] private float _showAtZoomProgress = 0.2f;

        [Tooltip("Ô nhỏ hơn ngần này pixel trên màn hình thì không hiện số khi LevelConfig không đặt Fade Switch Size.")]
        [SerializeField] private float _minCellScreenPixels = 32f;

        [Tooltip("Dải trễ quanh ngưỡng hiện số.")]
        [Range(0f, 0.5f)]
        [SerializeField] private float _showHysteresis = 0.08f;

        [Tooltip("Màu chữ số.")]
        [SerializeField] private Color _numberColor = Color.black;

        [Tooltip("Gỡ số ở ô đã tô.")]
        [SerializeField] private bool _hideOnPainted = true;

        [Tooltip("Gom các lần dựng lại trong ngần này giây thành một.")]
        [SerializeField] private float _rebuildCooldown = 0.12f;

        /// Hình học của một con số.
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

        private bool _dirty;
        private float _nextRebuildTime;

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

        /// Cảnh báo khi transform của object không phải đơn vị.
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

        /// Dựng khuôn hình học cho mọi con số màn này dùng.
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

            _renderer.sharedMaterial = sourceRenderer.sharedMaterial;
            _renderer.sortingLayerID = sourceRenderer.sortingLayerID;
            _renderer.sortingOrder = sourceRenderer.sortingOrder;

            sourceRenderer.enabled = false;

            Destroy(source.gameObject);

            return _stamps.Count > 0;
        }

        /// Chép hình học của một con số từ TextMeshPro.
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
                _baseSize = _camera.orthographicSize;
                _needsBaseCapture = false;
            }

            var show = !_boardView.IsCovered && ShouldShowNumbers();

            if (_renderer.enabled != show) _renderer.enabled = show;

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

            _mesh.indexFormat = IndexFormat.UInt32;

            _mesh.SetVertices(_vertices);

            if (_normals.Count == _vertices.Count) _mesh.SetNormals(_normals);
            if (_tangents.Count == _vertices.Count) _mesh.SetTangents(_tangents);
            if (_uv0.Count == _vertices.Count) _mesh.SetUVs(0, _uv0);
            if (_uv2.Count == _vertices.Count) _mesh.SetUVs(1, _uv2);
            if (_colors.Count == _vertices.Count) _mesh.SetColors(_colors);

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

        /// Ô đã tô và viên ngọc đã đáp.
        private bool IsDone(int x, int y)
        {
            if (_paintService == null || !_paintService.IsPainted(x, y)) return false;

            return _flyEffect == null || !_flyEffect.IsInFlight(new Vector2Int(x, y));
        }

        /// Có hiện số ở mức zoom hiện tại không.
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

        /// orthographicSize mà tại đó số bắt đầu hiện.
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
