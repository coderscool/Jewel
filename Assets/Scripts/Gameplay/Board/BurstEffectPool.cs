using UnityEngine;

namespace JewelPainter.Gameplay.Board
{
    /// Kho hiệu ứng loé dùng lại: ai cần loé một phát ở toạ độ nào thì gọi Play, không
    /// phải tự lo tạo, thu hồi hay đếm xem hiệu ứng chạy xong chưa.
    ///
    /// Đây là lớp cha trừu tượng, không phải một kho cụ thể. JewelLandSparkle và
    /// ColorCompleteSparkle chỉ biết tới bốn thành viên dưới đây, nên đổi từ Particle
    /// System sang Spine hay ngược lại là đổi component gán vào ô Burst Pool trong
    /// Inspector — không sửa một dòng nào của hai lớp đó, và cũng không mất cái cũ.
    ///
    /// Vì sao hiệu ứng KHÔNG được gắn thẳng vào prefab viên ngọc: JewelLayer thu ngọc về
    /// kho khi ô trôi ra ngoài khung hình rồi lấy ra dùng lại khi ô trở vào. Hiệu ứng nằm
    /// trong đó, tự chạy khi được bật, sẽ chạy lại mỗi lần viên được bật — kéo camera qua
    /// lại là cả bảng loé sáng như mới tô. Hiệu ứng có vòng đời riêng thì một sự kiện mới
    /// phát đúng một lần.
    public abstract class BurstEffectPool : MonoBehaviour
    {
        /// Kho đã có thứ để dựng ra chưa. False thì Play luôn thất bại, và bên gọi nên
        /// im lặng bỏ qua thay vì xếp hàng chờ mãi.
        public abstract bool HasPrefab { get; }

        /// Số hiệu ứng đang chạy dở ngay lúc này.
        ///
        /// Có mặt ở đây để bên ngoài trả lời được câu "loé xong chưa" — LevelFlowController
        /// phải đợi dải loé của màu cuối tắt hẳn rồi mới mở màn ăn mừng. Hàng chờ rỗng chưa
        /// đủ để kết luận: ô cuối cùng vừa được bắn ra vẫn còn sáng thêm gần một giây nữa.
        ///
        /// Đếm theo KHO chứ không theo bên gọi, nên hai hệ thống dùng chung một kho sẽ đếm
        /// lẫn của nhau. Ai cần hỏi câu trên thì phải có kho riêng.
        public abstract int ActiveCount { get; }

        /// false khi đã chạm trần đồng thời, hoặc thiếu prefab.
        ///
        /// Trả về kết quả thay vì im lặng bỏ qua: bên gọi cần biết để XẾP LẠI HÀNG. Nuốt
        /// lặng lẽ nghĩa là hiệu ứng mất hẳn, mà thứ duy nhất người chơi thấy là "sao nó
        /// không loé hết".
        public abstract bool Play(Vector2 world);

        /// Dựng sẵn lúc vào màn. Instantiate cả trăm hiệu ứng đúng vào frame cần dùng là
        /// cách chắc chắn nhất để khoảnh khắc đáng lẽ đã mắt biến thành cú khựng.
        public abstract void Prewarm();

        /// Thu mọi hiệu ứng đang chạy về kho ngay lập tức. Dùng khi dựng lại bảng.
        public abstract void ReleaseAll();
    }
}
