# Hướng dẫn Kích hoạt và Vận hành Tính năng Tự động Hủy Đơn Quá Hạn – Luxury Hotel

Tài liệu này hướng dẫn chi tiết cách cấu hình, bảo mật và vận hành tính năng tự động hủy đơn đặt phòng quá hạn (No-show / quá giờ nhận phòng) trên môi trường thực tế (Hosting / Production).

---

## 1. Tổng quan các thành phần trong Source Code

Các thành phần đã được hoàn thiện trực tiếp trong mã nguồn dự án `QLKhachSan`:
- **`Services/OverdueBookingJob.cs`**: Xử lý logic nghiệp vụ, xác thực token máy-với-máy (`SHA-256`) và gọi Stored Procedure, bảo đảm không có fallback kết nối ngầm.
- **`Controllers/OverdueJobController.cs`**: Endpoint nhận yêu cầu `POST /OverdueJob/Run` được bảo vệ bằng bộ lọc bảo mật.
- **`Filters/HotelSecurityFilter.cs`**: Cơ chế bypass AntiForgery dành riêng cho máy-với-máy thông qua việc xác thực header `X-Job-Token`.
- **`Database Scripts (`01_TaoJobTuHuyQuaHan.sql`)`**: Tạo bảng audit `dbo.HotelAutoCancelAudit` và Stored Procedure `dbo.HotelAutoCancelOverdueBookings` sử dụng khóa đồng bộ `sys.sp_getapplock` và transaction.
- **`.github/workflows/overdue-bookings.yml`**: GitHub Actions tự động kích hoạt cron job định kỳ lúc 00:10 giờ Việt Nam mỗi ngày.
- **`AdminController.cs` & `Views/Admin/BookingDetails.cshtml`**: Hiển thị chi tiết lý do tự hủy và nhãn cảnh báo tài chính `Cần xử lý tài chính` cho các đơn đã thanh toán bị hủy.

---

## 2. Các bước cấu hình thủ công trên Hosting & GitHub

### Bước 1: Thực thi Script SQL trên Database Production
Truy cập SQL Server Management Studio (SSMS) trên hosting (SmarterASP.NET), chọn đúng database sản phẩm (`QLKhachSanDB`) và chạy nội dung tệp `01_TaoJobTuHuyQuaHan.sql` để tạo:
1. Bảng `dbo.HotelAutoCancelAudit`
2. Stored Procedure `dbo.HotelAutoCancelOverdueBookings`

### Bước 2: Cấu hình Web.config trên Hosting
Mở tệp `Web.config` trên server hosting và bổ sung (hoặc cập nhật) khóa cấu hình bảo mật `OverdueJobToken` trong `<appSettings>`:
```xml
<appSettings>
    ...
    <add key="OverdueJobToken" value="CHUYEN_DOI_CHUOI_BI_MAT_NGAY_NGUYEN_TO_LON_HON_32_KY_TU" />
</appSettings>
```
*Lưu ý: Không dùng chuỗi mặc định, không đưa token thật lên kho lưu trữ công khai.*

### Bước 3: Thiết lập GitHub Secrets
Tại kho lưu trữ GitHub của dự án, đi tới **Settings > Secrets and variables > Actions**, tạo 2 Secret sau:
1. `OVERDUE_JOB_URL`: URL truy cập endpoint tại domain thực tế của bạn:
   `https://<ten-mien-cua-ban>/OverdueJob/Run`
2. `OVERDUE_JOB_TOKEN`: Giá trị chuỗi bí mật đã cấu hình trong `Web.config` ở Bước 2.

### Bước 4: Kích hoạt Workflow trên GitHub
1. Push toàn bộ mã nguồn lên repository chính.
2. Vào tab **Actions** trên GitHub, chọn workflow **Auto-cancel overdue hotel reservations**.
3. Nhấn **Run workflow** để thử nghiệm lần đầu và kiểm tra phản hồi HTTP 200 kèm JSON thống kê số đơn đã hủy.
