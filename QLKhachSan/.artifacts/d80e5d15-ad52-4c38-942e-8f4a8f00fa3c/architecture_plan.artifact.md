# Kế hoạch Kiến trúc & Triển khai 40 Phòng Vật lý - Luxury Hotel

Bài phân tích và đề xuất kiến trúc cho giai đoạn **A (Khảo sát & Lên kế hoạch)** nhằm nâng cấp hệ thống từ 8 loại phòng hiện tại thành **40 phòng vật lý có số phòng thực tế** (8 loại × 5 phòng/loại).

---

## 1. Phân tích Hệ thống Hiện tại
- **Bảng `Rooms` hiện tại**: Đang chứa 8 bản ghi đại diện cho **8 Loại phòng** (Standard, Twin Room, Superior, Family Room, Deluxe, Premium, Luxury Balcony Suite, Phòng Tổng Thống), lưu kèm giá, sức chứa, mô tả, hình ảnh và tổng số lượng (`TotalRooms`).
- **Bảng `Bookings` hiện tại**: Đang lưu `RoomId` trỏ trực tiếp đến bảng `Rooms` (tương ứng với loại phòng).
- **Vấn đề**: Các đơn đặt phòng lịch sử đang liên kết trực tiếp với `RoomId` của loại phòng. Nếu ghi đè trực tiếp bảng `Rooms`, các khóa ngoại cũ sẽ bị sai lệch.

---

## 2. Đề xuất Mô hình Dữ liệu An toàn (Không làm mất dữ liệu cũ)

Để đảm bảo **không làm mất dữ liệu cũ, không làm hỏng khóa ngoại của các booking lịch sử**, chúng ta giữ nguyên bảng `Rooms` làm **Bảng Danh mục Loại phòng (Room Type Master)**, đồng thời bổ sung bảng mới **`PhysicalRooms` (Phòng vật lý)**:

### Bảng mới: `PhysicalRooms`
```sql
CREATE TABLE dbo.PhysicalRooms (
    PhysicalRoomId INT IDENTITY(1,1) PRIMARY KEY,
    RoomTypeId INT NOT NULL FOREIGN KEY REFERENCES dbo.Rooms(RoomId),
    RoomNumber NVARCHAR(20) NOT NULL UNIQUE, -- VD: 101, 102, 301...
    Floor INT NOT NULL,                     -- Tầng (1, 2, 3...)
    Status NVARCHAR(50) NOT NULL DEFAULT N'Trống', -- Trống, Đã đặt, Đang ở, Bảo trì
    IsActive BIT NOT NULL DEFAULT 1,
    CreatedAt DATETIME NOT NULL DEFAULT GETDATE()
);
```

### Cải tiến bảng `Bookings`:
Bổ sung cột `PhysicalRoomId INT NULL FOREIGN KEY REFERENCES dbo.PhysicalRooms(PhysicalRoomId)` vào bảng `Bookings` (cho phép NULL để tương thích ngược 100% với các booking cũ).

---

## 3. Kế hoạch Phân bổ 40 Phòng Vật lý (8 Loại × 5 Phòng)

| Loại phòng (`RoomTypeId`) | Tên Loại phòng | Số phòng vật lý cụ thể (5 phòng/loại) | Tầng |
| :--- | :--- | :--- | :--- |
| **1** | Standard | 101, 102, 103, 104, 105 | Tầng 1 |
| **2** | Twin Room | 201, 202, 203, 204, 205 | Tầng 2 |
| **3** | Superior | 301, 302, 303, 304, 305 | Tầng 3 |
| **4** | Family Room | 401, 402, 403, 404, 405 | Tầng 4 |
| **5** | Deluxe | 501, 502, 503, 504, 505 | Tầng 5 |
| **6** | Premium | 601, 602, 603, 604, 605 | Tầng 6 |
| **7** | Luxury Balcony Suite | 701, 702, 703, 704, 705 | Tầng 7 |
| **8** | Phòng Tổng Thống | 801, 802, 803, 804, 805 | Tầng 8 |

---

## 4. Luồng Nghiệp vụ Đặt phòng & Chọn Số phòng Cụ thể

1. **Khách chọn Loại phòng & Thời gian**: Khách chọn loại phòng (VD: Deluxe) và ngày Check-in / Check-out.
2. **Lọc Phòng vật lý Khả dụng**: Hệ thống truy vấn các phòng trong bảng `PhysicalRooms` thuộc loại `Deluxe` mà **không bị trùng lịch** với bất kỳ booking nào khác trong khoảng thời gian đó.
3. **Hiển thị Sơ đồ / Dropdown Chọn Số phòng**: Giao diện hiển thị danh sách số phòng còn trống (VD: `Phòng 501`, `Phòng 502`,...). Khách chọn chính xác số phòng muốn ở.
4. **Xác nhận Đặt phòng**: Lưu `PhysicalRoomId` và `RoomId` vào đơn đặt phòng, đồng thời đánh dấu trạng thái phòng vật lý tương ứng là `Đã đặt` (nếu cần) hoặc quản lý qua kiểm tra lịch trùng lặp.
5. **Nhận phòng (Check-in) & Trả phòng (Check-out)**:
   - Khi Check-in: Trạng thái phòng vật lý chuyển thành `Đang ở`.
   - Khi Check-out: Trạng thái phòng vật lý được giải phóng về `Trống`.

---

## 5. Đề xuất Tác động & An toàn Dữ liệu
- **Dữ liệu cũ**: Không bị xóa, không bị ảnh hưởng, giữ nguyên liên kết `Booking.RoomId`.
- **Dữ liệu mới**: Các booking mới sẽ liên kết với `PhysicalRoomId` cụ thể.
- **Không tự ý chạy migration**: Sơ đồ và kế hoạch này đang ở **Giai đoạn A (Khảo sát & Lên kế hoạch)**. Toàn bộ thao tác cập nhật cơ sở dữ liệu và mã nguồn sẽ chỉ được thực hiện ở **Giai đoạn B** sau khi nhận được sự đồng ý phê duyệt từ bạn.
