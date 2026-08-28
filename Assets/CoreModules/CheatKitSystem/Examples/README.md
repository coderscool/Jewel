# CheatKit — Examples

Ví dụ **chạy được ngay** cho 4 dòng game: **Puzzle · Casual · RPG · Action**.
Mỗi ví dụ là một *mock-game thuần C#* + *adapter (bridge)* nối vào port của CheatKit — chứng minh
kit **không phụ thuộc game cụ thể**, và cách thêm cheat **đặc thù genre** mà không sửa kit (OCP).

> Toàn bộ thư mục gate `CHEAT_ENABLED` (asmdef `defineConstraints`) + `autoReferenced: false`
> → **0 byte ở release** và không lẫn vào Assembly-CSharp của game.

---

## Chạy thử (Editor)

1. Bật cheat: **`CoreModules ▸ CheatKit ▸ Enabled (Active Platform)`** (hoặc Settings) → Unity recompile.
2. **`CoreModules ▸ CheatKit ▸ Examples ▸ Puzzle / Casual / RPG / Action`** → tạo scene mới đã wire sẵn.
3. Bấm **▶ Play**. Mở panel: **chạm 5 lần vùng giữa-trên** màn hình (≈1.5s) hoặc phím **F1**.
4. Bấm thử **WIN / LOSE / Set Level / Add Coins**, kéo **Speed**, bật **cheat flags**; với RPG/Action có
   thêm section cheat riêng (God Mode/+XP… · Infinite Ammo/Spawn…). Quan sát **Console** (mock log) và
   nút **WIN tự khoá** khi phase rời Playing.

Muốn giữ scene? `File ▸ Save As` vào thư mục test của bạn (không bắt buộc).

---

## Mỗi ví dụ minh hoạ điều gì

| Genre | Mock-game | Port dùng | Điểm nhấn |
|---|---|---|---|
| **Puzzle** | `MockGameModel` (20 level) | 3 port chuẩn | Level-based đầy đủ — khuôn mẫu giống WordJigsort. |
| **Casual** | `MockGameModel` (endless) | 3 port chuẩn | `Count = 0` → panel **tự ẩn** thanh `x / N` (graceful degradation). |
| **RPG** | `RpgMockGame` | 3 chuẩn **+ `IRpgCheatService`** | **OCP**: custom port + `RpgCheatModule` thêm qua `AddModule<T>`. |
| **Action** | `ActionMockGame` | level+flow **+ `IActionCheatService`** | **ISP**: `progress: null` (không tiền tệ) → panel ẩn coin/progress. |

---

## Bản đồ SOLID trong code ví dụ

- **SRP** — `*MockGame` (state) · `*CheatBridge` (adapter) · `*CheatModule` (UI) · `ExampleCheatBootstrap`
  (composition root) tách bạch, mỗi class một lý do thay đổi.
- **OCP** — thêm cheat genre = thêm **port + module**, gắn bằng `CheatPanelBuilder.AddModule<T>(panel)`;
  builder/panel của kit KHÔNG đổi.
- **LSP** — mọi bridge thay thế được cho các interface port nó implement.
- **ISP** — port tách theo concern; genre chỉ implement/bind port mình cần (Action bỏ `IProgress…`).
- **DIP** — module phụ thuộc **interface** (`IRpgCheatService`/`IActionCheatService`) qua `Bind`;
  bootstrap mới là nơi biết concrete.

## Tối ưu mobile trong ví dụ

- Mock thuần C#, **0 alloc/frame**; bootstrap chỉ chạy ở `Start`.
- Module genre: `OnUpdate` chỉ chạm `Text.text` **khi giá trị đổi**; nút gate theo phase.
- Scene gần như rỗng (1 GameObject) — không texture/asset nặng.
- Toàn bộ strip khỏi release qua `CHEAT_ENABLED`.

---

## Tự thêm một genre mới (mẫu 4 bước)

1. `IMyGenreCheatService` — port đặc thù (đặt trong thư mục genre của bạn, **không** trong kit).
2. `MyGenreMockGame` (hoặc service thật) — nguồn state.
3. `MyGenreCheatBridge` — implement port chuẩn cần dùng **+** `IMyGenreCheatService`.
4. `MyGenreCheatModule : UITestModuleBase, ICheatBindable, ICheatModuleUi` — UI; lấy port qua
   `services.Get<IMyGenreCheatService>()`. Wire trong bootstrap: `AddModule<MyGenreCheatModule>(panel)`
   rồi `new CheatServices(level, flow, progress, bridge)`.
