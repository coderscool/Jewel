# Tool ảnh → lưới ô màu — kế hoạch triển khai

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Dựng Editor tool cho phép kéo ảnh vào, nhập số ô mỗi cạnh, sinh ra asset lưới trong đó mỗi ô mang chỉ số trỏ vào bảng màu dùng chung.

**Architecture:** Toàn bộ tính toán nằm ở `Gameplay/Domain/` dưới dạng class tĩnh thuần C# (`GridSampler`, `PaletteMatcher`) nên test EditMode được mà không cần scene. Thư mục `Editor/` chỉ là lớp vỏ: đọc `Texture2D`, gọi xuống Domain, ghi `ScriptableObject`. Muốn làm bản runtime sau này thì viết lớp vỏ mới, phần lõi dùng lại nguyên vẹn.

**Tech Stack:** Unity C#, Unity Test Framework (NUnit) cho EditMode test, VContainer đã có sẵn trong project nhưng tool này không cần tới.

## Global Constraints

- Namespace gốc `JewelPainter.*`, thư mục ↔ namespace 1:1.
- Chiều phụ thuộc `Bootstrap → UI → Gameplay → Core`, không bao giờ ngược lại. `Editor` phụ thuộc `Gameplay` + `Core`, không ai phụ thuộc ngược vào `Editor`.
- Không `public` field ngoài `const` và field của `[Serializable] struct Entry`. Dùng `[SerializeField] private` + property chỉ đọc.
- Một file một type public, tên file trùng tên type.
- `Domain/` không được dùng MonoBehaviour, ScriptableObject, hay bất kỳ API cần scene. **Được phép** dùng struct giá trị của UnityEngine (`Color32`, `Vector2Int`) — đây là ngoại lệ có chủ đích, Task 2 ghi nó vào `CLAUDE.md`.
- Ngưỡng alpha coi là trong suốt: `128`. Ô có **quá nửa** số pixel dưới ngưỡng → ô rỗng.
- Tạo file `.cs` mới bằng công cụ ngoài Unity thì an toàn. **Di chuyển hoặc đổi tên** file đã tồn tại thì phải làm trong cửa sổ Project của Unity.
- Commit kèm cả file `.meta`.

**Chạy test:** mở Unity → `Window > General > Test Runner` → tab `EditMode` → `Run All`.
Hoặc dòng lệnh: `Unity.exe -batchmode -projectPath D:\UnityProjects\JewelPainter -runTests -testPlatform EditMode -testResults results.xml`

---

## Cấu trúc file

| File | Trách nhiệm |
|---|---|
| `Gameplay/Domain/PixelGrid.cs` | Ma trận chỉ số palette, thuần C#. Không biết ảnh, không biết asset. |
| `Gameplay/Domain/SampledCell.cs` | Kết quả lấy mẫu một ô: màu trung bình + cờ rỗng. |
| `Gameplay/Domain/GridSampler.cs` | Mảng pixel → mảng `SampledCell`. Thuật toán lấy mẫu hộp. |
| `Gameplay/Domain/PaletteMatcher.cs` | Một màu → chỉ số gần nhất trong bảng màu, theo redmean. |
| `Gameplay/Palette/JewelPalette.cs` | `ScriptableObject` chứa bảng màu, có sẵn 16 màu mặc định. |
| `Gameplay/Data/LevelGridData.cs` | `ScriptableObject` chứa lưới đã sinh. Tool ghi đè toàn bộ mỗi lần chạy. |
| `Gameplay/Config/LevelConfig.cs` | *(sửa)* thêm tham chiếu sang `LevelGridData`. |
| `Editor/ImageToGridGenerator.cs` | Ráp Domain lại: bật Read/Write, tính kích thước lưới, lật ảnh, sinh `PixelGrid`. |
| `Editor/ImageToGridWindow.cs` | Cửa sổ EditorWindow: input, nút Generate, preview, nút Save. |
| `Tests/EditMode/*` | Test cho Domain và cho phần tính kích thước lưới. |

**Ghi chú về hệ toạ độ:** `GridSampler` không quan tâm trên/dưới — nó chỉ ánh xạ một mảng 2D sang mảng 2D thô hơn, giữ nguyên chiều. `Texture2D.GetPixels32()` trả về hàng dưới cùng trước, nên `ImageToGridGenerator` lật mảng trước khi gọi xuống, để `PixelGrid` có `y = 0` là hàng **trên cùng**.

**Ghi chú về namespace `JewelPainter.Editor`:** trùng tên rút gọn với `UnityEditor.Editor`. Không sao với `EditorWindow`, nhưng nếu sau này viết custom inspector thì phải ghi rõ `UnityEditor.Editor` khi kế thừa.

---

### Task 1: Khung test EditMode và `PixelGrid`

**Files:**
- Create: `Assets/Scripts/Tests/EditMode/JewelPainter.Tests.EditMode.asmdef`
- Create: `Assets/Scripts/Gameplay/Domain/PixelGrid.cs`
- Test: `Assets/Scripts/Tests/EditMode/PixelGridTests.cs`

**Interfaces:**
- Consumes: không có (task đầu tiên)
- Produces: `PixelGrid` với `PixelGrid.EmptyCell` (const int = -1), constructor `PixelGrid(int width, int height)`, `int Width { get; }`, `int Height { get; }`, `int GetCell(int x, int y)`, `void SetCell(int x, int y, int paletteIndex)`, `int[] ToArray()`, `static PixelGrid FromArray(int width, int height, int[] cells)`

- [ ] **Step 1: Tạo assembly definition cho test**

Tạo `Assets/Scripts/Tests/EditMode/JewelPainter.Tests.EditMode.asmdef`:

```json
{
    "name": "JewelPainter.Tests.EditMode",
    "rootNamespace": "JewelPainter.Tests",
    "references": [
        "JewelPainter.Core",
        "JewelPainter.Gameplay",
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner"
    ],
    "includePlatforms": [
        "Editor"
    ],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": [
        "nunit.framework.dll"
    ],
    "autoReferenced": false,
    "defineConstraints": [
        "UNITY_INCLUDE_TESTS"
    ],
    "versionDefines": [],
    "noEngineReferences": false
}
```

Quay lại Unity đợi import xong. Mở `Window > General > Test Runner`, tab EditMode phải thấy assembly mới (chưa có test nào).

- [ ] **Step 2: Viết test thất bại**

Tạo `Assets/Scripts/Tests/EditMode/PixelGridTests.cs`:

```csharp
using System;
using JewelPainter.Gameplay.Domain;
using NUnit.Framework;

namespace JewelPainter.Tests
{
    public class PixelGridTests
    {
        [Test]
        public void LuoiMoiTao_MoiODeuRong()
        {
            var grid = new PixelGrid(3, 2);

            for (var y = 0; y < grid.Height; y++)
            for (var x = 0; x < grid.Width; x++)
            {
                Assert.AreEqual(PixelGrid.EmptyCell, grid.GetCell(x, y));
            }
        }

        [Test]
        public void SetCell_RoiGetCell_TraVeDungGiaTri()
        {
            var grid = new PixelGrid(3, 2);

            grid.SetCell(2, 1, 7);

            Assert.AreEqual(7, grid.GetCell(2, 1));
            Assert.AreEqual(PixelGrid.EmptyCell, grid.GetCell(0, 0));
        }

        [Test]
        public void KichThuocKhongDuong_NemLoi()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new PixelGrid(0, 2));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PixelGrid(3, -1));
        }

        [Test]
        public void ToaDoNgoaiPhamVi_NemLoi()
        {
            var grid = new PixelGrid(3, 2);

            Assert.Throws<ArgumentOutOfRangeException>(() => grid.GetCell(3, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => grid.GetCell(0, 2));
        }

        [Test]
        public void ToArray_RoiFromArray_GiuNguyenDuLieu()
        {
            var grid = new PixelGrid(2, 2);
            grid.SetCell(0, 0, 1);
            grid.SetCell(1, 1, 4);

            var restored = PixelGrid.FromArray(2, 2, grid.ToArray());

            Assert.AreEqual(1, restored.GetCell(0, 0));
            Assert.AreEqual(4, restored.GetCell(1, 1));
            Assert.AreEqual(PixelGrid.EmptyCell, restored.GetCell(1, 0));
        }

        [Test]
        public void ToArray_TraVeBanSao_SuaKhongAnhHuongGoc()
        {
            var grid = new PixelGrid(2, 2);
            grid.SetCell(0, 0, 1);

            var copy = grid.ToArray();
            copy[0] = 99;

            Assert.AreEqual(1, grid.GetCell(0, 0));
        }
    }
}
```

- [ ] **Step 3: Chạy test để xác nhận nó hỏng**

Test Runner → EditMode → Run All.
Kết quả mong đợi: lỗi biên dịch `The type or namespace name 'PixelGrid' could not be found`.

- [ ] **Step 4: Viết `PixelGrid`**

Tạo `Assets/Scripts/Gameplay/Domain/PixelGrid.cs`:

```csharp
using System;

namespace JewelPainter.Gameplay.Domain
{
    /// Ma trận chỉ số bảng màu. Thuần C# — không biết Texture2D hay ScriptableObject,
    /// nên test EditMode được mà không cần scene.
    /// Quy ước: y = 0 là hàng TRÊN CÙNG. Bên gọi chịu trách nhiệm lật cho đúng.
    public class PixelGrid
    {
        /// Ô không được tô — thường là vùng trong suốt của ảnh gốc.
        public const int EmptyCell = -1;

        private readonly int[] _cells;

        public PixelGrid(int width, int height)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width), width, "Chiều rộng phải dương");
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height), height, "Chiều cao phải dương");

            Width = width;
            Height = height;
            _cells = new int[width * height];

            for (var i = 0; i < _cells.Length; i++) _cells[i] = EmptyCell;
        }

        public int Width { get; }
        public int Height { get; }

        public int GetCell(int x, int y) => _cells[Index(x, y)];

        public void SetCell(int x, int y, int paletteIndex) => _cells[Index(x, y)] = paletteIndex;

        /// Trả về bản sao — người gọi sửa mảng nhận được không ảnh hưởng lưới gốc.
        public int[] ToArray() => (int[])_cells.Clone();

        public static PixelGrid FromArray(int width, int height, int[] cells)
        {
            if (cells == null) throw new ArgumentNullException(nameof(cells));

            var grid = new PixelGrid(width, height);
            if (cells.Length != grid._cells.Length)
            {
                throw new ArgumentException(
                    $"Cần {grid._cells.Length} ô cho lưới {width}x{height}, nhận được {cells.Length}",
                    nameof(cells));
            }

            Array.Copy(cells, grid._cells, cells.Length);
            return grid;
        }

        private int Index(int x, int y)
        {
            if (x < 0 || x >= Width) throw new ArgumentOutOfRangeException(nameof(x), x, $"Ngoài phạm vi 0..{Width - 1}");
            if (y < 0 || y >= Height) throw new ArgumentOutOfRangeException(nameof(y), y, $"Ngoài phạm vi 0..{Height - 1}");

            return y * Width + x;
        }
    }
}
```

- [ ] **Step 5: Chạy test để xác nhận nó xanh**

Test Runner → EditMode → Run All.
Kết quả mong đợi: 6 test PASS.

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/Tests Assets/Scripts/Gameplay/Domain/PixelGrid.cs
git add Assets/Scripts/Tests/EditMode/*.meta Assets/Scripts/Gameplay/Domain/PixelGrid.cs.meta
git commit -m "feat: thêm PixelGrid và khung test EditMode"
```

---

### Task 2: `PaletteMatcher`

**Files:**
- Create: `Assets/Scripts/Gameplay/Domain/PaletteMatcher.cs`
- Modify: `Assets/Scripts/CLAUDE.md`
- Test: `Assets/Scripts/Tests/EditMode/PaletteMatcherTests.cs`

**Interfaces:**
- Consumes: không có
- Produces: `static class PaletteMatcher` với `static int FindNearest(Color32 color, IReadOnlyList<Color32> palette)`

- [ ] **Step 1: Viết test thất bại**

Tạo `Assets/Scripts/Tests/EditMode/PaletteMatcherTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using JewelPainter.Gameplay.Domain;
using NUnit.Framework;
using UnityEngine;

namespace JewelPainter.Tests
{
    public class PaletteMatcherTests
    {
        private static readonly List<Color32> BaMau = new()
        {
            new Color32(255, 0, 0, 255),
            new Color32(0, 255, 0, 255),
            new Color32(0, 0, 255, 255),
        };

        [Test]
        public void MauTrungKhopChinhXac_TraVeDungChiSo()
        {
            Assert.AreEqual(0, PaletteMatcher.FindNearest(new Color32(255, 0, 0, 255), BaMau));
            Assert.AreEqual(1, PaletteMatcher.FindNearest(new Color32(0, 255, 0, 255), BaMau));
            Assert.AreEqual(2, PaletteMatcher.FindNearest(new Color32(0, 0, 255, 255), BaMau));
        }

        [Test]
        public void MauGanDo_TraVeDo()
        {
            var result = PaletteMatcher.FindNearest(new Color32(230, 20, 20, 255), BaMau);

            Assert.AreEqual(0, result);
        }

        [Test]
        public void BangMauMotMau_LuonTraVeKhong()
        {
            var motMau = new List<Color32> { new Color32(10, 20, 30, 255) };

            Assert.AreEqual(0, PaletteMatcher.FindNearest(new Color32(200, 200, 200, 255), motMau));
        }

        [Test]
        public void BangMauRong_NemLoi()
        {
            Assert.Throws<ArgumentException>(
                () => PaletteMatcher.FindNearest(new Color32(0, 0, 0, 255), new List<Color32>()));
        }

        [Test]
        public void BangMauNull_NemLoi()
        {
            Assert.Throws<ArgumentNullException>(
                () => PaletteMatcher.FindNearest(new Color32(0, 0, 0, 255), null));
        }

        /// Ca này phân biệt redmean với khoảng cách RGB thẳng.
        /// Xám (128,128,128), hai ứng viên: lệch xanh lá 10, lệch xanh dương 11.
        /// RGB thẳng: 100 < 121 → chọn xanh lá.
        /// Redmean: xanh lá 4*100 = 400, xanh dương 2.496*121 ≈ 302 → chọn xanh dương.
        [Test]
        public void RedmeanPhatNangLechXanhLa_ChonUngVienXanhDuong()
        {
            var ungVien = new List<Color32>
            {
                new Color32(128, 138, 128, 255),
                new Color32(128, 128, 139, 255),
            };

            var result = PaletteMatcher.FindNearest(new Color32(128, 128, 128, 255), ungVien);

            Assert.AreEqual(1, result);
        }
    }
}
```

- [ ] **Step 2: Chạy test để xác nhận nó hỏng**

Test Runner → EditMode → Run All.
Kết quả mong đợi: lỗi biên dịch `The name 'PaletteMatcher' does not exist`.

- [ ] **Step 3: Viết `PaletteMatcher`**

Tạo `Assets/Scripts/Gameplay/Domain/PaletteMatcher.cs`:

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace JewelPainter.Gameplay.Domain
{
    /// Tìm màu gần nhất trong bảng màu.
    ///
    /// Dùng xấp xỉ "redmean" thay vì khoảng cách Euclid thẳng trên RGB. Thêm khoảng
    /// mười dòng nhưng bám cảm nhận mắt người tốt hơn đáng kể — khoảng cách RGB thẳng
    /// coi mọi kênh nặng như nhau, khiến tông da người hay bị đẩy sang xanh lá.
    public static class PaletteMatcher
    {
        public static int FindNearest(Color32 color, IReadOnlyList<Color32> palette)
        {
            if (palette == null) throw new ArgumentNullException(nameof(palette));
            if (palette.Count == 0) throw new ArgumentException("Bảng màu rỗng", nameof(palette));

            var bestIndex = 0;
            var bestDistance = double.MaxValue;

            for (var i = 0; i < palette.Count; i++)
            {
                var distance = SquaredDistance(color, palette[i]);
                if (distance >= bestDistance) continue;

                bestDistance = distance;
                bestIndex = i;
            }

            return bestIndex;
        }

        private static double SquaredDistance(Color32 a, Color32 b)
        {
            var meanRed = (a.r + b.r) / 2.0;
            var deltaRed = (double)a.r - b.r;
            var deltaGreen = (double)a.g - b.g;
            var deltaBlue = (double)a.b - b.b;

            return (2.0 + meanRed / 256.0) * deltaRed * deltaRed
                   + 4.0 * deltaGreen * deltaGreen
                   + (2.0 + (255.0 - meanRed) / 256.0) * deltaBlue * deltaBlue;
        }
    }
}
```

- [ ] **Step 4: Chạy test để xác nhận nó xanh**

Test Runner → EditMode → Run All.
Kết quả mong đợi: 6 test của `PaletteMatcherTests` PASS, cộng 6 test Task 1 vẫn PASS.

- [ ] **Step 5: Ghi ngoại lệ vào `CLAUDE.md`**

Trong `Assets/Scripts/CLAUDE.md`, tìm dòng trong bảng "Quy ước đã cắm sẵn trong khung code":

```markdown
| Domain thuần C#, không `using UnityEngine`, test được ở EditMode | `Gameplay/Domain/PlayerProgress.cs` |
```

Thay bằng hai dòng:

```markdown
| Domain thuần C#, không MonoBehaviour/ScriptableObject, test được ở EditMode | `Gameplay/Domain/PlayerProgress.cs` |
| Domain **được** dùng struct giá trị của Unity (`Color32`, `Vector2Int`) — chúng không cần scene | `Gameplay/Domain/PaletteMatcher.cs` |
```

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/Gameplay/Domain/PaletteMatcher.cs Assets/Scripts/Gameplay/Domain/PaletteMatcher.cs.meta
git add Assets/Scripts/Tests/EditMode/PaletteMatcherTests.cs Assets/Scripts/Tests/EditMode/PaletteMatcherTests.cs.meta
git add Assets/Scripts/CLAUDE.md
git commit -m "feat: thêm PaletteMatcher dùng redmean"
```

---

### Task 3: `SampledCell` và `GridSampler`

**Files:**
- Create: `Assets/Scripts/Gameplay/Domain/SampledCell.cs`
- Create: `Assets/Scripts/Gameplay/Domain/GridSampler.cs`
- Test: `Assets/Scripts/Tests/EditMode/GridSamplerTests.cs`

**Interfaces:**
- Consumes: không có
- Produces:
  - `readonly struct SampledCell` với `Color32 Color { get; }`, `bool IsEmpty { get; }`, constructor `SampledCell(Color32 color, bool isEmpty)`, `static SampledCell Empty { get; }`
  - `static class GridSampler` với `const byte DefaultAlphaThreshold = 128` và
    `static SampledCell[] Sample(IReadOnlyList<Color32> pixels, int imageWidth, int imageHeight, int gridWidth, int gridHeight, byte alphaThreshold = DefaultAlphaThreshold)`
    — mảng trả về dài `gridWidth * gridHeight`, phần tử `(x, y)` nằm ở chỉ số `y * gridWidth + x`

- [ ] **Step 1: Viết test thất bại**

Tạo `Assets/Scripts/Tests/EditMode/GridSamplerTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using JewelPainter.Gameplay.Domain;
using NUnit.Framework;
using UnityEngine;

namespace JewelPainter.Tests
{
    public class GridSamplerTests
    {
        private static List<Color32> AnhMotMau(int width, int height, Color32 color)
        {
            var pixels = new List<Color32>(width * height);
            for (var i = 0; i < width * height; i++) pixels.Add(color);
            return pixels;
        }

        [Test]
        public void AnhMotMau_MoiODeuMangMauDo()
        {
            var do_ = new Color32(255, 0, 0, 255);
            var pixels = AnhMotMau(4, 4, do_);

            var cells = GridSampler.Sample(pixels, 4, 4, 2, 2);

            Assert.AreEqual(4, cells.Length);
            foreach (var cell in cells)
            {
                Assert.IsFalse(cell.IsEmpty);
                Assert.AreEqual(255, cell.Color.r);
                Assert.AreEqual(0, cell.Color.g);
            }
        }

        [Test]
        public void AnhChiaDoi_NuaTraiDoNuaPhaiXanh()
        {
            var pixels = new List<Color32>();
            for (var y = 0; y < 4; y++)
            for (var x = 0; x < 4; x++)
            {
                pixels.Add(x < 2 ? new Color32(255, 0, 0, 255) : new Color32(0, 0, 255, 255));
            }

            var cells = GridSampler.Sample(pixels, 4, 4, 2, 2);

            Assert.AreEqual(255, cells[0].Color.r, "ô trái hàng 0 phải là đỏ");
            Assert.AreEqual(255, cells[1].Color.b, "ô phải hàng 0 phải là xanh dương");
            Assert.AreEqual(255, cells[2].Color.r, "ô trái hàng 1 phải là đỏ");
            Assert.AreEqual(255, cells[3].Color.b, "ô phải hàng 1 phải là xanh dương");
        }

        [Test]
        public void AnhTrongSuotHoanToan_MoiODeuRong()
        {
            var pixels = AnhMotMau(4, 4, new Color32(255, 0, 0, 0));

            var cells = GridSampler.Sample(pixels, 4, 4, 2, 2);

            foreach (var cell in cells) Assert.IsTrue(cell.IsEmpty);
        }

        [Test]
        public void PixelTrongSuotKhongKeoMauTrungBinh()
        {
            // Ô 2x1: một pixel đỏ đục, một pixel đen trong suốt.
            // Pixel trong suốt bị loại khỏi trung bình nên ô phải đỏ nguyên, không bị tối đi.
            var pixels = new List<Color32>
            {
                new Color32(255, 0, 0, 255),
                new Color32(0, 0, 0, 0),
            };

            var cells = GridSampler.Sample(pixels, 2, 1, 1, 1);

            Assert.IsFalse(cells[0].IsEmpty, "một nửa trong suốt chưa phải QUÁ nửa");
            Assert.AreEqual(255, cells[0].Color.r);
        }

        [Test]
        public void QuaNuaTrongSuot_ODoLaRong()
        {
            // Ô 3x1: một pixel đục, hai pixel trong suốt → 2/3 trong suốt, quá nửa.
            var pixels = new List<Color32>
            {
                new Color32(255, 0, 0, 255),
                new Color32(0, 0, 0, 0),
                new Color32(0, 0, 0, 0),
            };

            var cells = GridSampler.Sample(pixels, 3, 1, 1, 1);

            Assert.IsTrue(cells[0].IsEmpty);
        }

        [Test]
        public void AnhKhongVuong_KichThuocLuoiGiuNguyenYeuCau()
        {
            var pixels = AnhMotMau(8, 4, new Color32(100, 100, 100, 255));

            var cells = GridSampler.Sample(pixels, 8, 4, 4, 2);

            Assert.AreEqual(8, cells.Length);
        }

        [Test]
        public void LuoiMinHonAnh_KhongNemLoi()
        {
            var pixels = AnhMotMau(2, 2, new Color32(50, 60, 70, 255));

            var cells = GridSampler.Sample(pixels, 2, 2, 4, 4);

            Assert.AreEqual(16, cells.Length);
            foreach (var cell in cells)
            {
                Assert.IsFalse(cell.IsEmpty);
                Assert.AreEqual(50, cell.Color.r);
            }
        }

        [Test]
        public void SoLuongPixelKhongKhopKichThuoc_NemLoi()
        {
            var pixels = AnhMotMau(3, 3, new Color32(0, 0, 0, 255));

            Assert.Throws<ArgumentException>(() => GridSampler.Sample(pixels, 4, 4, 2, 2));
        }

        [Test]
        public void KichThuocLuoiKhongDuong_NemLoi()
        {
            var pixels = AnhMotMau(4, 4, new Color32(0, 0, 0, 255));

            Assert.Throws<ArgumentOutOfRangeException>(() => GridSampler.Sample(pixels, 4, 4, 0, 2));
        }
    }
}
```

- [ ] **Step 2: Chạy test để xác nhận nó hỏng**

Test Runner → EditMode → Run All.
Kết quả mong đợi: lỗi biên dịch `The name 'GridSampler' does not exist`.

- [ ] **Step 3: Viết `SampledCell`**

Tạo `Assets/Scripts/Gameplay/Domain/SampledCell.cs`:

```csharp
using UnityEngine;

namespace JewelPainter.Gameplay.Domain
{
    /// Kết quả lấy mẫu một ô lưới: màu trung bình, hoặc cờ báo ô này không được tô.
    public readonly struct SampledCell
    {
        public SampledCell(Color32 color, bool isEmpty)
        {
            Color = color;
            IsEmpty = isEmpty;
        }

        public Color32 Color { get; }
        public bool IsEmpty { get; }

        public static SampledCell Empty => new SampledCell(default, true);
    }
}
```

- [ ] **Step 4: Viết `GridSampler`**

Tạo `Assets/Scripts/Gameplay/Domain/GridSampler.cs`:

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace JewelPainter.Gameplay.Domain
{
    /// Chia ảnh thành lưới ô chữ nhật, mỗi ô lấy màu trung bình của các pixel bên trong.
    ///
    /// Không quan tâm trên/dưới — chỉ ánh xạ mảng 2D sang mảng 2D thô hơn, giữ nguyên
    /// chiều. Bên gọi chịu trách nhiệm lật ảnh cho đúng hướng trước khi truyền vào.
    ///
    /// Trung bình cộng thực hiện trong không gian sRGB. Về lý thuyết nên chuyển sang
    /// tuyến tính trước khi cộng, nhưng ở mức vài chục ô thì sai khác không nhìn thấy,
    /// còn làm vậy sẽ thêm một bước chuyển đổi cho từng pixel.
    public static class GridSampler
    {
        /// Alpha dưới ngưỡng này coi như trong suốt.
        public const byte DefaultAlphaThreshold = 128;

        public static SampledCell[] Sample(
            IReadOnlyList<Color32> pixels,
            int imageWidth,
            int imageHeight,
            int gridWidth,
            int gridHeight,
            byte alphaThreshold = DefaultAlphaThreshold)
        {
            if (pixels == null) throw new ArgumentNullException(nameof(pixels));
            if (imageWidth <= 0) throw new ArgumentOutOfRangeException(nameof(imageWidth), imageWidth, "Phải dương");
            if (imageHeight <= 0) throw new ArgumentOutOfRangeException(nameof(imageHeight), imageHeight, "Phải dương");
            if (gridWidth <= 0) throw new ArgumentOutOfRangeException(nameof(gridWidth), gridWidth, "Phải dương");
            if (gridHeight <= 0) throw new ArgumentOutOfRangeException(nameof(gridHeight), gridHeight, "Phải dương");

            if (pixels.Count != imageWidth * imageHeight)
            {
                throw new ArgumentException(
                    $"Cần {imageWidth * imageHeight} pixel cho ảnh {imageWidth}x{imageHeight}, nhận được {pixels.Count}",
                    nameof(pixels));
            }

            var cells = new SampledCell[gridWidth * gridHeight];

            for (var cellY = 0; cellY < gridHeight; cellY++)
            {
                var startY = cellY * imageHeight / gridHeight;
                var endY = (cellY + 1) * imageHeight / gridHeight;
                if (endY <= startY) endY = startY + 1;

                for (var cellX = 0; cellX < gridWidth; cellX++)
                {
                    var startX = cellX * imageWidth / gridWidth;
                    var endX = (cellX + 1) * imageWidth / gridWidth;
                    if (endX <= startX) endX = startX + 1;

                    cells[cellY * gridWidth + cellX] =
                        SampleCell(pixels, imageWidth, startX, endX, startY, endY, alphaThreshold);
                }
            }

            return cells;
        }

        private static SampledCell SampleCell(
            IReadOnlyList<Color32> pixels,
            int imageWidth,
            int startX,
            int endX,
            int startY,
            int endY,
            byte alphaThreshold)
        {
            long sumRed = 0;
            long sumGreen = 0;
            long sumBlue = 0;
            var opaqueCount = 0;
            var totalCount = 0;

            for (var y = startY; y < endY; y++)
            {
                for (var x = startX; x < endX; x++)
                {
                    var pixel = pixels[y * imageWidth + x];
                    totalCount++;

                    if (pixel.a < alphaThreshold) continue;

                    sumRed += pixel.r;
                    sumGreen += pixel.g;
                    sumBlue += pixel.b;
                    opaqueCount++;
                }
            }

            // Rỗng khi QUÁ nửa số pixel trong suốt. Đúng một nửa thì vẫn tô.
            if (opaqueCount == 0 || opaqueCount * 2 < totalCount) return SampledCell.Empty;

            var average = new Color32(
                (byte)(sumRed / opaqueCount),
                (byte)(sumGreen / opaqueCount),
                (byte)(sumBlue / opaqueCount),
                byte.MaxValue);

            return new SampledCell(average, false);
        }
    }
}
```

- [ ] **Step 5: Chạy test để xác nhận nó xanh**

Test Runner → EditMode → Run All.
Kết quả mong đợi: toàn bộ 21 test PASS (6 + 6 + 9).

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/Gameplay/Domain/SampledCell.cs Assets/Scripts/Gameplay/Domain/SampledCell.cs.meta
git add Assets/Scripts/Gameplay/Domain/GridSampler.cs Assets/Scripts/Gameplay/Domain/GridSampler.cs.meta
git add Assets/Scripts/Tests/EditMode/GridSamplerTests.cs Assets/Scripts/Tests/EditMode/GridSamplerTests.cs.meta
git commit -m "feat: thêm GridSampler lấy mẫu ảnh thành lưới ô"
```

---

### Task 4: `JewelPalette` với bảng màu mặc định

**Files:**
- Create: `Assets/Scripts/Gameplay/Palette/JewelPalette.cs`
- Test: `Assets/Scripts/Tests/EditMode/JewelPaletteTests.cs`

**Interfaces:**
- Consumes: không có
- Produces: `class JewelPalette : ScriptableObject` với `[Serializable] struct Entry { public string name; public Color32 color; }`, `IReadOnlyList<Entry> Entries { get; }`, `IReadOnlyList<Color32> Colors { get; }`

- [ ] **Step 1: Viết test thất bại**

Tạo `Assets/Scripts/Tests/EditMode/JewelPaletteTests.cs`:

```csharp
using JewelPainter.Gameplay.Palette;
using NUnit.Framework;
using UnityEngine;

namespace JewelPainter.Tests
{
    public class JewelPaletteTests
    {
        [Test]
        public void AssetMoiTao_Co16MauMacDinh()
        {
            var palette = ScriptableObject.CreateInstance<JewelPalette>();

            Assert.AreEqual(16, palette.Entries.Count);

            Object.DestroyImmediate(palette);
        }

        [Test]
        public void Colors_KhopSoLuongVaThuTuVoiEntries()
        {
            var palette = ScriptableObject.CreateInstance<JewelPalette>();

            Assert.AreEqual(palette.Entries.Count, palette.Colors.Count);
            for (var i = 0; i < palette.Entries.Count; i++)
            {
                Assert.AreEqual(palette.Entries[i].color.r, palette.Colors[i].r);
                Assert.AreEqual(palette.Entries[i].color.g, palette.Colors[i].g);
                Assert.AreEqual(palette.Entries[i].color.b, palette.Colors[i].b);
            }

            Object.DestroyImmediate(palette);
        }

        [Test]
        public void MoiMauMacDinhDeuCoTen()
        {
            var palette = ScriptableObject.CreateInstance<JewelPalette>();

            foreach (var entry in palette.Entries)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(entry.name));
            }

            Object.DestroyImmediate(palette);
        }

        [Test]
        public void KhongCoHaiMauMacDinhTrungNhau()
        {
            var palette = ScriptableObject.CreateInstance<JewelPalette>();

            for (var i = 0; i < palette.Colors.Count; i++)
            for (var j = i + 1; j < palette.Colors.Count; j++)
            {
                var a = palette.Colors[i];
                var b = palette.Colors[j];
                Assert.IsFalse(a.r == b.r && a.g == b.g && a.b == b.b, $"Màu {i} và {j} trùng nhau");
            }

            Object.DestroyImmediate(palette);
        }
    }
}
```

- [ ] **Step 2: Chạy test để xác nhận nó hỏng**

Test Runner → EditMode → Run All.
Kết quả mong đợi: lỗi biên dịch `The type or namespace name 'JewelPalette' could not be found`.

- [ ] **Step 3: Viết `JewelPalette`**

Tạo `Assets/Scripts/Gameplay/Palette/JewelPalette.cs`:

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace JewelPainter.Gameplay.Palette
{
    /// Bảng màu dùng chung cho toàn game. Mọi màn chơi tham chiếu cùng một asset,
    /// nhờ vậy thanh chọn màu ở UI cố định và sprite viên ngọc tái dùng được.
    ///
    /// Dictionary không serialize được trong Unity nên dùng List&lt;Entry&gt;.
    [CreateAssetMenu(fileName = "JewelPalette", menuName = "JewelPainter/Gameplay/Jewel Palette")]
    public class JewelPalette : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string name;
            public Color32 color;
        }

        [SerializeField]
        private List<Entry> _entries = new()
        {
            new Entry { name = "Đen",             color = new Color32(26, 26, 26, 255) },
            new Entry { name = "Xám đậm",         color = new Color32(90, 90, 90, 255) },
            new Entry { name = "Xám nhạt",        color = new Color32(175, 175, 175, 255) },
            new Entry { name = "Trắng",           color = new Color32(250, 250, 250, 255) },
            new Entry { name = "Đỏ",              color = new Color32(220, 50, 50, 255) },
            new Entry { name = "Hồng",            color = new Color32(240, 130, 170, 255) },
            new Entry { name = "Cam",             color = new Color32(240, 140, 50, 255) },
            new Entry { name = "Vàng",            color = new Color32(245, 210, 70, 255) },
            new Entry { name = "Xanh lá đậm",     color = new Color32(45, 120, 60, 255) },
            new Entry { name = "Xanh lá nhạt",    color = new Color32(120, 195, 90, 255) },
            new Entry { name = "Xanh ngọc",       color = new Color32(60, 190, 180, 255) },
            new Entry { name = "Xanh dương đậm",  color = new Color32(40, 80, 170, 255) },
            new Entry { name = "Xanh dương nhạt", color = new Color32(95, 160, 230, 255) },
            new Entry { name = "Tím",             color = new Color32(140, 80, 190, 255) },
            new Entry { name = "Nâu",             color = new Color32(120, 80, 50, 255) },
            new Entry { name = "Be",              color = new Color32(225, 200, 165, 255) },
        };

        private List<Color32> _colorCache;

        public IReadOnlyList<Entry> Entries => _entries;

        /// Danh sách màu phẳng để PaletteMatcher dùng. Dựng lại khi số lượng đổi
        /// (người dùng thêm/bớt màu trong Inspector).
        public IReadOnlyList<Color32> Colors
        {
            get
            {
                if (_colorCache != null && _colorCache.Count == _entries.Count) return _colorCache;

                _colorCache = new List<Color32>(_entries.Count);
                foreach (var entry in _entries) _colorCache.Add(entry.color);

                return _colorCache;
            }
        }
    }
}
```

- [ ] **Step 4: Chạy test để xác nhận nó xanh**

Test Runner → EditMode → Run All.
Kết quả mong đợi: toàn bộ 25 test PASS.

- [ ] **Step 5: Tạo asset bảng màu**

Trong Unity: `Assets > Create > JewelPainter > Gameplay > Jewel Palette`.
Lưu vào `Assets/Settings/JewelPalette.asset` (tạo thư mục `Settings` nếu chưa có).
Mở ra kiểm tra: phải thấy đủ 16 màu có tên.

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/Gameplay/Palette Assets/Settings
git add Assets/Scripts/Tests/EditMode/JewelPaletteTests.cs Assets/Scripts/Tests/EditMode/JewelPaletteTests.cs.meta
git commit -m "feat: thêm JewelPalette với 16 màu mặc định"
```

---

### Task 5: `LevelGridData` và liên kết vào `LevelConfig`

**Files:**
- Create: `Assets/Scripts/Gameplay/Data/LevelGridData.cs`
- Modify: `Assets/Scripts/Gameplay/Config/LevelConfig.cs`
- Test: `Assets/Scripts/Tests/EditMode/LevelGridDataTests.cs`

**Interfaces:**
- Consumes: `PixelGrid` (Task 1), `JewelPalette` (Task 4)
- Produces: `class LevelGridData : ScriptableObject` với `int Width { get; }`, `int Height { get; }`, `JewelPalette Palette { get; }`, `PixelGrid ToGrid()`, và `void SetData(int width, int height, JewelPalette palette, int[] cells)` bọc trong `#if UNITY_EDITOR`

- [ ] **Step 1: Viết test thất bại**

Tạo `Assets/Scripts/Tests/EditMode/LevelGridDataTests.cs`:

```csharp
using JewelPainter.Gameplay.Data;
using JewelPainter.Gameplay.Domain;
using JewelPainter.Gameplay.Palette;
using NUnit.Framework;
using UnityEngine;

namespace JewelPainter.Tests
{
    public class LevelGridDataTests
    {
        [Test]
        public void SetData_RoiToGrid_TraVeDungLuoi()
        {
            var data = ScriptableObject.CreateInstance<LevelGridData>();
            var palette = ScriptableObject.CreateInstance<JewelPalette>();

            var source = new PixelGrid(2, 2);
            source.SetCell(0, 0, 3);
            source.SetCell(1, 1, 7);

            data.SetData(2, 2, palette, source.ToArray());
            var restored = data.ToGrid();

            Assert.AreEqual(2, data.Width);
            Assert.AreEqual(2, data.Height);
            Assert.AreEqual(palette, data.Palette);
            Assert.AreEqual(3, restored.GetCell(0, 0));
            Assert.AreEqual(7, restored.GetCell(1, 1));
            Assert.AreEqual(PixelGrid.EmptyCell, restored.GetCell(1, 0));

            Object.DestroyImmediate(palette);
            Object.DestroyImmediate(data);
        }

        [Test]
        public void AssetChuaSinhDuLieu_ToGridTraVeNull()
        {
            var data = ScriptableObject.CreateInstance<LevelGridData>();

            Assert.IsNull(data.ToGrid());

            Object.DestroyImmediate(data);
        }
    }
}
```

- [ ] **Step 2: Chạy test để xác nhận nó hỏng**

Test Runner → EditMode → Run All.
Kết quả mong đợi: lỗi biên dịch `The type or namespace name 'LevelGridData' could not be found`.

- [ ] **Step 3: Viết `LevelGridData`**

Tạo `Assets/Scripts/Gameplay/Data/LevelGridData.cs`:

```csharp
using System;
using JewelPainter.Gameplay.Domain;
using JewelPainter.Gameplay.Palette;
using UnityEngine;

namespace JewelPainter.Gameplay.Data
{
    /// Dữ liệu lưới của một màn chơi, do Editor tool sinh ra.
    /// Tách khỏi LevelConfig vì tool ghi đè toàn bộ asset này mỗi lần sinh lại —
    /// không nên để tool đụng vào file người ta chỉnh tay.
    [CreateAssetMenu(fileName = "LevelGridData", menuName = "JewelPainter/Gameplay/Level Grid Data")]
    public class LevelGridData : ScriptableObject
    {
        [SerializeField] private int _width;
        [SerializeField] private int _height;
        [SerializeField] private JewelPalette _palette;
        [SerializeField] private int[] _cells = Array.Empty<int>();

        public int Width => _width;
        public int Height => _height;
        public JewelPalette Palette => _palette;

        /// Trả về null nếu asset chưa được tool sinh dữ liệu.
        public PixelGrid ToGrid()
        {
            if (_width <= 0 || _height <= 0) return null;
            if (_cells == null || _cells.Length != _width * _height) return null;

            return PixelGrid.FromArray(_width, _height, _cells);
        }

#if UNITY_EDITOR
        /// Chỉ dành cho Editor tool. Không gọi lúc chạy game.
        public void SetData(int width, int height, JewelPalette palette, int[] cells)
        {
            _width = width;
            _height = height;
            _palette = palette;
            _cells = cells;
        }
#endif
    }
}
```

- [ ] **Step 4: Chạy test để xác nhận nó xanh**

Test Runner → EditMode → Run All.
Kết quả mong đợi: toàn bộ 27 test PASS.

- [ ] **Step 5: Thêm tham chiếu vào `LevelConfig`**

Sửa `Assets/Scripts/Gameplay/Config/LevelConfig.cs` thành:

```csharp
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

        public int LevelId => _levelId;
        public Sprite TargetImage => _targetImage;
        public LevelGridData GridData => _gridData;
        public int TimeLimitSeconds => _timeLimitSeconds;
    }
}
```

- [ ] **Step 6: Xác nhận Console sạch lỗi**

Quay lại Unity, đợi biên dịch xong, kiểm tra Console không có lỗi.
Chạy lại Test Runner → EditMode → Run All → 27 test PASS.

- [ ] **Step 7: Commit**

```bash
git add Assets/Scripts/Gameplay/Data Assets/Scripts/Gameplay/Config/LevelConfig.cs
git add Assets/Scripts/Tests/EditMode/LevelGridDataTests.cs Assets/Scripts/Tests/EditMode/LevelGridDataTests.cs.meta
git commit -m "feat: thêm LevelGridData và nối vào LevelConfig"
```

---

### Task 6: Assembly Editor và `ImageToGridGenerator`

**Files:**
- Create: `Assets/Scripts/Editor/JewelPainter.Editor.asmdef`
- Create: `Assets/Scripts/Editor/ImageToGridGenerator.cs`
- Modify: `Assets/Scripts/Tests/EditMode/JewelPainter.Tests.EditMode.asmdef`
- Test: `Assets/Scripts/Tests/EditMode/ImageToGridGeneratorTests.cs`

**Interfaces:**
- Consumes: `PixelGrid`, `GridSampler`, `PaletteMatcher`, `SampledCell` (Task 1–3), `JewelPalette` (Task 4)
- Produces: `static class ImageToGridGenerator` với
  - `static Vector2Int CalculateGridSize(int imageWidth, int imageHeight, int longestSideCells)`
  - `static bool EnsureReadable(Texture2D texture)`
  - `static PixelGrid Generate(Texture2D texture, JewelPalette palette, int longestSideCells)`

- [ ] **Step 1: Tạo assembly definition cho Editor**

Tạo `Assets/Scripts/Editor/JewelPainter.Editor.asmdef`:

```json
{
    "name": "JewelPainter.Editor",
    "rootNamespace": "JewelPainter.Editor",
    "references": [
        "JewelPainter.Core",
        "JewelPainter.Gameplay"
    ],
    "includePlatforms": [
        "Editor"
    ],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [ ] **Step 2: Cho assembly test thấy assembly Editor**

Sửa `Assets/Scripts/Tests/EditMode/JewelPainter.Tests.EditMode.asmdef`, thêm `"JewelPainter.Editor"` vào mảng `references`:

```json
    "references": [
        "JewelPainter.Core",
        "JewelPainter.Gameplay",
        "JewelPainter.Editor",
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner"
    ],
```

- [ ] **Step 3: Viết test thất bại**

Tạo `Assets/Scripts/Tests/EditMode/ImageToGridGeneratorTests.cs`:

```csharp
using JewelPainter.Editor;
using JewelPainter.Gameplay.Domain;
using JewelPainter.Gameplay.Palette;
using NUnit.Framework;
using UnityEngine;

namespace JewelPainter.Tests
{
    public class ImageToGridGeneratorTests
    {
        [Test]
        public void AnhVuong_LuoiVuongDungSoODaNhap()
        {
            var size = ImageToGridGenerator.CalculateGridSize(100, 100, 32);

            Assert.AreEqual(32, size.x);
            Assert.AreEqual(32, size.y);
        }

        [Test]
        public void AnhNgang_CanhDaiLaChieuRong()
        {
            var size = ImageToGridGenerator.CalculateGridSize(400, 300, 32);

            Assert.AreEqual(32, size.x);
            Assert.AreEqual(24, size.y);
        }

        [Test]
        public void AnhDoc_CanhDaiLaChieuCao()
        {
            var size = ImageToGridGenerator.CalculateGridSize(300, 400, 32);

            Assert.AreEqual(24, size.x);
            Assert.AreEqual(32, size.y);
        }

        [Test]
        public void AnhRatDaiVaMong_CanhNganKhongBaoGioVeKhong()
        {
            var size = ImageToGridGenerator.CalculateGridSize(1000, 5, 32);

            Assert.AreEqual(32, size.x);
            Assert.GreaterOrEqual(size.y, 1);
        }

        [Test]
        public void Generate_AnhMotMauDo_MoiODeuTroVaoDoTrongPalette()
        {
            var texture = TaoAnhMotMau(8, 8, new Color32(255, 0, 0, 255));
            var palette = ScriptableObject.CreateInstance<JewelPalette>();

            var grid = ImageToGridGenerator.Generate(texture, palette, 4);

            var expected = PaletteMatcher.FindNearest(new Color32(255, 0, 0, 255), palette.Colors);
            Assert.AreEqual(4, grid.Width);
            Assert.AreEqual(4, grid.Height);
            for (var y = 0; y < grid.Height; y++)
            for (var x = 0; x < grid.Width; x++)
            {
                Assert.AreEqual(expected, grid.GetCell(x, y));
            }

            Object.DestroyImmediate(palette);
            Object.DestroyImmediate(texture);
        }

        [Test]
        public void Generate_AnhTrongSuot_MoiODeuRong()
        {
            var texture = TaoAnhMotMau(8, 8, new Color32(255, 0, 0, 0));
            var palette = ScriptableObject.CreateInstance<JewelPalette>();

            var grid = ImageToGridGenerator.Generate(texture, palette, 4);

            for (var y = 0; y < grid.Height; y++)
            for (var x = 0; x < grid.Width; x++)
            {
                Assert.AreEqual(PixelGrid.EmptyCell, grid.GetCell(x, y));
            }

            Object.DestroyImmediate(palette);
            Object.DestroyImmediate(texture);
        }

        [Test]
        public void Generate_NuaTrenTrangNuaDuoiDen_HangDauLaTrang()
        {
            // Kiểm tra chiều lật: PixelGrid quy ước y = 0 là hàng TRÊN CÙNG,
            // còn Texture2D.GetPixels32 trả hàng dưới cùng trước.
            var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            for (var y = 0; y < 4; y++)
            for (var x = 0; x < 4; x++)
            {
                // y của Texture2D: 0 là dưới cùng → nửa trên là y >= 2
                texture.SetPixel(x, y, y >= 2 ? Color.white : Color.black);
            }
            texture.Apply();

            var palette = ScriptableObject.CreateInstance<JewelPalette>();

            var grid = ImageToGridGenerator.Generate(texture, palette, 4);

            var trang = PaletteMatcher.FindNearest(new Color32(255, 255, 255, 255), palette.Colors);
            var den = PaletteMatcher.FindNearest(new Color32(0, 0, 0, 255), palette.Colors);

            Assert.AreEqual(trang, grid.GetCell(0, 0), "hàng trên cùng phải là trắng");
            Assert.AreEqual(den, grid.GetCell(0, 3), "hàng dưới cùng phải là đen");

            Object.DestroyImmediate(palette);
            Object.DestroyImmediate(texture);
        }

        private static Texture2D TaoAnhMotMau(int width, int height, Color32 color)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color32[width * height];
            for (var i = 0; i < pixels.Length; i++) pixels[i] = color;

            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }
    }
}
```

- [ ] **Step 4: Chạy test để xác nhận nó hỏng**

Test Runner → EditMode → Run All.
Kết quả mong đợi: lỗi biên dịch `The type or namespace name 'ImageToGridGenerator' could not be found`.

- [ ] **Step 5: Viết `ImageToGridGenerator`**

Tạo `Assets/Scripts/Editor/ImageToGridGenerator.cs`:

```csharp
using System;
using JewelPainter.Gameplay.Domain;
using JewelPainter.Gameplay.Palette;
using UnityEditor;
using UnityEngine;

namespace JewelPainter.Editor
{
    /// Lớp vỏ Editor: đọc Texture2D, gọi xuống Domain, trả về PixelGrid.
    /// Mọi tính toán thật nằm ở GridSampler và PaletteMatcher.
    public static class ImageToGridGenerator
    {
        public static PixelGrid Generate(Texture2D texture, JewelPalette palette, int longestSideCells)
        {
            if (texture == null) throw new ArgumentNullException(nameof(texture));
            if (palette == null) throw new ArgumentNullException(nameof(palette));
            if (palette.Colors.Count == 0) throw new InvalidOperationException("Bảng màu rỗng — thêm màu vào JewelPalette trước");
            if (longestSideCells < 1) longestSideCells = 1;

            if (!EnsureReadable(texture))
            {
                throw new InvalidOperationException(
                    $"Không bật được Read/Write cho '{texture.name}'. Ảnh sinh bằng code không cần bật; " +
                    "ảnh trong project thì mở Import Settings và tick Read/Write Enabled.");
            }

            var gridSize = CalculateGridSize(texture.width, texture.height, longestSideCells);
            var pixels = FlipVertically(texture.GetPixels32(), texture.width, texture.height);

            var cells = GridSampler.Sample(pixels, texture.width, texture.height, gridSize.x, gridSize.y);

            var grid = new PixelGrid(gridSize.x, gridSize.y);
            for (var y = 0; y < gridSize.y; y++)
            {
                for (var x = 0; x < gridSize.x; x++)
                {
                    var cell = cells[y * gridSize.x + x];
                    if (cell.IsEmpty) continue;   // PixelGrid đã khởi tạo sẵn EmptyCell

                    grid.SetCell(x, y, PaletteMatcher.FindNearest(cell.Color, palette.Colors));
                }
            }

            return grid;
        }

        /// Số ô cạnh dài do người dùng nhập; cạnh còn lại suy ra theo tỉ lệ ảnh.
        public static Vector2Int CalculateGridSize(int imageWidth, int imageHeight, int longestSideCells)
        {
            if (imageWidth <= 0) throw new ArgumentOutOfRangeException(nameof(imageWidth), imageWidth, "Phải dương");
            if (imageHeight <= 0) throw new ArgumentOutOfRangeException(nameof(imageHeight), imageHeight, "Phải dương");
            if (longestSideCells < 1) longestSideCells = 1;

            if (imageWidth >= imageHeight)
            {
                var height = Mathf.Max(1, Mathf.RoundToInt(longestSideCells * (float)imageHeight / imageWidth));
                return new Vector2Int(longestSideCells, height);
            }

            var width = Mathf.Max(1, Mathf.RoundToInt(longestSideCells * (float)imageWidth / imageHeight));
            return new Vector2Int(width, longestSideCells);
        }

        /// GetPixels32 ném lỗi nếu ảnh chưa bật Read/Write. Bật giúp người dùng
        /// thay vì bắt họ đi mở Import Settings.
        /// Ảnh tạo bằng code (không nằm trong AssetDatabase) vốn đã readable.
        public static bool EnsureReadable(Texture2D texture)
        {
            if (texture.isReadable) return true;

            var path = AssetDatabase.GetAssetPath(texture);
            if (string.IsNullOrEmpty(path)) return false;

            if (AssetImporter.GetAtPath(path) is not TextureImporter importer) return false;

            importer.isReadable = true;
            importer.SaveAndReimport();

            return texture.isReadable;
        }

        /// Texture2D trả hàng dưới cùng trước; PixelGrid quy ước y = 0 là hàng trên cùng.
        private static Color32[] FlipVertically(Color32[] pixels, int width, int height)
        {
            var flipped = new Color32[pixels.Length];

            for (var y = 0; y < height; y++)
            {
                var sourceRow = (height - 1 - y) * width;
                var targetRow = y * width;
                Array.Copy(pixels, sourceRow, flipped, targetRow, width);
            }

            return flipped;
        }
    }
}
```

- [ ] **Step 6: Chạy test để xác nhận nó xanh**

Test Runner → EditMode → Run All.
Kết quả mong đợi: toàn bộ 34 test PASS.

- [ ] **Step 7: Commit**

```bash
git add Assets/Scripts/Editor Assets/Scripts/Tests/EditMode
git commit -m "feat: thêm ImageToGridGenerator và assembly Editor"
```

---

### Task 7: Cửa sổ `ImageToGridWindow`

**Files:**
- Create: `Assets/Scripts/Editor/ImageToGridWindow.cs`

**Interfaces:**
- Consumes: `ImageToGridGenerator` (Task 6), `JewelPalette` (Task 4), `LevelGridData` (Task 5), `PixelGrid` (Task 1)
- Produces: `class ImageToGridWindow : EditorWindow`, mở từ menu `JewelPainter > Ảnh thành lưới ô`

Task này không có test tự động — nó là giao diện Editor, kiểm bằng tay ở Step 3. Toàn bộ logic đáng test đã nằm ở Task 1–6.

- [ ] **Step 1: Viết `ImageToGridWindow`**

Tạo `Assets/Scripts/Editor/ImageToGridWindow.cs`:

```csharp
using JewelPainter.Gameplay.Data;
using JewelPainter.Gameplay.Domain;
using JewelPainter.Gameplay.Palette;
using UnityEditor;
using UnityEngine;

namespace JewelPainter.Editor
{
    /// Cửa sổ chuyển ảnh thành lưới ô màu.
    /// Chỉ là lớp vỏ: thu thập input, gọi ImageToGridGenerator, hiện preview, ghi asset.
    public class ImageToGridWindow : EditorWindow
    {
        private const int MinCells = 1;
        private const int MaxCells = 256;
        private const int PreviewMaxSize = 320;

        private Texture2D _sourceTexture;
        private JewelPalette _palette;
        private int _longestSideCells = 32;

        private PixelGrid _grid;
        private Texture2D _previewTexture;
        private string _message;
        private MessageType _messageType = MessageType.None;

        [MenuItem("JewelPainter/Ảnh thành lưới ô")]
        public static void Open()
        {
            var window = GetWindow<ImageToGridWindow>();
            window.titleContent = new GUIContent("Ảnh thành lưới ô");
            window.minSize = new Vector2(380, 480);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Nguồn", EditorStyles.boldLabel);

            _sourceTexture = (Texture2D)EditorGUILayout.ObjectField(
                "Ảnh", _sourceTexture, typeof(Texture2D), false);

            _palette = (JewelPalette)EditorGUILayout.ObjectField(
                "Bảng màu", _palette, typeof(JewelPalette), false);

            _longestSideCells = EditorGUILayout.IntSlider(
                "Số ô cạnh dài", _longestSideCells, MinCells, MaxCells);

            DrawSizePreview();

            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(!CanGenerate()))
            {
                if (GUILayout.Button("Sinh lưới", GUILayout.Height(28))) Generate();
            }

            if (!CanGenerate())
            {
                EditorGUILayout.HelpBox("Chọn cả ảnh lẫn bảng màu trước khi sinh.", MessageType.Info);
            }

            if (!string.IsNullOrEmpty(_message))
            {
                EditorGUILayout.HelpBox(_message, _messageType);
            }

            if (_grid == null) return;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Kết quả", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Kích thước lưới", $"{_grid.Width} x {_grid.Height} ({_grid.Width * _grid.Height} ô)");

            DrawPreview();

            EditorGUILayout.Space();
            if (GUILayout.Button("Lưu thành asset", GUILayout.Height(28))) Save();
        }

        private void DrawSizePreview()
        {
            if (_sourceTexture == null) return;

            var size = ImageToGridGenerator.CalculateGridSize(
                _sourceTexture.width, _sourceTexture.height, _longestSideCells);

            EditorGUILayout.LabelField("Lưới sẽ là", $"{size.x} x {size.y}");

            var longestImageSide = Mathf.Max(_sourceTexture.width, _sourceTexture.height);
            if (_longestSideCells > longestImageSide)
            {
                EditorGUILayout.HelpBox(
                    $"Số ô ({_longestSideCells}) lớn hơn cạnh dài của ảnh ({longestImageSide} pixel). " +
                    "Lưới sẽ mịn hơn dữ liệu thật nên nhiều ô cạnh nhau bị trùng màu.",
                    MessageType.Warning);
            }
        }

        private bool CanGenerate() => _sourceTexture != null && _palette != null;

        private void Generate()
        {
            try
            {
                _grid = ImageToGridGenerator.Generate(_sourceTexture, _palette, _longestSideCells);
                RebuildPreview();

                if (IsGridEmpty())
                {
                    SetMessage("Lưới sinh ra không có ô nào được tô — ảnh nguồn trong suốt hoàn toàn?",
                        MessageType.Warning);
                }
                else
                {
                    SetMessage($"Đã sinh lưới {_grid.Width} x {_grid.Height}.", MessageType.Info);
                }
            }
            catch (System.Exception exception)
            {
                _grid = null;
                ClearPreview();
                SetMessage(exception.Message, MessageType.Error);
            }
        }

        private bool IsGridEmpty()
        {
            for (var y = 0; y < _grid.Height; y++)
            for (var x = 0; x < _grid.Width; x++)
            {
                if (_grid.GetCell(x, y) != PixelGrid.EmptyCell) return false;
            }

            return true;
        }

        private void RebuildPreview()
        {
            ClearPreview();

            _previewTexture = new Texture2D(_grid.Width, _grid.Height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                hideFlags = HideFlags.HideAndDontSave,
            };

            var colors = _palette.Colors;

            for (var y = 0; y < _grid.Height; y++)
            {
                for (var x = 0; x < _grid.Width; x++)
                {
                    var index = _grid.GetCell(x, y);
                    var color = index == PixelGrid.EmptyCell
                        ? new Color32(0, 0, 0, 0)
                        : colors[index];

                    // Texture2D có y = 0 ở dưới cùng, PixelGrid có y = 0 ở trên cùng
                    _previewTexture.SetPixel(x, _grid.Height - 1 - y, color);
                }
            }

            _previewTexture.Apply();
        }

        private void DrawPreview()
        {
            if (_previewTexture == null) return;

            var scale = Mathf.Min(
                PreviewMaxSize / (float)_previewTexture.width,
                PreviewMaxSize / (float)_previewTexture.height);

            var width = _previewTexture.width * scale;
            var height = _previewTexture.height * scale;

            var rect = GUILayoutUtility.GetRect(width, height, GUILayout.ExpandWidth(false));
            EditorGUI.DrawTextureTransparent(rect, _previewTexture, ScaleMode.ScaleToFit);
        }

        private void Save()
        {
            var defaultName = _sourceTexture != null ? $"{_sourceTexture.name}GridData" : "LevelGridData";

            var path = EditorUtility.SaveFilePanelInProject(
                "Lưu dữ liệu lưới", defaultName, "asset", "Chọn nơi lưu asset");

            if (string.IsNullOrEmpty(path)) return;

            var data = CreateInstance<LevelGridData>();
            data.SetData(_grid.Width, _grid.Height, _palette, _grid.ToArray());

            AssetDatabase.CreateAsset(data, path);
            AssetDatabase.SaveAssets();

            EditorGUIUtility.PingObject(data);
            SetMessage($"Đã lưu vào {path}", MessageType.Info);
        }

        private void SetMessage(string message, MessageType type)
        {
            _message = message;
            _messageType = type;
        }

        private void ClearPreview()
        {
            if (_previewTexture == null) return;

            DestroyImmediate(_previewTexture);
            _previewTexture = null;
        }

        private void OnDisable()
        {
            ClearPreview();
        }
    }
}
```

- [ ] **Step 2: Xác nhận biên dịch sạch và test cũ vẫn xanh**

Quay lại Unity, đợi biên dịch. Console phải sạch lỗi.
Test Runner → EditMode → Run All → 34 test PASS.

- [ ] **Step 3: Kiểm bằng tay**

Chuẩn bị một ảnh PNG bất kỳ trong project (nếu chưa có, kéo một file PNG vào `Assets/`).

Mở `JewelPainter > Ảnh thành lưới ô`, rồi kiểm từng mục:

1. Chưa chọn gì → nút "Sinh lưới" mờ, có dòng nhắc chọn ảnh và bảng màu.
2. Chọn ảnh và `JewelPalette.asset` (tạo ở Task 4) → dòng "Lưới sẽ là" hiện đúng tỉ lệ ảnh.
3. Kéo thanh trượt số ô → dòng "Lưới sẽ là" đổi theo.
4. Bấm "Sinh lưới" → preview hiện ra, ảnh **không bị lộn ngược**.
5. Nếu ảnh chưa bật Read/Write → tool tự bật, không báo lỗi. Kiểm chứng bằng cách mở Import Settings của ảnh, thấy "Read/Write Enabled" đã tick.
6. Đặt số ô lớn hơn cạnh dài của ảnh → hiện cảnh báo màu vàng.
7. Bấm "Lưu thành asset" → chọn chỗ lưu → asset hiện ra và được ping trong Project.
8. Chọn asset vừa lưu, Inspector hiển thị đúng Width, Height, Palette.
9. Đóng cửa sổ rồi mở lại → không có lỗi trong Console (kiểm tra preview texture được dọn đúng).

- [ ] **Step 4: Nối vào một `LevelConfig`**

Tạo hoặc mở một `LevelConfig` asset, kéo `LevelGridData` vừa sinh vào ô `Grid Data`. Xác nhận gán được.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Editor/ImageToGridWindow.cs Assets/Scripts/Editor/ImageToGridWindow.cs.meta
git commit -m "feat: thêm cửa sổ Editor chuyển ảnh thành lưới ô"
```

---

## Tự rà lại kế hoạch

**Phủ spec:** mọi mục trong spec đều có task tương ứng — `PixelGrid` (T1), `PaletteMatcher` + redmean (T2), `GridSampler` + ngưỡng alpha + quy tắc quá nửa (T3), `JewelPalette` 16 màu (T4), `LevelGridData` tách khỏi `LevelConfig` + sửa `LevelConfig` (T5), bật Read/Write tự động + tính kích thước lưới theo tỉ lệ (T6), cửa sổ với ô nhập số ô + preview + các cảnh báo lỗi (T7), cập nhật `CLAUDE.md` (T2 Step 5).

**Bảng lỗi trong spec:** ảnh chưa readable → T6 `EnsureReadable`; chưa chọn ảnh/palette → T7 `CanGenerate`; số ô ≤ 0 → T6 kẹp về 1 và T7 giới hạn thanh trượt tại `MinCells`; số ô lớn hơn ảnh → T7 `DrawSizePreview` cảnh báo; palette rỗng → T6 `Generate` ném lỗi, T7 bắt và hiện đỏ; ảnh toàn trong suốt → T7 `IsGridEmpty` cảnh báo.

**Nhất quán kiểu:** `PixelGrid.EmptyCell` dùng thống nhất ở T1, T5, T6, T7. `SampledCell.IsEmpty` dùng ở T3 và T6. `JewelPalette.Colors` trả `IReadOnlyList<Color32>`, khớp tham số của `PaletteMatcher.FindNearest`. `CalculateGridSize` trả `Vector2Int`, dùng ở T6 và T7 với `.x`/`.y`.

**Chưa có test tự động:** `EnsureReadable` với ảnh thật trong AssetDatabase, và toàn bộ `ImageToGridWindow`. Cả hai cần asset trên đĩa hoặc tương tác giao diện; T7 Step 3 kiểm bằng tay thay thế.
