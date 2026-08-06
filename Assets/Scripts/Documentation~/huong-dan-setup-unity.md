# JewelPainter — hướng dẫn setup Unity từ số không

Làm theo đúng thứ tự. Asset phải có trước, vì các ô trong scene sẽ trỏ vào chúng.

---

## Nếu bạn đã setup theo bản hướng dẫn cũ

Kiến trúc vừa đổi. Chỉ **một** việc dưới đây là bắt buộc — sinh lại `LevelGridData`.
Phần còn lại là dọn code chết: để nguyên cũng không nổ, không ai tham chiếu tới
chúng nữa, nhưng giữ lại thì lần sau đọc code sẽ nhầm.

- [ ] Xoá file `Assets/Scripts/Gameplay/Palette/JewelPalette.cs` (xoá cả thư mục `Palette`)
- [ ] Xoá file `Assets/Scripts/Gameplay/Board/JewelLayer.cs`
- [ ] Xoá file `Assets/Scripts/Gameplay/Board/HintLayer.cs`
- [ ] Xoá asset `Assets/Settings/JewelPalette.asset`
- [ ] Xoá file `Assets/Scripts/Gameplay/Board/HintOverlay.cs` — đã quay lại dùng
      `HintLayer` với sprite; giữ cả hai chỉ gây nhầm
- [ ] GameObject `Hints` và prefab `CellHint` thì **giữ lại**, xem 5.8
- [ ] **BẮT BUỘC** — sinh lại mọi `LevelGridData` bằng tool. Asset cũ lưu tham chiếu
      tới bảng màu dùng chung đã bị bỏ, nên giờ không còn màu nào; bảng sẽ trống và
      Console báo `không có màu nào`

Prefab `Jewel_01` giữ lại nhưng **phải đổi ảnh sang tông trắng hoặc xám nhạt** —
xem 4.3.

**Vì sao đổi:** màu giờ rút thẳng từ từng ảnh nên không còn bảng màu dùng chung. Và
ô đã tô được vẽ vào texture thay vì mỗi ô một GameObject — nhờ vậy lưới 64×64
(4096 ô) tốn đúng bằng lưới 16×16.

---

## Phần 0 — Kiểm tra package

- [ ] **DOTween** — cài từ Asset Store, rồi `Tools > Demigiant > DOTween Utility Panel
      > Setup DOTween...`. Chỉ dùng phần core nên **không cần** tick module nào.
- [ ] **VContainer** — `Window > Package Manager > In Project`. Chưa có thì cài qua
      `+ > Install package from git URL...`:
      `https://github.com/hadashiA/VContainer.git?path=VContainer/Assets/VContainer`
- [ ] **Input System** — `Unity Registry`, tìm `Input System`, Install. Rồi
      `Edit > Project Settings > Player > Other Settings > Active Input Handling`
      = `Input System Package (New)` hoặc `Both`
- [ ] **TextMeshPro** — `Window > TextMeshPro > Import TMP Essential Resources`
- [ ] Console sạch lỗi đỏ trước khi đi tiếp

---

## Phần 1 — Tạo thư mục

Trong `Assets/`, tạo: `Settings`, `Prefabs`, `Levels`, `Images`.

---

## Phần 2 — Asset cấu hình

### 2.1 Cấu hình popup

- [ ] `Assets > Create > JewelPainter > UI > Popup Config`
- [ ] Lưu vào `Assets/Settings/`, tên `PopupConfig`, để `Entries` rỗng

### 2.2 Cấu hình âm thanh

- [ ] `Assets > Create > JewelPainter > Core > Sound Config`
- [ ] Lưu vào `Assets/Settings/`, tên `SoundConfig`, để `Entries` rỗng

---

## Phần 3 — Sinh lưới từ ảnh

### 3.1 Chuẩn bị ảnh

- [ ] Kéo một file PNG vào `Assets/Images/`

Ảnh vẽ mảng màu rõ cho kết quả đẹp nhất. Nền trong suốt thì những chỗ đó thành ô
không tô. Không cần chỉnh Import Settings — tool tự bật `Read/Write Enabled`.

### 3.2 Chạy tool

- [ ] Mở `JewelPainter > Ảnh thành lưới ô`
- [ ] Ô `Ảnh`: kéo file PNG vào
- [ ] `Số ô ngang` và `Số ô dọc`: nhập riêng từng cạnh, tối đa 64
- [ ] Bấm **Theo tỉ lệ ảnh** nếu muốn giữ đúng tỉ lệ gốc — nó lấy cạnh dài hiện tại
      làm mốc rồi tính cạnh kia. Tỉ lệ lệch quá thì tool cảnh báo hình sẽ bị kéo méo.
- [ ] `Số màu tối đa`: 32 (ảnh ít màu hơn thì bảng màu tự ngắn lại)
- [ ] `Gộp màu gần giống`: 24

`Gộp màu gần giống` chạy sau khi cắt: những màu sát nhau bị gộp làm một, nên bảng
màu thật sự thường ngắn hơn số tối đa. 0 là không gộp, 20 gộp các sắc độ rất sát,
60 gộp mạnh tay. Số màu cuối cùng hiện ở phần Kết quả.

Đây là cách rẻ nhất để rút gọn thanh chọn màu: thay vì hạ `Số màu tối đa` và mất
những màu thật sự khác biệt, cứ để cao rồi tăng mức gộp — thuật toán tự bỏ những
sắc độ trùng lặp mà giữ lại các màu khác nhau.
- [ ] Bấm **Sinh lưới**
- [ ] Xem preview: hình phải nhận ra được và **không lộn ngược**
- [ ] Xem dải màu ngay dưới preview — đây là bảng màu tool rút ra, kiểm xem có bắt
      đúng tông ảnh không
- [ ] Bấm **Lưu thành asset**, lưu vào `Assets/Levels/`

Bảng màu nằm luôn trong asset vừa lưu. Mỗi màn một bộ màu riêng.

### 3.3 Tạo cấu hình màn chơi

- [ ] `Assets > Create > JewelPainter > Gameplay > Level Config`
- [ ] Lưu vào `Assets/Levels/`, tên `Level01`
- [ ] Gán:
  - `Level Id` = **1** (bắt buộc, người chơi mới bắt đầu ở level 1)
  - `Target Image` = ảnh PNG (tuỳ chọn, chỉ để đối chiếu)
  - `Grid Data` = asset vừa lưu ở 3.2
  - `Time Limit Seconds` = 0
  - `Camera Min Size` = 0, `Camera Max Size` = 0 (để 0 là tự tính)
  - `Fade Switch Size` = **0** (để 0 là dùng giá trị trên component)

**Ba ô zoom của một màn đều là `orthographicSize`**, đọc từ xa tới gần:

| Ô | Ý nghĩa | Ví dụ |
|---|---|---|
| `Camera Max Size` | mức lúc vào màn, cũng là xa nhất | 35 |
| `Fade Switch Size` | lớp màu tan hết, viền ô hiện đủ | 12 |
| `Camera Min Size` | gần nhất | 9 |

Để 0 thì dùng giá trị đặt sẵn trên component tương ứng.

`Fade Switch Size` còn là mốc dưới để `BoardNumberLayer` tính lúc nào hiện số — để
trống ô này thì lớp số quay về ngưỡng pixel, xem bước 5.7.

---

## Phần 4 — Prefab

### 4.1 Prefab chữ số

- [ ] `GameObject > 3D Object > Text - TextMeshPro` (**3D Object**, không phải `UI > Text`)
- [ ] Đổi tên `CellNumber`
- [ ] `TextMeshPro`: `Font Size` = 4, `Alignment` = Center + Middle,
      `Wrapping` = Disabled, `Overflow` = Overflow
- [ ] `RectTransform`: `Width` = 1, `Height` = 1
- [ ] Mục `Extra Settings`: `Order in Layer` = **3**
- [ ] Kéo vào `Assets/Prefabs/`, xoá khỏi Hierarchy

`Order in Layer = 3` đặt số **trên** bảng màu và **trên** lớp gợi ý, nên marker gợi ý
không che mất số. Đổi lại số luôn hiện chứ không còn ẩn hiện theo hiệu ứng mờ — độ
lộn xộn lúc zoom xa do ô `Min Cell Screen Pixels` lo, không phải do thứ tự lớp.

### 4.2 Prefab ô màu

- [ ] `GameObject > UI > Button - TextMeshPro`, đổi tên `ColorSwatch`
- [ ] Xoá object `Text (TMP)` con mặc định
- [ ] `Add Component` → `Color Swatch View`
- [ ] Tạo hai con:
  - `UI > Image` tên `ColorFill` — phủ kín nút
  - `UI > Text - TextMeshPro` tên `Number` — đặt giữa
- [ ] Trên `ColorFill` và `Number`: **bỏ tick `Raycast Target`**
- [ ] Gán vào `Color Swatch View`: `Color Image` = `ColorFill`,
      `Number Text` = `Number`, `Button` = chính nó
- [ ] `Text Color` = **đen** (dùng chung cho cả số thứ tự lẫn số ô còn lại)
- [ ] Ba ô dưới mục **Tuỳ chọn** để trống lúc này
- [ ] `RectTransform`: `Width` = 140, `Height` = 140
- [ ] Kéo vào `Assets/Prefabs/`, xoá khỏi Hierarchy

Ba phần tuỳ chọn thêm sau khi nào cần:

| Ô | Con cần tạo | Bỏ trống thì |
|---|---|---|
| `Remaining Text` | `UI > Text - TMP` tên `Remaining`, góc dưới, cỡ nhỏ | không hiện số ô còn lại |
| `Selected Highlight` | `UI > Image` tên `SelectedHighlight`, viền dày, tắt sẵn | không có dấu hiệu màu đang chọn |
| `Progress Ring` | `UI > Image` tên `ProgressRing` — xem dưới | không có vòng tiến độ |

**Vòng tiến độ** hiện phần trăm số ô đã tô của riêng màu đó:

- [ ] Import ảnh vòng tròn: `Texture Type` = `Sprite (2D and UI)`
- [ ] Chuột phải `ColorSwatch` → `UI > Image`, đổi tên `ProgressRing`
- [ ] `RectTransform`: phủ kín ô màu, hoặc to hơn chút để vòng nằm ngoài rìa
- [ ] `Source Image` = ảnh vòng tròn
- [ ] `Image Type` = **Filled**
- [ ] `Fill Method` = **Radial 360**
- [ ] `Fill Origin` = Top, tick `Clockwise`
- [ ] **Bỏ tick `Raycast Target`**
- [ ] Kéo `ProgressRing` vào ô `Progress Ring` của `Color Swatch View`

Code chỉ gán `fillAmount` từ 0 tới 1, Unity lo phần vẽ cung tròn — không cần shader
hay animation. Đặt `Color` của Image này khác màu ô để vòng nổi lên.

### 4.3 Prefab viên ngọc

Ô tô xong sẽ hiện viên ngọc này, **nhuộm theo màu của ô**.

- [ ] Vẽ hoặc tải một ảnh ngọc **tông trắng hoặc xám nhạt**, 128×128 trở lên

Đây là điểm dễ sai nhất: code nhân màu ô vào sprite, nên ảnh gốc phải gần trắng.
Ảnh ngọc màu xanh mà nhuộm đỏ sẽ ra nâu đen. Vẽ hình khối, bóng đổ và highlight
bằng các sắc độ xám, để màu thật do code gán.

- [ ] Import: `Sprite (2D and UI)`, `Pixels Per Unit` = **đúng bằng cạnh ảnh**,
      **bỏ tick `Generate Mip Maps`**, `Filter Mode` = Bilinear
- [ ] `GameObject > 2D Object > Sprite`, đổi tên `Jewel_01`
- [ ] Gán ảnh, đặt `Order in Layer` = **4**
- [ ] Kéo vào `Assets/Prefabs/`, xoá khỏi Hierarchy

`Pixels Per Unit` bằng cạnh ảnh làm viên ngọc vừa khít một ô. Mip map làm sprite mờ
khi thu nhỏ — đúng thứ không muốn.

---

## Phần 5 — Dựng scene

```
SampleScene
├── GameLifetimeScope
├── Main Camera
├── Audio
├── LevelManager
├── PaintManager
├── LevelFlow
├── Board                    ← vị trí (0, 0, 0)
│   ├── Numbers              ← vị trí (0, 0, 0)
│   ├── GridLines            ← vị trí (0, 0, 0)
│   ├── Hints                ← vị trí (0, 0, 0)
│   └── Jewels               ← vị trí (0, 0, 0)
├── Canvas                   ← tĩnh: HUD, popup
│   ├── Hud
│   │   └── LevelText
│   └── Popups
├── PaletteCanvas            ← động, tách riêng để khỏi rebuild Canvas tĩnh
│   └── PaletteScroll
│       └── Viewport
│           └── PaletteBar
└── EventSystem
```

### 5.1 GameLifetimeScope

- [ ] Create Empty, tên `GameLifetimeScope`, `Add Component > Game Lifetime Scope`

### 5.2 Main Camera

- [ ] `Projection` = **Orthographic**, `Position` = **(0, 0, -10)**
- [ ] `Add Component > Board Camera`, ô `Camera` = chính nó, ô `Board View` để trống
- [ ] `Pan Margin Cells` = **2**

Cho kéo ra ngoài mép bảng thêm 2 ô. Sát mép thì ngón tay che mất chính ô đang định tô
ở hàng ngoài cùng. Để 0 là khoá sát mép như trước.

### 5.3 Audio

- [ ] Create Empty, tên `Audio`
- [ ] `Add Component > Sound Service` (Unity tự thêm một `AudioSource`)
- [ ] `Add Component` thêm một `AudioSource` **thứ hai**
- [ ] `Config` = `SoundConfig`, `Sfx Source` = AudioSource 1, `Music Source` = AudioSource 2
- [ ] Bỏ tick `Play On Awake` ở cả hai

### 5.4 LevelManager

- [ ] Create Empty, tên `LevelManager`, `Add Component > Level Manager`
- [ ] `Levels`: Size = 1, `Element 0` = `Level01`

### 5.5 PaintManager

- [ ] Create Empty, tên `PaintManager`, `Add Component > Paint Manager`

Không gán gì, `GameEntryPoint` nối dây lúc chạy.

### 5.5B LevelFlow

- [ ] Create Empty, tên `LevelFlow`, `Add Component > Level Flow Controller`
- [ ] `Delay Seconds` = 1.5

Tô kín bảng thì chờ ngần đó giây rồi tự sang màn kế. Không còn màn nào mang id tiếp
theo thì **dừng lại**, và tiến trình **không** tăng — tăng rồi là lần mở game sau nạp
một màn không tồn tại, bảng trống trơn.

Muốn có nhiều màn thì thêm `LevelConfig` vào mảng `Levels` của `LevelManager`, với
`Level Id` chạy liên tiếp 1, 2, 3...

### 5.6 Board

- [ ] Create Empty, tên `Board`, `Position` = **(0, 0, 0)** — bắt buộc
- [ ] `Add Component > Sprite Renderer`, đặt `Order in Layer` = **0**
- [ ] `Add Component > Board View`, ô `Renderer` = chính nó, tick `Grayscale`
- [ ] `Add Component > Board Color Fade`: `Camera` = Main Camera, `Renderer` = chính nó,
      `Board View` = chính nó, `Opaque Size` = **0**, `Transparent Size` = **12**,
      `Level Size Is Opaque` = **bỏ tick**

`Opaque Size` = 0 nghĩa là lấy mức zoom lúc vào màn làm mốc đục. Từ đó phóng to dần
thì ảnh mờ dần, và **đến `orthographicSize` = 12 là mất hẳn** — zoom sát hơn nữa vẫn
trong suốt, không mờ thêm gì.

Hai mốc này là `orthographicSize` tuyệt đối, không phải tỉ lệ. Đặt `Opaque Size` lớn
hơn `Transparent Size` thì chiều mờ đảo lại: đục khi phóng sát, mờ khi kéo ra xa.
- [ ] `Add Component > Board Input`: `Camera` = Main Camera, `Board View` để trống

### 5.7 Numbers

- [ ] Chuột phải `Board` → Create Empty, tên `Numbers`, `Position` = (0, 0, 0)
- [ ] `Add Component > Board Number Layer`
- [ ] `Camera` = Main Camera, `Number Prefab` = `CellNumber`, `Root` = chính nó
- [ ] `Show At Zoom Progress` = **0.6**
- [ ] `Min Cell Screen Pixels` = 32 (đường lui, xem dưới)

`Show At Zoom Progress` đo theo **dải zoom của từng màn**: mốc trên là mức lúc vào màn
(`Camera Max Size`), mốc dưới là `Fade Switch Size`. 0 là số hiện ngay khi vào màn,
1 là mãi tới lúc lớp màu tan hết, 0.6 là đi được 60% quãng đường giữa hai mốc.

Muốn số hiện **sớm hơn** thì **hạ** con số này xuống.

Nhờ đo tương đối nên màn nào cũng cho cảm giác như nhau. Ngưỡng pixel cố định thì màn
có `Camera Max Size` lớn phải kéo sâu hơn hẳn mới thấy số.

`Min Cell Screen Pixels` chỉ dùng khi `LevelConfig` để trống `Fade Switch Size` — lúc
đó không có mốc dưới để chia tỉ lệ.

### 5.7B GridLines

Không cần ảnh nào — viền sinh thẳng bằng code.

- [ ] Chuột phải `Board` → Create Empty, tên `GridLines`, `Position` = (0, 0, 0)
- [ ] `Add Component > Sprite Renderer`, `Order in Layer` = **1**, ô `Sprite` **để trống**
- [ ] `Add Component > Board Grid Lines`:
  - `Renderer` = chính nó
  - `Pixels Per Cell` = **16**
  - `Line Thickness` = **1**
  - `Inset` = **0**
  - `Line Color` = trắng

Chỉ ô **có màu** mới có khung; ô rỗng thì không. Khung của mỗi ô nằm **gọn trong ô
đó**, không tràn sang ô bên.

**Chỉnh độ mảnh:** giữ `Line Thickness` = 1 và nâng `Pixels Per Cell`. Đường luôn dày
`Thickness / Pixels Per Cell` so với bề rộng ô, nên 16 cho đường bằng 1/16 ô, 32 cho
mảnh gấp đôi thế. Texture này dựng **một lần** lúc vào màn rồi không đụng tới nữa,
nên nâng lên 32 cũng không tốn gì lúc chơi.

**`Inset`** thụt khung vào trong ô. Để 0 thì khung sát mép, hai ô cạnh nhau có hai
đường dính nhau. Tăng lên 2 thì mỗi ô là một khung tách rời, có khe hở rõ ràng.
- [ ] `Add Component > Board Color Fade` — **đảo ngược hai mốc so với bảng**:
  - `Camera` = Main Camera, `Renderer` = chính nó, `Board View` = `Board`
  - `Opaque Size` = **12**
  - `Transparent Size` = **0** (dùng mức zoom lúc vào màn)
  - `Level Size Is Opaque` = **tick**

Tick ô đó vì với viền, `Fade Switch Size` trong `LevelConfig` là mốc **hiện đủ**,
còn với lớp màu nó là mốc **tan hết** — cùng một con số nhưng nằm ở hai vai ngược
nhau. Tick sai thì điền `Fade Switch Size` sẽ làm hai lớp mờ cùng chiều.

Đảo hai mốc là được hiệu ứng ngược: zoom ra thì bảng đục và viền mờ, zoom vào qua
mốc 12 thì bảng biến mất và viền hiện đủ. Hai lớp giao nhau đúng tại 12, nên lúc nào
cũng nhìn ra được ranh giới các ô.

Không có dòng code fade nào cho viền — dùng lại y nguyên component của bảng.

### 5.8 Hints

Trước hết cần prefab marker:

- [ ] Import ảnh hoạ tiết (12×12 của bạn): `Sprite (2D and UI)`,
      `Pixels Per Unit` = **12** (bằng cạnh ảnh), bỏ tick `Generate Mip Maps`
- [ ] `GameObject > 2D Object > Sprite`, đổi tên `CellHint`, gán ảnh
- [ ] `Order in Layer` = **2**
- [ ] Kéo vào `Assets/Prefabs/`, xoá khỏi Hierarchy

Rồi dựng object trong scene:

- [ ] Chuột phải `Board` → Create Empty, tên `Hints`, `Position` = (0, 0, 0)
- [ ] `Add Component > Hint Layer`
- [ ] `Camera` = Main Camera, `Hint Prefab` = `CellHint`, `Root` = chính nó
- [ ] `Min Cell Screen Pixels` = **5**, `Prewarm Count` = 400

**Vì sao ngưỡng ở đây thấp hơn hẳn lớp số và lớp ngọc (14):** ngọc bị cull thì ô vẫn
còn màu trong texture nên không ai nhận ra, còn marker bị cull là **mất hẳn dấu hiệu**.
Mà gợi ý lại chủ yếu dùng lúc zoom xa để dò xem còn sót ô nào. Đặt ngưỡng cao là nó
biến mất đúng lúc cần nhất.

Đổi lại, zoom xa thì nhiều marker cùng tồn tại. Thấy khựng thì nâng
`Min Cell Screen Pixels` lên, chấp nhận mất gợi ý ở mức zoom xa nhất.

### 5.8B Jewels

- [ ] Chuột phải `Board` → Create Empty, tên `Jewels`, `Position` = (0, 0, 0)
- [ ] `Add Component > Jewel Layer`
- [ ] `Camera` = Main Camera, `Jewel Prefab` = `Jewel_01`, `Root` = chính nó
- [ ] `Min Cell Screen Pixels` = 14, `Prewarm Count` = 200

Chỉ sinh ngọc cho ô **đang lọt trong tầm nhìn**, nên bảng tô kín 4000 ô vẫn chỉ có
chừng trăm viên tồn tại. Kéo camera thì viên trôi ra ngoài được thu về, viên mới lấy
ra dùng lại.

Cull được là nhờ texture của bảng vẫn giữ màu bên dưới — viên ngọc bị gỡ thì ô đó
vẫn còn nguyên màu, không ai nhận ra có gì biến mất.

### 5.8C JewelFly

- [ ] Chuột phải `Board` → Create Empty, tên `JewelFly`, `Position` = (0, 0, **-1**)
- [ ] `Add Component > Jewel Fly Effect`
- [ ] `Jewel Prefab` = `Jewel_01`, `Root` = chính nó
- [ ] `Duration` = 0.35, `Jump Power` = 2.5, `Start Scale` = 0.5
- [ ] `Max Concurrent` = 24, `Prewarm Count` = 24

`z = -1` để viên đang bay nằm trên mọi lớp khác.

Viên bay ra **từ ô màu trên thanh chọn**, nên `ColorPaletteBar` phải gán ô
`World Camera` (bước 5.10) — thiếu thì không có hiệu ứng, ngọc hiện ngay lập tức
chứ không nổ.

Ngọc ở ô đích chỉ hiện **khi viên bay đáp xuống**. Lớp màu trong texture vẫn đổi ngay
lúc tô nên ô không bị trống trong lúc chờ.

### 5.9 Canvas và HUD

- [ ] `GameObject > UI > Canvas` (Unity tự tạo kèm `EventSystem`)
- [ ] `Render Mode` = Screen Space - Overlay, `Sort Order` = 0
- [ ] `Canvas Scaler`: Scale With Screen Size, 1080 × 1920
- [ ] `EventSystem` báo lỗi Input System thì bấm **Replace with InputSystemUIInputModule**
- [ ] Chuột phải `Canvas` → Create Empty, tên `Hud`, `Add Component > Hud View`
- [ ] Chuột phải `Hud` → `UI > Text - TextMeshPro`, tên `LevelText`, neo góc trên trái
- [ ] Gán `LevelText` vào ô `Level Text` của `Hud View`
- [ ] Chuột phải `Canvas` → Create Empty, tên `Popups`,
      `Add Component > Popup Manager`, `Config` = `PopupConfig`, `Root` = chính nó

### 5.10 Thanh chọn màu cuộn ngang

Tách Canvas riêng: đổi gì trên một Canvas là Unity dựng lại **toàn bộ** Canvas đó,
mà số ô còn lại nhảy liên tục lúc bạn kéo tay tô.

- [ ] `GameObject > UI > Canvas`, tên `PaletteCanvas`, `Sort Order` = 1
- [ ] `Canvas Scaler` đặt giống `Canvas` chính

- [ ] Chuột phải `PaletteCanvas` → `UI > Scroll View`, đổi tên `PaletteScroll`
- [ ] `RectTransform`: neo đáy màn hình, giãn ngang, `Height` = 180
- [ ] Trên `Scroll Rect`: tick `Horizontal`, **bỏ tick `Vertical`**
- [ ] Xoá hai object con `Scrollbar Horizontal` và `Scrollbar Vertical`
- [ ] Trên `Scroll Rect`, để trống hai ô `Horizontal Scrollbar` và `Vertical Scrollbar`

- [ ] Đổi tên object `Content` thành `PaletteBar`
- [ ] Trên `PaletteBar`: `Add Component > Horizontal Layout Group`
      (Child Alignment = Middle Left, Spacing = 12, Padding trái phải = 20,
      bỏ tick cả hai `Child Force Expand`)
- [ ] `Add Component > Content Size Fitter`:
      `Horizontal Fit` = **Preferred Size**, `Vertical Fit` = Unconstrained
- [ ] `Add Component > Color Palette Bar`:
      `Swatch Prefab` = `ColorSwatch`, `Root` = chính `PaletteBar`,
      **`World Camera` = `Main Camera`**

`World Camera` để hiệu ứng ngọc bay biết đổi vị trí ô màu trên màn hình sang world.
Bỏ trống thì tô vẫn chạy, chỉ mất hiệu ứng bay.

`Content Size Fitter` là thứ làm thanh dài ra theo số màu để cuộn được. Thiếu nó thì
32 ô màu bị nén vào bề rộng cố định.

- [ ] **Lưu scene**

---

## Phần 6 — Chạy thử

**Bảng:**

- [ ] Console sạch lỗi
- [ ] Bảng hiện giữa màn hình, dạng xám, **không lộn ngược**
- [ ] `LevelText` hiện `Level 1`
- [ ] Cuộn chuột → phóng to thu nhỏ, không vượt hai đầu giới hạn
- [ ] Phóng to → lớp màu mờ dần, số hiện ra; thu nhỏ → ngược lại
- [ ] Chuột **phải** kéo → bảng di chuyển

**Tô màu:**

- [ ] Thanh dưới màn hình hiện các ô màu, **chỉ những màu ảnh dùng**
- [ ] Kéo ngang thanh màu → cuộn được, thấy hết các màu
- [ ] Bấm một ô màu → những ô tô được bằng màu đó **sáng lên trên bảng**
- [ ] Chuột trái bấm vào ô sáng → ô đó hiện **viên ngọc màu tương ứng**
- [ ] Viên ngọc đúng màu của ô, không bị ám nâu đen (ám là do ảnh gốc chưa trắng)
- [ ] Zoom xa hết cỡ → ngọc biến mất, ô vẫn giữ **màu phẳng** (đúng thiết kế)
- [ ] Zoom vào lại → ngọc hiện lại đúng những ô đã tô

**Thắng màn:**

- [ ] Tô kín bảng → chờ khoảng 1,5 giây → tự sang màn kế, `LevelText` đổi số
- [ ] Màn mới dựng đúng ảnh của nó, thanh màu đổi theo
- [ ] Ở màn cuối tô kín → **dừng lại**, Console ghi một dòng thông báo, không có lỗi
- [ ] Thoát Play rồi vào lại → đúng màn đang chơi dở, không quay về màn 1
- [ ] Bấm vào ô **không** sáng → không có gì xảy ra
- [ ] Giữ chuột trái kéo **bắt đầu từ ô có gợi ý** → tô liên tiếp
- [ ] Giữ chuột trái kéo **bắt đầu từ ô không có gợi ý** → **bảng di chuyển**
- [ ] Đang kéo di chuyển mà đi ngang qua ô có gợi ý → **không** đột ngột chuyển sang tô
- [ ] Bấm vào thanh màu rồi kéo → không tô, cũng không làm bảng chạy
- [ ] Bấm sang màu khác → vùng sáng chuyển sang các ô của màu mới
- [ ] Tô thêm ô → vòng tiến độ trên ô màu đó nhích lên
- [ ] Tô hết một màu → **ô màu đó biến mất khỏi thanh**, các ô còn lại dồn sang
- [ ] Số trên mọi ô màu đều **đen**, kể cả trên ô màu tối
- [ ] Chuột phải kéo → luôn di chuyển được, kể cả đứng trên ô có gợi ý
- [ ] Kéo tới mép bảng → đi được thêm khoảng 2 ô ra ngoài rồi mới dừng
- [ ] Zoom xa nhất (thấy trọn bảng) → bảng vẫn khoá giữa, không trôi lệch
- [ ] Trên điện thoại: một ngón theo cùng luật trên, hai ngón luôn là di chuyển và zoom

**Hiệu năng (thử với ảnh 64 ô cạnh dài):**

- [ ] Trên máy thật: FPS chạm 60, không phải 30 — `ApplicationSettings` lo việc này,
      không cần gán gì trong scene
- [ ] Để yên vài phút không chạm: màn hình **không** tự tắt

- [ ] Kéo tay tô nhanh không giật
- [ ] Bấm chọn màu không khựng
- [ ] Tô gần kín bảng vẫn mượt như lúc mới vào

---

## Phần 7 — Lỗi hay gặp

| Lỗi | Nguyên nhân | Xử lý |
|---|---|---|
| `VContainerException: X is not in this scene` | Thiếu object mang component X | Phần 5 |
| `NullReferenceException` ở `PopupManager.Awake` | Ô `Config` chưa gán | 5.9 |
| `NullReferenceException` ở `HudView.Init` | Ô `Level Text` chưa gán | 5.9 |
| Cảnh báo `chưa gán GridData` | `Level01` thiếu `Grid Data` | 3.3 |
| Cảnh báo `không có màu nào` | `LevelGridData` sinh từ bản tool cũ | Sinh lại ở 3.2 |
| Không thấy bảng | `Board` không ở `(0,0,0)`, hoặc camera không Orthographic | 5.2, 5.6 |
| Số chồng nhau hoặc bé li ti | `Font Size` trong prefab `CellNumber` | 4.1 |
| Số bị marker gợi ý che | `CellNumber` chưa đặt `Order in Layer` = 3 | 4.1 |
| Chọn màu không thấy marker nào | `Hint Prefab` chưa gán | 5.8 — Console có cảnh báo |
| Marker biến mất khi zoom xa | `Min Cell Screen Pixels` đặt cao quá | Hạ về 5, xem 5.8 |
| Marker bị lớp màu che | `CellHint` chưa đặt `Order in Layer` = 2 | 5.8 |
| Viền không hiện khi zoom vào | `BoardColorFade` trên `GridLines` chưa đảo hai mốc | 5.7B |
| Khung quá dày | Giữ `Line Thickness` = 1, nâng `Pixels Per Cell` lên 24 hoặc 32 | 5.7B |
| Khung răng cưa khi zoom sát | Nâng `Pixels Per Cell` — texture này tĩnh nên không tốn gì | 5.7B |
| Hai ô cạnh nhau dính khung | Tăng `Inset` lên 1–2 để chừa khe hở | 5.7B |
| Marker to hoặc nhỏ hơn ô | `Pixels Per Unit` chưa bằng cạnh ảnh (12) | 5.8 |
| Zoom xa bị khựng | Quá nhiều marker cùng lúc | Nâng `Min Cell Screen Pixels` |
| Thanh màu không cuộn được | Thiếu `Content Size Fitter` trên `PaletteBar` | 5.10 |
| Thanh màu trống trơn | `PaintManager` chưa có trong scene, hoặc `Swatch Prefab` chưa gán | 5.5, 5.10 |
| Bấm thanh màu lại tô nhầm ô | Scene thiếu `EventSystem` | 5.9 |
| Kéo mãi không tô được | Nét bắt đầu từ ô **không** có gợi ý nên camera nhận — bấm đúng ô sáng rồi mới kéo | — |
| Kéo mãi không di chuyển được bảng | Nét bắt đầu từ ô có gợi ý nên bị nhận làm nét tô — bấm chỗ trống, hoặc dùng chuột phải | — |
| Zoom kẹt một mức | `Camera Min Size` lớn hơn `Camera Max Size` | Console có cảnh báo, sửa ở 3.3 |
| Ngọc bị ám nâu đen | Ảnh gốc `Jewel_01` có sẵn màu, nhân với màu ô ra màu bẩn | Đổi sang ảnh tông trắng, 4.3 |
| Tô xong không thấy ngọc | `Jewel Prefab` chưa gán, hoặc đang zoom quá xa | 5.8B — Console có cảnh báo nếu thiếu prefab |
| Ngọc bị số hoặc gợi ý che | `Jewel_01` chưa đặt `Order in Layer` = 4 | 4.3 |
| Ngọc to hoặc nhỏ hơn ô | `Pixels Per Unit` chưa bằng cạnh ảnh | 4.3 |

---

## Ba lớp chồng nhau — để dễ hình dung

| Lớp | Order in Layer | Vai trò |
|---|---|---|
| Bảng màu (texture) | 0 | Ô chưa tô xám, ô đã tô màu thật. Luôn có, không bao giờ bị cull |
| Viền ô (texture) | 1 | Chỉ quanh ô có màu. Dựng một lần lúc vào màn |
| Gợi ý (sprite) | 2 | Chỉ sinh cho ô tô được **đang nhìn thấy** |
| Số | 3 | Trên gợi ý để marker không che số |
| Ngọc (sprite) | 4 | Trên số — ô tô xong thì ngọc che số, thành dấu hiệu "đã xong" |

Hai lớp texture có chi phí cố định, không phụ thuộc số ô. Hai lớp sprite bị cull theo
tầm nhìn nên số object phụ thuộc mức zoom, không phụ thuộc kích thước bảng.

Ngưỡng ẩn khác nhau có chủ ý: ngọc và số ẩn ở 14 pixel, marker gợi ý ẩn ở 5. Ngọc bị
cull thì ô vẫn còn màu trong texture, còn marker bị cull là mất hẳn dấu hiệu.
