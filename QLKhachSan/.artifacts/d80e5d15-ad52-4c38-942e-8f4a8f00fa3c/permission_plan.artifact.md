# Kế hoạch Triển khai Trang "Phân quyền Nhân viên" - Luxury Hotel (Giai đoạn A)

Báo cáo khảo sát hệ thống hiện tại và đề xuất kiến trúc thiết kế tính năng **Phân quyền Nhân viên (Staff Permission Management)** cho khu vực Admin.

---

## 1. Khảo sát Hệ thống Hiện tại
- **Model Người dùng (`User`)**: Đang lưu trữ trong bảng `Users` với các thuộc tính `UserId`, `Username`, `Email`, `FullName`, `PasswordHash`, `Role`, `IsActive`.
- **Phân quyền hiện tại**: Hệ thống phân biệt 3 vai trò (`Role`) chính: `Admin`, `Staff`, và `Customer`.
  - `Admin`: Được bảo vệ bởi `AdminAuthorizeAttribute`.
  - `Staff`: Kiểm tra qua phương thức `IsStaff()` trong `StaffController`.
  - `Customer`: Khách hàng đăng ký/đăng nhập thông thường.
- **Vấn đề cần giải quyết**: Hiện tại tài khoản `Staff` có quyền chung chung đối với toàn bộ các chức năng nhân viên. Cần xây dựng trang quản trị phân quyền chi tiết cho từng nhân viên (`Staff`).

---

## 2. Danh sách Chức năng (Permissions) có thể Cấp cho Nhân viên

Các quyền được chia theo nhóm mô-đun thực tế có trong dự án:

| Mã quyền (`PermissionCode`) | Tên hiển thị quyền | Mô tả chức năng |
| :--- | :--- | :--- |
| `rooms.view` | Xem danh sách phòng | Xem thông tin, danh sách và trạng thái phòng |
| `rooms.manage` | Quản lý phòng | Thêm, sửa, đổi trạng thái/bảo trì phòng |
| `bookings.view` | Xem danh sách đặt phòng | Xem danh sách và lịch sử đặt phòng của khách |
| `bookings.approve` | Xác nhận / Hủy đặt phòng | Duyệt đơn chờ xác nhận hoặc hủy đặt phòng |
| `bookings.checkin` | Nhận phòng (Check-in) | Thực hiện thủ tục nhận phòng cho khách |
| `bookings.checkout` | Trả phòng (Check-out) | Thực hiện thủ tục trả phòng và thanh toán |
| `customers.view` | Xem khách hàng | Xem danh sách và chi tiết thông tin khách hàng |
| `services.manage` | Quản lý dịch vụ | Thêm, sửa, bật/tắt dịch vụ khách sạn |
| `restaurant.manage` | Quản lý nhà hàng & món ăn | Thêm, sửa, bật/tắt món ăn và danh mục |
| `restaurant.orders` | Xem & xử lý đơn món ăn | Theo dõi, cập nhật trạng thái đơn gọi món F&B |
| `payments.manage` | Quản lý thanh toán & hóa đơn | Xem lịch sử thanh toán và xác nhận thanh toán |
| `revenue.view` | Xem báo cáo doanh thu | Xem biểu đồ, báo cáo và xuất file CSV doanh thu |

---

## 3. Đề xuất Cấu trúc Dữ liệu (Database Schema)

Để lưu trữ quyền lâu dài trong SQL Server, ta sẽ bổ sung 2 bảng (Code First / Entity Framework):

### Bảng 1: `Permissions` (Định nghĩa danh mục quyền)
```sql
CREATE TABLE dbo.Permissions (
    PermissionId INT IDENTITY(1,1) PRIMARY KEY,
    PermissionCode NVARCHAR(100) NOT NULL UNIQUE, -- VD: rooms.manage
    PermissionName NVARCHAR(200) NOT NULL,        -- VD: Quản lý phòng
    Category NVARCHAR(100) NOT NULL               -- VD: Phòng, Đặt phòng, Nhà hàng...
);
```

### Bảng 2: `UserPermissions` (Lưu quyền được cấp cho từng nhân viên)
```sql
CREATE TABLE dbo.UserPermissions (
    UserPermissionId INT IDENTITY(1,1) PRIMARY KEY,
    UserId INT NOT NULL FOREIGN KEY REFERENCES dbo.Users(UserId) ON DELETE CASCADE,
    PermissionCode NVARCHAR(100) NOT NULL,
    GrantedAt DATETIME NOT NULL DEFAULT GETDATE()
);
```

---

## 4. Cơ chế Kiểm tra Quyền (Authorization Flow)
1. **Lưu quyền vào Session khi đăng nhập**: Khi nhân viên đăng nhập thành công, hệ thống load danh sách `PermissionCode` của nhân viên đó và lưu vào `Session["Permissions"]` (dạng `List<string>`).
2. **Action Filter / Helper Kiểm tra**: Tạo một Action Filter tùy chỉnh `[StaffPermissionAuthorize("rooms.manage")]` hoặc phương thức kiểm tra `HasPermission(string permissionCode)` trong Base Controller / Staff Controller.
3. **Ẩn/Hiện Menu trên Giao diện Admin/Staff**: Dựa vào `Session["Permissions"]` để hiển thị hoặc ẩn các nút bấm và menu tương ứng.
4. **Bảo vệ Endpoint & AJAX**: Mọi Controller Action / Endpoint AJAX đều kiểm tra quyền trước khi thực thi, ngăn chặn tuyệt đối việc vượt quyền bằng cách gõ URL trực tiếp hoặc gọi AJAX.

---

## 5. Giao diện Trang "PHÂN QUYỀN NHÂN VIÊN" (Admin)
- Đặt trong giao diện Admin (`AdminController` / Views/Admin).
- Gồm 2 cột:
  - **Cột trái**: Danh sách nhân viên (có ô tìm kiếm theo tên/email).
  - **Cột phải**: Bảng checkbox các nhóm quyền được phân loại theo module, có nút **"LƯU PHÂN QUYỀN"** và **"KHÔI PHỤC"**.
- Tích hợp hệ thống thông báo **Luxury Hotel** hiện có khi lưu thành công.

---
*Kế hoạch trên đang ở **Giai đoạn A (Khảo sát & Lên kế hoạch)**. Toàn bộ mã nguồn và cấu trúc cơ sở dữ liệu sẽ chỉ được triển khai (Giai đoạn B) sau khi nhận được sự đồng ý phê duyệt từ bạn.*
