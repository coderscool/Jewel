# Hiển thị lưới ô màu kèm số — thiết kế

Ngày: 2026-08-04
Trạng thái: đã duyệt, chờ lập kế hoạch triển khai

## Mục tiêu

Khi `LevelManager` nạp một màn chơi, dựng lên trong world một bảng ô màu lấy từ
`LevelGridData`, mỗi ô hiện một con số bằng chỉ số bảng màu. Ô cùng màu tự khắc
cùng số. Người chơi phóng to, thu nhỏ và kéo bảng được.

Ngoài phạm vi lần này: chạm để tô ô, kiểm tra tô đúng sai, thanh chọn màu, hiệu ứng
hoàn thành. Bảng lần này hiện sẵn màu hoàn chỉnh — dùng để xem và để kiểm chứng
bằng mắt rằng tool cắt ảnh chạy đúng.

## Quyết định đã chốt

| Quyết định | Lựa chọn | Lý do |
|---|---|---|
| Nguồn dữ liệu | `LevelGridData` do tool sinh sẵn | Không tốn gì lúc vào màn; `_targetImage` chỉ để đối chiếu |
| Trạng thái đầu | Tô sẵn màu, số chồng lên | Kiểm chứng được tool cắt ảnh trước khi xây phần chơi |
| Cách vẽ màu | Một `Texture2D` bằng đúng kích thước lưới | Một draw call cho mọi ô; tô ô sau này chỉ là ghi pixel |
| Cách vẽ số | Pool `TextMeshPro`, cull theo tầm nhìn | 1024 TMP dựng sẵn là lãng phí; nhìn thấy bao nhiêu dựng bấy nhiêu |
| Kích thước lưới | Đọc từ `LevelGridData.Width/Height` | 32×32 chỉ là trần, không hard-code ở đâu |
| Input | Input System mới | Project đang bật `Input System Package (New)` |
| Số hiển thị | Chỉ số palette **+ 1** | Người chơi thấy 1–16 thay vì 0–15 |

Đã cân nhắc và loại: mỗi ô một GameObject (2048 object, khựng lúc khởi tạo), và
Tilemap (thêm package, số bị đóng cứng thành sprite).

## Kiến trúc

```
Gameplay/Board/
├── BoardLayout.cs        thuần C#: ô ↔ world, ô nào đang lọt tầm nhìn
├── BoardView.cs          dựng Texture2D màu, gắn lên SpriteRenderer
├── BoardNumberLayer.cs   pool TextMeshPro, cull theo camera
└── BoardCamera.cs        zoom, kéo, kẹp biên
```

Đặt ở `Gameplay/` chứ không phải `UI/`: đây là mặt sân chơi và bước sau sẽ nhận chạm
để tô ô. `UI/` giữ nguyên vai trò HUD và popup.

Chiều phụ thuộc không đổi. `Gameplay/Board/` chỉ dùng `Gameplay/Data`,
`Gameplay/Domain`, `Gameplay/Palette` — không `using` lên `UI/`.

### Thay đổi chạm vào code đã có

1. `Gameplay/Interfaces/ILevelService.cs` — thêm `LevelGridData CurrentGrid { get; }`.
2. `Gameplay/Managers/LevelManager.cs` — hiện thực property đó bằng `_currentConfig?.GridData`.
3. `Gameplay/JewelPainter.Gameplay.asmdef` — thêm `Unity.InputSystem` và `Unity.TextMeshPro` vào `references`.
4. `Bootstrap/GameLifetimeScope.cs` — đăng ký ba MonoBehaviour của board.
5. `Bootstrap/GameEntryPoint.cs` — gọi `Init` cho chúng.

## Giao diện các thành phần

### `BoardLayout` (thuần C#)

Dựng từ `gridWidth`, `gridHeight`. Bảng căn giữa gốc toạ độ, mỗi ô rộng một world
unit.

- `Vector2 CellToWorldCenter(int x, int y)` — hàng `y = 0` nằm **trên cùng** trong world
- `bool TryWorldToCell(Vector2 world, out Vector2Int cell)` — false nếu điểm nằm ngoài bảng
- `Bounds WorldBounds` — dùng để kẹp camera
- `RectInt VisibleCells(Rect viewportWorldRect)` — giao của tầm nhìn với bảng, đã kẹp trong biên

Không biết `Texture2D`, không biết camera. Test EditMode được.

### `BoardView`

Nghe `ILevelService.OnLevelStarted`, đọc `CurrentGrid`, dựng lại bảng.

Tạo `Texture2D` đúng `Width × Height` của lưới, `filterMode = Point`, dựng `Sprite`
với `pixelsPerUnit = 1` nên **một pixel là một ô là một world unit**. Ô rỗng ghi
alpha 0.

Texture sinh lúc chạy nên phải `Destroy` khi dựng lại và trong `OnDestroy`, nếu không
mỗi lần vào màn lại rò một texture.

Công khai `BoardLayout Layout { get; }` để hai thành phần kia dùng chung, và
`event Action OnBoardRebuilt` để chúng biết lúc phải dựng lại.

### `BoardNumberLayer`

Giữ pool `TextMeshPro` world-space. Tính lại khi camera đổi vị trí hoặc mức zoom —
**không** mỗi frame.

Mỗi lần tính: lấy `VisibleCells` từ layout, trả các số đã trôi ra ngoài về pool, lấy
số mới cho ô vừa lọt vào. Ô rỗng không hiện số.

Ngưỡng đọc được: ô chiếu lên màn hình nhỏ hơn 14 pixel thì không hiện số nào. Zoom
vào là hiện lại. "Luôn hiện" nghĩa là không có nút bật tắt, không phải là hiện cả khi
không đọc nổi.

### `BoardCamera`

Camera orthographic.

- Cuộn chuột hoặc chụm hai ngón → đổi `orthographicSize`
- Giữ chuột trái kéo, hoặc một ngón kéo → di camera
- `orthographicSize` kẹp giữa: xa nhất là vừa trọn bảng cộng lề, gần nhất là thấy khoảng 5 ô
- Vị trí kẹp theo `Layout.WorldBounds` để không kéo bảng ra khỏi màn hình

Dùng `UnityEngine.InputSystem`: `Mouse.current` cho chuột, `Touchscreen.current.touches`
cho cảm ứng. Không cần `EnhancedTouch`.

## Xử lý lỗi

| Tình huống | Cách xử lý |
|---|---|
| `LevelConfig` chưa gán `GridData` | Xoá bảng, `Debug.LogWarning`, không ném lỗi |
| `GridData` chưa được tool sinh (`ToGrid()` trả null) | Như trên |
| `GridData` chưa gán palette | Như trên |
| Chỉ số ô vượt quá số màu trong palette | Ô đó vẽ trong suốt, cảnh báo **một lần** mỗi lần nạp màn |
| Không có `Touchscreen` hoặc `Mouse` (thiết bị thiếu) | Bỏ qua nhánh đó, không ném lỗi |

Trường hợp chỉ số vượt palette xảy ra thật: xoá bớt màu trong `JewelPalette` sau khi
đã sinh lưới là chỉ số cũ trỏ ra ngoài ngay. Không chặn thì ném `IndexOutOfRange`
giữa lúc chơi.

## Cách kiểm thử

`BoardLayout` thuần C#, test EditMode được:

- đổi ô sang world rồi ngược lại phải về đúng ô cũ
- hàng `y = 0` phải có world y **lớn hơn** hàng cuối — bắt lỗi lật ngược
- `TryWorldToCell` với điểm ngoài bảng phải trả false
- `VisibleCells` khi tầm nhìn trùm cả bảng phải trả trọn lưới
- `VisibleCells` khi tầm nhìn nằm hẳn ngoài bảng phải trả hình chữ nhật rỗng
- lưới không vuông phải ra bounds đúng tỉ lệ

Ba thành phần còn lại kiểm bằng mắt: bảng đúng chiều không lộn ngược, số khớp màu,
zoom kéo không ra khỏi biên, đổi màn thì bảng dựng lại sạch.

Người dùng hiện đang bỏ phần test tự động; mục này giữ lại để dùng khi bật lại.
