**Rà soát cuối SQL, CSRF, transaction và file bàn giao — 04/11/2026**

Báo cáo này bổ sung báo cáo sửa lỗi trước, cập nhật các phát hiện và kết quả kiểm thử của lượt rà soát cuối. Chưa commit, push, publish hay deploy; không kết nối hoặc chạy SQL trên hosting.

**1. Đối chiếu database thực tế**

Đã kết nối chỉ đọc metadata LocalDB `QLKhachSanDB`, không giả định cột đã có:

| Bảng/cột | Kiểu thực tế | Nullable | Ghi chú |
|---|---|---|---|
| DonDatMon.PayWithBooking | bit | Không | Default ((0)) |
| DonDatMon.BookingServiceId | int | Có | FK đến DatPhongDichVu |
| DonDatMon.PaymentReference | nvarchar(200) | Có | max_length SQL là 400 byte |
| DonDatMon.PaymentEvidence | nvarchar(500) | Có | max_length SQL là 1000 byte |
| DonDatMon.PaidAt | datetime2 | Có | Không điền giả cho lịch sử cũ |
| RoomMaintenances.EndedAt | datetime | Có | Dùng kết thúc sớm và giữ lịch sử |

Các cột này được dùng trực tiếp hoặc qua projection bởi HotelOperations, RestaurantController và AdminController. Chúng đều tồn tại trong LocalDB hiện tại.

Đã đối chiếu: `IX_Bookings_RoomDates`, `IX_RoomMaintenances_RoomDates`, `UX_PhysicalRooms_RoomNumber`, `UX_DonDatMon_PaymentReference` đều tồn tại và không bị disable. Hai index UX là unique; PaymentReference có filter `IS NOT NULL`. FK `FK_DonDatMon_BookingService` tồn tại, được bật và trusted.

Không phát hiện cột/index bổ sung bị thiếu ở LocalDB. Chưa thể khẳng định cấu trúc tương tự trên hosting vì chưa kết nối hosting.

**2. Lỗi đã sửa trong lượt này**

1. **Transaction có thể commit khi action trả lỗi đã bắt.** Trước đây chỉ xét `context.Exception == null`, nên catch rồi trả JSON `success:false` vẫn commit. Nay kiểm tra thêm cờ thất bại, JSON false (anonymous object hoặc dictionary), HTTP lỗi, Response.StatusCode và ModelState. Các nhánh redirect kèm lỗi đánh dấu `FailOperation()`. Đường exception cũng kết thúc transaction còn mở. Kết quả thành công mới commit; không trả thành công giả khi rollback.
2. **Hai lớp CSRF không cùng hỗ trợ JSON header.** Bộ lọc toàn cục đọc `X-CSRF-Token`, trong khi attribute MVC cũ chỉ đọc form. Nay vẫn giữ attribute bảo vệ tường minh, đổi sang `HotelAntiForgeryAttribute` tự kiểm tra token form/header bằng cùng hàm crypto. Attribute vẫn hoạt động độc lập nếu không có global filter; không bỏ bảo vệ CSRF.
3. **Script nâng cấp chỉ kiểm tra sự tồn tại chưa đủ.** Bổ sung kiểm tra kiểu, độ dài, nullability, EndedAt và default PayWithBooking=0. Thiếu default thì bổ sung; schema có kiểu/default không tương thích thì báo lỗi thay vì tự chuyển đổi/xóa dữ liệu cũ.
4. **Tài liệu hỗ trợ chưa hiển thị trong project.** Thêm liên kết `None` cho SQL/tests/docs trong csproj. Các file C#/Razor mới đã có Compile/Content phù hợp từ trước và được kiểm tra lại. Không biến mã kiểm thử/SQL/tài liệu thành nội dung web công khai.

**3. File thay đổi trong lượt rà soát**

- `QLKhachSan/Controllers/HotelController.cs`: quyết định commit/rollback và cờ thất bại.
- `QLKhachSan/Filters/HotelSecurityFilter.cs`: hàm kiểm tra CSRF dùng chung và attribute bảo vệ tường minh.
- `AccountController.cs`, `AdminController.cs`, `StaffController.cs`, `BookingHistoryController.cs`: sử dụng attribute CSRF tương thích; các controller kế thừa HotelController đánh dấu nhánh lỗi trước redirect.
- `RestaurantController.cs`: đánh dấu các nhánh lỗi trong giao dịch do base controller quản lý.
- `QLKhachSan/QLKhachSan.csproj`: liên kết tài liệu nguồn SQL/tests/docs, không Compile test vào website.
- `database/20261104_safe_hotel_fixes.sql`: bổ sung guard schema/default, giữ tính idempotent.
- `tests/HotelRegressionTests.cs`: thêm 8 kịch bản transaction dùng bảng tạm SQL, không sửa bản ghi khách sạn để thử commit/rollback.
- `tests/http-smoke.js`: thêm kiểm tra form/JSON CSRF đăng nhập, đăng ký, đặt món và quyền cho bảo trì/tạo đơn tại quầy.
- `tests/schema-dry-run.sql`: chạy migration trong outer transaction rồi rollback, chỉ cho LocalDB.
- `tests/check-project-files.js`: kiểm tra file thực tế, project inclusion và tài liệu hỗ trợ.
- Báo cáo bổ sung này.

**4. Kết quả thực thi**

| Kiểm tra | Kết quả |
|---|---|
| Build Debug + Razor (`MvcBuildViews=true`) | Thành công |
| Build Release + Razor (`MvcBuildViews=true`) | Thành công |
| C#/EF6 regression | 100 kiểm tra đạt |
| HTTP qua IIS Express | 35 kiểm tra đạt |
| Chrome desktop/mobile và Admin DOM fixture | 17 kiểm tra đạt |
| Parse JavaScript | 28 script trong 21 view đạt |
| Project/file manifest | 181 Compile/Content file tồn tại; toàn bộ C# Controller/Model/Filter có trong project; SQL/tests/docs có liên kết |
| Migration dry-run | Đạt trên LocalDB, rollback outer transaction |
| git diff --check | Đạt |

8 kiểm tra transaction mới: JSON false; dictionary false; exception bị action bắt rồi JSON false; HttpStatusCodeResult lỗi; Response.StatusCode lỗi; redirect có cờ thất bại; exception thoát ra; kết quả thành công. SQL INSERT vào bảng tạm được rollback ở 7 nhánh lỗi và commit đúng ở nhánh thành công. Bảng tạm mất khi đóng connection; không tạo dữ liệu khách sạn giả.

HTTP đã kiểm tra token hợp lệ cho **cả form và JSON** đăng nhập/đăng ký, đi được đến validation dữ liệu; thiếu token bị chặn. Đặt món và đặt phòng với payload không hợp lệ bị từ chối sau khi CSRF hợp lệ. Thanh toán không thuộc quyền và POST bảo trì/tạo đơn tại quầy chưa đăng nhập vẫn bị chặn.

Đây không phải kiểm thử đăng ký tài khoản thật hoặc commit booking/thu tiền/bảo trì thành công qua HTTP. Các nhánh ghi đã được kiểm tra bằng regression có rollback; Admin UI dùng fixture DOM cô lập, truy vấn DB thật được kiểm tra riêng.

**5. SQL cần chạy thủ công**

Trên hosting, chọn đúng `db_ad0565_luxuryhotel`, sao lưu trước, rồi chạy:

`database/20261104_safe_hotel_fixes.sql`

Không cần chạy một migration thứ hai. Script đã bao gồm đủ các cột/index/FK mới, không tạo lại RoomMaintenances và không xóa lịch sử. Nếu cột/default cũ không tương thích hoặc index unique gặp dữ liệu trùng, dừng để đối chiếu; không tự xóa hay ép kiểu dữ liệu.

LocalDB hiện tại đã đủ schema; lượt này chỉ kiểm tra script qua dry-run rollback. Trên LocalDB khác, chạy thủ công nếu chưa nâng cấp:

```powershell
sqlcmd -S "(LocalDB)\MSSQLLocalDB" -d QLKhachSanDB -E -b -i database/20261104_safe_hotel_fixes.sql
```

Dry-run chỉ dành cho LocalDB, chạy từ repository root:

```powershell
sqlcmd -S "(LocalDB)\MSSQLLocalDB" -d QLKhachSanDB -E -b -i tests/schema-dry-run.sql
```

**6. Phần chưa kiểm chứng và bàn giao**

- Chưa kiểm tra DB/IIS, quyền sp_getapplock, cấu hình SMTP và ngân hàng thật trên hosting.
- Chưa đăng nhập ba vai trò bằng trình duyệt với tài khoản vận hành thật và commit nghiệp vụ qua HTTP. Không thay mật khẩu để lấy quyền kiểm thử.
- Chưa tạo hoặc triển khai gói publish/ZIP. Kiểm tra manifest chứng minh file nằm trên đĩa và có liên kết project, không chứng minh nội dung của một archive chưa được tạo.
- Bản bàn giao source phải bao gồm cả thư mục gốc `database`, `tests`, `docs`, không chỉ sao chép thư mục web `QLKhachSan`. Các file mới còn chưa được Git track/stage vì người dùng chưa thực hiện Commit; cần đưa chúng vào thay đổi khi bàn giao.
- Workflow hiện tại bỏ Web.config khi deploy và có thể tự deploy khi push main. Giữ nguyên connection string; merge cấu hình cần thiết có kiểm soát và chạy SQL trước khi đưa DLL mới lên hosting. Chưa thay đổi workflow.
- Không tuyên bố hệ thống đã được kiểm chứng hoàn toàn trên production.
