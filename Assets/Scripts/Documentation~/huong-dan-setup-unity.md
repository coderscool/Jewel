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

Quay lại điền `Entries` sau khi làm xong prefab popup ở mục 4.5.

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
- [ ] Chuột phải `ColorSwatch` → `Create Empty`, đổi tên `Content`, phủ kín nút
- [ ] Tạo hai con **bên trong `Content`**:
  - `UI > Image` tên `ColorFill` — phủ kín
  - `UI > Text - TextMeshPro` tên `Number` — đặt giữa
- [ ] Trên `ColorFill` và `Number`: **bỏ tick `Raycast Target`**
- [ ] Gán vào `Color Swatch View`: `Color Image` = `ColorFill`,
      `Number Text` = `Number`, `Button` = chính nó
- [ ] `Text Color` = **đen** (dùng chung cho cả số thứ tự lẫn số ô còn lại)
- [ ] `Rise Target` = **`Content`**, `Selected Rise` = **24**
- [ ] Ba ô dưới mục **Tuỳ chọn** để trống lúc này

**`Content` bắt buộc phải là object con**, không nâng thẳng `ColorSwatch` được:
`Horizontal Layout Group` điều khiển vị trí con trực tiếp của nó nên sẽ kéo về ngay
lần dựng lại kế tiếp.

**Nút `ColorSwatch` nên có `Image` nền riêng.** `ColorFill` giờ **chỉ hiện ở ô đang
chọn**, nên ô chưa chọn sẽ trống trơn nếu không có nền phía sau.
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

### 4.4 Hiệu ứng lấp lánh — dựng bằng Particle System

Ba hiệu ứng dựng hoàn toàn trong Editor từ hai sprite sheet có sẵn. Không thêm dòng
code nào: Particle System tự chạy khi GameObject được bật, mà `JewelLayer` thì bật/tắt
viên ngọc từ kho — nên hiệu ứng tự phát đúng lúc ngọc hiện ra.

Vì sao Particle System chứ không phải Animator: một Animator là một component nặng,
mà lúc zoom xa có tới vài trăm viên ngọc sống cùng lúc. Particle System còn cho **ngẫu
nhiên hoá** (delay, cỡ, frame bắt đầu) ngay trong Inspector — thứ Animator phải viết
code mới có, và thiếu nó thì cả bảng nhấp nháy đồng loạt như đèn báo.

#### Bước 1 — Cắt sprite sheet

Làm với **từng** file: `Shining_1.png` (192×32, 6 frame) và `Shining_2.png` (256×32, 8 frame).

- [ ] Chọn file, Inspector: `Texture Type` = Sprite (2D and UI)
- [ ] `Sprite Mode` = **Multiple**
- [ ] `Pixels Per Unit` = **32** (bằng cạnh một frame)
- [ ] `Filter Mode` = **Point (no filter)** — giữ nét pixel
- [ ] `Compression` = None
- [ ] **Bỏ tick** `Generate Mip Maps`
- [ ] `Apply`
- [ ] `Sprite Editor` → `Slice` → `Type` = **Grid By Cell Size** → `Pixel Size` = **32 × 32**
- [ ] `Slice` → `Apply` → đóng cửa sổ

Xong sẽ thấy mũi tên xổ ra ở file trong Project: `Shining_1_0…5`, `Shining_2_0…7`.

`Pixels Per Unit` = 32 là chỗ dễ sai nhất. Để mặc định 100 thì hiệu ứng chỉ bằng
1/3 viên ngọc.

#### Bước 2 — Material phát sáng

- [ ] Chuột phải `Assets/Materials` → `Create > Material`, tên `SparkleAdditive`
- [ ] `Shader` = **Universal Render Pipeline/Particles/Unlit** (URP)
      hoặc **Mobile/Particles/Additive** (Built-in)
- [ ] `Surface Type` = Transparent, `Blending Mode` = **Additive**
- [ ] Ô texture để **trống** — Particle System tự nạp sprite vào

Additive nghĩa là cộng ánh sáng vào nền: vệt trắng trên viên ngọc đỏ ra hồng sáng,
đúng cảm giác phản quang. Để Alpha Blend thì vệt trắng đè kín màu ngọc, trông như
dán giấy.

Muốn giữ đúng tông pixel art gốc thì dùng `Sprites-Default` thay vì Additive — lấp
lánh sẽ đục hơn nhưng sắc nét hơn.

#### Bước 3 — VFX 1: Vệt sáng quét

Vệt sáng quét qua ô ngay khi viên ngọc đáp xuống, phát **đúng một lần cho mỗi ô
trong cả màn**.

> ⚠️ **Đừng gắn Particle System vào trong prefab `Jewel_01`.** `JewelLayer` thu viên
> ngọc về kho khi ô trôi ra ngoài khung hình rồi lấy ra dùng lại khi ô trở vào — hệ hạt
> nằm trong đó, bật `Play On Awake`, sẽ chạy lại mỗi lần viên được bật. Kéo camera qua
> lại là cả vùng đã tô loé sáng như vừa tô xong. Hiệu ứng phải là prefab **đứng riêng**,
> do code gọi đúng lúc ô có ngọc.

- [ ] `GameObject > Effects > Particle System`, đổi tên `JewelLandBurst`
- [ ] `Position` = (0, 0, 0), **`Rotation` = (0, 0, 0)**

Unity tạo Particle System mới với `Rotation` = **(-90, 0, 0)** — đó là lý do hạt bay
lên trên. Đưa về (0, 0, 0) rồi điều khiển hướng bằng `Velocity over Lifetime` (xem
Bước 3b), dễ đọc hơn nhiều so với vặn Euler.

Điền các module — bỏ tick những module không nhắc tới:

**Main**

| Ô | Giá trị | Vì sao |
|---|---|---|
| Duration | `0.4` | 6 frame ÷ 15 fps |
| Looping | **tắt** | quét một lần rồi thôi |
| Start Delay | `0` | |
| Start Lifetime | `0.4` | phải **bằng** Duration |
| Start Speed | `0` | vệt sáng đứng yên trên ô |
| Start Size | `1` | vừa khít một ô |
| Start Color | trắng, alpha 255 | |
| Gravity Modifier | `0` | |
| Simulation Space | `Local` | |
| **Play On Awake** | **TẮT** | code gọi Play(), bật là nó tự nổ lung tung |
| Stop Action | `None` | kho lo phần thu về |
| Max Particles | `2` | |

**Emission**

- [ ] `Rate over Time` = **0**
- [ ] `Bursts`: bấm `+`, đặt `Time` = 0, `Count` = **1**, `Cycles` = 1

**Shape** — bỏ tick hẳn module này (hạt sinh đúng tâm viên ngọc).

**Texture Sheet Animation** — bật

| Ô | Giá trị |
|---|---|
| Mode | **Sprites** |
| Sprites | bấm `+` 6 lần, gán `Shining_1_0` … `Shining_1_5` **đúng thứ tự** |
| Time Mode | `Lifetime` |
| Frame over Time | để nguyên đường chéo 0→1 |
| Start Frame | `0` |
| Cycles | `1` |

**Renderer**

| Ô | Giá trị |
|---|---|
| Render Mode | `Billboard` |
| Material | `SparkleAdditive` |
| Sorting Layer | cùng layer với viên ngọc |
| **Order in Layer** | **12** |
| Culling Mode | `Pause` |

`Order in Layer` = 12 nằm giữa **4** (viên ngọc, mục 4.3) và **15** (viên đang bay,
mục 5.8C): lấp lánh phủ lên ngọc, nhưng viên đang bay vẫn che được tất cả.

- [ ] Kéo vào `Assets/Prefabs/`, **xoá khỏi Hierarchy**

**Object trong scene** — làm sau khi đã dựng `Board` ở Phần 5:

- [ ] Chuột phải `Board` → Create Empty, tên `JewelLand`, `Position` = (0, 0, 0)
- [ ] `Add Component > Particle Burst Pool`
  - `Prefab` = `JewelLandBurst`, `Root` = chính nó
  - `Prewarm Count` = 32, `Max Concurrent` = 160, `Min Alive Seconds` = 0.3
- [ ] `Add Component > Jewel Land Sparkle`
  - `Camera` = Main Camera, `Burst Pool` = **chính object này**
  - `Min Cell Screen Pixels` = 14

`ParticleBurstPool` là kho dùng chung: nó tạo, thu hồi và đếm xem hệ hạt chạy xong
chưa. `JewelLandSparkle` chỉ nghe sự kiện "ngọc vừa đáp" rồi bảo kho loé ở toạ độ nào.
Popup ăn mừng ở Bước 4 cũng dùng đúng kiểu kho này, chỉ khác lúc gọi.

Nhờ tách ra, số hệ hạt sống cùng lúc phụ thuộc **số ô vừa tô**, không phụ thuộc số
ngọc đang hiện — kéo xa thấy 200 viên vẫn chỉ có vài hệ hạt đang chạy.

#### Bước 3b — Cho vệt sáng trượt theo hướng mong muốn

Trong Particle System có **hai thứ tên là rotation**, và chúng làm việc khác hẳn nhau:

| Ô | Điều khiển | Dùng khi |
|---|---|---|
| `Transform > Rotation` | **đường đi** của hạt (trục phát của Shape) | đổi hướng bay |
| `Main > Start Rotation` | **ảnh** bị xoay quanh tâm nó | nghiêng chính cái vệt |
| `Renderer > Flip` | lật ảnh theo X hoặc Y | đảo chiều vệt, không vỡ nét |

Muốn vệt sáng **trượt chéo xuống dưới**, đừng vặn `Transform > Rotation`. Cách gọn hơn:

- [ ] `Transform > Rotation` = (0, 0, 0)
- [ ] `Main > Start Speed` = **0**
- [ ] Module `Shape` = **tắt**
- [ ] Bật module **Velocity over Lifetime**
- [ ] `Linear` = `X` **1.2**, `Y` **-1.2**, `Z` 0
- [ ] `Space` = **Local**

X dương là sang phải, Y âm là xuống dưới — đọc thẳng ra hướng, không phải suy từ Euler.

Quãng đường đi được = tốc độ × `Start Lifetime`. Với 1.2 và vòng đời 0.4 giây thì vệt
trượt khoảng **0.5 ô** — đủ thấy chuyển động mà không rời khỏi viên ngọc. Để 3 trở lên
là vệt bay hẳn sang ô bên cạnh.

Nếu vẫn muốn làm bằng `Transform > Rotation` (hạt phát theo trục **+Z cục bộ**):

| Hướng | Rotation (X, Y, Z) |
|---|---|
| Lên | (-90, 0, 0) ← mặc định Unity |
| Xuống | (90, 0, 0) |
| Phải | (0, 90, 0) |
| Trái | (0, -90, 0) |
| **Chéo xuống-phải** | **(45, 90, 0)** |
| Chéo xuống-trái | (45, -90, 0) |
| Chéo lên-phải | (-45, 90, 0) |

Nhớ đặt `Start Speed` > 0 (ví dụ 1.2), vì cách này lấy hướng từ trục phát chứ không
phải từ vận tốc.

Còn muốn **bản thân cái vệt nghiêng đi** thì đó là `Main > Start Rotation`. Nhưng
sprite pixel art xoay góc lẻ sẽ vỡ nét — chỉ nên dùng bội số của 90, hoặc dùng
`Renderer > Flip` để đảo chiều mà không đụng tới pixel.

#### Bước 4 — VFX 2: Loé sáng khi tô xong một màu

Tô hết sạch ô của một màu thì **mọi ô mang màu đó loé lên cùng lúc**.

Phần hình ảnh vẫn dựng ở đây, trong Editor. Nhưng riêng hiệu ứng này cần một script
nhỏ (`ColorCompleteSparkle`) làm nhiệm vụ bấm nút: Editor không có cách nào tự biết
"màu số 3 vừa xong" — đó là trạng thái trong game. Script chỉ trả lời *lúc nào* và
*ở những ô nào*, không đụng gì tới hình ảnh.

Dựng giống hệt VFX 1: prefab đứng riêng, **tắt `Play On Awake`**, lấy ra dùng qua một
`ParticleBurstPool`. Chỉ khác ở chỗ ai gọi và gọi lúc nào.

- [ ] `GameObject > Effects > Particle System`, đổi tên `ColorCompleteBurst`
- [ ] `Position` = (0, 0, 0), **`Rotation` = (0, 0, 0)**

**Main**

| Ô | Giá trị | Vì sao |
|---|---|---|
| Duration | `0.6` | |
| Looping | **tắt** | loé một lần |
| Start Delay | `Random Between Two Constants`, **0 → 0.12** | xem ghi chú dưới |
| Start Lifetime | `0.57` | 8 frame ÷ 14 fps |
| Start Speed | `0` | |
| Start Size | `Random Between Two Constants`, **0.7 → 1.1** | |
| Start Rotation | `0` | pixel art xoay là vỡ nét |
| Simulation Space | `Local` | |
| **Play On Awake** | **TẮT** | code gọi Play(), bật là nó tự nổ lung tung |
| Max Particles | `3` | |
| Stop Action | `None` | code lo phần thu về kho |

Đổi một ô sang `Random Between Two Constants`: bấm mũi tên nhỏ ▾ ở **cuối** dòng đó.

`Start Delay` ngẫu nhiên 0–0.12 giây là chỗ đáng chú ý nhất. Nổ chính xác cùng một
frame nghe thì đúng yêu cầu, nhưng nhìn ra một mảng phẳng bẹt loé rồi tắt. Lệch nhau
vài phần trăm giây thì mắt vẫn đọc là "cùng lúc" mà hiệu ứng có chiều sâu, như pháo
hoa lan ra. Muốn đúng nghĩa đồng loạt thì để `Start Delay` = 0.

**Emission**

- [ ] `Rate over Time` = **0**
- [ ] `Bursts`: bấm `+`, `Time` = 0, `Count` = **2**, `Cycles` = 1

**Shape** — bật

| Ô | Giá trị |
|---|---|
| Shape | `Box` |
| Scale | `(0.6, 0.6, 0)` |

Hộp nhỏ này làm hai ngôi sao rơi lệch tâm mỗi cái một chỗ, thay vì chồng lên nhau
giữa ô.

**Texture Sheet Animation** — bật

| Ô | Giá trị |
|---|---|
| Mode | **Sprites** |
| Sprites | 8 ô, gán `Shining_2_0` … `Shining_2_7` |
| Time Mode | `Lifetime` |
| **Start Frame** | `Random Between Two Constants`, **0 → 8** |
| Cycles | `1` |

**Renderer** — y hệt Bước 3 (`SparkleAdditive`, `Order in Layer` = 12,
`Culling Mode` = `Pause`).

- [ ] Kéo vào `Assets/Prefabs/`, **xoá khỏi Hierarchy**

**Object trong scene**

- [ ] Chuột phải `Board` → Create Empty, tên `ColorComplete`, `Position` = (0, 0, 0)
- [ ] `Add Component > Particle Burst Pool`
  - `Prefab` = `ColorCompleteBurst`, `Root` = chính nó
  - `Prewarm Count` = **300**, `Max Concurrent` = **0** (bỏ trần)
  - `Min Alive Seconds` = **0.3** — phải **lớn hơn** `Start Delay` lớn nhất ở trên
- [ ] `Add Component > Color Complete Sparkle`
  - `Camera` = Main Camera, `Burst Pool` = **chính object này**
  - `Max Per Frame` = **0** — cả màu loé cùng một lúc
  - `Visible Cells Only` = tick, `Log Burst Count` = bỏ tick
  - `Min Cell Screen Pixels` = 14

**`Max Per Frame` = 0 là cả màu loé cùng một lúc.** Đặt một số dương thì hiệu ứng rải
ra nhiều frame cho nhẹ máy — đó là nhịp rải, KHÔNG phải giới hạn: ô chưa tới lượt nằm
chờ frame sau, không ô nào bị bỏ.

| Nhịp | Màu 300 ô |
|---|---|
| **0 (cùng lúc)** | **1 frame** |
| 40/frame | 8 frame (133ms) |
| 12/frame | 25 frame (417ms) |

> ⚠️ **Loé cùng lúc thì `Prewarm Count` của kho phải đủ lớn.** Kho thiếu hàng thì phải
> `Instantiate` bù ngay trong frame đó — cả trăm Particle System trong một frame là cú
> khựng thấy rõ, đúng vào khoảnh khắc đáng lẽ phải đã mắt nhất.
>
> Đặt `Prewarm Count` **lớn hơn số ô của màu nhiều ô nhất trong màn**, và `Max Concurrent`
> cũng vậy (hoặc để 0). Việc dựng sẵn chạy lúc vào màn, khi màn hình chờ đang che.

**`Max Concurrent` của kho quyết định nó loé XONG nhanh hay chậm**, không còn quyết
định loé được bao nhiêu ô. Chạm trần thì ô đó nằm chờ trong hàng, không bị mất:

| `Max Concurrent` | Màu 200 ô |
|---|---|
| 400 | đủ 200 ô, xong sau 83ms |
| 160 | đủ 200 ô, xong sau 617ms |
| 32 | đủ 200 ô, xong sau **3.6 giây** — lê thê thấy rõ |

Vì mỗi lần loé sống khoảng 0.6 giây, đặt trần thấp hơn số ô của màu là hiệu ứng phải
chia làm nhiều đợt nối nhau. Đặt `Max Concurrent` **lớn hơn số ô của màu nhiều ô nhất
trong màn** thì cả màu loé cùng một lượt. Để 0 là bỏ trần.

**`Visible Cells Only` là thủ phạm hay gặp nhất của "sao nó không loé hết".** Tô xong
một màu thì thường bạn đang zoom sát, nên phần lớn ô của màu đó nằm ngoài khung hình và
không được xếp hàng. Bỏ tick thì loé cả những ô đó — đổi lại tốn hệ hạt cho thứ không
ai nhìn thấy.

Bật **`Log Burst Count`** để biết chắc. Nó in ra Console tổng số ô của màu trên lưới và
số ô thật sự xếp hàng:

```
[ColorComplete] màu 1: 214 ô trên lưới, 38 ô xếp hàng loé.
Thiếu 176 ô nằm ngoài khung hình — bỏ tick Visible Cells Only nếu muốn loé cả những ô đó.
```

Hai con số khớp nhau mà vẫn thấy thiếu thì nguyên nhân nằm ở `Max Concurrent`, không
phải ở đây.

Mỗi hiệu ứng có **kho riêng** vì hai prefab khác nhau. Đừng dùng chung một
`ParticleBurstPool` cho cả `JewelLand` lẫn `ColorComplete` — kho chỉ giữ được một prefab.

Vì sao chặn ở 120 ô: một màu trên bảng 64×64 có thể chiếm hơn 500 ô. Script chỉ phát
ở ô **đang lọt trong khung hình**, và cắt tiếp ở 120 — mắt không đếm được hơn chừng
đó đốm sáng nổ cùng lúc, nhưng máy thì vẫn phải vẽ đủ.

`Min Alive Seconds` để script biết chờ bao lâu rồi mới tin là hệ hạt đã chạy xong.
Đặt nhỏ hơn `Start Delay` thì nó thu hệ hạt về kho ngay trước khi hạt đầu tiên kịp
bắn ra, và bạn thấy đúng một nửa số ô loé.

#### Bước 5 — VFX 3: Lấp lánh dày

Đúng viên thứ ba trong ảnh mẫu: có cả vệt quét lẫn sao, và sao dày hơn.

- [ ] Trong Project chọn `ColorCompleteBurst`, `Ctrl+D` để nhân đôi thành `ColorCompleteBurstBig`
- [ ] `Bursts > Count` = **4**, `Max Particles` = **6**
- [ ] `Shape > Scale` = `(0.9, 0.9, 0)` — sao văng rộng hơn
- [ ] Thêm module **Size over Lifetime**, đường cong đi xuống — sao to rồi teo dần

Thử cả hai bằng cách đổi ô `Prefab` trên `Particle Burst Pool` của `ColorComplete`,
xem cái nào hợp bảng của bạn. Bảng nhỏ chịu được bản dày, bảng 64×64 thì bản thường đã đủ chật.

#### Bước 6 — Xem thử ngay trong Editor

- [ ] Kéo `JewelLandBurst` hoặc `ColorCompleteBurst` vào Scene, chọn nó
- [ ] Cửa sổ **Particle Effect** hiện ở góc dưới phải Scene View
- [ ] Bấm `Restart` để xem lại từ đầu, `Playback Speed` = 0.2 để soi từng frame
- [ ] Xong nhớ **xoá khỏi Hierarchy**

Cửa sổ Particle Effect gọi Play() giùm bạn, nên xem thử được cả prefab đã tắt
`Play On Awake`.

Hiệu ứng không hiện lúc **chơi thật** thì soi ba ô này trước: ô `Sprites` có đủ frame
không, `Order in Layer` có lớn hơn của viên ngọc không, và ô `Prefab` trên
`Particle Burst Pool` đã gán chưa. Cả hai prefab đều phải **tắt** `Play On Awake`.

#### Bước 7 — Giá phải trả về hiệu năng

Cả hai VFX đều lấy hệ hạt từ `ParticleBurstPool`, nên số hệ hạt sống cùng lúc phụ
thuộc **số sự kiện vừa xảy ra**, không phụ thuộc số ngọc đang hiện trên màn. Kéo xa
thấy 200 viên ngọc vẫn chỉ có vài hệ hạt đang chạy.

Cắt bớt chi phí:

- [ ] `Max Particles` để **2** — không bao giờ để mặc định 1000
- [ ] `Culling Mode` = **Pause** — viên ra ngoài khung hình thì ngừng tính
- [ ] Tắt hẳn `Collision`, `Trails`, `Lights`, `Noise`, `Sub Emitters`
- [ ] Mọi VFX dùng **chung một material** → Unity gộp được draw call
- [ ] `Min Cell Screen Pixels` trên `JewelLand` tăng từ 14 lên **20–24** — zoom xa thì
      thôi không loé, ở cỡ đó cũng chẳng nhìn ra

Cách kiểm chứng, đừng đoán:

- [ ] `File > Build Profiles` → bật **Development Build** + **Autoconnect Profiler**
- [ ] Build lên máy thật, mở `Window > Analysis > Profiler`
- [ ] Xem dòng **ParticleSystem.Update** trong tab CPU khi zoom xa nhất

Quá nặng thì hạ `Max Per Burst` xuống 60, hoặc hạ `Max Concurrent` của kho. Bỏ hẳn
VFX 1 cũng được — chỉ cần xoá component `Jewel Land Sparkle`, không phải sửa gì khác.

### 4.5 Popup bộ sưu tập

Bày ảnh của mọi màn chơi; màn chưa tới thì xám lại và đeo ổ khoá. Chỉ để xem —
bấm vào ô không làm gì.

Ảnh lấy từ ô `Target Image` của từng `LevelConfig`, nên màn nào bỏ trống ô đó sẽ hiện
một ô rỗng. Điền trước rồi hãy làm bước này.

#### Prefab một ô tranh

- [ ] `GameObject > UI > Image`, tên `CollectionItem`, `Width/Height` = 220
- [ ] Chuột phải nó → `UI > Image`, tên `Artwork`, kéo giãn kín ô cha
- [ ] Chuột phải nó → `UI > Image`, tên `LockIcon`, đặt giữa, gán sprite ổ khoá
- [ ] Chuột phải nó → `UI > Text - TextMeshPro`, tên `LevelText`, neo góc dưới trái
- [ ] Chọn `CollectionItem` → `Add Component > Collection Item View`
- [ ] `Artwork` = `Artwork`, `Level Text` = `LevelText`, `Lock Icon` = `LockIcon`
- [ ] `Locked Tint` = xám tối, ví dụ `(107, 107, 115)`
- [ ] Kéo vào `Assets/Prefabs/`, xoá khỏi Hierarchy

> `Locked Tint` **làm ảnh tối đi, không rút màu ra**. Màu của Image là phép nhân, mà
> nhân với xám thì đỏ vẫn ra đỏ sẫm. Muốn xám thật thì gán một material dùng shader
> greyscale vào ô `Locked Material` — bỏ trống thì chỉ dùng tint, vẫn đọc được là
> "chưa mở" nhờ ổ khoá.

#### Prefab popup

- [ ] `GameObject > UI > Panel`, tên `CollectionPopup`, kéo giãn kín màn hình
- [ ] `Add Component > Canvas Group` (bắt buộc — `PopupView` yêu cầu)
- [ ] Chuột phải nó → `UI > Scroll View`, tên `LevelScroll`, tắt `Horizontal`
- [ ] Chọn `LevelScroll > Viewport > Content`:
  - `Add Component > Grid Layout Group`, `Cell Size` = 220 × 220, `Spacing` = 16
  - `Add Component > Content Size Fitter`, `Vertical Fit` = **Preferred Size**
- [ ] Chuột phải `CollectionPopup` → `UI > Button - TextMeshPro`, tên `CloseButton`
- [ ] Chọn `CollectionPopup` → `Add Component > Collection Popup View`
- [ ] `Canvas Group` = chính nó, `Item Prefab` = `CollectionItem`
- [ ] `Item Root` = **`Content`** (không phải `LevelScroll`)
- [ ] `Close Button` = `CloseButton`
- [ ] Kéo vào `Assets/Prefabs/`, xoá khỏi Hierarchy

`Content Size Fitter` là chỗ hay quên: thiếu nó thì `Content` giữ nguyên chiều cao ban
đầu, các ô tràn ra ngoài mà thanh cuộn không chạy được ô nào.

**Không** gán gì vào `On Click ()` của `CloseButton` — `CollectionPopupView` tự đăng ký
trong `Awake`.

#### Khai vào PopupConfig

- [ ] Mở `Assets/Settings/PopupConfig`
- [ ] `Entries`: Size = 1, `Key` = **Collection**, `Prefab` = `CollectionPopup`

Thiếu bước này thì bấm nút chỉ thấy một dòng đỏ trong Console, popup không hiện.

Popup được tạo **một lần** ở lần mở đầu tiên rồi bật/tắt để tái dùng. Danh sách ô dựng
lại ở mỗi lần mở, nên qua được một màn là ổ khoá của màn kế rơi ra ngay lần mở sau.

### 4.6 Popup thắng màn

Hiện ra khi tô xong bức tranh, có nút sang màn kế. Màn chơi **không tự chuyển** nữa —
bức tranh vừa hoàn thành đứng yên bao lâu tuỳ người chơi.

- [ ] `GameObject > UI > Panel`, tên `WinPopup`, kéo giãn kín màn hình
- [ ] `Add Component > Canvas Group` (bắt buộc — `PopupView` yêu cầu)
- [ ] Chuột phải nó → `UI > Text - TextMeshPro`, tên `LevelText`
- [ ] Chuột phải nó → `UI > Button - TextMeshPro`, tên `NextButton`
- [ ] Chuột phải nó → `UI > Text - TextMeshPro`, tên `LastLevelNotice`,
      nội dung kiểu "Hết màn rồi!", **tắt object này đi**
- [ ] Chuột phải nó → `UI > Image`, tên `Banner` (băng CONGRATULATION), neo trên
- [ ] Chuột phải nó → `UI > Text - TextMeshPro`, tên `RewardText` ("Reward: 10")
- [ ] Chuột phải nó → `UI > Image`, tên `CoinIcon` ở góc trên phải, kèm một
      `Text - TextMeshPro` tên `CoinTotalText` bên cạnh
- [ ] Chuột phải nó → Create Empty, tên `CoinsParent`, **kéo giãn kín màn hình**
- [ ] Chọn `WinPopup` → `Add Component > Coin Fly VFX`
  - `Coin Prefab` = prefab một đồng tiền (RectTransform + Image, pivot 0.5/0.5)
  - `Coins Parent` = `CoinsParent`
  - `Coin Count` = 7, `Sorting Layer Name` để **trống**
- [ ] Chọn `WinPopup` → `Add Component > Win Popup View`
  - `Canvas Group` = chính nó
  - `Continue Button` = `NextButton` (đổi tên thành `ContinueButton` cho khớp)
  - `Banner` = `Banner`, `Banner Drop Distance` = 260, `Banner Duration` = 0.45
  - `Button Duration` = 0.35
  - `Coin Fly` = chính nó, `Coin From` = `RewardText`, `Coin Target` = `CoinIcon`
  - `Reward Text` = `RewardText`, `Coin Total Text` = `CoinTotalText`
  - `Level Text` = `LevelText`, `Last Level Notice` = `LastLevelNotice`
- [ ] Kéo vào `Assets/Prefabs/`, **xoá khỏi Hierarchy**

**Nhịp của popup** — ba pha nối đuôi, không nổ cùng lúc:

```
băng rơi xuống + nảy nhẹ  (0.45s)
   └─ tiền vãi ra rồi bay lên icon, số tổng tăng dần
        └─ nút Continue phóng từ 0 lên 1  (0.35s)
```

Nút chỉ hiện **sau khi tiền bay xong**, nên không ai bấm mất hiệu ứng.

> `Coins Parent` phải **phủ kín màn hình và không bị Mask hay Content Size Fitter nào
> cắt**. Coin bay ra ngoài khung cha sẽ bị xén mất nửa đường. Để nó là con trực tiếp
> của gốc popup, sibling index trên cùng.

**Tiền được cộng NGAY lúc popup mở**, không đợi coin bay xong — hiệu ứng chỉ là hình
ảnh, người chơi thoát app giữa chừng vẫn phải có tiền. Con số trên màn thì đi theo coin
để nhìn cho khớp.

**Nút Continue đưa về Home**, không vào thẳng màn kế. Nó đẩy tiến trình sang màn sau
rồi mở Home; chính nút `Play` trong Home mới nạp màn.

**Số tiền thưởng** đặt ở ô `Reward Coins` trong từng `LevelConfig` (mục 3.3), mặc định
10.

#### Phím tắt W — xem lại hiệu ứng

Bấm **W** để mở lại popup thắng màn bất cứ lúc nào. Chỉnh nhịp của băng, tiền bay và
nút Continue mà mỗi lần thử phải tô kín cả bảng thì không ai chỉnh nổi.

Không phải setup gì: object tự sinh lúc chạy bằng `RuntimeInitializeOnLoadMethod`, và
cả file nằm trong `#if UNITY_EDITOR || DEVELOPMENT_BUILD` nên build phát hành không có
class đó, cũng không có tham chiếu "Missing Script" nào trỏ tới nó.

> Mỗi lần bấm W là **cộng thật** thêm một lần tiền thưởng vào ví, vì popup cộng tiền
> ngay lúc mở. Thử nhiều rồi muốn về 0 thì `Edit > Clear All PlayerPrefs`.

**Khai vào PopupConfig**

- [ ] Mở `Assets/Settings/PopupConfig`, thêm một phần tử vào `Entries`
- [ ] `Key` = **LevelComplete**, `Prefab` = `WinPopup`

**Không** gán gì vào `On Click ()` của `NextButton` — `WinPopupView` tự đăng ký trong
`Awake`.

Nút sang màn kế **tự ẩn khi đang ở màn cuối**, và `LastLevelNotice` hiện lên thay chỗ.
Màn cuối cũng **không tăng tiến trình**: tăng rồi thì lần mở game sau nạp một màn không
tồn tại và người chơi nhận được bảng trống.

### 4.7 Popup Cài đặt (hai bản) và popup Nhắc nhở

**Ba popup, hai class.** Hai bản Cài đặt dùng chung một class `SettingsPopupView`, chỉ
khác nhau ở chỗ có gán nút Home hay không.

#### Popup Cài đặt trong game — key `Settings`

- [ ] `GameObject > UI > Panel`, tên `SettingsPopup`, `Add Component > Canvas Group`
- [ ] Dựng bên trong: `MusicButton` (kèm hai icon con `MusicOn` / `MusicOff`),
      `SoundButton` (kèm `SoundOn` / `SoundOff`), `HomeButton`, `CloseButton`
- [ ] `Add Component > Settings Popup View`, gán đủ các ô trên
- [ ] Kéo vào `Assets/Prefabs/`, xoá khỏi Hierarchy

#### Popup Cài đặt ở Home — key `SettingsHome`

- [ ] `Ctrl+D` nhân đôi prefab trên, đổi tên `SettingsHomePopup`
- [ ] **Xoá `HomeButton`** và bỏ trống ô `Home Button`

Đang đứng ở Home rồi thì không có gì để về. Để trống ô đó là đủ — `SettingsPopupView`
tự bỏ qua.

#### Popup Nhắc nhở — key `Notification`

- [ ] `GameObject > UI > Panel`, tên `NotificationPopup`, `Add Component > Canvas Group`
- [ ] Thêm `Text - TextMeshPro`, nội dung kiểu "Chọn một màu trước đã!"
- [ ] `Add Component > Notification Popup View`
  - `Message Text` = dòng chữ đó
  - `Auto Hide Seconds` = **1.6**
- [ ] Kéo vào `Assets/Prefabs/`, xoá khỏi Hierarchy

Popup này **tự tắt**, không có nút đóng. Đây là một câu nhắc, không phải một câu hỏi —
bắt người chơi bấm để bỏ qua thứ chính họ vừa gây ra là phạt họ hai lần.

#### Khai vào PopupConfig

- [ ] Mở `Assets/Settings/PopupConfig`, thêm ba phần tử:

| Key | Prefab |
|---|---|
| `Settings` | `SettingsPopup` |
| `SettingsHome` | `SettingsHomePopup` |
| `Notification` | `NotificationPopup` |

#### Presenter trong scene

- [ ] Trên object `Popups`, `Add Component > Notification Presenter` — không gán gì

#### Ba popup này mở lúc nào

| Popup | Mở khi |
|---|---|
| `Settings` | bấm nút bánh răng trên HUD |
| `SettingsHome` | bấm nút Cài đặt trên màn hình Home |
| `Notification` | chạm vào ô tô được, hoặc bấm nút gợi ý, **mà chưa chọn màu** |

**Nút trên HUD giờ mở popup Cài đặt, không mở thẳng Home nữa.** Đường về Home nằm
trong chính popup đó. Ô `Settings Button` tự nhận lại nút cũ nhờ `[FormerlySerializedAs]`,
bạn chỉ cần đổi tên object và ảnh nút.

**Nút gợi ý không còn xám khi chưa chọn màu** — bấm được, và bấm thì hiện lời nhắc.
Nút xám ngắt không nói được gì, mà đó lại đúng lúc người chơi cần biết nhất. Nó chỉ
xám khi màu đang chọn đã tô hết.

Hai đường dẫn tới lời nhắc — chạm ô và bấm gợi ý — gộp vào **một** sự kiện
`IPaintService.OnColorRequired`, nên chỗ hiển thị chỉ phải nghe một chỗ.

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
├── HintFocus
├── Board                    ← vị trí (0, 0, 0)
│   ├── Numbers              ← vị trí (0, 0, 0)
│   ├── GridLines            ← vị trí (0, 0, 0)
│   ├── Hints                ← vị trí (0, 0, 0)
│   ├── HintMarker           ← vị trí (0, 0, 0), xem mục 5.8F
│   ├── Jewels               ← vị trí (0, 0, 0)
│   ├── JewelLand            ← vị trí (0, 0, 0), xem mục 4.4
│   ├── ColorComplete        ← vị trí (0, 0, 0), xem mục 4.4
│   └── WinCelebration       ← vị trí (0, 0, 0), xem mục 5.8E
├── Canvas                   ← tĩnh: HUD, popup
│   ├── Hud
│   │   ├── LevelText
│   │   ├── HintButton
│   │   └── CollectionButton
│   └── Popups
├── LoadingCanvas            ← che lúc mở game, xem mục 5.12
│   └── LoadingRoot
├── HomeCanvas               ← mở bằng nút Home trên HUD, xem mục 5.11
│   └── HomeRoot
│       ├── TopBar (Settings, Collection)
│       ├── LevelScroll
│       │   └── Viewport
│       │       └── Content
│       └── PlayButton
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
- [ ] `Pan Margin Screen Fraction` = **0.5**

Cho kéo ra ngoài mép bảng thêm **nửa màn hình** — nửa chiều rộng theo trục ngang, nửa
chiều cao theo trục dọc. Kéo hết cỡ thì mép bảng nằm đúng giữa màn.

Đo theo màn hình chứ không theo số ô, nên zoom mức nào cũng kéo thừa ra được đúng
bấy nhiêu phần màn. Tính bằng ô thì lúc phóng sát lề chiếm gần hết màn, còn lúc kéo
xa thì gần như không thấy.

Để 0 là khoá sát mép bảng.

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
- [ ] Trên chính object đó, `Add Component > Paint Progress Store`
- [ ] `Auto Save Seconds` = **5**

Không gán gì thêm, `GameEntryPoint` nối dây lúc chạy.

**Tiến độ tô được lưu lại.** Đóng game giữa chừng rồi mở lại thì bức tranh còn nguyên
chỗ đang dở. Mỗi màn một key riêng trong PlayerPrefs (`painted_1`, `painted_2`...),
và key của một màn bị **xoá ngay khi màn đó hoàn thành**.

Trạng thái tô gói theo **bit** rồi mã hoá Base64: bảng 64×64 là 4096 ô nhưng chỉ tốn
512 byte, ra khoảng 700 ký tự. Lưu từng ô thành một số nguyên riêng thì cùng bảng đó
là 4096 key.

`Auto Save Seconds` là chu kỳ ghi xuống đĩa khi có thay đổi. **Không** ghi mỗi lần tô
một ô: `PlayerPrefs.Save` ghi cả file ra đĩa, gọi nó theo nhịp kéo tay tô là cách chắc
chắn để game giật. Ngoài chu kỳ đó, nó còn ghi ở ba mốc mà mất dữ liệu là mất thật:
app chuyển nền, mất tiêu điểm, và thoát hẳn.

> Trên mobile, thoát app thường **không** gọi `OnApplicationQuit` — hệ điều hành chỉ
> đưa app xuống nền rồi có thể giết bất cứ lúc nào. `OnApplicationPause` mới là mốc
> đáng tin, và đó là lý do cả ba mốc đều được bắt.

Sinh lại lưới cho một màn đã chơi dở thì bản lưu cũ **không khớp cỡ** và bị bỏ kèm một
cảnh báo trong Console. Đó là cố ý: đắp bản lưu cũ lên lưới mới sẽ tô sai chỗ hàng
loạt, mất tiến độ còn dễ hiểu hơn một bức tranh lem nhem không rõ vì sao.

### 5.5B LevelFlow

- [ ] Create Empty, tên `LevelFlow`, `Add Component > Level Flow Controller`
- [ ] `Popup Delay Seconds` = **1.6**

Đây là **một con số tuyệt đối**: bao lâu kể từ lúc viên ngọc cuối đáp xuống thì popup
hiện ra. Không cộng dồn, không suy từ thời lượng của `WinCelebration`.

Đổi lại, bạn phải **tự canh** nó với màn ăn mừng. Hai thứ chạy song song và độc lập:

```
tô xong ô cuối
   ├─ WinCelebration: camera lùi + quét chéo   (tự chạy theo ô của nó)
   └─ đếm Popup Delay Seconds  ──►  hiện popup + ẩn HUD
```

Đặt ngắn hơn màn ăn mừng thì popup hiện đè lên lúc dải lấp lánh còn đang quét — đôi
khi đó lại là thứ bạn muốn. Với số mặc định thì quét xong khoảng 1.55 giây, nên 1.6 là
vừa chạm mép.

Hết khoảng chờ thì nó **bắn tín hiệu thắng màn**, không tự sang màn kế. Việc đi tiếp
do người chơi bấm nút trong popup (mục 4.6).

Tô kín bảng thì chờ ngần đó giây rồi tự sang màn kế. Không còn màn nào mang id tiếp
theo thì **dừng lại**, và tiến trình **không** tăng — tăng rồi là lần mở game sau nạp
một màn không tồn tại, bảng trống trơn.

### 5.5C HintFocus — nút gợi ý

- [ ] Create Empty, tên `HintFocus`, `Add Component > Hint Focus Controller`

Không gán gì, `GameEntryPoint` nối dây lúc chạy.

Bấm nút gợi ý thì nó bốc **ngẫu nhiên một ô chưa tô** của màu đang chọn rồi đưa camera
tới đó, đồng thời phóng về `Camera Min Size` của màn (mục 3.3).

Nút tự **xám đi** khi chưa chọn màu nào, hoặc màu đang chọn đã tô hết. Một nút bấm vào
mà không có gì xảy ra thì người chơi tưởng game đứng.

Ô ngẫu nhiên chứ không phải ô gần nhất, đúng như yêu cầu. Muốn đổi sang "ô gần camera
nhất" thì sửa `HintFocusController.UseHint()` — chỗ bốc `ordinal` là toàn bộ phần
quyết định.

Muốn có nhiều màn thì thêm `LevelConfig` vào mảng `Levels` của `LevelManager`, với
`Level Id` chạy liên tiếp 1, 2, 3...

### 5.6 Board

Bảng gồm **hai lớp texture chồng nhau**, không phải một:

| Lớp | Order in Layer | Bị làm mờ khi zoom? |
|---|---|---|
| Ô **chưa tô** (xám) | **-1** | có — mờ đi để lộ số |
| Ô **đã tô** (màu thật) | **0** | **không bao giờ** |

Gộp chung một texture thì không tách được, vì alpha là của cả `SpriteRenderer`: làm
mờ để hiện số cũng đồng thời xoá mất màu của những ô người chơi vừa tô xong. Hai ô
không bao giờ cùng nằm trên cả hai lớp — tô tới đâu, pixel bên lớp chưa tô bị xoá
tới đó.

- [ ] Create Empty, tên `Board`, `Position` = **(0, 0, 0)** — bắt buộc
- [ ] `Add Component > Sprite Renderer`, đặt `Order in Layer` = **-1**
- [ ] Chuột phải `Board` → `2D Object > Sprite > Empty`, tên `Painted`,
      `Position` = **(0, 0, 0)**, `Order in Layer` = **0**, ô `Sprite` để trống
- [ ] Chọn lại `Board` → `Add Component > Board View`
  - `Unpainted Renderer` = **chính `Board`**
  - `Painted Renderer` = **`Painted`**
  - tick `Grayscale`
- [ ] `Add Component > Board Color Fade`: `Camera` = Main Camera, `Renderer` = chính nó,
      `Board View` = chính nó, `Opaque Size` = **0**, `Transparent Size` = **12**,
      `Level Size Is Opaque` = **bỏ tick**

> ⚠️ **Không gắn `Board Color Fade` lên `Painted`.** Chính việc lớp đó không bị làm mờ
> là toàn bộ lý do nó tồn tại.

> ⚠️ **`Painted` phải có Position (0, 0, 0) và Scale (1, 1, 1).** Lệch một chút thôi là
> màu đã tô vẽ trượt khỏi ô của nó: có ô hiện màu của hàng xóm, có ô trông như chưa
> được tô dù đã có ngọc, và mép ô nào cũng chỉ phủ một phần.
>
> Triệu chứng đó khó lần ra bằng mắt vì mọi thứ khác vẫn đúng. `BoardView` tự kiểm lúc
> vào màn và in cảnh báo kèm số đo lệch nếu phát hiện.

`Order in Layer` = -1 cho lớp chưa tô là để **không phải đụng vào các lớp khác**:
GridLines vẫn ở 1, gợi ý ở 2, số ở 3, ngọc ở 4.

`Opaque Size` = 0 nghĩa là lấy mức zoom lúc vào màn làm mốc đục. Từ đó phóng to dần
thì lớp xám mờ dần, và **đến `orthographicSize` = 12 là mất hẳn** — zoom sát hơn nữa
vẫn trong suốt, không mờ thêm gì.

Hai mốc này là `orthographicSize` tuyệt đối, không phải tỉ lệ. Đặt `Opaque Size` lớn
hơn `Transparent Size` thì chiều mờ đảo lại: đục khi phóng sát, mờ khi kéo ra xa.
- [ ] `Add Component > Board Input`: `Camera` = Main Camera, `Board View` để trống
- [ ] `Hold To Pick Seconds` = **1.5**, `Hold Move Tolerance Pixels` = **24**

**Giữ tay để chọn màu.** Đặt tay lên một ô **chưa tô** và giữ yên 1.5 giây thì màu của
ô đó được chọn — khỏi phải dò trong thanh màu xem số đó nằm đâu.

Chỉ chạy trên nét thuộc về **camera**. Ô đúng màu đang chọn thì chạm vào là tô ngay,
nét đó thuộc về Paint và không có gì để chọn nữa.

`Hold Move Tolerance Pixels` là ngưỡng phân biệt "đang giữ" với "đang kéo", đo bằng
**pixel màn hình thật**. 24 hợp với màn 1080p; màn 1440p hoặc cao hơn thì nới lên
32–40, không thì tay run một chút là mất lượt.

Để `Hold To Pick Seconds` = 0 là tắt hẳn tính năng này.

> Chưa có dấu hiệu nào cho biết đang đếm giờ — 1.5 giây im lặng dễ làm người chơi
> tưởng máy đơ. Muốn thêm vòng tròn chạy quanh ngón tay thì cần một view riêng nghe
> tiến độ từ `BoardInput`.

### 5.7 Numbers

- [ ] Chuột phải `Board` → Create Empty, tên `Numbers`, `Position` = (0, 0, 0)
- [ ] `Add Component > Board Number Layer`
- [ ] `Camera` = Main Camera, `Number Prefab` = `CellNumber`, `Root` = chính nó
- [ ] `Show At Zoom Progress` = **0.6**
- [ ] `Min Cell Screen Pixels` = 32 (đường lui, xem dưới)
- [ ] `Max Spawn Per Frame` = **48**
- [ ] `Prewarm Per Number` = **64**
- [ ] `Show Hysteresis` = **0.08**

`Show At Zoom Progress` đo theo **dải zoom của từng màn**: mốc trên là mức lúc vào màn
(`Camera Max Size`), mốc dưới là `Fade Switch Size`. 0 là số hiện ngay khi vào màn,
1 là mãi tới lúc lớp màu tan hết, 0.6 là đi được 60% quãng đường giữa hai mốc.

Muốn số hiện **sớm hơn** thì **hạ** con số này xuống.

Nhờ đo tương đối nên màn nào cũng cho cảm giác như nhau. Ngưỡng pixel cố định thì màn
có `Camera Max Size` lớn phải kéo sâu hơn hẳn mới thấy số.

`Min Cell Screen Pixels` chỉ dùng khi `LevelConfig` để trống `Fade Switch Size` — lúc
đó không có mốc dưới để chia tỉ lệ.

**`Max Spawn Per Frame` không phải số chữ hiện ra mỗi frame.** Toàn bộ số trong tầm
nhìn luôn hiện **cùng một lúc**; ô này chỉ chia phần việc *dựng* chữ ra nhiều frame.

Mỗi `TextMeshPro` phải dựng lưới chữ của nó. Dựng hàng trăm cái trong một frame là một
cú khựng thấy rõ, nên chúng được dựng dần ở `alpha` = 0 — người chơi không thấy gì —
rồi khi cả vùng nhìn đã xong thì tất cả bật lên một lượt. Đổi alpha chỉ ghi lại màu
đỉnh của lưới đã có sẵn, rẻ hơn hẳn nên làm đồng loạt được.

Đánh đổi nằm ở **độ trễ trước khi số hiện**: bảng ~900 ô chia cho 48 là 19 frame,
khoảng 0.3 giây. Nâng ô này lên thì số hiện sớm hơn nhưng mỗi frame nặng hơn — 48 chữ
tốn chừng 5ms, vẫn lọt trong ngân sách 16ms của 60fps.

#### Vì sao zoom lần sau không còn khựng

Thứ gây khựng là `SetText`: nó buộc `TextMeshPro` **dựng lại lưới chữ**, tốn cỡ 0.1ms
mỗi chữ. Chín trăm chữ là 90ms — một cú khựng thấy rõ.

Kho chữ vì thế **chia theo từng số**, không dùng chung một ngăn. Kho dùng chung thì chữ
lấy ra gần như luôn mang sai số nên lần nào cũng phải dựng lại. Chia theo số thì chữ
"3" lấy ra đã sẵn là "3" — chỉ cần đặt vị trí và bật lên.

| | Lần zoom đầu | Các lần sau |
|---|---|---|
| Kho dùng chung (bản cũ) | 90ms | **90ms** |
| Kho chia theo số | 90ms | **3.6ms** |
| Kho chia theo số + `Prewarm Per Number` | ~0ms | 3.6ms |

**`Prewarm Per Number`** dựng sẵn chữ cho mọi số màn này dùng, ngay lúc vào màn — tức
lúc **màn hình chờ đang che**, nên người chơi không thấy. Đặt cỡ số ô nhiều nhất của
một màu thì cú zoom đầu tiên cũng mượt. Để 0 là tắt.

Nó chỉ dựng cho số **thật sự có trong lưới**: bảng màu có thể khai 16 màu mà ảnh chỉ
dùng 9, dựng cả 16 là phí một phần ba số chữ.

> Đổi lại: `Number Color` giờ chỉ áp dụng lúc chữ được TẠO. Chỉnh ô đó trong Play Mode
> sẽ không thấy gì đổi — vào lại màn thì mới ăn.

#### `Show Hysteresis` — dải trễ quanh ngưỡng hiện số

Không có dải này thì zoom qua lại quanh **đúng** ngưỡng là cả nghìn chữ bị tắt rồi bật
lại **mỗi frame**. Mỗi lần bật tắt là một lần `SetActive` duyệt cây con và gửi thông
điệp vòng đời — đây là cú giật nặng nhất khi zoom liên tục.

Dải trễ tính theo **phần của khoảng từ ngưỡng tới mức zoom xa nhất**, không phải theo
tỉ lệ của chính ngưỡng. Với ngưỡng 33.6, `Camera Max Size` 36 và dải trễ 8%:

```
đang ẩn   →  hiện khi orthographicSize ≤ 33.60
đang hiện →  tắt  khi orthographicSize > 33.79
```

> ⚠️ Nhân theo tỉ lệ của ngưỡng (33.6 × 1.08 = **36.29**) là sai: con số đó **lớn hơn
> mức camera có thể kéo ra** (36), nên số đã hiện thì không bao giờ ẩn lại được nữa.
> Đó là lý do dải trễ phải đo theo khoảng còn lại, không đo theo ngưỡng.

**Ô `Spawn All In One Frame`** ở mục `Thử nghiệm` bỏ qua hạn mức hoàn toàn: dựng hết
số trong đúng một frame. Với kho chia theo số thì giờ nó khả thi hơn hẳn — 3.6ms cho
900 chữ vẫn lọt ngân sách 16ms.

Đo trong Editor không nói lên gì — Editor chậm hơn build vài lần và có cả overhead của
chính nó. Muốn biết máy thật chịu được không thì `Build Settings` → bật
**Development Build** + **Autoconnect Profiler**, build lên máy, rồi xem
`TextMeshPro.GenerateTextMesh` trong tab CPU đúng frame zoom qua ngưỡng hiện số. Máy
yếu nhất bạn định hỗ trợ mới là máy đáng đo.

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

Cả hai mốc đều hiểu **0 là "mức zoom lúc vào màn"**. Bảng dùng nó cho mốc đục, viền
dùng nó cho mốc trong suốt — cùng một quy ước, hai vai ngược nhau.

Kiểm nhanh xem đã đúng chưa với `Camera Max Size` = 36, `Fade Switch Size` = 32:

| orthographicSize | Lớp màu | Lớp viền |
|---|---|---|
| 36 (vừa vào màn) | đục | **vô hình** |
| 32 (điểm giao) | vừa tan hết | vừa hiện đủ |
| 9 (phóng sát) | vô hình | đục |

Thấy viền ngay lúc mới vào màn là sai — xem mục "Lỗi hay gặp".

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
- [ ] `Prewarm From Largest Color` = **tick**
- [ ] `Max Spawn Per Frame` = **0** — tất cả marker hiện cùng một lúc

**Vì sao ngưỡng ở đây thấp hơn hẳn lớp số và lớp ngọc (14):** ngọc bị cull thì ô vẫn
còn màu trong texture nên không ai nhận ra, còn marker bị cull là **mất hẳn dấu hiệu**.
Mà gợi ý lại chủ yếu dùng lúc zoom xa để dò xem còn sót ô nào. Đặt ngưỡng cao là nó
biến mất đúng lúc cần nhất.

Đổi lại, zoom xa thì nhiều marker cùng tồn tại. Thấy khựng thì nâng
`Min Cell Screen Pixels` lên, chấp nhận mất gợi ý ở mức zoom xa nhất.

#### Vì sao chọn màu không còn khựng

Marker là `SpriteRenderer`, lấy từ kho chỉ tốn một lần bật object và đặt vị trí — rẻ
hơn hẳn chữ `TextMeshPro` vốn phải dựng lại lưới. Nên hạn mức mỗi frame ở đây tồn tại
chỉ vì **một lý do**: kho thiếu hàng thì phải `Instantiate` bù ngay lúc đó.

Giải quyết bằng cách làm kho không bao giờ thiếu, rồi bỏ hạn mức:

**`Prewarm From Largest Color`** dựng sẵn đủ marker cho màu nhiều ô nhất của màn. Đó
đúng là trường hợp xấu nhất — chọn màu nào thì chỉ ô của màu đó mới có marker, nên
không màu nào cần nhiều hơn màu đông ô nhất. Bảng lớn tới đâu kho cũng đủ, khỏi chỉnh
tay từng màn. `Prewarm Count` khi đó chỉ còn là mức sàn.

**`Max Spawn Per Frame` = 0** cho tất cả marker hiện trong một frame. An toàn vì kho đã
có sẵn hàng.

Việc dựng sẵn chạy lúc vào màn, khi màn hình chờ đang che, nên người chơi không thấy.

### 5.8B Jewels

- [ ] Chuột phải `Board` → Create Empty, tên `Jewels`, `Position` = (0, 0, 0)
- [ ] `Add Component > Jewel Layer`
- [ ] `Camera` = Main Camera, `Jewel Prefab` = `Jewel_01`, `Root` = chính nó
- [ ] `Min Cell Screen Pixels` = 14, `Prewarm Count` = 200
- [ ] `Prewarm From Board Size` = **tick**
- [ ] `Max Spawn Per Frame` = **0** — cả vùng mới hiện trọn ngay

Chỉ sinh ngọc cho ô **đang lọt trong tầm nhìn**, nên bảng tô kín 4000 ô vẫn chỉ có
chừng trăm viên tồn tại. Kéo camera thì viên trôi ra ngoài được thu về, viên mới lấy
ra dùng lại.

#### Bật/tắt renderer, không bật/tắt GameObject

`JewelLayer` và `HintLayer` gạt `SpriteRenderer.enabled` thay vì `SetActive`.

`SetActive` phải duyệt cả cây con, gửi thông điệp vòng đời và cập nhật lại cấu trúc
culling — đắt gấp nhiều lần so với gạt một cờ bool. Zoom qua lại liên tục là hàng trăm
lần bật tắt mỗi frame.

An toàn vì hai prefab đó chỉ có đúng một `SpriteRenderer`: tắt renderer với tắt object
là một về mặt hình ảnh. Prefab nào có thêm con thì cách này không còn tương đương.

> Lớp số **vẫn dùng `SetActive`**. `TextMeshPro.enabled` gọi `OnEnable` của chính
> component, và TMP đánh dấu cần dựng lại lưới chữ trong đó — đúng thứ mà kho chia theo
> số đang tránh. Lớp số dựa vào `Show Hysteresis` để bật tắt ít lần hơn.

Cull được là nhờ texture của bảng vẫn giữ màu bên dưới — viên ngọc bị gỡ thì ô đó
vẫn còn nguyên màu, không ai nhận ra có gì biến mất.

#### Vì sao zoom nhanh không còn thấy ngọc mọc dần

Zoom ra nhanh đẩy hàng trăm ô vào tầm nhìn cùng lúc. Hạn mức 24 marker mỗi frame nghĩa
là 300 ô phải mất 13 frame — đủ chậm để mắt bắt được, và thứ người chơi thấy là ngọc
lần lượt mọc lên thay vì bức tranh đã sẵn ở đó.

Cùng cách chữa như lớp gợi ý: làm kho không bao giờ thiếu rồi bỏ hạn mức.

**`Prewarm From Board Size`** dựng sẵn đủ ngọc cho **mọi ô có màu** của màn. Đó là
trường hợp xấu nhất — tranh tô kín và kéo ra thấy trọn bảng thì mọi ô đều cần một viên.

Cái giá là bộ nhớ và thời gian vào màn:

| Bảng | Số viên dựng sẵn | Thời gian |
|---|---|---|
| 27×36 | ~970 | ~19ms |
| 64×64 | ~4090 | ~80ms |

Việc đó chạy lúc màn hình chờ đang che nên không ai thấy. Máy yếu mà thấy vào màn lâu
thì bỏ tick, quay lại `Prewarm Count` cố định và chấp nhận hạn mức mỗi frame.

**`Max Spawn Per Frame` = 0** cho cả vùng mới hiện trọn trong một frame.

### 5.8C JewelFly

- [ ] Chuột phải `Board` → Create Empty, tên `JewelFly`, `Position` = (0, 0, **-1**)
- [ ] `Add Component > Jewel Fly Effect`
- [ ] `Jewel Prefab` = `Jewel_01`, `Root` = chính nó

**Đường bay**

| Ô | Giá trị | Vì sao |
|---|---|---|
| `Duration` | `0.4` | thời gian bay ở quãng tham chiếu |
| `Reference Distance` | `8` | quãng mà tại đó bay đúng `Duration` |
| `Min Duration` | `0.42` | sàn thời gian bay |
| `Max Duration` | `0.62` | |
| `Duration Falloff` | `0.25` | thời gian bám theo quãng đường chặt tới đâu |
| `Duration Variance` | `0.08` | ±8%, để các viên không đi thành hàng lối |
| `Move Ease` | `OutCubic` | nhịp cho quãng **xa** — vọt ra nhanh, hạ dần |
| `Near Move Ease` | `InOutSine` | nhịp cho quãng **gần** — êm cả hai đầu |
| `Near Ease Reach` | `0.8` | dưới 0.8 × Reference Distance thì tính là gần |

Ba ô đầu là thứ đáng chú ý nhất. Thời gian bay **cố định** làm ô ngay sát thanh màu
bò lừ đừ còn ô ở mép bảng thì lao vun vút — mắt đọc ra ngay là hai chuyển động khác
nhau, và đó chính là cái làm hiệu ứng thấy gợn. Ba ô này giữ **tốc độ** đều thay vì
giữ thời gian đều, rồi kẹp lại để quãng cực ngắn không giật và quãng cực dài không lê.

**Cỡ viên**

| Ô | Giá trị | Vì sao |
|---|---|---|
| `Start Scale` | `2.6` | cỡ lúc rời thanh màu, cho quãng **xa** |
| `Near Start Scale` | `1.2` | cỡ lúc rời thanh màu, cho quãng **rất ngắn** |
| `Settle Scale` | `0.92` | hơi nhỏ hơn ô ngay lúc chạm |
| `Settle Portion` | `0.18` | 18% cuối dành cho pha nở về 1 |
| `Scale Ease` | `InOutSine` | |

**`Near Start Scale` chữa cái giật ở những ô sát thanh màu.** Mắt bắt *tốc độ đổi cỡ*
chứ không bắt quãng đường. Quãng ngắn dù nới thời gian tới đâu cũng chỉ được chừng
0.3 giây, mà nếu vẫn phải co từ cỡ 5 về 1 thì nhịp đổi cỡ vọt lên gấp bốn lần một cú
bay dài — đọc ra là búng, không phải bay. Cho cỡ xuất phát đi theo quãng đường thì
nhịp đó phẳng lại:

| Quãng | Giây | Cỡ đầu | Co cỡ mỗi giây |
|---|---|---|---|
| 1.5 | 0.30 | 1.46 | 1.5 |
| 3 | 0.30 | 1.73 | 2.4 |
| 6 | 0.35 | 2.25 | 3.6 |
| 10 | 0.45 | 2.60 | 3.6 |
| 20 | 0.62 | 2.60 | 2.6 |

Cột cuối là thứ đáng nhìn: nằm gọn trong 1.5–3.6 ở mọi quãng. Để `Start Scale` = 5 và
`Near Start Scale` bằng nó thì cột đó vọt từ 6.5 lên 15.4 — chênh gấp đôi, và đó chính
là cảm giác giật ở những ô sát thanh màu.

**Ô gần "bay nhanh" chủ yếu là do EASING, không phải do thời gian.**

`OutCubic` có tốc độ đỉnh bằng **3 lần** tốc độ trung bình, và cả cú vọt đó dồn vào
ngay lúc rời thanh màu — nó đi 66% quãng đường trong 30% thời gian đầu. Quãng dài thì
không sao vì còn cả đoạn sau để hạ dần. Quãng ngắn thì người chơi chỉ kịp thấy đúng
cú vọt đó.

`InOutSine` có đỉnh chỉ **1.57 lần**, và đỉnh nằm ở giữa quãng nên hai đầu đều êm.
Vì vậy quãng gần dùng ease riêng:

| Quãng | Trước: giây / tốc độ đỉnh | Sau: giây / ease / tốc độ đỉnh |
|---|---|---|
| 1.5 | 0.38 — 11.8 | 0.42 — InOutSine — **5.6** |
| 3 | 0.38 — 23.7 | 0.42 — InOutSine — **11.2** |
| 6 | 0.38 — 47.4 | 0.42 — InOutSine — **22.4** |
| 10 | 0.42 — 70.9 | 0.42 — OutCubic — 70.9 |
| 20 | 0.50 — 119.3 | 0.50 — OutCubic — 119.3 |
| 40 | 0.60 — 200.6 | 0.60 — OutCubic — 200.6 |

Quãng gần giảm còn **một nửa** tốc độ đỉnh, quãng xa không đổi một chút nào.

Đổi ease đột ngột qua ngưỡng không nhìn ra được: mỗi cú bay là một sự kiện riêng,
không có hai cú cạnh nhau để mà so. Thấy quãng tầm trung vẫn gắt thì nâng
`Near Ease Reach` lên 1, lúc đó mọi quãng dưới Reference Distance đều dùng InOutSine.

**`Duration Falloff` và `Min Duration` là cần gạt thứ hai.** Mắt đọc
nhịp theo **thời gian**, không theo quãng đường — một cú bay 0.30 giây thấy vụt một
cái là xong, dù nó chậm hơn hẳn về world unit mỗi giây.

| Quãng | `1 / 0.26` (tỉ lệ thẳng) | `0.25 / 0.38` (mặc định) | `0 / 0.38` (bằng nhau) |
|---|---|---|---|
| 1.5 | 0.26 | 0.38 | 0.40 |
| 3 | 0.26 | 0.38 | 0.40 |
| 6 | 0.30 | 0.38 | 0.40 |
| 10 | 0.50 | 0.42 | 0.40 |
| 20 | 0.62 | 0.50 | 0.40 |
| 40 | 0.62 | 0.60 | 0.40 |
| **chênh lệch** | **2.4 lần** | **1.6 lần** | **1 lần** |

Muốn **mọi cú bay đúng một nhịp** thì đặt `Duration Falloff` = **0** — lúc đó `Duration`
(0.4) là thời gian của tất cả, và `Min`/`Max` không còn tác dụng. Đổi lại, ô ở mép bảng
sẽ lao rất nhanh về tốc độ.

`Settle Scale` là toàn bộ cảm giác "đáp êm": viên co xuống hơi nhỏ hơn ô rồi giãn về
đúng cỡ trong 18% cuối. Không có pha này thì viên **dừng phựt** đúng kích thước cuối,
và mắt đọc ra là va chạm chứ không phải đặt xuống. Để `Settle Portion` = 0 là bỏ hẳn.

**Hiện dần**

- [ ] `Fade In Portion` = **0.2**

Viên hiện dần từ trong suốt trong 20% đầu, thay vì bật ra đột ngột ở thanh màu.

**Giới hạn**

- [ ] `Max Concurrent` = 24, `Prewarm Count` = 24, `Flying Sorting Order` = 15

Muốn **nhẹ hơn nữa** thì nâng `Duration` lên 0.5 và đổi `Move Ease` sang `InOutSine` —
mềm cả hai đầu, nhưng khởi động chậm nên phản hồi lúc bấm kém dứt khoát hơn. Muốn
**gọn gàng, dứt khoát** thì hạ `Duration` xuống 0.3 và dùng `OutQuart`.

`z = -1` để viên đang bay nằm trên mọi lớp khác.

Viên bay ra **từ ô màu trên thanh chọn**, nên `ColorPaletteBar` phải gán ô
`World Camera` (bước 5.10) — thiếu thì không có hiệu ứng, ngọc hiện ngay lập tức
chứ không nổ.

Ngọc ở ô đích chỉ hiện **khi viên bay đáp xuống**. Lớp màu trong texture vẫn đổi ngay
lúc tô nên ô không bị trống trong lúc chờ.

### 5.8E WinCelebration — ăn mừng khi thắng màn

Tô xong ô cuối thì ba thứ chạy cùng lúc: camera thu về toàn cảnh, bảng về giữa màn
hình, và một dải lấp lánh quét chéo từ góc trên trái xuống góc dưới phải.

Dùng lại đúng prefab `JewelLandBurst` ở mục 4.4, nhưng qua một **kho riêng** — kho chỉ
giữ được một prefab, và để chung với `JewelLand` thì hai hiệu ứng tranh chỗ nhau.

- [ ] Chuột phải `Board` → Create Empty, tên `WinCelebration`, `Position` = (0, 0, 0)
- [ ] `Add Component > Particle Burst Pool`
  - `Prefab` = `JewelLandBurst`, `Root` = chính nó
  - `Prewarm Count` = **120**, `Max Concurrent` = **200**
  - `Min Alive Seconds` = 0.3
- [ ] `Add Component > Win Celebration`
  - `Burst Pool` = **chính object này**

| Ô | Giá trị | Ý nghĩa |
|---|---|---|
| `Camera Duration` | `1.1` | thời gian camera lùi về toàn cảnh |
| `Sweep Duration` | `1.4` | dải sáng đi hết từ góc này sang góc kia |
| `Sweep Start Delay` | `0.15` | chờ camera lùi một chút rồi mới quét. 0 là chạy cùng lúc |
| `Cell Step` | `4` | cứ 4 ô loé 1 ô, theo cả hai trục |
| `Max Spawn Per Frame` | `12` | chặn khựng ở đoạn giữa bảng |

**`Cell Step` là ô quan trọng nhất.** Để 1 thì bảng 64×64 loé đủ 4096 ô trong 1.4 giây
— chắc chắn khựng, mà nhìn cũng chỉ ra một mảng trắng. Để 4 thì còn 1/16, khoảng 256
lần loé, đọc ra thành dải lấp lánh thưa. Bảng nhỏ (12×12) thì hạ xuống 2.

Quét theo **đường chéo** là vì mọi ô có cùng tổng `x + y` nằm trên một đường chéo, nên
chỉ cần cho một con số chạy từ 0 tới `W + H - 2` là có ngay mặt sóng đi từ góc trên
trái xuống góc dưới phải. Không phải xếp trước danh sách ô nào.

Camera trong đoạn này **không huỷ được bằng chạm**, khác với nút gợi ý — người chơi
giằng camera giữa chừng chỉ làm hỏng nhịp, mà lúc đó cũng chẳng còn ô nào để tô.

**Nhịp của cả đoạn thắng màn:**

```
tô xong ô cuối
   ├─ camera lùi về toàn cảnh      (Camera Duration 1.1s)
   └─ chờ 0.15s rồi quét chéo      (Sweep Duration 1.4s)
hiện popup thắng màn (+ ẩn HUD) sau Popup Delay Seconds
   └─ người chơi bấm nút  ──►  màn kế (+ hiện lại HUD)
```

`LevelFlow` **hỏi** `WinCelebration` xem xong chưa chứ không cộng sẵn một con số chờ.
Bạn đổi `Sweep Duration` thành 3 giây thì luồng tự giãn theo, không phải nhớ sửa
`Delay Seconds`.

### 5.8F HintMarker — kính lúp thả xuống ô gợi ý

Bấm nút gợi ý thì camera bay tới ô cần tô, rồi một icon kính lúp rơi xuống đúng ô đó.

- [ ] Vẽ hoặc tải icon kính lúp, import `Sprite (2D and UI)`
- [ ] `GameObject > 2D Object > Sprite`, tên `HintIcon`, gán ảnh,
      `Order in Layer` = **13**
- [ ] Kéo vào `Assets/Prefabs/`, xoá khỏi Hierarchy
- [ ] Chuột phải `Board` → Create Empty, tên `HintMarker`, `Position` = (0, 0, 0)
- [ ] `Add Component > Hint Marker Effect`, `Icon Prefab` = `HintIcon`,
      `Root` = chính nó

| Ô | Giá trị | Ý nghĩa |
|---|---|---|
| `Start Delay` | `0.45` | chờ camera bay tới nơi rồi mới thả |
| `Drop Height` | `6` | rơi từ cao hơn ô bao nhiêu world unit |
| `Drop Duration` | `0.32` | |
| `Hold Seconds` | `0.7` | nằm lại bao lâu rồi mờ đi |
| `Fade Duration` | `0.25` | |
| `Scale` | `1.4` | cỡ icon so với một ô |
| `Sorting Order` | `13` | trên ngọc (4), dưới viên đang bay (15) |

**`Start Delay` phải LỚN HƠN `Focus Duration` của `Main Camera > Board Camera`** (mặc
định 0.4). Thả sớm hơn thì icon rơi vào một ô đang trôi ngang qua màn hình, và người
chơi mất dấu nó giữa đường.

Hai thứ **không nối vào nhau** mà mỗi bên tự đếm giờ. Đổi lại là bạn phải canh tay,
nhưng bù lại camera bị người chơi chạm huỷ giữa chừng cũng không kéo theo icon biến mất.

Chỉ có **một** icon sống cùng lúc — bấm gợi ý liên tục thì lần sau ghi đè lần trước.
Đây không phải hiệu ứng hàng loạt như ngọc bay nên không cần kho.

### 5.9 Canvas và HUD

- [ ] `GameObject > UI > Canvas` (Unity tự tạo kèm `EventSystem`)
- [ ] `Render Mode` = Screen Space - Overlay, `Sort Order` = 0
- [ ] `Canvas Scaler`: Scale With Screen Size, 1080 × 1920
- [ ] `EventSystem` báo lỗi Input System thì bấm **Replace with InputSystemUIInputModule**
- [ ] Chuột phải `Canvas` → Create Empty, tên `Hud`, `Add Component > Hud View`
- [ ] Chuột phải `Hud` → `UI > Text - TextMeshPro`, tên `LevelText`, neo góc trên trái
- [ ] Gán `LevelText` vào ô `Level Text` của `Hud View`
- [ ] Chuột phải `Hud` → `UI > Button - TextMeshPro`, tên `HintButton`, neo góc dưới phải
- [ ] Gán `HintButton` vào ô `Hint Button` của `Hud View`
- [ ] Chuột phải `Hud` → `UI > Button - TextMeshPro`, tên `SettingsButton`, neo góc trên phải
- [ ] Gán `SettingsButton` vào ô `Settings Button` của `Hud View`
- [ ] Ô `Content` của `Hud View`: để **trống**

Cả HUD **tự ẩn khi thắng màn** và hiện lại khi màn mới bắt đầu, để popup đứng một mình
trên bức tranh vừa hoàn thành. Để trống ô `Content` thì nó ẩn chính object `Hud`. Muốn
giữ lại một phần HUD thì gom phần bị ẩn vào một object con rồi gán vào ô đó.
- [ ] Chuột phải `Canvas` → Create Empty, tên `Popups`,
      `Add Component > Popup Manager`, `Config` = `PopupConfig`, `Root` = chính nó
- [ ] Trên chính `Popups` đó, `Add Component > Win Popup Presenter` — không gán gì,
      `GameEntryPoint` nối dây lúc chạy

**Không** gán gì vào `On Click ()` của `HintButton` trong Inspector — `HudView` tự
đăng ký lúc chạy. Gán thêm ở Inspector là bấm một cái chạy hai lần.

Nút này tự xám đi khi chưa chọn màu. Muốn nó đổi hình thay vì chỉ mờ đi thì chỉnh
`Transition` của `Button`, `HudView` chỉ đụng tới `interactable`.

**Nút bánh răng** mở popup Cài đặt (mục 4.7). Đường về Home nằm trong chính popup đó,
và cũng chính nó lo việc ẩn HUD — HUD không cần biết Home tồn tại.

Popup Bộ sưu tập mở từ **trong Home**, không có nút riêng trên HUD.

**Camera bay tới ô gợi ý** chỉnh ở `Main Camera > Board Camera`:

| Ô | Mặc định | Ý nghĩa |
|---|---|---|
| `Focus Duration` | 0.4 | thời gian bay, để 0 là nhảy tức thì |
| `Focus Input Grace` | 0.2 | chạm màn hình sau ngần này giây là huỷ chuyến bay |

`Focus Input Grace` không được để 0. Bấm nút gợi ý cũng là một cú chạm, mà ngón tay
chưa kịp nhấc ra khỏi màn hình — không có khoảng chờ thì chính cú chạm đó huỷ luôn
chuyến bay nó vừa gọi, và bạn thấy nút bấm mãi không ăn.

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

### 5.11 HomeCanvas — màn hình đầu game

Vào game là thấy Home trước, bấm `Play` mới nạp màn. Mỗi ô trong danh sách hiện đúng
trạng thái của màn đó:

| Trạng thái | Hiện gì |
|---|---|
| Đã xong | tranh hoàn thiện, đủ màu |
| Đang chơi | **ảnh tiến độ ngay lúc này** — giống hệt thứ đang thấy trong game |
| Chưa mở | ô xám |

#### Prefab một ô

- [ ] `GameObject > UI > Image`, tên `HomeLevelItem`, `Height` = 220
- [ ] Chuột phải nó → `UI > Image`, tên `Thumbnail`, kéo giãn kín ô cha
- [ ] Chuột phải nó → `UI > Image`, tên `LockedPlaceholder`, màu xám, kéo giãn kín
- [ ] Chuột phải nó → `UI > Text - TextMeshPro`, tên `LevelText`, đặt giữa ô
- [ ] Chuột phải nó → `UI > Image`, tên `CurrentHighlight` (viền cho màn đang chơi),
      **tắt object này đi**

`LevelText` **chỉ hiện ở màn chưa mở khoá**. Màn đã mở thì chính bức tranh đã nói nó
là màn nào, thêm con số đè lên chỉ che mất tranh.
- [ ] Chọn `HomeLevelItem` → `Add Component > Home Level Item View`, gán bốn ô trên
- [ ] Kéo vào `Assets/Prefabs/`, xoá khỏi Hierarchy

`Thumbnail` nên đặt `Preserve Aspect` = tick. Ảnh sinh ra đúng tỉ lệ lưới (27×36 pixel
cho bảng 27×36), tràn khung nếu ô vuông mà tranh thì chữ nhật.

#### Canvas trong scene

- [ ] `GameObject > UI > Canvas`, tên `HomeCanvas`, `Sort Order` = **10**
- [ ] `Canvas Scaler`: Scale With Screen Size, 1080 × 1920
- [ ] Chuột phải nó → Create Empty, tên `HomeRoot`, kéo giãn kín màn hình
- [ ] Trong `HomeRoot` dựng: hai nút `SettingsButton` / `CollectionButton` ở trên,
      một `Scroll View` tên `LevelScroll` ở giữa, một `PlayButton` ở dưới
- [ ] `LevelScroll`: tắt `Horizontal`
- [ ] `LevelScroll`: ô `Vertical Scrollbar` = **None**, rồi **xoá object
      `Scrollbar Vertical`** trong `LevelScroll`

Để ô `Vertical Scrollbar` trống mà vẫn giữ object `Scrollbar Vertical` thì thanh cuộn
vẫn nằm đó, chỉ là không còn ai điều khiển. Phải xoá hẳn object.
- [ ] `LevelScroll > Viewport > Content`:
  - `Add Component > Vertical Layout Group`, `Spacing` = 24
  - **Bỏ tick cả `Control Child Width` lẫn `Control Child Height`**
  - Tick `Child Force Expand Width`
  - Tick **`Reverse Arrangement`** — màn 1 nằm DƯỚI CÙNG, danh sách mọc lên trên
  - `Add Component > Content Size Fitter`, `Vertical Fit` = **Preferred Size**

`Reverse Arrangement` chỉ đảo thứ tự các ô bên trong `Content`, không đổi cách
`Content` lớn lên. Phần cuộn tới màn hiện tại vẫn chạy đúng vì nó đọc vị trí thật của
ô chứ không giả định ô nào ở đâu.

> ⚠️ **`Control Child Width/Height` là cái bẫy ở đây.** Tick vào thì Layout Group tự
> quyết kích thước ô, và nó hỏi ô đó "mày muốn rộng bao nhiêu". Root của
> `HomeLevelItem` là một `Image` **không có sprite**, nên câu trả lời là **0** — ô co
> lại còn bề rộng bằng không.
>
> Hậu quả rất dễ nhận ra: `Thumbnail` và `LockedPlaceholder` kéo giãn theo cha nên biến
> mất cùng, còn `LevelText` neo giữa với kích thước cố định thì vẫn hiện. Thấy ô chỉ
> còn mỗi con số là gần như chắc chắn do đây.
>
> Muốn dùng `Control Child Height` thì phải gắn `Layout Element` lên `HomeLevelItem` và
> điền `Preferred Width` / `Preferred Height`.
- [ ] Chọn `HomeCanvas` → `Add Component > Home Screen View`
  - `Content` = `HomeRoot`
  - `Item Prefab` = `HomeLevelItem`
  - `Item Root` = **`Content`** (không phải `LevelScroll`)
  - `Scroll Rect` = `LevelScroll`
  - `Focus Alignment` = **1** (0 = sát mép trên, 0.5 = giữa, 1 = sát mép dưới)
  - `Focus Scaler` = chính `HomeCanvas` (xem ngay dưới)
- [ ] Chọn `HomeCanvas` → `Add Component > Scroll Focus Scaler`
  - `Scroll Rect` = `LevelScroll`
  - `Focus Alignment` = **1** — phải TRÙNG với ô cùng tên ở trên
  - `Focus Scale` = **1.25**, `Falloff Pixels` = **500**
  - `Play Button` = `PlayButton`, `Play Level Text` = chữ "Level N" trong nút
  - `Collection Button`, `Settings Button` = hai nút ở trên

Home **không tự mở lúc vào game** — nút Home trên HUD mới mở nó.

**Mở Home là tự cuộn tới màn đang chơi.** Ô đó dừng ở đâu trong khung nhìn thì do
`Focus Alignment` quyết định — mặc định **1**, tức sát mép dưới.

Con số tính theo **cạnh** của ô chứ không theo tâm, nên 0 và 1 vẫn thấy trọn ô chứ
không bị cắt mất một nửa:

| `Focus Alignment` | Ô dừng ở |
|---|---|
| 0 | cạnh TRÊN ô chạm mép trên khung nhìn |
| 0.5 | tâm ô ở giữa khung nhìn |
| 1 | cạnh DƯỚI ô chạm mép dưới khung nhìn |

#### Ô ở tiêu điểm phóng to

`ScrollFocusScaler` phóng ô đang đứng ở tiêu điểm lên `Focus Scale`, và nhỏ dần theo
khoảng cách tới đó.

Cỡ là **hàm của vị trí cuộn**, không phải một tween chạy theo thời gian. Nhờ vậy nó tự
mượt khi kéo tay, tự đúng khi thả cho quán tính trôi, và không bao giờ kẹt ở một cỡ dở
dang vì bị ngắt giữa chừng.

Hai ô cần để ý:

**`Focus Alignment` phải trùng** với ô cùng tên trên `Home Screen View`. Lệch nhau thì
lúc mở Home, ô được cuộn tới lại không phải ô được phóng to.

**`Falloff Pixels`** là tầm ảnh hưởng. Nên đặt cỡ một ô cộng spacing — nhỏ quá thì ô
đổi cỡ giật cục khi cuộn, lớn quá thì mọi ô đều hơi to và không ô nào ra dáng được
chọn. Với ô cao 220 và spacing 200 thì 500 là hợp.

> Phóng to bằng `localScale` **không đẩy các ô khác ra**: Layout Group xếp chỗ theo
> kích thước rect, không theo scale. Ô to lên sẽ đè lên hàng xóm nếu `Spacing` không
> đủ. Với spacing 200 và phóng 1.25 thì thoải mái.

Việc cuộn chạy ở **frame sau** khi dựng xong danh sách, không phải cùng frame. Layout
Group và Content Size Fitter tính lại kích thước ở cuối frame, và ngay sau đó ScrollRect
tự kẹp lại vị trí cuộn theo kích thước mới — đặt vị trí sớm hơn là đặt xong bị ghi đè.

`Sort Order` = 10 để Home nằm trên Canvas gameplay. Popup vẫn nằm trên Home vì
`Popups` ở trong Canvas riêng — nếu popup bị Home che thì nâng `Sort Order` của Canvas
chứa `Popups` lên cao hơn 10.

**Nút Settings sẽ báo lỗi đỏ trong Console** cho tới khi bạn khai một popup mang key
`Settings` vào `PopupConfig`. Chưa làm popup đó thì cứ để ô `Settings Button` trống.

#### Vì sao không cần tắt tay thứ gì

`GameEntryPoint` **không nạp màn lúc khởi động** nữa. Chưa nạp màn thì bảng chưa có
texture, `HudView` tự ẩn, thanh màu chưa dựng ô nào — cả ba tự im lặng, không phải đi
tắt từng cái.

Bấm `Play` thì Home ẩn đi và `LoadLevel` chạy, mọi lớp dựng lại theo sự kiện như
thường lệ.

#### Ảnh tiến độ được dựng thế nào

`LevelThumbnailBuilder` vẽ một `Texture2D` **một pixel là một ô**, đúng cách BoardView
vẽ bàn chơi. Bảng 27×36 ra ảnh 27×36 — vài KB, và `Image` phóng nó lên bao nhiêu tuỳ
layout. Nhớ để `Filter Mode` của ảnh là Point (builder đã đặt sẵn).

Ô "đã tô" đọc từ chính bản lưu ở mục 5.5. Màn đã xong thì không cần bản lưu — bản lưu
của nó bị xoá ngay lúc nó xong, và "đã xong" theo định nghĩa là mọi ô đều đã tô.

> Ảnh này **tự dựng lúc chạy, không phải asset**. Unity không dọn giúp. `HomeScreenView`
> huỷ cả `Sprite` lẫn `Texture` mỗi lần dựng lại danh sách — `Destroy(sprite)` một mình
> để lại texture mồ côi, vì `Sprite.Create` không sở hữu texture nó trỏ tới.

### 5.12 LoadingCanvas — màn hình chờ lúc mở game

Vào game là màn này che trước, nạp màn đang chơi dở, rồi tắt. Không qua Home.

- [ ] `GameObject > UI > Canvas`, tên `LoadingCanvas`, `Sort Order` = **100**
- [ ] `Canvas Scaler`: Scale With Screen Size, 1080 × 1920
- [ ] Chuột phải nó → Create Empty, tên `LoadingRoot`, kéo giãn kín màn hình
- [ ] Trong `LoadingRoot` đặt nền đục, logo, và một `Image` tên `ProgressFill` nếu muốn
      thanh tiến trình — `Image Type` = **Filled**, `Fill Method` = Horizontal
- [ ] Chọn `LoadingCanvas` → `Add Component > Loading Screen View`
  - `Content` = `LoadingRoot`
  - `Minimum Seconds` = **0.8**
  - `Progress Fill` = `ProgressFill` (để trống cũng chạy)

`Sort Order` = 100 để nó nằm trên tất cả, kể cả popup.

**`Minimum Seconds` là thời lượng thật sự.** Việc nạp màn hiện chạy đồng bộ và gần như
tức thì, nên màn chờ này chủ yếu để che cú dựng bàn và cho một nhịp chuyển. Khi nào có
nạp asset thật thì chỗ chờ đã sẵn ở đây.

**Vì sao code nhường một frame trước khi nạp:** `LoadLevel` chạy đồng bộ. Gọi thẳng
trong cùng frame với lệnh bật màn chờ thì Canvas chưa kịp vẽ khung nào, và người chơi
không thấy màn chờ — chỉ thấy game đứng hình một nhịp rồi vào màn.

#### Luồng lúc mở game

```
GameEntryPoint.Start()
   └─ LoadingCanvas hiện
        └─ nhường 1 frame  ──►  LoadLevel(màn đang chơi dở)
             └─ đủ Minimum Seconds  ──►  LoadingCanvas tắt
```

Home **không** tự mở nữa. Nó mở bằng nút Home trên HUD (mục 5.9).

---

### 5.13 Tutorial — hướng dẫn cho người chơi mới

Vào màn 1 mà chưa tô ô nào thì hiện một bàn tay chỉ vào ô màu đầu tiên trên thanh chọn,
kèm một bảng nhắc. Chọn màu xong là tắt.

Dựng trong **Canvas chứa HUD**, không dựng canvas riêng.

- [ ] Chọn Canvas của HUD → chuột phải → Create Empty, tên `Tutorial`.
      Kéo nó xuống **cuối cùng** trong danh sách con để nó vẽ trên mọi thứ
- [ ] Trong `Tutorial` → Create Empty, tên `TutorialContent`, kéo giãn kín màn hình
- [ ] Trong `TutorialContent` đặt hai thứ:
  - một `Image` tên `Finger` — ảnh bàn tay, **Pivot để ở đầu ngón trỏ**
  - một bảng nhắc: nền + `TextMeshProUGUI`, đặt ở chỗ không che thanh chọn màu
- [ ] Chọn **mọi** `Image` và `Text` trong `Tutorial` → **bỏ tick `Raycast Target`**
- [ ] Chọn `Tutorial` → `Add Component > Tutorial Overlay View`
  - `Content` = `TutorialContent`
  - `Finger` = `Finger`
  - `Finger Offset` = **(0, -60)** — chỉnh tới khi đầu ngón chạm đúng ô màu
  - `Tutorial Level Id` = **1**
  - `Delay Seconds` = **0.6**
- [ ] Tắt `TutorialContent` trong Inspector (code tự bật khi cần)

> **Bỏ tick `Raycast Target` là bước bắt buộc, không phải tuỳ chọn.**
> Để bật thì lớp hướng dẫn nuốt cú chạm: người chơi bấm đúng viên ngọc mà ngón tay đang
> chỉ, không có gì xảy ra, và hướng dẫn không bao giờ tắt được. Đây là lỗi khó lần nhất
> trong cả mục này vì mọi thứ trông vẫn đúng.

> **`Content` phải là object CON.** Bỏ trống hoặc trỏ về chính `Tutorial` thì lúc tắt
> hướng dẫn sẽ huỷ luôn coroutine của chính nó. Code báo lỗi đỏ rõ ràng nếu gán nhầm.

**Điều kiện hiện** đọc thẳng từ trạng thái tô (`IsUntouched`), không lưu cờ "đã xem".
Muốn thử lại bao nhiêu lần cũng được: `Edit > Clear All PlayerPrefs`, hoặc tô một ô rồi
vào lại màn — tô rồi thì thôi không hiện.

**Vì sao không dùng popup:** popup có nền chặn bấm, mà thứ ngón tay đang chỉ vào lại
chính là cái người chơi phải bấm. Lớp này chỉ là ảnh đè lên, không chặn gì.

**Nghe `OnBoardReady` chứ không nghe `OnLevelStarted`:** lúc màn bắt đầu, thanh chọn màu
chưa dựng xong các ô, mà ngón tay cần biết ô màu đầu tiên đứng ở đâu. Vì cùng lý do đó,
`GameEntryPoint` gọi `Init` của nó **sau** `ColorPaletteBar`.

#### Luồng

```
Vào màn  ──►  OnBoardReady
                └─ đúng màn 1?  và  IsUntouched?
                     └─ chờ Delay Seconds
                          └─ đặt ngón tay vào ô màu đầu tiên, bật Content
                               └─ ngón tay gõ nhè nhẹ theo nhịp
Chọn màu ──►  tắt ngay
Đổi màn  ──►  tắt ngay
```

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
- [ ] Bấm một ô màu → ô đó **nhô cao lên và hiện màu**, ô khác phẳng lại và mất màu
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
- [ ] Kéo tới mép bảng → đi thêm được nửa màn nữa rồi mới dừng
- [ ] Kéo hết cỡ → mép bảng nằm đúng giữa màn hình, nửa màn còn lại là nền trống
- [ ] Trên điện thoại: một ngón theo cùng luật trên, hai ngón luôn là di chuyển và zoom

**Hiệu năng (thử với ảnh 64 ô cạnh dài):**

- [ ] Trên máy thật: FPS chạm 60, không phải 30 — `ApplicationSettings` lo việc này,
      không cần gán gì trong scene
- [ ] Để yên vài phút không chạm: màn hình **không** tự tắt

- [ ] Kéo tay tô nhanh không giật
- [ ] Bấm chọn màu không khựng
- [ ] Tô gần kín bảng vẫn mượt như lúc mới vào

---

## Phần 6B — Đo hiệu năng

### Đo nhanh ngay trong Play Mode

Không cần build. `Window > Analysis > Profiler`, bấm Play, tick `Record`.

Ba lớp nặng nhất đã được **gắn nhãn sẵn**, nên chúng hiện thành dòng riêng trong tab
CPU thay vì lẫn vào `LateUpdate`:

| Nhãn | Lớp |
|---|---|
| `JewelPainter.Numbers.Refresh` | lớp số |
| `JewelPainter.Hints.Refresh` | lớp gợi ý |
| `JewelPainter.Jewels.Refresh` | lớp ngọc |

Cách xem: chọn module **CPU Usage** → chế độ **Hierarchy** → zoom qua lại vài giây →
`Pause` → kéo tới frame cao nhất → sắp cột `Time ms` giảm dần.

> ⚠️ **Con số tuyệt đối trong Editor không dùng được.** Editor chậm hơn build vài lần,
> lại cộng thêm Scene view, domain reload và cấp phát chỉ có trong Editor. Một frame
> 30ms ở đây có thể là 6ms trên máy thật.
>
> Thứ dùng được là **thứ hạng**: lớp nào chiếm phần lớn nhất thì trên máy thật cũng
> vậy. Đủ để biết sửa chỗ nào.
>
> Đóng cửa sổ Scene lại trước khi đo, để Unity không vẽ hai lần.

### Đo thật trên máy

Khi cần con số tin được:

- [ ] `File > Build Profiles` (Unity 6 không còn `Build Settings`)
- [ ] Chọn nền tảng, tick **Development Build**
- [ ] Tick **Autoconnect Profiler** — chỉ hiện sau khi tick ô trên
- [ ] **Đừng** tick `Deep Profiling Support`: nó đo mọi lời gọi hàm nên bóp méo đúng
      con số cần tìm
- [ ] `Build And Run`, rồi mở Profiler trong Editor

Không tự nối được thì chọn thiết bị bằng tay ở dropdown góc trên Profiler.

### So sánh trước và sau khi sửa

Cài package **Profile Analyzer** (`Window > Package Manager > Unity Registry`). Nó cho
lưu lại một loạt frame rồi **so hai bản ghi cạnh nhau**, trả lời được câu "sửa xong có
thật sự nhanh hơn không" — thứ mà nhìn biểu đồ chạy thời gian thực không nói được.

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
| Thấy sọc viền ô ngay lúc mới vào màn | `GridLines > Level Size Is Opaque` chưa tick, hoặc `Transparent Size` khác 0 | 5.7B |
| Thắng màn thì khựng một nhịp | `Cell Step` quá nhỏ so với cỡ bảng | 5.8E |
| Mở lại game mất hết phần đã tô | `Paint Progress Store` chưa gắn lên `PaintManager` | 5.5 |
| Console báo bản lưu không khớp cỡ lưới | Đã sinh lại `GridData` cho màn đang chơi dở | bình thường, bản lưu cũ bị bỏ |
| Đổi `Number Color` lúc đang chạy không thấy gì | Màu chỉ áp dụng lúc tạo chữ — vào lại màn | 5.7 |
| Số bò dần từ trên xuống thay vì hiện cùng lúc | Bản cũ. Nay số dựng ở alpha 0 rồi bật một lượt | 5.7 |
| Không thấy màn hình chờ, game vào thẳng | `Loading Screen View` chưa gắn, hoặc `Minimum Seconds` = 0 | 5.12 |
| Màn hình chờ bị popup che | `LoadingCanvas` có `Sort Order` thấp hơn Canvas chứa `Popups` | 5.12 |
| Popup bị màn hình Home che | Canvas chứa `Popups` có `Sort Order` thấp hơn `HomeCanvas` | 5.11 |
| Ô Home chỉ hiện số màn, mất ảnh và ô xám | `Content` đang tick `Control Child Width` — xem cảnh báo ở 5.11 | 5.11 |
| Mở Home không tự cuộn tới màn hiện tại | Chưa gán ô `Scroll Rect` trên `Home Screen View` | 5.11 |
| Ô trong danh sách Home méo hình | `Thumbnail` chưa tick `Preserve Aspect` | 5.11 |
| Coin bay được nửa đường thì mất | `Coins Parent` bị Mask hoặc không phủ kín màn hình | 4.6 |
| Nút Continue không hiện | Chưa gán ô `Continue Button` — thiếu mảnh nào của phần tiền thì nút vẫn hiện bình thường | 4.6 |
| Icon gợi ý rơi lúc camera còn đang bay | `HintMarker > Start Delay` nhỏ hơn `Board Camera > Focus Duration` | 5.8F |
| Bấm bánh răng ra dòng đỏ trong Console | Chưa khai `Settings` vào `PopupConfig` | 4.7 |
| Popup nhắc nhở không tự tắt | `Auto Hide Seconds` đang để 0 | 4.7 |
| Tô xong một màu, các ô loé lần lượt chứ không cùng lúc | `Max Per Frame` khác 0, hoặc `Max Concurrent` nhỏ hơn số ô của màu | 4.6 |
| Khựng một nhịp đúng lúc một màu vừa xong | `Prewarm Count` của kho nhỏ hơn số ô của màu đó | 4.6 |
| Chọn màu thì marker hiện lần lượt chứ không cùng lúc | `Hints > Max Spawn Per Frame` khác 0 | 5.8 |
| Chọn màu bị khựng một nhịp | `Prewarm From Largest Color` chưa tick và `Prewarm Count` quá nhỏ | 5.8 |
| Zoom ra nhanh thấy ngọc mọc dần | `Jewels > Max Spawn Per Frame` khác 0 | 5.8B |
| Vào màn lâu hơn trước | Ba lớp đều dựng sẵn object; bỏ tick các ô `Prewarm From...` nếu máy yếu | 5.7, 5.8, 5.8B |
| Zoom qua lại liên tục vẫn giật | Xem `Show Hysteresis` (5.7); nâng lên 0.15–0.2 nếu vẫn còn | 5.7 |
| Zoom ra hết cỡ mà số vẫn không ẩn | `Show Hysteresis` quá lớn so với khoảng từ ngưỡng tới `Camera Max Size` — hạ xuống | 5.7 |
| Ô có ngọc mà không có màu, hoặc màu không phủ hết ô | Hai lớp bảng lệch nhau. Console báo rõ khi vào màn | 5.6 |
| Zoom to thì ô đã tô mất màu | `Painted Renderer` chưa gán, hoặc lỡ gắn `Board Color Fade` lên `Painted` | 5.6 |
| Ô đã tô che mất viền và số | `Painted` đặt `Order in Layer` lớn hơn 0 | 5.6 |
| Ngọc to hoặc nhỏ hơn ô | `Pixels Per Unit` chưa bằng cạnh ảnh | 4.3 |
| Hướng dẫn hiện ra nhưng bấm ô màu không ăn, và nó không tắt | Còn `Image` nào trong `Tutorial` đang bật `Raycast Target` | 5.13 |
| Console báo đỏ về ô `Content` của Tutorial | `Content` bỏ trống hoặc trỏ về chính object mang script | 5.13 |
| Vào màn 1 không thấy hướng dẫn | Màn đó đã tô ít nhất một ô — xoá tiến độ rồi thử lại | 5.13 |
| Ngón tay chỉ lệch khỏi ô màu | `Finger Offset` chưa chỉnh, hoặc Pivot của ảnh tay không ở đầu ngón | 5.13 |

---

## Các lớp chồng nhau — để dễ hình dung

| Lớp | Order in Layer | Vai trò |
|---|---|---|
| Ô **chưa tô** (texture) | -1 | Màu xám. **Mờ dần khi phóng to** để lộ số |
| Ô **đã tô** (texture) | 0 | Màu thật. **Không bao giờ mờ** — đó là lý do nó tách riêng |
| Viền ô (texture) | 1 | Chỉ quanh ô có màu. Dựng một lần lúc vào màn |
| Gợi ý (sprite) | 2 | Chỉ sinh cho ô tô được **đang nhìn thấy** |
| Số | 3 | Trên gợi ý để marker không che số |
| Ngọc (sprite) | 4 | Trên số — ô tô xong thì ngọc che số, thành dấu hiệu "đã xong" |
| Lấp lánh (particle) | 12 | Lấy từ kho theo sự kiện, không gắn vào ngọc |
| Ngọc đang bay | 15 | Trên tất cả |

Ba lớp texture có chi phí cố định, không phụ thuộc số ô. Các lớp sprite bị cull theo
tầm nhìn nên số object phụ thuộc mức zoom, không phụ thuộc kích thước bảng.

Một ô nằm trên **đúng một** trong hai lớp texture đầu: tô tới đâu, pixel bên lớp chưa
tô bị xoá tới đó. Nhờ vậy làm mờ lớp xám không đụng gì tới màu đã tô.

Ngưỡng ẩn khác nhau có chủ ý: ngọc và số ẩn ở 14 pixel, marker gợi ý ẩn ở 5. Ngọc bị
cull thì ô vẫn còn màu trong texture, còn marker bị cull là mất hẳn dấu hiệu.
