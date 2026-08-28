# CheatKit

Bộ công cụ **cheat / dev-test in-game** tái sử dụng cho Unity (mobile / editor / WebGL).
Cung cấp panel runtime với các module sẵn có (chọn level, ép thắng/thua, test no-moves,
chỉnh tiến trình/tiền, time-scale, cheat flags, performance stats) — **không phụ thuộc game cụ thể**.
Game chỉ cần viết một *adapter* mỏng implement vài interface (port).

> **Bị loại hoàn toàn khỏi bản release**: toàn bộ asmdef gate bởi define `CHEAT_ENABLED`.
> Không set define → 0 byte trong build (an toàn cho store/production).

---

## 1. Triết lý kiến trúc (Ports & Adapters / Hexagonal)

```
        ┌─────────────────────────── CoreModules.CheatKit (asmdef, CHEAT_ENABLED) ──────────────────────────┐
        │  Core/                         Ports/ (interface)            Modules/                              │
        │   UITestPanel  (vòng đời)       ILevelCheatService            LevelSelectModule                    │
        │   UITestModuleBase              IFlowCheatService             GameControlModule                    │
        │   IUITestModule                 IProgressCheatService         PerformanceStatsModule               │
        │   CheatFlags (static)           IScreenshotCheat              Screenshot/ScreenshotCaptureService   │
        │   CheatUi / CheatPanelBuilder   CheatServices (DTO)                                                 │
        │   IPanelRevealGesture / ScreenTapReveal   ICheatBindable / ICheatModuleUi   CheatLog               │
        └──────────────────────────────────────────────▲────────────────────────────────────────────────────┘
                                                        │ implement (DIP)
        ┌───────────────────────────────────────────────┴──────────── Game (Assembly-CSharp, #if CHEAT_ENABLED) ┐
        │  <Game>CheatBridge : ILevelCheatService, IFlowCheatService, IProgressCheatService  (ADAPTER)           │
        │  CheatInstaller : IInstaller   → resolve services từ DI, dựng bridge, build/bind panel                │
        └────────────────────────────────────────────────────────────────────────────────────────────────────┘
```

- **CheatKit không biết game nào.** Nó phụ thuộc *abstraction* (port), không phụ thuộc engine cụ thể (DIP).
- **Game cung cấp adapter** map port → API thật của mình. Bê kit sang project khác = viết 1 adapter + 1 installer.
- **SOLID**: mỗi port một concern (ISP); thêm cheat = thêm module/port, không sửa panel (OCP);
  module phụ thuộc interface qua `Bind(CheatServices)` (DIP); panel chỉ lo vòng đời module (SRP).

---

## 2. Cấu trúc thư mục

```
CoreModules/CheatKitSystem/
  README.md
  Runtime/
    CoreModules.CheatKitSystem.Runtime.asmdef   (autoReferenced, defineConstraints: ["CHEAT_ENABLED"])
    Config/
      CoreModules.CheatKitSystem.Config.asmdef   (KHÔNG gate → Editor đọc/sửa kể cả khi cheat tắt)
      CheatKitConfig.cs       ScriptableObject: reveal taps/window/region, startVisible, bật/tắt module.
    Core/
      UITestPanel.cs          Quản lý vòng đời + ẩn/hiện + cử chỉ mở. Singleton.
      UITestModuleBase.cs     Base MonoBehaviour cho module (Template Method).
      IUITestModule.cs        Interface module.
      ICheatBindable.cs       Module nhận service: Bind(CheatServices).
      ICheatModuleUi.cs       Module tự dựng UI mặc định: BuildDefaultUI(section).
      CheatFlags.cs           Cờ tĩnh game tự đọc (Invincible / InfiniteMoves / …).
      CheatUi.cs              Factory UGUI (bọc DefaultControls + LayoutElement touch-target).
      CheatPanelBuilder.cs    Dựng panel runtime (no-prefab).
      CheatLog.cs             Logger nội bộ (Conditional → 0 cost release).
      IPanelRevealGesture.cs  Strategy mở panel.
      ScreenTapReveal.cs      Mặc định: N tap ở vùng top-center.
    Ports/
      ILevelCheatService.cs   Count / CurrentIndex / NameOf / Load / ReloadCurrent.
      IFlowCheatService.cs    Phase / CanForceWin / ForceWin / ForceLose / TriggerNoMoves.
      IProgressCheatService.cs Coins / UnlockAll / SetProgress / AddCoins / ClearSave.
      IScreenshotCheat.cs     IsAvailable / Capture.
      CheatServices.cs        DTO gói các port (field null = game không hỗ trợ).
    Modules/
      LevelSelectModule.cs    Điều hướng level.
      GameControlModule.cs    Win/Lose/NoMoves + progress/currency + time-scale + cheat flags.
      PerformanceStatsModule.cs  FPS / memory / VSync / GC.
    Screenshot/
      ScreenshotCaptureService.cs  Chụp 1 vùng world-space ra PNG (generic).
  Editor/
    CoreModules.CheatKitSystem.Editor.asmdef   (Editor-only, KHÔNG gate → toggle dùng được khi cheat tắt)
    CheatDefineSymbols.cs   Bật/tắt define CHEAT_ENABLED theo platform.
    CheatKitSettingsWindow.cs  Cửa sổ CoreModules ▸ CheatKit ▸ Settings (toggle + sửa config).
  Examples/                  (gate CHEAT_ENABLED, autoReferenced:false → 0 byte release, không lẫn vào game)
    Shared/    GenreKind · MockGameModel (thuần C#) · ExampleCheatBootstrap (composition root)
    Puzzle/    PuzzleCheatBridge          Casual/  CasualCheatBridge
    Rpg/       IRpgCheatService + RpgMockGame + RpgCheatBridge + RpgCheatModule      (OCP demo)
    Action/    IActionCheatService + ActionMockGame + ActionCheatBridge + ActionCheatModule
    Editor/    CheatExampleSceneMenu  → menu CoreModules ▸ CheatKit ▸ Examples ▸ <genre> (tạo scene test)
```

> **Examples**: 4 ví dụ chạy-ngay cho Puzzle/Casual/RPG/Action — xem `Examples/README.md` & §9–§10.

---

## 3. Cách dùng (người chơi/QA, trong build có cheat)

| Hành động | Thao tác |
|---|---|
| **MỞ panel** | Chạm **5 lần liên tiếp** ở **vùng giữa-trên** màn hình (trong ~1.5s). |
| **ĐÓNG panel** | Bấm nút **🐞** góc phải-trên (chỉ hiện khi panel đang mở). |
| Mở/đóng nhanh (Editor/WebGL) | Phím **F1**. |

Mở panel KHÔNG chặn input phần còn lại của màn hình (vẫn thao tác game được).

---

## 4. Tích hợp vào một project mới (3 bước)

### B1. Bật package
Thêm define `CHEAT_ENABLED` cho cấu hình dev/test (xem §6). Khi đó asmdef CheatKit được biên dịch
và Assembly-CSharp tự tham chiếu (autoReferenced).

### B2. Viết adapter implement các port cần dùng
```csharp
#if CHEAT_ENABLED
using CoreModules.CheatKit.Ports;

public sealed class MyGameCheatBridge : ILevelCheatService, IFlowCheatService, IProgressCheatService
{
    // map sang API thật của game (level loader, FSM, save, currency…)
    public int Count => _levels.Count;
    public bool IsReady => _levels.Current != null;
    public int CurrentIndex => _levels.CurrentIndex;
    public string NameOf(int i) => "";
    public void Load(int i) => _flow.GoToLevel(i);
    public void ReloadCurrent() => _flow.Reload();

    public CheatGamePhase Phase => /* map state máy của bạn */ CheatGamePhase.Playing;
    public bool CanForceWin => Phase == CheatGamePhase.Playing;
    public void ForceWin() { /* hoàn thành điều kiện thắng thật */ }
    public void ForceLose() { /* … */ }
    public void TriggerNoMoves() { /* … */ }

    public int Coins => _wallet.Coins;
    public void UnlockAll() => _progress.SetMax();
    public void SetProgress(int i) => _progress.Set(i);
    public void AddCoins(int n) => _wallet.Add(n);
    public void ClearSave() => _save.Clear(CurrentIndex);
}
#endif
```
Field nào không áp dụng (vd không có tiền tệ) → trả `-1`/no-op; module tự ẩn/disable.

### B3. Dựng & bind panel lúc khởi tạo
```csharp
#if CHEAT_ENABLED
using CoreModules.CheatKit;
using CoreModules.CheatKit.Ports;

var bridge   = new MyGameCheatBridge(/* deps */);
var services = new CheatServices(level: bridge, flow: bridge, progress: bridge);

var panel = UnityEngine.Object.FindObjectOfType<UITestPanel>(true) ?? CheatPanelBuilder.Build();
foreach (var b in panel.GetComponentsInChildren<ICheatBindable>(true))
    b.Bind(services);
#endif
```
Trong project dùng DI (như WordJigsort) → bọc đoạn trên trong một `CheatInstaller : IInstaller`
và thêm vào pipeline dưới `#if CHEAT_ENABLED`.

---

## 5. Mở rộng

### Thêm module mới
```csharp
public sealed class MyModule : UITestModuleBase, ICheatBindable, ICheatModuleUi
{
    protected override void OnInitialize() { _moduleName = "My Module"; /* wire listeners */ }
    public void Bind(CheatServices s) { /* cache port + refresh */ }
    public void BuildDefaultUI(RectTransform section)
    {
        CheatUi.Label(section, "▼ MY MODULE", 26);
        CheatUi.Button(section, "Do Thing", Color.cyan).onClick.AddListener(/*…*/);
    }
}
```
Gắn vào panel **đã dựng** mà KHÔNG sửa builder/panel (OCP) qua seam public:
```csharp
var panel = UITestPanel.Instance ?? CheatPanelBuilder.Build();
CheatPanelBuilder.AddModule<MyModule>(panel);   // dựng section + register + initialize
// rồi Bind như thường: foreach ICheatBindable → Bind(services)
```
Module chạy được cả 2 đường: prefab (wire field qua Inspector) hoặc runtime (`BuildDefaultUI`).
Cheat **đặc thù genre** (RPG/Action…) dùng đúng seam này — xem §10.

### Đổi cách mở panel
Implement `IPanelRevealGesture` rồi `uiPanel.SetRevealGesture(myGesture)`.
Mặc định `ScreenTapReveal(taps, window, region)` — chỉnh số tap / vùng / thời gian.

---

## 6. Build & strip theo platform

CheatKit gate bởi define **`CHEAT_ENABLED`**:

- **Không set** (Marketing/Production) → asmdef bị loại, mọi `#if CHEAT_ENABLED` rỗng → **0 byte cheat**.
- **Set** (Development/Testing) → cheat hoạt động.

### Cách NHANH NHẤT (Editor) — `CoreModules ▸ CheatKit`
- **`CoreModules ▸ CheatKit ▸ Enabled (Active Platform)`** — menu có dấu check: bấm để bật/tắt `CHEAT_ENABLED`
  cho platform đang active (Unity tự recompile).
- **`CoreModules ▸ CheatKit ▸ Settings`** — cửa sổ: BẬT/TẮT (1 hoặc mọi platform) + tạo/chỉnh `CheatKitConfig`
  (số tap mở, vùng tap, startVisible, bật/tắt từng module).
- Tool toggle nằm trong asmdef Editor **không gate** nên luôn dùng được, kể cả khi cheat đang tắt.

### Trong project này (BuildPipelineSystem)
`CHEAT_ENABLED` đã gắn vào template **🧪 Testing** (`BuildSystem/BuildTemplates.json`).
- Build cheat: `Tools → Build Configuration` → Apply template **Testing** → Build.
- Test trong Editor: Apply template Testing → tab *Scripting Define Symbols* → **Sync to PlayerSettings**
  (Editor recompile với `CHEAT_ENABLED`) → Play.
- Marketing/Production: template không có define → cheat tự biến mất.

### Project khác
Thêm `CHEAT_ENABLED` vào *Scripting Define Symbols* (Player Settings) cho platform/cấu hình dev,
hoặc cơ chế build tương đương. KHÔNG thêm cho bản release.

---

## 7. Tối ưu & tương thích

- **Mobile**: touch-target ≥ 84px; Update 0-alloc (reveal chỉ so sánh số + bool); panel ẩn không
  tốn raycast vùng trống; `unscaledTime` nên reveal chạy cả khi pause.
- **Editor / WebGL**: reveal fallback sang chuột; F1 toggle; dùng legacy `UnityEngine.Input`
  (yêu cầu Active Input Handling = *Input Manager* hoặc *Both*).
- **Logger**: `CheatLog.Info/Warning` strip ngoài Editor/Development Build (Conditional); `Error` luôn giữ.
- **UI**: dựng qua `DefaultControls` (hierarchy widget chuẩn của Unity) → ít rủi ro sai layout.

---

## 8. Phụ thuộc

- Chỉ `UnityEngine` + `UnityEngine.UI` (UGUI). Không phụ thuộc package/game khác.
- `ScreenshotCaptureService` dùng `AsyncGPUReadback` (fallback `ReadPixels` cho thiết bị cũ).

---

## 9. Genre Cookbook — map cheat theo từng dòng game

3 port chuẩn cố ý **trừu tượng** nên một port mang ngữ nghĩa khác nhau tuỳ genre. Đừng ép mọi game
vào một khuôn — hãy map theo bảng dưới (ví dụ chạy được nằm ở `Examples/`, chọn qua menu
`CoreModules ▸ CheatKit ▸ Examples ▸ <genre>`).

| Cheat (port) | **Puzzle** (WordJigsort) | **Casual** (endless/score) | **RPG** | **Action** |
|---|---|---|---|---|
| `ILevel.Count` | tổng số level | **0** (vô tận → ẩn `x/N`) | số chapter | số checkpoint |
| `ILevel.Load/Reload` | nạp level | nhảy stage | nạp chapter | tới checkpoint |
| `IFlow.ForceWin` | ráp đủ tranh → win-flow | đạt target score | clear boss | clear wave |
| `IFlow.ForceLose` | publish LevelLost | hết lượt | party wipe | player death |
| `IFlow.TriggerNoMoves` | test rescue | reshuffle board | — (no-op) | — (no-op) |
| `IProgress.Coins` | coin | gem | **gold** | **−1** (không có → ẩn) |
| `IProgress.UnlockAll/SetProgress` | mở khoá level | mở stage | mở chapter | mở checkpoint |
| Cheat flags (`CheatFlags.*`) | Invincible/InfiniteMoves/Undo | InfiniteMoves | dùng custom port | dùng custom port |
| **Custom port** | — | — | `IRpgCheatService` | `IActionCheatService` |

**Nguyên tắc chọn port (ISP):** chỉ implement/bind port game **thực sự dùng**. Không có tiền tệ →
`Coins => -1` *hoặc* truyền `progress: null` (panel tự ẩn coin/progress). Không có thua → `ForceLose`
no-op. Cờ nào không áp dụng → tắt module tương ứng trong `CheatKitConfig`.

---

## 10. Thêm cheat ĐẶC THÙ genre (custom port + module) — OCP

Cheat riêng của một dòng game (RPG: God Mode/XP/Gold · Action: Ammo/Spawn/Slow-Mo) **không** thuộc kit.
Mẫu 4 bước (xem code đầy đủ trong `Examples/Rpg` & `Examples/Action`):

**B1 — Custom port** (đặt trong thư mục genre của bạn, KHÔNG sửa kit):
```csharp
public interface IRpgCheatService
{
    bool GodMode { get; set; }
    int PartyLevel { get; } int Xp { get; } int Gold { get; }
    void AddXp(int n); void LevelUp(); void AddGold(int n);
}
```

**B2 — Bridge** implement port chuẩn cần dùng **+** custom port (cùng nguồn state):
```csharp
public sealed class RpgCheatBridge : ILevelCheatService, IFlowCheatService,
                                     IProgressCheatService, IRpgCheatService { /* map vào game */ }
```

**B3 — Module genre** lấy custom port qua `CheatServices.Get<T>()`:
```csharp
public sealed class RpgCheatModule : UITestModuleBase, ICheatBindable, ICheatModuleUi
{
    private IRpgCheatService _rpg;
    public void BuildDefaultUI(RectTransform s) { /* Toggle God Mode, +XP, Level Up, +Gold */ }
    public void Bind(CheatServices services) => _rpg = services?.Get<IRpgCheatService>();
    public override void OnUpdate() { /* cập nhật text CHỈ khi giá trị đổi → 0 alloc */ }
}
```

**B4 — Wire ở composition root** — append module rồi đẩy custom port vào `extras`:
```csharp
var bridge = new RpgCheatBridge(game);
var panel  = UITestPanel.Instance ?? CheatPanelBuilder.Build();
CheatPanelBuilder.AddModule<RpgCheatModule>(panel);                 // OCP: kit KHÔNG đổi
var services = new CheatServices(level: bridge, flow: bridge,
                                 progress: bridge, bridge /* → extras: IRpgCheatService */);
foreach (var b in panel.GetComponentsInChildren<ICheatBindable>(true)) b.Bind(services);
```

`CheatServices.Get<T>()` tìm trong `extras` trước, rồi tới 3 port chuẩn — zero-reflection (`is`).
Action tương tự với `IActionCheatService` và truyền `progress: null` (ISP). Bấm menu Examples để xem
cả hai chạy thật.
