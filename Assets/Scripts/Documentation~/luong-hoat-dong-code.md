# JewelPainter — luồng hoạt động của code

Liệt kê từng hàm theo đúng thứ tự chạy. Danh sách hàm lấy trực tiếp từ code, không
viết theo trí nhớ.

---

# Phần A — Từ lúc vào game tới khi nạp xong màn

## Giai đoạn 1 — Trước khi scene nạp

| Hàm | Vai trò |
|---|---|
| `ApplicationSettings.Configure()` | Chạy qua `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]`. Không ai gọi, không cần object trong scene. Tắt vSync, đặt `targetFrameRate = 60`, chặn màn hình tự tắt. |

Tắt vSync **trước** khi đặt frame rate — vSync ghi đè `targetFrameRate`.

## Giai đoạn 2 — Dựng container

| Hàm | Vai trò |
|---|---|
| `GameLifetimeScope.Configure(builder)` | VContainer gọi trong `Awake`. Đăng ký `PlayerPrefsSaveService`, `PlayerProgress`, 15 component tìm trong scene, và entry point. |
| `PlayerProgress(ISaveService)` | Constructor **đọc luôn màn đã lưu** từ `PlayerPrefs`. Tới đây game đã biết người chơi đang ở màn mấy. |
| `PlayerPrefsSaveService.GetInt/GetBool/...` | Bọc `PlayerPrefs`. Tồn tại để `PlayerProgress` không đụng thẳng vào Unity API và test được. |

`RegisterComponentInHierarchy` chạy ở bước này — thiếu component nào trong scene thì
`VContainerException` ném ngay tại đây, kèm đúng tên class.

## Giai đoạn 3 — Nối dây

`GameEntryPoint.Start()` gọi `Init` cho từng thành phần. **Chưa có gì hiện ra** —
mọi `Init` chỉ đăng ký lắng nghe.

Thứ tự gọi, và mỗi cái đăng ký nghe gì:

| Thứ tự | Hàm | Đăng ký nghe |
|---|---|---|
| 1 | `SoundService.Init(save)` | — (đọc trạng thái bật/tắt tiếng đã lưu) |
| 2 | `LevelManager.Init(progress)` | — |
| 3 | `HudView.Init(levelService)` | `OnLevelStarted` |
| 4 | `PaintManager.Init(levelService)` | `OnLevelStarted` |
| 5 | `BoardView.Init(levelService)` | `OnLevelStarted` |
| 6 | `BoardNumberLayer.Init(boardView)` | `OnBoardRebuilt` |
| 7 | `BoardGridLines.Init(boardView)` | `OnBoardRebuilt` |
| 8 | `BoardInput.Init(boardView, paintService)` | — (đọc input trong `Update`) |
| 9 | `BoardCamera.Init(boardView, levelService, boardInput)` | `OnBoardRebuilt` |
| 10 | `ColorPaletteBar.Init(paintService, levelService)` | `OnBoardReady`, `OnCellPainted`, `OnColorSelected` |
| 11 | `JewelFlyEffect.Init(boardView, paintService, paletteBar)` | `OnBoardRebuilt`, `OnCellPainted` |
| 12 | `HintLayer.Init(boardView, paintService, flyEffect)` | `OnBoardRebuilt`, `OnColorSelected`, `OnJewelLanded` |
| 13 | `JewelLayer.Init(boardView, paintService, flyEffect)` | `OnBoardRebuilt`, `OnJewelLanded` |
| 14 | `LevelFlowController.Init(levelService, paintService, flyEffect)` | `OnJewelLanded`, `OnLevelStarted` |

`BoardColorFade` **không** có `Init` — nó tự đăng ký trong `OnEnable`, vì có hai
instance trong scene mà DI chỉ tìm được một.

Thứ tự này **quyết định thứ tự phản ứng** ở giai đoạn sau. Quan trọng nhất:
`PaintManager` phải trước `BoardView`, để trạng thái tô sẵn sàng trước khi bảng dựng.

## Giai đoạn 4 — `LoadLevel` châm ngòi

`GameEntryPoint.Start` kết thúc bằng `_levelService.LoadLevel(_progress.Level)`.

### 4.1 LevelManager

| Hàm | Vai trò |
|---|---|
| `LevelManager.LoadLevel(id)` | Gọi `FindConfig`, gán `_currentConfig`, bắn `OnLevelStarted(id)`. |
| `LevelManager.FindConfig(id)` | Duyệt mảng `_levels` tìm `LevelConfig` có `LevelId` khớp. `null` nếu không có. |

### 4.2 Bốn nơi nghe `OnLevelStarted`, theo đúng thứ tự Init

**HudView**

| Hàm | Vai trò |
|---|---|
| `HandleLevelStarted(id)` | Gọi `SetLevel`. |
| `SetLevel(level)` | `SetText("Level {0}", level)` — chỉ gọi khi số đổi, tránh sinh rác. |

**PaintManager**

| Hàm | Vai trò |
|---|---|
| `HandleLevelStarted(id)` | Xoá state cũ, bỏ chọn màu, đọc `CurrentGrid.ToGrid()`, tạo `PaintState` mới, bắn `OnBoardReady`. |
| `PaintState(grid)` | Constructor gọi `ScanGrid`. |
| `PaintState.ScanGrid()` | Duyệt toàn lưới: đếm số ô mỗi màu vào `_remaining` và `_totals`, gom danh sách màu ảnh dùng, sắp xếp tăng dần. Đây là nơi `UsedPaletteIndices` sinh ra. |

→ `OnBoardReady` kéo theo **ColorPaletteBar**:

| Hàm | Vai trò |
|---|---|
| `HandleBoardReady()` | Ẩn hết ô cũ, đọc màu từ `CurrentGrid.Colors`, dựng một ô cho mỗi màu trong `UsedPaletteIndices`. Bỏ qua màu đã tô xong. |
| `GetSwatch(slot)` | Lấy ô có sẵn hoặc `Instantiate` thêm — tạo một lần rồi tái dùng, không destroy mỗi màn. |
| `ColorSwatchView.Bind(index, color, onClicked)` | Gán màu nền, số thứ tự, màu chữ, nối sự kiện bấm. |
| `ColorSwatchView.SetRemaining(n)` / `SetProgress(p)` | Cập nhật số ô còn lại và vòng tiến độ. Chỉ gán khi giá trị đổi. |

**BoardView** — nặng nhất

| Hàm | Vai trò |
|---|---|
| `HandleLevelStarted(id)` | Gọi `Rebuild`. |
| `Rebuild()` | Giải phóng texture cũ, gán `Config`, đọc `CurrentGrid`, `ToGrid()`, lấy bảng màu. Hỏng ở bất kỳ bước nào thì gọi `ClearBoard` và dừng. |
| `LevelGridData.ToGrid()` | Dựng `PixelGrid` từ mảng int đã serialize. `null` nếu asset chưa sinh dữ liệu. |
| `PixelGrid.FromArray(w, h, cells)` | Kiểm độ dài mảng rồi copy vào lưới mới. |
| `BuildPixels()` | Duyệt từng ô: rỗng thì trong suốt, có màu thì lấy bản xám. Lật trục y vì `PixelGrid` đếm từ trên còn `Texture2D` đếm từ dưới. |
| `BoardColors.ToGrayscale(color)` | Đổi màu sang xám theo trọng số cảm nhận. |
| `WritePixel(x, y, color)` | Ghi vào mảng `_pixels`, lo phần lật y. |
| `ClearBoard(reason)` | Gỡ sprite, xoá `Grid`/`Colors`/`Layout`, ghi cảnh báo, **vẫn bắn `OnBoardRebuilt`** để các lớp khác biết mà dọn. |
| `ReleaseTexture()` | `Destroy` sprite và texture — chúng sinh lúc chạy nên không tự thu hồi. |

Cuối `Rebuild`: tạo `Texture2D`, `Sprite.Create` với `pixelsPerUnit = 1`, gán vào
renderer, tạo `BoardLayout`, rồi bắn `OnBoardRebuilt`.

**LevelFlowController**

| Hàm | Vai trò |
|---|---|
| `HandleLevelStarted(id)` | `StopAllCoroutines`, reset cờ `_isTransitioning`. Chặn coroutine chuyển màn cũ chạy tiếp nếu màn được nạp lại giữa chừng. |

### 4.3 Bảy lớp nghe `OnBoardRebuilt`

| Lớp | Hàm | Vai trò |
|---|---|---|
| `BoardCamera` | `HandleBoardRebuilt()` | Gọi `ResolveZoomRange`, đặt camera về mức xa nhất, đưa về giữa. |
| | `ResolveZoomRange(layout)` | Lấy min/max từ `LevelConfig`; để 0 thì tự tính từ kích thước bảng. Min lớn hơn max thì đổi chỗ và cảnh báo. |
| `BoardGridLines` | `HandleBoardRebuilt()` | Dựng texture viền **một lần duy nhất** cho cả màn. |
| | `DrawGrid(...)` | Mỗi ô có màu được một khung khép kín nằm gọn trong khối pixel của nó. |
| | `FillRect(...)` | Ghi một hình chữ nhật đặc vào mảng pixel. |
| `BoardColorFade` | `HandleBoardRebuilt()` | Bật cờ lấy lại mốc zoom. |
| `BoardNumberLayer` | `HandleBoardRebuilt()` | Trả hết chữ về pool, bật cờ lấy lại mốc và cờ cần refresh. |
| `HintLayer` | `HandleBoardRebuilt()` | Trả hết marker về pool rồi `Prewarm` dựng sẵn 400 cái. |
| `JewelFlyEffect` | `HandleBoardRebuilt()` | Kill toàn bộ tween, xoá danh sách đang bay, `Prewarm`. |
| `JewelLayer` | `HandleBoardRebuilt()` | Trả hết ngọc về pool, `Prewarm`, bật cờ refresh. |

### 4.4 Frame đầu tiên sau đó

| Hàm | Vai trò |
|---|---|
| `BoardColorFade.LateUpdate()` | Lấy `_baseSize` từ camera (đợi tới `LateUpdate` để `BoardCamera` kịp đặt xong), rồi `ApplyAlpha`. |
| `BoardColorFade.AlphaFor(size)` | Nội suy alpha giữa `Opaque Size` và `Transparent Size`. `LevelConfig.FadeSwitchSize` ghi đè một trong hai tuỳ ô tick. |
| `BoardNumberLayer.LateUpdate()` | Lấy `_baseSize`, phát hiện camera đổi, gọi `Refresh`. |
| `BoardNumberLayer.ShouldShowNumbers()` | So mức zoom hiện tại với mốc nội suy giữa `_baseSize` và `FadeSwitchSize`. Không có `FadeSwitchSize` thì quay về ngưỡng pixel. |
| `*.Refresh()` (ba lớp) | Tính vùng ô đang nhìn thấy, trả ô ngoài vùng về pool, sinh ô mới **tối đa N cái mỗi frame**. Trả `false` khi hết hạn mức để frame sau làm tiếp. |
| `BoardLayout.VisibleCells(rect)` | Giao của tầm nhìn với bảng, đã kẹp trong biên. |
| `BoardLayout.CellToWorldCenter(x, y)` | Đổi toạ độ ô sang world. Lật y vì ô đếm từ trên còn world +y hướng lên. |

Tới đây màn đã nạp xong và chờ người chơi.

---

# Phần B — Ba luồng lúc chơi

## B1. Chọn một màu

```
ColorSwatchView.HandleClick
  → ColorPaletteBar.HandleSwatchClicked
    → PaintManager.SelectColor(index)
      → bắn OnColorSelected
        → HintLayer.HandleColorSelected   : trả hết marker, bật cờ refresh
        → ColorPaletteBar.HandleColorSelected : đổi ô nào đang hiện viền chọn
```

`PaintManager.SelectColor` bỏ qua nếu màu không có trong ảnh hoặc trùng màu đang chọn.

Frame kế tiếp `HintLayer.Refresh` sinh marker cho ô tô được, chia đều nhiều frame.

## B2. Tô một ô

```
BoardInput.Update
  → TryGetStrokePosition   : một ngón hoặc chuột trái; hai ngón thì bỏ, camera lo
  → DecideOwner            : CHỈ CHẠY LÚC BẤM XUỐNG
      trên UI          → None
      ô có gợi ý       → Paint
      chỗ khác         → Camera
  → PaintAt                : chỉ khi owner là Paint
    → BoardLayout.TryWorldToCell
    → PaintManager.TryPaint(x, y)
      → PaintState.TryPaint : kiểm CanPaint, đánh dấu đã tô, trừ bộ đếm
      → bắn OnCellPainted
        → JewelFlyEffect.HandleCellPainted
        → ColorPaletteBar.HandleCellPainted : trừ số còn lại, nhích vòng tiến độ,
                                              ẩn ô màu nếu vừa hết
```

`JewelFlyEffect` là mắt xích quyết định phần còn lại:

| Hàm | Vai trò |
|---|---|
| `HandleCellPainted(cell, index)` | `if (!TryStartFlight(...)) Land(...)` — **mọi đường thoát đều phải tới `Land`**, thiếu một nhánh là ô đó kẹt vĩnh viễn. |
| `TryStartFlight(...)` | Xin điểm xuất phát từ `IPaintOriginProvider`, thuê một viên từ pool, chạy `DOMove` + `DOScale`. Ghi ô vào `_inFlight`. |
| `ColorPaletteBar.TryGetOriginWorldPosition(index)` | Tìm ô màu tương ứng, đổi vị trí trên màn hình sang world. `false` nếu chưa gán `World Camera`. |
| `Release(flyer)` | **Kill tween trước** rồi mới trả về pool — object tái dùng còn tween cũ sẽ bị kéo về vị trí lần trước. |
| `Land(cell, index)` | Gọi `BoardView.RevealCell` rồi bắn `OnJewelLanded`. |

```
JewelFlyEffect.Land
  → BoardView.RevealCell   : ghi màu thật vào pixel, bật cờ dirty
  → bắn OnJewelLanded
    → HintLayer.HandleJewelLanded  : gỡ marker của ô đó
    → JewelLayer.HandleJewelLanded : hiện viên ngọc
    → LevelFlowController.HandleJewelLanded : kiểm điều kiện thắng
```

Cuối frame `BoardView.LateUpdate` thấy cờ dirty thì gọi `SetPixels32` + `Apply` **một
lần duy nhất**, dù frame đó có tô mười ô.

## B3. Thắng màn

```
LevelFlowController.HandleJewelLanded
  → PaintManager.IsComplete → PaintState.IsComplete (_remainingTotal == 0)
  → StartCoroutine(GoToNextLevel)
      chờ Delay Seconds
      ILevelService.HasLevel(level + 1)?
        không → dừng, KHÔNG tăng tiến trình, ghi log
        có    → CompleteCurrentLevel() → PlayerProgress.Advance() → lưu PlayerPrefs
                LoadLevel(level mới)   → quay lại Giai đoạn 4
```

Không tăng tiến trình ở màn cuối là có chủ ý: tăng rồi thì lần mở game sau nạp một
màn không tồn tại và người chơi nhận bảng trống.

---

# Phần C — Bảng tra sự kiện

| Sự kiện | Ai bắn | Ai nghe |
|---|---|---|
| `OnLevelStarted` | `LevelManager.LoadLevel` | HudView, PaintManager, BoardView, LevelFlowController |
| `OnBoardReady` | `PaintManager.HandleLevelStarted` | ColorPaletteBar |
| `OnBoardRebuilt` | `BoardView.Rebuild` và `ClearBoard` | BoardCamera, BoardGridLines, BoardColorFade ×2, BoardNumberLayer, HintLayer, JewelFlyEffect, JewelLayer |
| `OnColorSelected` | `PaintManager.SelectColor` | HintLayer, ColorPaletteBar |
| `OnCellPainted` | `PaintManager.TryPaint` | JewelFlyEffect, ColorPaletteBar |
| `OnJewelLanded` | `JewelFlyEffect.Land` | HintLayer, JewelLayer, LevelFlowController |

**Không thành phần nào gọi thẳng thành phần khác.** `LevelManager` không biết
`BoardView` tồn tại. Tất cả nối qua sự kiện, `GameEntryPoint` là nơi duy nhất biết
mặt tất cả.

Cái giá phải trả: **thứ tự đăng ký trở thành thứ tự thực thi ngầm**. Đây đúng là chỗ
đã sinh ra ba lỗi trước đây, khi mỗi lớp tự nghe `OnCellPainted` và phản ứng sớm theo
cách riêng — màu đổi trước, marker biến mất trước. Sửa xong thì `JewelFlyEffect`
thành nơi duy nhất quyết định lúc nào một ô coi như xong.

---

# Phần D — Các lớp thuần C#, không dính Unity lifecycle

Test EditMode được, không cần scene.

| Lớp | Hàm chính | Vai trò |
|---|---|---|
| `PixelGrid` | `GetCell`, `SetCell`, `ToArray`, `FromArray` | Ma trận chỉ số bảng màu. Quy ước `y = 0` là hàng **trên cùng**. |
| `PaintState` | `CanPaint`, `TryPaint`, `IsPainted`, `RemainingFor`, `TotalFor`, `ProgressFor`, `IsComplete` | Toàn bộ luật tô. `ScanGrid` đếm một lần lúc dựng. |
| `PlayerProgress` | `Advance`, `Reset` | Màn hiện tại, tự lưu qua `ISaveService`. |
| `BoardLayout` | `CellToWorldCenter`, `TryWorldToCell`, `VisibleCells`, `CellScreenPixels` | Toán toạ độ bảng. Không biết camera hay texture. |
| `PaletteMatcher` | `FindNearest`, `Distance` | Tìm màu gần nhất theo công thức redmean. |
| `ColorQuantizer` | `Quantize` | Median cut rút bảng màu từ ảnh, kèm gộp màu gần giống. |
| `GridSampler` | `Sample` | Chia ảnh thành lưới, mỗi ô lấy **màu chiếm phần lớn**. |
| `BoardColors` | `Luminance`, `ToGrayscale` | Phép tính màu dùng chung. |

---

# Phần E — Chỉ chạy trong Editor

| Hàm | Vai trò |
|---|---|
| `ImageToGridWindow.OnGUI()` | Vẽ cửa sổ tool, thu input. |
| `ImageToGridWindow.Generate()` | Gọi generator, dựng preview. |
| `ImageToGridWindow.Save()` | Tạo `LevelGridData` asset, gọi `SetData`. |
| `ImageToGridGenerator.Generate(...)` | Bật Read/Write cho ảnh, lật pixel, `GridSampler.Sample`, `ColorQuantizer.Quantize`, `PaletteMatcher.FindNearest` cho từng ô. |
| `ImageToGridGenerator.EnsureReadable(texture)` | Tự bật `Read/Write Enabled` qua `TextureImporter` thay vì bắt người dùng đi sửa tay. |
