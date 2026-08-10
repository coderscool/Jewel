# JewelPainter — Scripts

## Quy tắc kiến trúc

Mọi thay đổi phải tuân theo skill `unity-architecture` (chọn thư mục, namespace) và
`unity-clean-code` (viết nội dung class).
Code chạm Unity API: `unity-runtime-safety`. Tối ưu hiệu năng: `unity-performance`.
Trước khi commit: `unity-code-review`.

Chiều phụ thuộc — **không bao giờ ngược lại**:

```
Bootstrap → UI → Gameplay → Core
```

- `Core` không biết gì về Gameplay/UI/Bootstrap.
- `Gameplay` không `using` UI — chỉ định nghĩa interface (`ILevelService`) cho UI dùng.
- `Bootstrap` là ngoại lệ duy nhất: composition root, biết mọi tầng.

## Thông tin riêng của project

- Namespace gốc: `JewelPainter.*` (thư mục = namespace, 1:1)
- DI: **VContainer** — `GameLifetimeScope` + `GameEntryPoint : IStartable`
- Message: event C# trực tiếp (`event Action<int>` trên manager). Struct message đã khai sẵn ở `Core/Messages/GameEvents.cs`, chuyển sang MessagePipe khi cần nhiều người nghe.
- Tween: **DOTween**, chỉ dùng phần core (`Transform.DOMove`, `DOScale`) ở `Gameplay/Board/JewelFlyEffect.cs`. Không đụng module UI/Sprite để khỏi phải khai thêm assembly. Mọi tween phải `Kill()` trong `OnDestroy` **và** trước khi trả object về pool — object tái dùng mang theo tween cũ sẽ bị kéo về vị trí lần trước.
- Assembly: `JewelPainter.Core` / `.Gameplay` / `.UI` / `.Bootstrap`

## Quy ước đã cắm sẵn trong khung code

| Luật | File mẫu |
|---|---|
| Domain thuần C#, không MonoBehaviour/ScriptableObject, test được ở EditMode | `Gameplay/Domain/PlayerProgress.cs` |
| Domain **được** dùng struct giá trị của Unity (`Color32`, `Vector2Int`) — chúng không cần scene | `Gameplay/Domain/PaletteMatcher.cs` |
| Domain không đụng `PlayerPrefs`, đi qua abstraction | `Core/Persistence/ISaveService.cs` |
| `Dictionary` không serialize được → `List<Entry>` + build ở `Awake` | `UI/Data/PopupConfig.cs`, `Core/Services/SoundConfig.cs` |
| Popup tạo 1 lần, bật/tắt để tái dùng — không `Instantiate`/`Destroy` mỗi lần | `UI/Managers/PopupManager.cs` |
| Prefab cần `[Inject]` phải tạo qua `_resolver.Instantiate` | `UI/Managers/PopupManager.cs` |
| Huỷ đăng ký event trong `OnDestroy` | `UI/Views/HudView.cs` |
| Chỉ `SetText` khi giá trị đổi (tránh rác GC mỗi frame) | `UI/Views/HudView.cs` |
| Cha đưa phụ thuộc cho con qua `Init()`, con không tự đi tìm | `Bootstrap/GameEntryPoint.cs` |
| Cấu hình toàn ứng dụng chạy qua `RuntimeInitializeOnLoadMethod`, không cần object trong scene | `Bootstrap/ApplicationSettings.cs` |

## Nợ kiến trúc đã biết (đừng làm nặng thêm)

| Vấn đề | Vị trí |
|---|---|
| (chưa có — project mới scaffold) | |

## Nhắc riêng cho Unity

- **Di chuyển / đổi tên file `.cs`**: làm trong cửa sổ Project của Unity, không dùng terminal hay File Explorer — làm ngoài sẽ sinh GUID `.meta` mới và **mất toàn bộ reference trong prefab/scene**.
- **Tạo file `.cs` mới** bằng công cụ ngoài thì an toàn — Unity tự sinh `.meta` khi import.
- Đổi tên class hoặc field đã `[SerializeField]` sẽ **mất giá trị đã gán**. Cần giữ thì dùng `[FormerlySerializedAs]`.
- Commit **cả file `.meta`**.
- Sửa asmdef báo lỗi thì **sửa chỗ vi phạm chiều phụ thuộc**, đừng thêm reference để dập lỗi.

## Cấm

- Thư mục sọt rác: `Common/`, `Utils/`, `Misc/`, `Helpers/`, `Shared/`
- Nhóm theo kiểu C#: `Enums/`, `Interfaces/` toàn cục, `Scriptables/`
- `static instance` (project đã có DI — hai cơ chế đối nghịch)
- `FindObjectOfType` ngoài Bootstrap/Editor/cheat
- `public` field — dùng `[SerializeField] private` + property chỉ đọc
- File `.cs` nằm thẳng ở gốc `Scripts/`
