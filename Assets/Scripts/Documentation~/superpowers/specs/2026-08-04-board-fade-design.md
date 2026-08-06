# Mờ lớp màu theo mức zoom — thiết kế

Ngày: 2026-08-04
Trạng thái: đã duyệt

## Mục tiêu

Số nằm **dưới** lớp màu. Khi người chơi phóng to, lớp màu mờ dần để lộ số; phóng
quá một mốc thì lớp màu trong suốt hẳn, chỉ còn số.

## Quyết định đã chốt

| Quyết định | Lựa chọn | Lý do |
|---|---|---|
| Xếp lớp | `Order in Layer`, không phải trục z | `SpriteRenderer` và `TextMeshPro` là hai loại renderer khác nhau; xếp theo z giữa chúng cho kết quả khó đoán |
| Ai giữ logic | Component mới `BoardColorFade` | `BoardCamera` không nên với vào renderer; `BoardView` không nên biết camera |
| Mốc so sánh | Mức zoom **lúc mới vào màn**, không phải số tuyệt đối | Lưới 16x16 và 32x32 cho cảm giác như nhau mà không phải chỉnh lại |
| Ngưỡng để đâu | `[SerializeField]`, không `const` | Đây là chuyện cảm giác, phải kéo thử lúc chạy mới biết đẹp |
| Đường cong alpha | Tuyến tính | Đủ dùng; phức tạp hơn thì thêm `AnimationCurve` sau |

## Xếp lớp

- Prefab `CellNumber`: `Order in Layer` = **-1**
- `BoardView` (`SpriteRenderer`): `Order in Layer` = **0**
- `Numbers`: vị trí z chuyển từ `-1` về **0**

Bảng vẽ sau nên đè lên số. Alpha giảm thì số hiện dần qua lớp màu.

## Thang alpha

Lúc vào màn, ghi lại `orthographicSize` làm mốc — gọi là `baseSize`. Mọi thứ đo bằng
tỉ lệ `currentSize / baseSize`: bằng 1 lúc mới vào, nhỏ dần khi phóng to.

Alpha nội suy tuyến tính giữa hai mốc: `1` tại `_opaqueRatio`, `0` tại
`_transparentRatio`, kẹp về `0..1` ngoài khoảng đó.

**Thứ tự hai mốc quyết định chiều mờ** — không có cờ bật tắt, không rẽ nhánh:

| Cấu hình | Hiệu ứng |
|---|---|
| `_opaqueRatio` 1.0, `_transparentRatio` 0.4 | Đục lúc vào, mờ dần khi phóng to |
| `_opaqueRatio` 0.4, `_transparentRatio` 1.0 | Đục khi phóng sát, mờ dần khi kéo ra xa |

`Mathf.InverseLerp` chạy đúng cả khi mốc đầu lớn hơn mốc sau và tự kẹp kết quả, nên
một công thức phục vụ được cả hai chiều. Hai mốc trùng nhau thì giữ alpha 1.

**Ràng buộc từ `BoardCamera`:** mức zoom xa nhất bị kẹp đúng bằng mức lúc vào màn,
nên tỉ lệ chỉ chạy trong khoảng `minSize/baseSize` đến `1.0`, không bao giờ vượt 1.
Đặt mốc lớn hơn 1 sẽ không bao giờ chạm tới.

**Lấy `baseSize` lúc nào:** ở `LateUpdate` đầu tiên sau mỗi lần bảng dựng lại, chứ
không lấy ngay trong handler của `OnBoardRebuilt`. Lý do: `BoardCamera` cũng nghe sự
kiện đó và đặt lại mức zoom; lấy trong handler thì kết quả phụ thuộc thứ tự đăng ký
event, mà thứ tự đó không có gì bảo đảm.

## Thay đổi chạm vào code đã có

1. `BoardLayout` — thêm hàm tĩnh `CellScreenPixels(float screenHeight, float orthographicSize)`,
   dùng cho ngưỡng đọc được của `BoardNumberLayer`.
2. `BoardNumberLayer` — bỏ hàm riêng `CellScreenPixels()`, gọi hàm tĩnh trên.
   Đổi hằng `MinCellScreenPixels` thành `[SerializeField] _minCellScreenPixels = 14`.
   Đây là ngưỡng **đọc được**, độc lập với hiệu ứng mờ: số có thể đã sinh ra nhưng
   còn khuất dưới lớp màu chưa mờ. Hai điều kiện khác nhau, không cần khớp.
3. `BoardColorFade` — file mới ở `Gameplay/Board/`.

**Không** đụng `GameLifetimeScope` hay `GameEntryPoint`: `BoardColorFade` chỉ cần
`Camera` và `SpriteRenderer`, cả hai gán trong Inspector, không qua DI.

## Xử lý lỗi

| Tình huống | Cách xử lý |
|---|---|
| Chưa gán `Camera` hoặc `SpriteRenderer` | Không làm gì, không ném lỗi |
| Chưa gán `BoardView` | Vẫn chạy, chỉ là không lấy lại mốc khi đổi màn |
| Hai mốc tỉ lệ đặt ngược nhau | Giữ alpha 1 |
| `baseSize` chưa lấy được (bằng 0 hoặc âm) | Giữ alpha 1 |
| `orthographicSize` bằng 0 | `CellScreenPixels` trả 0 thay vì chia cho không |

## Cách kiểm thử

`BoardLayout.CellScreenPixels` thuần C#, test EditMode được: màn 1000 pixel với
`orthographicSize` 10 phải ra 50 pixel mỗi ô; `orthographicSize` 0 phải trả 0 chứ
không ném.

Phần còn lại kiểm bằng mắt:

- vừa vào màn: ảnh đục hoàn toàn, không thấy số
- phóng to dần: ảnh mờ dần, số hiện ra qua lớp màu
- phóng tới khoảng 2.5 lần: ảnh mất hẳn, chỉ còn số
- thu nhỏ lại: ảnh đặc dần trở lại
- đổi sang màn khác kích thước lưới khác: mốc lấy lại, cảm giác vẫn như cũ
