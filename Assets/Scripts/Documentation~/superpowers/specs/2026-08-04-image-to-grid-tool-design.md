# Tool chuyển ảnh thành lưới ô màu — thiết kế

Ngày: 2026-08-04
Trạng thái: đã duyệt, chờ lập kế hoạch triển khai

## Mục tiêu

Cho phép người làm game kéo một ảnh vào cửa sổ trong Unity Editor, nhập số ô mỗi
cạnh, và nhận về dữ liệu lưới: mỗi ô mang một chỉ số trỏ vào bảng màu dùng chung
của toàn game. Dữ liệu này là đầu vào cho màn chơi tô màu.

Ngoài phạm vi lần này: bản chạy runtime cho người chơi tự chọn ảnh từ máy họ.
Thiết kế phải để ngỏ khả năng đó nhưng không xây.

## Quyết định đã chốt

| Quyết định | Lựa chọn | Lý do |
|---|---|---|
| Nơi chạy | Editor tool | Sinh màn chơi lúc phát triển; không cần lo hiệu năng mobile hay quyền truy cập ảnh |
| Bảng màu | Cố định, dùng chung toàn game | Thanh chọn màu ở UI ổn định, sprite viên ngọc tái dùng, kiểm soát được tông màu toàn game |
| Số ô | Người dùng nhập, mặc định 32 | Mỗi ảnh một độ mịn khác nhau |
| Ảnh không vuông | Nhập số ô cạnh dài, cạnh kia suy ra theo tỉ lệ | Không cắt xén, không bóp méo |
| Vùng trong suốt | Ô quá nửa diện tích trong suốt → "không tô" | Ảnh có nền trong suốt là trường hợp phổ biến |
| Domain và UnityEngine | Cho phép struct giá trị (`Color32`, `Vector2Int`) | Không cần scene hay GameObject; test EditMode vẫn chạy |

Đã cân nhắc và loại: median cut và k-means (tự sinh palette riêng cho từng ảnh).
Cả hai cho màu sát bản gốc hơn, nhưng mỗi màn một bảng màu khác nhau khiến UI chọn
màu phải dựng động và tổng thể game dễ loạn tông.

## Kiến trúc

```
Gameplay/
├── Palette/
│   └── JewelPalette.cs        ScriptableObject: danh sách màu + tên
├── Domain/
│   ├── PixelGrid.cs           ma trận chỉ số, thuần C#
│   ├── GridSampler.cs         Color32[] → màu trung bình mỗi ô
│   └── PaletteMatcher.cs      màu → chỉ số gần nhất trong palette
└── Data/
    └── LevelGridData.cs       ScriptableObject: dữ liệu lưới đã sinh

Editor/
├── JewelPainter.Editor.asmdef  "includePlatforms": ["Editor"]
├── ImageToGridWindow.cs        cửa sổ: ô ảnh, ô nhập số cạnh, Generate, preview
└── ImageToGridGenerator.cs     ráp Domain lại, ghi asset
```

Toàn bộ tính toán nằm ở `Domain/`, thuần C#, không MonoBehaviour. `Editor/` chỉ là
lớp vỏ: nhận input, gọi xuống, ghi asset. Muốn làm bản runtime sau này thì viết lớp
vỏ mới, phần lõi dùng lại nguyên vẹn.

`Editor/` phụ thuộc `Gameplay` và `Core`. Không ai phụ thuộc ngược vào `Editor/`.
Chiều phụ thuộc chung của project không đổi: `Bootstrap → UI → Gameplay → Core`.

### Vì sao tách `LevelGridData` khỏi `LevelConfig`

`LevelConfig` là file người ta chỉnh tay. `LevelGridData` bị tool ghi đè toàn bộ mỗi
lần sinh lại. Trộn chung nghĩa là mỗi lần chạy tool lại làm bẩn file đang chỉnh tay.
`LevelConfig` chỉ giữ một tham chiếu sang `LevelGridData`.

## Giao diện các thành phần

### `PixelGrid` (Domain)

Giữ `width`, `height`, và mảng chỉ số phẳng. Chỉ số `EmptyCell` biểu diễn ô không tô.
Có `GetCell(x, y)`. Không biết gì về `Texture2D` hay `ScriptableObject`.

### `GridSampler` (Domain)

Vào: mảng `Color32` cùng kích thước ảnh, kích thước ảnh, kích thước lưới mong muốn.
Ra: mảng `Color32` một phần tử mỗi ô, kèm cờ đánh dấu ô rỗng.

Chia ảnh thành các ô chữ nhật, lấy trung bình cộng các pixel trong ô. Pixel có alpha
dưới 128 (tức dưới 50%) không tham gia trung bình. Ô có quá nửa số pixel nằm dưới
ngưỡng đó → ô rỗng.

Trung bình cộng thực hiện trong không gian sRGB. Về mặt lý thuyết nên chuyển sang
tuyến tính trước khi cộng, nhưng ở mức 32 ô thì sai khác không nhìn thấy, và làm vậy
sẽ thêm một bước chuyển đổi cho mọi pixel.

### `PaletteMatcher` (Domain)

Vào: một `Color32` và danh sách màu palette. Ra: chỉ số màu gần nhất.

Dùng công thức redmean thay vì khoảng cách Euclid thẳng trên RGB. Thêm khoảng mười
dòng nhưng bám sát cảm nhận mắt người hơn đáng kể — khoảng cách RGB thẳng hay đẩy
tông da người sang xanh lá.

### `JewelPalette` (Gameplay/Palette)

`ScriptableObject` chứa danh sách cặp (tên, màu). Bảng mặc định 16 màu: đen, trắng,
hai mức xám, đỏ, hồng, cam, vàng, hai mức xanh lá, xanh ngọc, hai mức xanh dương,
tím, nâu, be. Người dùng sửa trực tiếp trong Inspector sau khi tool sinh ra.

### `ImageToGridWindow` (Editor)

Cửa sổ `EditorWindow` mở từ menu. Gồm: ô chọn `Texture2D`, ô chọn `JewelPalette`,
ô nhập số ô cạnh dài (mặc định 32), nút Generate, vùng preview lưới kết quả, nút Save.

Preview hiện trước khi lưu để người dùng thử vài mức độ mịn rồi mới quyết định.

## Xử lý lỗi

| Tình huống | Cách xử lý |
|---|---|
| Ảnh chưa bật Read/Write | Tool tự bật qua `TextureImporter` rồi reimport — không bắt người dùng đi sửa tay |
| Chưa chọn ảnh hoặc palette | Nút Generate mờ đi, kèm dòng nhắc |
| Số ô nhập vào ≤ 0 | Kẹp về tối thiểu 1, hiện cảnh báo |
| Số ô lớn hơn kích thước ảnh | Cho chạy nhưng cảnh báo: lưới sẽ mịn hơn dữ liệu thật, nhiều ô trùng màu |
| Palette rỗng | Chặn, báo lỗi rõ ràng |
| Ảnh toàn trong suốt | Sinh ra lưới toàn ô rỗng, cảnh báo |

## Cách kiểm thử

`GridSampler` và `PaletteMatcher` nhận mảng `Color32` và trả về số — test EditMode
được, không cần Play Mode, không cần scene.

Các ca:

- ảnh một màu → mọi ô cùng một chỉ số
- ảnh chia đôi hai màu → nửa trái một chỉ số, nửa phải chỉ số khác
- ảnh có vùng trong suốt → đúng những ô đó là ô rỗng
- ảnh không vuông → kích thước lưới đúng tỉ lệ
- ảnh nhỏ hơn số ô yêu cầu → không ném lỗi, không chia cho không
- màu nằm giữa hai màu palette → chọn đúng màu gần hơn theo redmean

## Việc kèm theo

Sửa `LevelConfig`: thêm một trường tham chiếu sang `LevelGridData`. Đây là thay đổi
duy nhất chạm vào code đã có.

Cập nhật `CLAUDE.md`: ghi rõ ngoại lệ `Domain/` được dùng struct giá trị của
UnityEngine (`Color32`, `Vector2Int`), kèm lý do — để sau này không ai hiểu nhầm là
quy tắc bị phá lỏng dần.
