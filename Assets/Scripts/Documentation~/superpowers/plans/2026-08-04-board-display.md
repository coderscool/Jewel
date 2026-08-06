# Hiển thị lưới ô màu kèm số — kế hoạch triển khai

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Khi `LevelManager` nạp màn chơi, dựng trong world một bảng ô màu lấy từ `LevelGridData`, mỗi ô hiện chỉ số bảng màu, người chơi zoom và kéo được.

**Architecture:** Màu vẽ bằng một `Texture2D` đúng kích thước lưới trên một `SpriteRenderer` với `pixelsPerUnit = 1` — một pixel là một ô là một world unit, nên toàn bộ ô chỉ tốn một draw call. Số vẽ bằng pool `TextMeshPro` chỉ sinh cho ô đang lọt tầm nhìn. Phần toán toạ độ tách hẳn ra `BoardLayout` thuần C#.

**Tech Stack:** Unity 6000.0.67f1, Input System mới (`UnityEngine.InputSystem`), TextMeshPro, VContainer.

## Global Constraints

- Namespace gốc `JewelPainter.*`, thư mục ↔ namespace 1:1.
- Chiều phụ thuộc `Bootstrap → UI → Gameplay → Core`. `Gameplay/Board/` không `using` lên `UI/`.
- Không `public` field ngoài `const` và field của `[Serializable] struct Entry`.
- Một file một type public, tên file trùng tên type.
- `Domain/` và các class thuần C# được dùng struct giá trị của Unity (`Color32`, `Vector2Int`, `Rect`, `Bounds`), không được dùng MonoBehaviour hay ScriptableObject.
- Huỷ đăng ký event trong `OnDestroy`.
- Texture sinh lúc chạy phải `Destroy` khi dựng lại và trong `OnDestroy`.
- Kích thước lưới đọc từ `LevelGridData.Width/Height` — **không hard-code 32** ở bất kỳ đâu.
- Số hiển thị là chỉ số palette **+ 1**.
- Ngưỡng ẩn số: ô chiếu lên màn hình nhỏ hơn **14 pixel**.
- Zoom gần nhất: thấy khoảng **5 ô**. Zoom xa nhất: trọn bảng cộng lề **10%**.
- Input dùng `UnityEngine.InputSystem` — không dùng `Input.GetAxis` hay `Input.touches`.
- Tạo file `.cs` mới bằng công cụ ngoài Unity thì an toàn; **di chuyển hoặc đổi tên** file đã có thì phải làm trong cửa sổ Project của Unity.

**Kiểm chứng:** người dùng chạy Unity thủ công. Mỗi task kết thúc bằng một bước kiểm bằng mắt, không phải chạy test tự động. Test EditMode cho `BoardLayout` nằm ở phụ lục cuối file, dùng khi bật lại test.

---

## Cấu trúc file

| File | Trách nhiệm |
|---|---|
| `Gameplay/Board/BoardLayout.cs` | Thuần C#: đổi ô ↔ world, tính ô nào lọt tầm nhìn, bounds của bảng. |
| `Gameplay/Board/BoardView.cs` | Dựng `Texture2D` màu từ `LevelGridData`, gắn lên `SpriteRenderer`, phát sự kiện dựng xong. |
| `Gameplay/Board/BoardNumberLayer.cs` | Pool `TextMeshPro`, sinh số cho ô đang nhìn thấy, chọn màu chữ tương phản. |
| `Gameplay/Board/BoardCamera.cs` | Zoom bằng cuộn chuột và chụm hai ngón, kéo bằng chuột trái và một ngón, kẹp biên. |
| `Gameplay/Interfaces/ILevelService.cs` | *(sửa)* thêm `CurrentGrid`. |
| `Gameplay/Managers/LevelManager.cs` | *(sửa)* hiện thực `CurrentGrid`. |
| `Gameplay/JewelPainter.Gameplay.asmdef` | *(sửa)* thêm `Unity.InputSystem`, `Unity.TextMeshPro`. |
| `Bootstrap/GameLifetimeScope.cs` | *(sửa)* đăng ký ba MonoBehaviour của board. |
| `Bootstrap/GameEntryPoint.cs` | *(sửa)* gọi `Init` cho chúng. |

**Hệ toạ độ — chỗ dễ sai nhất trong plan này.** Có ba hệ:

- `PixelGrid`: `y = 0` là hàng **trên cùng**
- `Texture2D`: `y = 0` là hàng **dưới cùng** → `BoardView` lật khi ghi pixel
- World: `+y` hướng **lên** → `BoardLayout.CellToWorldCenter` trả y giảm dần khi `cellY` tăng

---

### Task 1: `BoardLayout`

**Files:**
- Create: `Assets/Scripts/Gameplay/Board/BoardLayout.cs`

**Interfaces:**
- Consumes: không có
- Produces: `class BoardLayout` với constructor `BoardLayout(int width, int height)`, `int Width { get; }`, `int Height { get; }`, `Bounds WorldBounds { get; }`, `Vector2 CellToWorldCenter(int x, int y)`, `bool TryWorldToCell(Vector2 world, out Vector2Int cell)`, `RectInt VisibleCells(Rect viewportWorldRect)`

- [ ] **Step 1: Viết `BoardLayout`**

Tạo `Assets/Scripts/Gameplay/Board/BoardLayout.cs`:

```csharp
using System;
using UnityEngine;

namespace JewelPainter.Gameplay.Board
{
    /// Toán toạ độ của bảng. Thuần C# — không MonoBehaviour, không camera, không texture.
    ///
    /// Bảng căn giữa gốc toạ độ, mỗi ô rộng đúng một world unit.
    /// Quy ước: ô (0, 0) nằm ở góc TRÊN BÊN TRÁI, khớp với PixelGrid.
    /// World có +y hướng lên, nên cellY tăng thì world y giảm.
    public class BoardLayout
    {
        public BoardLayout(int width, int height)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width), width, "Phải dương");
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height), height, "Phải dương");

            Width = width;
            Height = height;
        }

        public int Width { get; }
        public int Height { get; }

        public Bounds WorldBounds => new Bounds(Vector3.zero, new Vector3(Width, Height, 0f));

        public Vector2 CellToWorldCenter(int x, int y)
        {
            var worldX = x - Width / 2f + 0.5f;
            var worldY = Height / 2f - y - 0.5f;

            return new Vector2(worldX, worldY);
        }

        /// false nếu điểm nằm ngoài bảng. cell vẫn được gán để bên gọi xem được nó lệch đâu.
        public bool TryWorldToCell(Vector2 world, out Vector2Int cell)
        {
            var x = Mathf.FloorToInt(world.x + Width / 2f);
            var y = Mathf.FloorToInt(Height / 2f - world.y);

            cell = new Vector2Int(x, y);

            return x >= 0 && x < Width && y >= 0 && y < Height;
        }

        /// Giao của tầm nhìn với bảng, đã kẹp trong biên. Trả hình chữ nhật rỗng
        /// nếu tầm nhìn nằm hẳn ngoài bảng.
        public RectInt VisibleCells(Rect viewportWorldRect)
        {
            var minX = Mathf.FloorToInt(viewportWorldRect.xMin + Width / 2f);
            var maxX = Mathf.CeilToInt(viewportWorldRect.xMax + Width / 2f);

            // world y lớn ứng với cell y nhỏ, nên hai đầu đảo nhau
            var minY = Mathf.FloorToInt(Height / 2f - viewportWorldRect.yMax);
            var maxY = Mathf.CeilToInt(Height / 2f - viewportWorldRect.yMin);

            minX = Mathf.Clamp(minX, 0, Width);
            maxX = Mathf.Clamp(maxX, 0, Width);
            minY = Mathf.Clamp(minY, 0, Height);
            maxY = Mathf.Clamp(maxY, 0, Height);

            return new RectInt(minX, minY, Mathf.Max(0, maxX - minX), Mathf.Max(0, maxY - minY));
        }
    }
}
```

- [ ] **Step 2: Kiểm biên dịch**

Quay lại Unity, đợi import. Console phải sạch lỗi.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Gameplay/Board/BoardLayout.cs Assets/Scripts/Gameplay/Board/BoardLayout.cs.meta
git commit -m "feat: thêm BoardLayout tính toạ độ bảng"
```

---

### Task 2: Mở đường lấy dữ liệu lưới

**Files:**
- Modify: `Assets/Scripts/Gameplay/JewelPainter.Gameplay.asmdef`
- Modify: `Assets/Scripts/Gameplay/Interfaces/ILevelService.cs`
- Modify: `Assets/Scripts/Gameplay/Managers/LevelManager.cs`

**Interfaces:**
- Consumes: `LevelGridData` (đã có ở `Gameplay/Data/`)
- Produces: `ILevelService.CurrentGrid` kiểu `LevelGridData` — `BoardView` ở Task 3 đọc property này

- [ ] **Step 1: Thêm reference vào asmdef**

Sửa `Assets/Scripts/Gameplay/JewelPainter.Gameplay.asmdef`, thay khối `references`:

```json
    "references": [
        "JewelPainter.Core",
        "VContainer",
        "Unity.InputSystem",
        "Unity.TextMeshPro"
    ],
```

Không đổi gì khác trong file.

- [ ] **Step 2: Thêm `CurrentGrid` vào `ILevelService`**

Sửa `Assets/Scripts/Gameplay/Interfaces/ILevelService.cs` thành:

```csharp
using System;
using JewelPainter.Gameplay.Data;

namespace JewelPainter.Gameplay.Interfaces
{
    /// Contract do Gameplay tự định nghĩa. UI phụ thuộc interface này,
    /// Gameplay không bao giờ using ngược lên UI.
    public interface ILevelService
    {
        int CurrentLevel { get; }

        /// Dữ liệu lưới của màn đang chơi. null nếu màn chưa nạp hoặc chưa gán GridData.
        LevelGridData CurrentGrid { get; }

        event Action<int> OnLevelStarted;
        event Action<int> OnLevelCompleted;

        void LoadLevel(int levelId);
        void CompleteCurrentLevel();
    }
}
```

- [ ] **Step 3: Hiện thực `CurrentGrid` ở `LevelManager`**

Trong `Assets/Scripts/Gameplay/Managers/LevelManager.cs`, thêm `using` và một property.

Thêm vào khối using:

```csharp
using JewelPainter.Gameplay.Data;
```

Thêm ngay dưới dòng `public LevelConfig CurrentConfig => _currentConfig;`:

```csharp
        public LevelGridData CurrentGrid => _currentConfig != null ? _currentConfig.GridData : null;
```

Không dùng `_currentConfig?.GridData` — `LevelConfig` là `UnityEngine.Object`, toán tử `?.` bỏ qua phép so sánh null của Unity nên object đã bị huỷ vẫn lọt qua.

- [ ] **Step 4: Kiểm biên dịch**

Quay lại Unity. Console phải sạch lỗi.

Nếu báo `The type or namespace name 'InputSystem' does not exist` thì package Input System chưa cài: `Window > Package Manager > Unity Registry > Input System > Install`.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Gameplay/JewelPainter.Gameplay.asmdef
git add Assets/Scripts/Gameplay/Interfaces/ILevelService.cs
git add Assets/Scripts/Gameplay/Managers/LevelManager.cs
git commit -m "feat: mở đường lấy LevelGridData qua ILevelService"
```

---

### Task 3: `BoardView`

**Files:**
- Create: `Assets/Scripts/Gameplay/Board/BoardView.cs`

**Interfaces:**
- Consumes: `BoardLayout` (Task 1), `ILevelService.CurrentGrid` (Task 2), `LevelGridData.ToGrid()`, `LevelGridData.Palette`, `PixelGrid.EmptyCell`, `JewelPalette.Colors`
- Produces: `class BoardView : MonoBehaviour` với `void Init(ILevelService levelService)`, `BoardLayout Layout { get; }`, `PixelGrid Grid { get; }`, `IReadOnlyList<Color32> Colors { get; }`, `event Action OnBoardRebuilt` — cả ba thuộc tính trả null khi chưa có bảng

- [ ] **Step 1: Viết `BoardView`**

Tạo `Assets/Scripts/Gameplay/Board/BoardView.cs`:

```csharp
using System;
using System.Collections.Generic;
using JewelPainter.Gameplay.Domain;
using JewelPainter.Gameplay.Interfaces;
using UnityEngine;

namespace JewelPainter.Gameplay.Board
{
    /// Dựng toàn bộ ô màu thành MỘT texture rồi gắn lên một SpriteRenderer.
    /// pixelsPerUnit = 1 nên một pixel là một ô là một world unit.
    [RequireComponent(typeof(SpriteRenderer))]
    public class BoardView : MonoBehaviour
    {
        private static readonly Color32 Transparent = new Color32(0, 0, 0, 0);

        [SerializeField] private SpriteRenderer _renderer;

        private ILevelService _levelService;
        private Texture2D _texture;
        private Sprite _sprite;

        public BoardLayout Layout { get; private set; }
        public PixelGrid Grid { get; private set; }
        public IReadOnlyList<Color32> Colors { get; private set; }

        public event Action OnBoardRebuilt;

        /// Bootstrap gọi. Chỉ đăng ký lắng nghe — bảng dựng khi màn chơi được nạp.
        public void Init(ILevelService levelService)
        {
            _levelService = levelService;
            _levelService.OnLevelStarted += HandleLevelStarted;
        }

        private void OnDestroy()
        {
            if (_levelService != null) _levelService.OnLevelStarted -= HandleLevelStarted;

            ReleaseTexture();
        }

        private void HandleLevelStarted(int levelId) => Rebuild();

        private void Rebuild()
        {
            ReleaseTexture();

            var data = _levelService.CurrentGrid;
            if (data == null)
            {
                ClearBoard("LevelConfig của màn này chưa gán GridData");
                return;
            }

            var grid = data.ToGrid();
            if (grid == null)
            {
                ClearBoard($"'{data.name}' chưa được tool sinh dữ liệu lưới");
                return;
            }

            if (data.Palette == null)
            {
                ClearBoard($"'{data.name}' chưa gán JewelPalette");
                return;
            }

            var colors = data.Palette.Colors;
            var pixels = BuildPixels(grid, colors);

            _texture = new Texture2D(grid.Width, grid.Height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            _texture.SetPixels32(pixels);
            _texture.Apply();

            _sprite = Sprite.Create(
                _texture,
                new Rect(0f, 0f, grid.Width, grid.Height),
                new Vector2(0.5f, 0.5f),
                1f);

            _renderer.sprite = _sprite;

            Grid = grid;
            Colors = colors;
            Layout = new BoardLayout(grid.Width, grid.Height);

            OnBoardRebuilt?.Invoke();
        }

        private static Color32[] BuildPixels(PixelGrid grid, IReadOnlyList<Color32> colors)
        {
            var pixels = new Color32[grid.Width * grid.Height];
            var reportedOutOfRange = false;

            for (var y = 0; y < grid.Height; y++)
            {
                for (var x = 0; x < grid.Width; x++)
                {
                    var index = grid.GetCell(x, y);
                    var color = Transparent;

                    if (index != PixelGrid.EmptyCell)
                    {
                        if (index >= 0 && index < colors.Count)
                        {
                            color = colors[index];
                        }
                        else if (!reportedOutOfRange)
                        {
                            // Xảy ra khi bảng màu bị xoá bớt sau lúc sinh lưới.
                            Debug.LogWarning(
                                $"Lưới có chỉ số màu {index} nhưng bảng màu chỉ có {colors.Count} màu. " +
                                "Những ô đó vẽ trong suốt. Sinh lại lưới bằng tool để hết cảnh báo này.");
                            reportedOutOfRange = true;
                        }
                    }

                    // PixelGrid có y = 0 ở trên, Texture2D có y = 0 ở dưới
                    pixels[(grid.Height - 1 - y) * grid.Width + x] = color;
                }
            }

            return pixels;
        }

        private void ClearBoard(string reason)
        {
            Debug.LogWarning($"Không dựng được bảng: {reason}");

            _renderer.sprite = null;
            Grid = null;
            Colors = null;
            Layout = null;

            OnBoardRebuilt?.Invoke();
        }

        private void ReleaseTexture()
        {
            if (_sprite != null)
            {
                Destroy(_sprite);
                _sprite = null;
            }

            if (_texture != null)
            {
                Destroy(_texture);
                _texture = null;
            }
        }
    }
}
```

- [ ] **Step 2: Kiểm biên dịch**

Quay lại Unity. Console phải sạch lỗi.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Gameplay/Board/BoardView.cs Assets/Scripts/Gameplay/Board/BoardView.cs.meta
git commit -m "feat: thêm BoardView dựng texture màu từ LevelGridData"
```

---

### Task 4: `BoardNumberLayer`

**Files:**
- Create: `Assets/Scripts/Gameplay/Board/BoardNumberLayer.cs`

**Interfaces:**
- Consumes: `BoardView.Layout`, `BoardView.Grid`, `BoardView.Colors`, `BoardView.OnBoardRebuilt` (Task 3), `BoardLayout.VisibleCells`, `BoardLayout.CellToWorldCenter` (Task 1), `PixelGrid.EmptyCell`
- Produces: `class BoardNumberLayer : MonoBehaviour` với `void Init(BoardView boardView)`

- [ ] **Step 1: Viết `BoardNumberLayer`**

Tạo `Assets/Scripts/Gameplay/Board/BoardNumberLayer.cs`:

```csharp
using System.Collections.Generic;
using JewelPainter.Gameplay.Domain;
using TMPro;
using UnityEngine;

namespace JewelPainter.Gameplay.Board
{
    /// Hiện chỉ số bảng màu lên từng ô.
    ///
    /// Chỉ sinh TextMeshPro cho ô đang lọt tầm nhìn camera, và chỉ tính lại khi
    /// camera đổi — không phải mỗi frame. Ô nhỏ hơn ngưỡng đọc được thì ẩn hết số.
    public class BoardNumberLayer : MonoBehaviour
    {
        /// Ô chiếu lên màn hình nhỏ hơn ngần này pixel thì không hiện số.
        private const float MinCellScreenPixels = 14f;

        /// Độ sáng để chọn chữ đen hay chữ trắng cho tương phản.
        private const float DarkTextLuminanceThreshold = 140f;

        [SerializeField] private Camera _camera;
        [SerializeField] private TextMeshPro _numberPrefab;
        [SerializeField] private Transform _root;

        private readonly Dictionary<Vector2Int, TextMeshPro> _active = new();
        private readonly Stack<TextMeshPro> _pool = new();
        private readonly List<Vector2Int> _toRelease = new();

        private BoardView _boardView;
        private Vector3 _lastCameraPosition;
        private float _lastOrthographicSize = -1f;

        public void Init(BoardView boardView)
        {
            _boardView = boardView;
            _boardView.OnBoardRebuilt += HandleBoardRebuilt;
        }

        private void OnDestroy()
        {
            if (_boardView != null) _boardView.OnBoardRebuilt -= HandleBoardRebuilt;
        }

        private void HandleBoardRebuilt()
        {
            ReleaseAll();
            _lastOrthographicSize = -1f;   // ép tính lại ở LateUpdate kế tiếp
        }

        private void LateUpdate()
        {
            if (_boardView == null || _boardView.Layout == null) return;
            if (!HasCameraChanged()) return;

            _lastCameraPosition = _camera.transform.position;
            _lastOrthographicSize = _camera.orthographicSize;

            Refresh();
        }

        private bool HasCameraChanged()
        {
            if (!Mathf.Approximately(_lastOrthographicSize, _camera.orthographicSize)) return true;

            return _lastCameraPosition != _camera.transform.position;
        }

        private void Refresh()
        {
            if (CellScreenPixels() < MinCellScreenPixels)
            {
                ReleaseAll();
                return;
            }

            var layout = _boardView.Layout;
            var grid = _boardView.Grid;
            var colors = _boardView.Colors;
            var visible = layout.VisibleCells(CameraWorldRect());

            ReleaseOutside(visible);

            for (var y = visible.yMin; y < visible.yMax; y++)
            {
                for (var x = visible.xMin; x < visible.xMax; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (_active.ContainsKey(cell)) continue;

                    var index = grid.GetCell(x, y);
                    if (index == PixelGrid.EmptyCell) continue;
                    if (index < 0 || index >= colors.Count) continue;

                    var label = Rent();
                    label.transform.position = layout.CellToWorldCenter(x, y);
                    label.color = TextColorFor(colors[index]);
                    label.SetText("{0}", index + 1);

                    _active[cell] = label;
                }
            }
        }

        private float CellScreenPixels()
        {
            // Một ô cao đúng một world unit; camera orthographic thấy 2 * size world unit theo chiều dọc.
            return Screen.height / (2f * _camera.orthographicSize);
        }

        private Rect CameraWorldRect()
        {
            var halfHeight = _camera.orthographicSize;
            var halfWidth = halfHeight * _camera.aspect;
            var center = _camera.transform.position;

            return new Rect(
                center.x - halfWidth,
                center.y - halfHeight,
                halfWidth * 2f,
                halfHeight * 2f);
        }

        private static Color TextColorFor(Color32 cellColor)
        {
            var luminance = 0.299f * cellColor.r + 0.587f * cellColor.g + 0.114f * cellColor.b;

            return luminance > DarkTextLuminanceThreshold ? Color.black : Color.white;
        }

        private void ReleaseOutside(RectInt visible)
        {
            _toRelease.Clear();

            foreach (var pair in _active)
            {
                if (!visible.Contains(pair.Key)) _toRelease.Add(pair.Key);
            }

            foreach (var cell in _toRelease) Release(cell);
        }

        private void ReleaseAll()
        {
            _toRelease.Clear();
            foreach (var pair in _active) _toRelease.Add(pair.Key);

            foreach (var cell in _toRelease) Release(cell);
        }

        private void Release(Vector2Int cell)
        {
            if (!_active.TryGetValue(cell, out var label)) return;

            label.gameObject.SetActive(false);
            _pool.Push(label);
            _active.Remove(cell);
        }

        private TextMeshPro Rent()
        {
            if (_pool.Count > 0)
            {
                var pooled = _pool.Pop();
                pooled.gameObject.SetActive(true);
                return pooled;
            }

            return Instantiate(_numberPrefab, _root);
        }
    }
}
```

- [ ] **Step 2: Kiểm biên dịch**

Quay lại Unity. Console phải sạch lỗi.

Nếu báo `The type or namespace name 'TMPro' could not be found` thì Task 2 Step 1 chưa được áp dụng — kiểm lại `Unity.TextMeshPro` đã nằm trong `references` của `JewelPainter.Gameplay.asmdef` chưa.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Gameplay/Board/BoardNumberLayer.cs Assets/Scripts/Gameplay/Board/BoardNumberLayer.cs.meta
git commit -m "feat: thêm BoardNumberLayer hiện số theo tầm nhìn"
```

---

### Task 5: `BoardCamera`

**Files:**
- Create: `Assets/Scripts/Gameplay/Board/BoardCamera.cs`

**Interfaces:**
- Consumes: `BoardView.Layout`, `BoardView.OnBoardRebuilt` (Task 3), `BoardLayout.WorldBounds` (Task 1)
- Produces: `class BoardCamera : MonoBehaviour` với `void Init(BoardView boardView)`

- [ ] **Step 1: Viết `BoardCamera`**

Tạo `Assets/Scripts/Gameplay/Board/BoardCamera.cs`:

```csharp
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace JewelPainter.Gameplay.Board
{
    /// Zoom và kéo bảng. Toàn bộ phần đọc input gói trong file này —
    /// đổi sang API input khác chỉ phải sửa ở đây.
    public class BoardCamera : MonoBehaviour
    {
        /// Zoom gần nhất: thấy khoảng ngần này ô theo chiều dọc.
        private const float MinVisibleCells = 5f;

        /// Zoom xa nhất: trọn bảng cộng thêm lề.
        private const float FitMargin = 1.1f;

        private const float ScrollZoomSpeed = 0.001f;
        private const float PinchZoomSpeed = 0.005f;

        [SerializeField] private Camera _camera;
        [SerializeField] private BoardView _boardView;

        private float _minSize = 1f;
        private float _maxSize = 10f;

        private bool _isDragging;
        private Vector2 _dragOriginWorld;
        private float _lastPinchDistance;

        public void Init(BoardView boardView)
        {
            _boardView = boardView;
            _boardView.OnBoardRebuilt += HandleBoardRebuilt;
        }

        private void OnDestroy()
        {
            if (_boardView != null) _boardView.OnBoardRebuilt -= HandleBoardRebuilt;
        }

        private void HandleBoardRebuilt()
        {
            var layout = _boardView.Layout;
            if (layout == null) return;

            var fitByHeight = layout.Height / 2f;
            var fitByWidth = layout.Width / 2f / Mathf.Max(0.0001f, _camera.aspect);

            _maxSize = Mathf.Max(fitByHeight, fitByWidth) * FitMargin;
            _minSize = Mathf.Min(MinVisibleCells / 2f, _maxSize);

            _camera.orthographicSize = _maxSize;
            transform.position = new Vector3(0f, 0f, transform.position.z);

            _isDragging = false;
            _lastPinchDistance = 0f;
        }

        private void Update()
        {
            if (_boardView == null || _boardView.Layout == null) return;

            if (!HandleTouch()) HandleMouse();

            ClampPosition();
        }

        /// Trả true nếu cảm ứng đang được dùng — khi đó bỏ qua chuột.
        private bool HandleTouch()
        {
            var screen = Touchscreen.current;
            if (screen == null) return false;

            TouchControl first = null;
            TouchControl second = null;

            foreach (var touch in screen.touches)
            {
                if (!touch.press.isPressed) continue;

                if (first == null) first = touch;
                else { second = touch; break; }
            }

            if (first == null)
            {
                _isDragging = false;
                _lastPinchDistance = 0f;
                return false;
            }

            if (second != null)
            {
                var distance = Vector2.Distance(first.position.ReadValue(), second.position.ReadValue());

                if (_lastPinchDistance > 0f)
                {
                    ApplyZoom(-(distance - _lastPinchDistance) * PinchZoomSpeed * _camera.orthographicSize);
                }

                _lastPinchDistance = distance;
                _isDragging = false;
                return true;
            }

            _lastPinchDistance = 0f;
            DragTo(first.position.ReadValue());
            return true;
        }

        private void HandleMouse()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            var scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                ApplyZoom(-scroll * ScrollZoomSpeed * _camera.orthographicSize);
            }

            if (!mouse.leftButton.isPressed)
            {
                _isDragging = false;
                return;
            }

            DragTo(mouse.position.ReadValue());
        }

        /// Ghim điểm world dưới ngón tay, rồi mỗi frame dịch camera sao cho điểm đó
        /// quay lại đúng dưới ngón. Tự sửa sai nên không tích luỹ trôi.
        private void DragTo(Vector2 screenPosition)
        {
            if (!_isDragging)
            {
                _isDragging = true;
                _dragOriginWorld = ScreenToWorld(screenPosition);
                return;
            }

            var current = ScreenToWorld(screenPosition);
            var move = _dragOriginWorld - current;

            transform.position += new Vector3(move.x, move.y, 0f);
        }

        private Vector2 ScreenToWorld(Vector2 screenPosition)
        {
            var depth = Mathf.Abs(transform.position.z);

            return _camera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, depth));
        }

        private void ApplyZoom(float delta)
        {
            _camera.orthographicSize = Mathf.Clamp(_camera.orthographicSize + delta, _minSize, _maxSize);
        }

        /// Không cho kéo bảng ra khỏi màn hình. Khi zoom xa hơn cả bảng thì khoá về giữa.
        private void ClampPosition()
        {
            var bounds = _boardView.Layout.WorldBounds;

            var halfHeight = _camera.orthographicSize;
            var halfWidth = halfHeight * _camera.aspect;

            var maxX = Mathf.Max(0f, bounds.extents.x - halfWidth);
            var maxY = Mathf.Max(0f, bounds.extents.y - halfHeight);

            var position = transform.position;

            transform.position = new Vector3(
                Mathf.Clamp(position.x, -maxX, maxX),
                Mathf.Clamp(position.y, -maxY, maxY),
                position.z);
        }
    }
}
```

- [ ] **Step 2: Kiểm biên dịch**

Quay lại Unity. Console phải sạch lỗi.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Gameplay/Board/BoardCamera.cs Assets/Scripts/Gameplay/Board/BoardCamera.cs.meta
git commit -m "feat: thêm BoardCamera zoom và kéo bảng"
```

---

### Task 6: Nối vào Bootstrap và dựng scene

**Files:**
- Modify: `Assets/Scripts/Bootstrap/GameLifetimeScope.cs`
- Modify: `Assets/Scripts/Bootstrap/GameEntryPoint.cs`

**Interfaces:**
- Consumes: `BoardView.Init(ILevelService)` (Task 3), `BoardNumberLayer.Init(BoardView)` (Task 4), `BoardCamera.Init(BoardView)` (Task 5)
- Produces: không có — đây là task cuối

- [ ] **Step 1: Đăng ký board trong `GameLifetimeScope`**

Sửa `Assets/Scripts/Bootstrap/GameLifetimeScope.cs`.

Thêm vào khối using:

```csharp
using JewelPainter.Gameplay.Board;
```

Thêm ngay trên dòng `builder.RegisterEntryPoint<GameEntryPoint>();`:

```csharp
            builder.RegisterComponentInHierarchy<BoardView>();
            builder.RegisterComponentInHierarchy<BoardNumberLayer>();
            builder.RegisterComponentInHierarchy<BoardCamera>();
```

- [ ] **Step 2: Gọi `Init` trong `GameEntryPoint`**

Sửa `Assets/Scripts/Bootstrap/GameEntryPoint.cs` thành:

```csharp
using JewelPainter.Core.Persistence;
using JewelPainter.Core.Services;
using JewelPainter.Gameplay.Board;
using JewelPainter.Gameplay.Domain;
using JewelPainter.Gameplay.Interfaces;
using JewelPainter.Gameplay.Managers;
using JewelPainter.UI.Views;
using VContainer.Unity;

namespace JewelPainter.Bootstrap
{
    /// Chạy một lần sau khi container dựng xong: đưa phụ thuộc cho các
    /// MonoBehaviour trong scene rồi mở màn chơi hiện tại.
    /// Class thuần C# — không phải MonoBehaviour.
    ///
    /// Constructor dài là bình thường ở composition root: đây đúng là nơi mọi thứ
    /// gặp nhau, và thà thấy hết ở một chỗ còn hơn để từng object tự đi tìm.
    public class GameEntryPoint : IStartable
    {
        private readonly ISaveService _save;
        private readonly PlayerProgress _progress;
        private readonly SoundService _sound;
        private readonly LevelManager _levelManager;
        private readonly ILevelService _levelService;
        private readonly HudView _hud;
        private readonly BoardView _boardView;
        private readonly BoardNumberLayer _numberLayer;
        private readonly BoardCamera _boardCamera;

        public GameEntryPoint(
            ISaveService save,
            PlayerProgress progress,
            SoundService sound,
            LevelManager levelManager,
            ILevelService levelService,
            HudView hud,
            BoardView boardView,
            BoardNumberLayer numberLayer,
            BoardCamera boardCamera)
        {
            _save = save;
            _progress = progress;
            _sound = sound;
            _levelManager = levelManager;
            _levelService = levelService;
            _hud = hud;
            _boardView = boardView;
            _numberLayer = numberLayer;
            _boardCamera = boardCamera;
        }

        public void Start()
        {
            _sound.Init(_save);
            _levelManager.Init(_progress);
            _hud.Init(_levelService);

            // Board phải Init trước khi nạp màn, vì nó dựng bảng khi nghe OnLevelStarted
            _boardView.Init(_levelService);
            _numberLayer.Init(_boardView);
            _boardCamera.Init(_boardView);

            _levelService.LoadLevel(_progress.Level);
        }
    }
}
```

- [ ] **Step 3: Tạo prefab chữ số**

Trong Unity:

1. `GameObject > 3D Object > Text - TextMeshPro` (loại world-space, **không** phải UI Text). Nếu hiện hộp thoại nhập TMP Essentials thì bấm Import.
2. Đặt tên `CellNumber`.
3. Trong Inspector của `TextMeshPro`: `Font Size` = 4, `Alignment` = Center + Middle, `Wrapping` = Disabled, `Overflow` = Overflow.
4. Trong `RectTransform`: `Width` = 1, `Height` = 1.
5. Kéo vào `Assets/Prefabs/` để thành prefab, rồi xoá khỏi scene.

Cỡ chữ 4 với ô rộng 1 unit trông sẽ quá to; chỉnh lại ở bước kiểm bằng mắt cho vừa mắt.

- [ ] **Step 4: Dựng scene**

1. Chọn `Main Camera`: đặt `Projection` = `Orthographic`, `Position` = `(0, 0, -10)`.
2. Thêm component `BoardCamera` vào `Main Camera`. Gán ô `Camera` bằng chính nó. Ô `Board View` để trống — `Init` sẽ gán.
3. Tạo GameObject rỗng tên `Board`, `Position` = `(0, 0, 0)`. **Phải ở gốc toạ độ** vì `BoardLayout` căn bảng quanh gốc.
4. Thêm `SpriteRenderer` và `BoardView` vào `Board`. Gán ô `Renderer` bằng `SpriteRenderer` của chính nó.
5. Tạo GameObject con của `Board` tên `Numbers`, `Position` = `(0, 0, -1)` để số nổi trên bảng.
6. Thêm `BoardNumberLayer` vào `Numbers`. Gán: `Camera` = `Main Camera`, `Number Prefab` = prefab `CellNumber`, `Root` = chính `Numbers`.
7. Đảm bảo trong scene có `GameLifetimeScope`, `LevelManager`, `SoundService`, `PopupManager`, `HudView` như khung Bootstrap đã dựng.
8. Chọn `LevelManager`, gán một `LevelConfig` vào mảng `Levels`. `LevelConfig` đó phải có `Level Id` khớp level hiện tại (mặc định là 1) và đã gán `Grid Data` bằng asset sinh từ tool.

- [ ] **Step 5: Kiểm bằng mắt**

Bấm Play, rồi kiểm từng mục:

1. Bảng hiện ra, màu khớp ảnh gốc, **không lộn ngược trên dưới** — so với ảnh nguồn trong Project.
2. Số hiện trên các ô, ô cùng màu cùng số.
3. Chữ đen trên ô sáng, chữ trắng trên ô tối, đọc được cả hai.
4. Cuộn chuột: bảng phóng to thu nhỏ, không zoom xa hơn mức trọn bảng, không zoom gần hơn khoảng 5 ô.
5. Zoom xa nhất: số biến mất khi ô nhỏ quá. Zoom vào: số hiện lại.
6. Giữ chuột trái kéo: bảng di theo, không kéo được ra khỏi màn hình.
7. Khi zoom xa trọn bảng, kéo không có tác dụng — camera khoá về giữa. Đây là đúng.
8. Console không có lỗi, không có cảnh báo lặp mỗi frame.
9. Nếu chữ số quá to hoặc quá nhỏ so với ô: sửa `Font Size` trong prefab `CellNumber`.

- [ ] **Step 6: Kiểm các đường lỗi**

1. Bỏ trống ô `Grid Data` trong `LevelConfig`, bấm Play → Console có một cảnh báo `LevelConfig của màn này chưa gán GridData`, không có exception, không có bảng.
2. Gán lại, mở `JewelPalette` xoá bớt vài màu cuối, bấm Play → Console có đúng **một** cảnh báo về chỉ số vượt bảng màu, những ô đó trong suốt. Sau đó Undo lại palette.

- [ ] **Step 7: Commit**

```bash
git add Assets/Scripts/Bootstrap Assets/Prefabs Assets/Scenes
git commit -m "feat: nối board vào bootstrap và dựng scene"
```

---

## Tự rà lại kế hoạch

**Phủ spec:** `BoardLayout` (T1); `ILevelService.CurrentGrid` + asmdef (T2); texture màu, ô rỗng alpha 0, giải phóng texture, bốn đường lỗi (T3); pool TMP, cull theo tầm nhìn, ngưỡng 14 pixel, số +1, màu chữ tương phản (T4); zoom cuộn và chụm, kéo, kẹp biên, Input System mới (T5); đăng ký VContainer, gọi `Init`, dựng scene (T6).

**Nhất quán kiểu:** `BoardView` công khai `Layout`, `Grid`, `Colors`, `OnBoardRebuilt` ở T3; T4 và T5 dùng đúng bốn tên đó. `Init(BoardView)` cùng chữ ký ở cả `BoardNumberLayer` và `BoardCamera`. `BoardLayout.VisibleCells` trả `RectInt`, T4 duyệt bằng `yMin/yMax/xMin/xMax` và `Contains`.

**Ba chỗ lật toạ độ** đều được nêu rõ: `BuildPixels` lật khi ghi texture (T3), `CellToWorldCenter` lật khi ra world (T1), `VisibleCells` đảo hai đầu y (T1). Bước kiểm mục 1 của T6 Step 5 bắt lỗi này.

**Chưa có test tự động:** toàn bộ. `BoardLayout` là phần duy nhất test được không cần scene — code test ở phụ lục dưới.

---

## Phụ lục: test EditMode cho `BoardLayout`

Dùng khi bật lại test. Cần assembly test như mô tả ở plan trước
(`Documentation~/superpowers/plans/2026-08-04-image-to-grid-tool.md`, Task 1).

Tạo `Assets/Scripts/Tests/EditMode/BoardLayoutTests.cs`:

```csharp
using JewelPainter.Gameplay.Board;
using NUnit.Framework;
using UnityEngine;

namespace JewelPainter.Tests
{
    public class BoardLayoutTests
    {
        [Test]
        public void DoiOSangWorldRoiNguocLai_VeDungOCu()
        {
            var layout = new BoardLayout(4, 4);

            for (var y = 0; y < 4; y++)
            for (var x = 0; x < 4; x++)
            {
                var world = layout.CellToWorldCenter(x, y);

                Assert.IsTrue(layout.TryWorldToCell(world, out var cell), $"ô ({x},{y}) phải nằm trong bảng");
                Assert.AreEqual(x, cell.x);
                Assert.AreEqual(y, cell.y);
            }
        }

        [Test]
        public void HangDauNamTrenHangCuoiTrongWorld()
        {
            var layout = new BoardLayout(4, 4);

            var top = layout.CellToWorldCenter(0, 0);
            var bottom = layout.CellToWorldCenter(0, 3);

            Assert.Greater(top.y, bottom.y, "y = 0 phải là hàng trên cùng");
        }

        [Test]
        public void DiemNgoaiBang_TryWorldToCellTraFalse()
        {
            var layout = new BoardLayout(4, 4);

            Assert.IsFalse(layout.TryWorldToCell(new Vector2(100f, 0f), out _));
            Assert.IsFalse(layout.TryWorldToCell(new Vector2(0f, -100f), out _));
        }

        [Test]
        public void TamNhinTrumCaBang_TraTronLuoi()
        {
            var layout = new BoardLayout(4, 4);

            var visible = layout.VisibleCells(new Rect(-50f, -50f, 100f, 100f));

            Assert.AreEqual(0, visible.xMin);
            Assert.AreEqual(0, visible.yMin);
            Assert.AreEqual(4, visible.width);
            Assert.AreEqual(4, visible.height);
        }

        [Test]
        public void TamNhinNamHanNgoaiBang_TraHinhChuNhatRong()
        {
            var layout = new BoardLayout(4, 4);

            var visible = layout.VisibleCells(new Rect(100f, 100f, 10f, 10f));

            Assert.AreEqual(0, visible.width * visible.height);
        }

        [Test]
        public void LuoiKhongVuong_BoundsDungTiLe()
        {
            var layout = new BoardLayout(8, 4);

            var bounds = layout.WorldBounds;

            Assert.AreEqual(8f, bounds.size.x, 0.001f);
            Assert.AreEqual(4f, bounds.size.y, 0.001f);
        }
    }
}
```
