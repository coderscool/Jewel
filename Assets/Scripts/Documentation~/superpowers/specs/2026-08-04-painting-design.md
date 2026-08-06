# Tô màu — thiết kế

Ngày: 2026-08-04
Trạng thái: đã duyệt

## Mục tiêu

Thanh màu dưới màn hình hiện những màu ảnh thật sự dùng, kèm số ô còn lại của mỗi
màu. Chọn một màu rồi chạm hoặc kéo trên bảng: ô nào đúng màu đó thì được đặt một
viên ngọc lên.

Ngoài phạm vi lần này: sự kiện hoàn thành màn, gợi ý, hoàn tác, hiệu ứng.

## Quyết định đã chốt

| Quyết định | Lựa chọn | Lý do |
|---|---|---|
| Chạm ô sai màu | Không có gì xảy ra | Người dùng chọn |
| Kéo để tô | Có | Người dùng chọn |
| Phân vai input | Một ngón tô, hai ngón di chuyển và zoom | Kéo không thể vừa tô vừa di chuyển; đây là lối mọi game tô màu lớn dùng |
| Prefab ngọc | Mỗi màu một prefab | Người dùng chọn |
| Khai prefab ở đâu | Thêm ô vào từng dòng `JewelPalette` | Một asset một dòng một màu — không có cách nào lệch nhau |
| Số trên ô đã tô | Giữ nguyên, không ẩn | Ngọc đè lên trên nên không cần |
| Hoàn thành màn | Chưa làm | Người dùng chọn |

## Kiến trúc

```
Gameplay/Domain/PaintState.cs          thuần C#: ô nào đã tô, đếm còn lại theo màu
Gameplay/Interfaces/IPaintService.cs   contract, UI dùng
Gameplay/Managers/PaintManager.cs      giữ PaintState, phát sự kiện
Gameplay/Board/BoardInput.cs           chạm và kéo → toạ độ ô → gọi tô
Gameplay/Board/JewelLayer.cs           pool ngọc, đặt vào ô
UI/Views/ColorPaletteBar.cs            thanh màu
UI/Views/ColorSwatchView.cs            một ô màu trong thanh
```

Bám cấu trúc đã có: `PaintState` cạnh `PlayerProgress` trong `Domain/`,
`PaintManager` cạnh `LevelManager` trong `Managers/`. Không dựng thư mục mới.

### Luồng

`PaintManager` nghe `OnLevelStarted` rồi dựng `PaintState`. `BoardInput` đổi điểm
chạm sang toạ độ ô và gọi `TryPaint`. Tô được thì `PaintManager` bắn
`OnCellPainted(cell, paletteIndex)`. `JewelLayer` và `ColorPaletteBar` cùng nghe sự
kiện đó — một bên đặt ngọc, một bên trừ số còn lại.

Bảng không biết thanh màu tồn tại và ngược lại.

### Phân vai input

| Thao tác | Việc |
|---|---|
| Một ngón, hoặc chuột trái | Tô |
| Hai ngón | Di chuyển và zoom |
| Chuột phải | Di chuyển |
| Cuộn chuột | Zoom |

`BoardCamera` bỏ nhánh kéo một ngón. Hai file cùng đọc input nhưng không chồng lấn:
`BoardInput` bỏ qua khi có từ hai ngón, `BoardCamera` bỏ qua khi chỉ có một.

## Thay đổi chạm vào code đã có

1. `JewelPalette.Entry` — thêm `GameObject jewelPrefab`, thêm `GetJewelPrefab(int)`.
2. `BoardView` — thêm `JewelPalette Palette { get; }` để `JewelLayer` lấy prefab.
3. `BoardCamera` — bỏ kéo một ngón; hai ngón di chuyển theo trung điểm; chuột phải thay chuột trái.
4. `Gameplay.asmdef` — thêm `UnityEngine.UI` (cần `EventSystem` để chặn tô xuyên qua thanh màu).
5. `GameLifetimeScope`, `GameEntryPoint` — đăng ký và nối bốn thành phần mới.

## Hai cái bẫy xử lý sẵn

**Chạm vào thanh màu không được tô ô phía sau.** Thanh màu nằm trên Canvas đè lên
bảng. `BoardInput` kiểm `EventSystem.IsPointerOverGameObject()` **một lần lúc bắt
đầu nét** rồi khoá cả nét — kiểm mỗi frame thì kéo tay ra khỏi nút là lại tô.

**Kéo chậm không tô lại một ô nhiều lần.** `BoardInput` nhớ ô vừa xử lý; trùng thì
bỏ qua. Nhớ cả khi tô hụt, để không thử lại mỗi frame trên ô sai màu.

## Xử lý lỗi

| Tình huống | Cách xử lý |
|---|---|
| Dòng palette thiếu prefab | Cảnh báo một lần mỗi màn, ô vẫn tính là đã tô |
| Chạm ngoài bảng | Không làm gì |
| Chọn màu không có trong ảnh | Bỏ qua |
| Chưa chọn màu mà chạm | Không làm gì |
| Chưa nạp lưới | Mọi thao tác tô trả false |

## Hiệu năng

Tô kín bảng 32×32 là 1024 GameObject ngọc sống cùng lúc. **Chưa làm culling theo
tầm nhìn** — sprite tĩnh gộp batch khá tốt, và thêm culling lúc này là tối ưu mù.
Nếu Profiler cho thấy tụt frame thì thêm sau, dùng lại đúng cơ chế của
`BoardNumberLayer`.

Ngọc dùng pool riêng theo từng màu, đổi màn mới trả về pool — không `Destroy`.

## Cách kiểm thử

`PaintState` thuần C#, test EditMode được:

- tô đúng màu → true, bộ đếm màu đó giảm 1
- tô sai màu → false, không đổi gì
- tô lại ô đã tô → false, bộ đếm không giảm thêm
- tô ô rỗng → false
- toạ độ ngoài bảng → false, không ném
- `UsedPaletteIndices` bỏ qua ô rỗng và sắp xếp tăng dần
- `IsComplete` chỉ đúng khi mọi ô không rỗng đều đã tô

Phần còn lại kiểm bằng mắt theo checklist trong hướng dẫn setup.
