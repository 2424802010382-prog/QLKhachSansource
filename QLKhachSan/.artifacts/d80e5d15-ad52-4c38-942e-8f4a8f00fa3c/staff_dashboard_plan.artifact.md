# Kế hoạch Nâng cấp Staff Dashboard & Tích hợp Phân quyền (Giai đoạn A)

Bài báo cáo khảo sát hiện trạng và kế hoạch kiến trúc nâng cấp **Staff Dashboard** kết nối trực tiếp với hệ thống **Phân quyền Nhân viên (Permissions System)**.

---

## 1. Khảo sát Hiện trạng Hệ thống Nhân viên (`StaffController` & Views)
- **Controller hiện có**: `StaffController.cs` đã được xây dựng và hỗ trợ các chức năng:
  - `Index()`: Dashboard tổng quan công việc của nhân viên (`Views/Staff/Index.cshtml`).
  - `Bookings()`: Quản lý danh sách đặt phòng (`Views/Staff/Bookings.cshtml`).
  - `TodayCheckIns()`: Danh sách khách nhận phòng trong ngày (`Views/Staff/TodayCheckIns.cshtml`).
  - `CreateBooking()`: Nhân viên tạo đặt phòng trực tiếp tại quầy (`Views/Staff/CreateBooking.cshtml`).
  - `FoodOrders()`: Quản lý đơn gọi món ăn F&B (`Views/Staff/FoodOrders.cshtml`).
- **Hệ thống Phân quyền đã triển khai**:
  - Đã có 12 quyền chuẩn trong cơ sở dữ liệu (`Permissions` & `UserPermissions`).
  - Khi đăng nhập, danh sách mã quyền được cấp cho nhân viên được lưu vào `Session["Permissions"]` (`List<string>`).
- **Nhu cầu nâng cấp**:
  - Tự động điều chỉnh Staff Dashboard (`Views/Staff/Index.cshtml`) để **chỉ hiển thị các thẻ thống kê, menu điều hướng và lối tắt thao tác nhanh** ứng với đúng các quyền nhân viên được Admin cấp.
  - Tích hợp kiểm tra quyền ở phía **Server (Controller Actions)**, ngăn chặn nhân viên thực hiện thao tác khi chưa được cấp quyền (ngay cả khi gõ URL hoặc gọi AJAX trực tiếp).

---

## 2. Kế hoạch Tích hợp Phân quyền vào Staff Dashboard

### a) Hiển thị Động theo Bộ phận & Quyền hạn (Giao diện `Views/Staff/Index.cshtml`)
| Nhóm Quyền (`PermissionCode`) | Thành phần hiển thị trên Staff Dashboard |
| :--- | :--- |
| `bookings.view` / `bookings.approve` | Thẻ thống kê Đơn chờ duyệt, Đặt phòng hôm nay. Lối tắt đến *Quản lý đặt phòng*. |
| `bookings.checkin` / `bookings.checkout` | Thẻ thống kê Khách nhận phòng/trả phòng hôm nay. Lối tắt đến *Danh sách Check-in hôm nay*. |
| `rooms.view` / `rooms.manage` | Sơ đồ Ma trận **40 Phòng vật lý** (theo tầng 1-8 với trạng thái `Trống`, `Đang ở`, `Chờ nhận`, `Bảo trì`). |
| `restaurant.orders` / `restaurant.manage` | Thẻ thống kê Đơn gọi món F&B cần xử lý. Lối tắt đến *Danh sách đơn gọi món*. |
| `payments.manage` / `revenue.view` | Thẻ thống kê Doanh thu thu tiền mặt/chuyển khoản trong ngày. |

### b) Kiểm tra Quyền ở phía Server (`StaffController.cs`)
Tạo helper method `HasStaffPermission(string code)` trong `StaffController.cs`:
- Kiểm tra `Session["Permissions"]` (hoặc truy vấn trực tiếp DB `UserPermissions`).
- Áp dụng kiểm tra phía Server cho các hành động:
  - `CheckInBooking`: Yêu cầu quyền `bookings.checkin` + Đã hoàn tất thanh toán.
  - `CheckOutBooking`: Yêu cầu quyền `bookings.checkout`.
  - `ConfirmBooking` / `CancelBooking`: Yêu cầu quyền `bookings.approve`.
  - `UpdateFoodOrderStatus`: Yêu cầu quyền `restaurant.orders`.
  - `CreateBooking`: Yêu cầu quyền `bookings.approve` / `bookings.view`.

---

## 3. Ma trận 40 Phòng Vật lý trên Staff Dashboard
Sử dụng dữ liệu thực tế từ bảng `PhysicalRooms` (40 phòng: 101-105, 201-205,..., 801-805) để render sơ đồ phòng cho nhân viên:
- 🟩 **Xanh**: Phòng trống (Sẵn sàng đón khách).
- 🟥 **Đỏ**: Khách đang ở (Có nút mở chi tiết hoặc gọi món F&B phòng).
- 🟨 **Vàng**: Chờ nhận phòng hôm nay.
- 🧹 **Xám**: Đang dọn dẹp / Bảo trì.

---

## 4. Thiết kế Giao diện Luxury Hotel
- Sử dụng bảng màu hoàng gia: Nâu đậm `#241c17`, Vàng gold `#d4af37`, Kem `#f8f1e7`.
- Tích hợp hệ thống Modal thông báo Luxury Hotel (`_LuxuryModal.cshtml`).
- Responsive 100% trên máy tính và điện thoại di động.

---
*Kế hoạch trên đang ở **Giai đoạn A (Khảo sát & Lên kế hoạch)**. Toàn bộ mã nguồn sẽ chỉ được cập nhật ở **Giai đoạn B** sau khi nhận được sự đồng ý phê duyệt từ bạn.*
