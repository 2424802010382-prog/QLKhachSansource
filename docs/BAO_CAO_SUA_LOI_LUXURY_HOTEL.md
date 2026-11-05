**BÁO CÁO SỬA LỖI VÀ KIỂM THỬ QLKhachSan — 04/11/2026**

**1. Phạm vi, số lượng và trạng thái bàn giao**

Đã sửa trực tiếp trên Solution hiện tại; không tạo dự án thay thế. Có **37 nhóm lỗi được xác nhận và xử lý**: 32 nhóm trong báo cáo phân tích ban đầu, cộng 5 lỗi phát hiện khi sửa/kiểm thử (mục 33–37). Phân loại: 2 nghiêm trọng, 18 cao, 17 trung bình. Đây là số nhóm nguyên nhân, không phải số endpoint hoặc số kịch bản tấn công đã thực thi.

Không commit, push, publish hoặc deploy. Không thay connection string, mật khẩu hay tài khoản hiện có. Workflow và publish profile không được chỉnh sửa. File ZIP có sẵn được giữ nguyên; `Views.zip` được loại khỏi danh sách nội dung publish của project.

LocalDB đã được cập nhật **schema bổ sung** bằng script ở mục 8. Các kiểm thử ghi nghiệp vụ chạy trong transaction và rollback. So sánh checksum các bảng nghiệp vụ trước/sau kiểm thử không phát hiện thay đổi dữ liệu. Không chạy chức năng giải phóng hàng loạt để kiểm thử. Không chạy migration trên hosting.

**2. Lỗi đã sửa và nguyên nhân gốc**

| # | Mức độ | Nguyên nhân đã xác nhận | Cách xử lý |
|---|---|---|---|
| 1 | Nghiêm trọng | Link reset mật khẩu được trả cho người yêu cầu qua TempData/View | Bỏ hiển thị token; chỉ gửi qua SMTP đến email chủ tài khoản; production yêu cầu URL HTTPS chuẩn cấu hình riêng |
| 2 | Nghiêm trọng | API mô phỏng/tiền mặt ghi trạng thái theo ID, không kiểm tra chủ đơn và làm lùi trạng thái booking | Kiểm tra chủ sở hữu; simulator luôn từ chối ghi tiền; yêu cầu chuyển khoản chỉ tạo trạng thái chờ xác minh; không lùi trạng thái booking |
| 3 | Cao | Đăng nhập tự bật lại IsActive của tài khoản bị khóa | Từ chối trước khi xác minh/chuyển đổi mật khẩu; chỉ luồng Admin quản lý trạng thái tài khoản |
| 4 | Cao | Fallback mật khẩu thường chấp nhận chính chuỗi hash | Không áp dụng fallback với dữ liệu hash; giữ tương thích mật khẩu thường cũ không phải hash |
| 5 | Cao | Quyền nhân viên chỉ ẩn nút, không kiểm tra tại action | Bộ lọc quyền toàn cục áp dụng cho từng action Staff và nghiệp vụ nhà hàng |
| 6 | Cao | Vai trò/quyền cũ trong Session vẫn được tin sau khi Admin thay đổi | Đọc lại tài khoản đang hoạt động, vai trò và quyền mỗi request; không tin vai trò Session cũ |
| 7 | Cao | Payment/Success/CheckPaymentStatus không kiểm tra quyền sở hữu | Đối chiếu Bookings.UserId; khách vãng lai chỉ truy cập đơn được tạo trong chính Session của họ |
| 8 | Cao | Equals với StringComparison nằm trong LINQ to Entities | Chuyển sang kiểm tra bằng ID phòng vật lý và các truy vấn EF6 hỗ trợ |
| 9 | Cao | RoomNumber không được xác thực tồn tại, loại phòng và hoạt động | Danh sách số phòng phải khớp tập phòng vật lý khả dụng ở server |
| 10 | Cao | Ghi chú tự do có thể chèn tag gán phòng | Loại tag do khách nhập; chỉ bộ xử lý nghiệp vụ được tạo tag gán phòng |
| 11 | Cao | RoomCount nhiều phòng nhưng chỉ lưu một số phòng | DTO/UI hỗ trợ danh sách số phòng; server yêu cầu đủ số lượng và không trùng; Staff có thể chọn hoặc tự gán đủ phòng |
| 12 | Cao | Kiểm tra khả dụng rồi ghi không được đồng bộ giữa request | Transaction kết hợp khóa SQL sp_getapplock dùng chung cho đặt phòng, xác nhận, thu tiền, nhận/trả và bảo trì |
| 13 | Cao | Tồn phòng lấy từ TotalRooms, bỏ qua phòng vật lý ngừng hoạt động/bảo trì | Dùng chung bộ tính khả dụng từ phòng vật lý và khoảng lịch |
| 14 | Cao | Đồng bộ trạng thái không xét ngày bắt đầu, chọn booking không có ưu tiên | Tính theo ngày, khoảng [nhận, trả), ưu tiên đang ở/xác nhận/chờ; không cập nhật booking chỉ vì xem danh sách |
| 15 | Cao | Admin có thể nhận đơn đã quá ngày trả; thiếu kiểm tra lại phòng | Kiểm tra đủ tiền, trạng thái, hạn lưu trú, gán đủ phòng và xung đột trước khi nhận |
| 16 | Cao | Gọi món cộng hóa đơn phòng thiếu chủ sở hữu, dùng tên loại phòng và dịch vụ ID cố định | Chỉ chủ booking đang ở được gọi; xác định phòng vật lý; tạo đơn món/chi tiết/liên kết hóa đơn thật; cập nhật khoản còn phải thu nguyên tử |
| 17 | Cao | Giải phóng hàng loạt xóa nhiều bảng riêng lẻ và vướng FK đơn món | Giữ route/chức năng giải phóng nhưng chỉ hủy đơn chưa thu tiền, chưa nhận phòng; giữ lịch sử; transaction bao toàn bộ |
| 18 | Cao | Nhiều POST thiếu CSRF | Bộ lọc CSRF toàn cục; partial dùng chung cấp token cho form, fetch và XMLHttpRequest |
| 19 | Trung bình | Form hồ sơ POST sai action | Gửi đến UserProfile; kiểm tra model null/không hợp lệ |
| 20 | Trung bình | JavaScript chọn phòng giao món bị bản định nghĩa sau ghi đè; phụ thuộc jQuery chưa nạp | Khôi phục lời gọi tải phòng trong hàm đang được sử dụng; dùng fetch; xử lý lỗi phiên và HTTP |
| 21 | Trung bình | Date trực tiếp trên cột EF và projection FoodPriceRow thiếu cột | So sánh ngày bằng khoảng; sử dụng projection đủ trường; endpoint cũ gọi luồng dùng chung đã kiểm tra |
| 22 | Trung bình | Đơn giao món dựa vào email/tên loại và luôn lưu BookingId null | Dựa vào UserId, booking đang ở, số phòng; lưu BookingId; giữ tương thích endpoint room-service cũ khi xác định duy nhất được phòng |
| 23 | Trung bình | Báo cáo nhà hàng trả cố định 0 | Truy vấn khoản đã thu; thêm PaidAt cho lần thu mới; tách phần món ăn thu cùng phòng để không tính hai lần |
| 24 | Trung bình | Đơn món có thể chuyển tùy ý và đảo thanh toán | Quy tắc chuyển trạng thái dùng chung; chặn hồi sinh đơn kết thúc và hủy/đảo khoản đã thu; thu tiền cần chứng từ và người xác minh |
| 25 | Trung bình | Sửa TotalRooms không khớp số phòng thực tế | Phòng vật lý là nguồn tồn; chặn sửa số lượng lệch; loại phòng mới chưa có phòng vật lý không được bán như phòng có thật |
| 26 | Trung bình | Staff/cache trạng thái không xét lịch bảo trì | Snapshot cùng bộ quy tắc cho Staff/Admin/khách; lịch bảo trì theo ngày tách khỏi khóa bảo trì thủ công |
| 27 | Trung bình | Kết thúc bảo trì sớm xóa hiệu lực cả lịch quá khứ khi xem lại | Giữ StartDate/EndDate gốc, dùng EndedAt; giải phóng từ ngày kết thúc sớm theo lịch ngày; không xóa lịch sử |
| 28 | Trung bình | Booking tại quầy và payment được lưu riêng không có transaction chung | Transaction theo action, rollback khi một bước thất bại |
| 29 | Trung bình | Khách hàng/chi tiết/CSV ghép đơn theo email khác với lịch sử UserId | Thống nhất UserId cho danh sách, chi tiết và export; tiền đã thu lấy từ bản ghi thu tiền |
| 30 | Trung bình | Hoàn lượt mã giảm giá không nhất quán, tăng lượt không khóa | Dùng khóa giao dịch chung; hoàn lượt khi hủy ở cả ba luồng; giữ nguyên lịch sử mã |
| 31 | Trung bình | GetAvailableRooms loại cả loại khi có một booking | Trả loại phòng còn phòng vật lý khả dụng, giữ các trường phản hồi cũ |
| 32 | Trung bình | Dữ liệu dịch vụ/phòng/khách/lý do bảo trì được chèn HTML hoặc JS chưa đúng ngữ cảnh | Encode dữ liệu trước khi tạo DOM; chỉ giữ một số tag định dạng an toàn của modal; encode chuỗi trong onclick và JS |
| 33 | Trung bình | AdminOrders có hàm updateOrderStatus khai báo dở, gây SyntaxError | Bỏ đoạn khai báo dở; giữ hàm hoàn chỉnh, thêm kiểm tra HTTP và trạng thái loading |
| 34 | Cao | Seed Admin có mật khẩu mặc định viết trong source | Bỏ mật khẩu mặc định; bootstrap mới chỉ dùng biến môi trường riêng, không chạy seed và không đổi tài khoản hiện có |
| 35 | Trung bình | Giới hạn giảm giá âm có thể làm tăng tiền thay vì giảm | Kiểm tra giá trị tối thiểu/giảm tối đa/số lượt ở backend |
| 36 | Cao | CSV chỉ escape dấu nháy, không chống công thức từ văn bản người dùng | Trung hòa tiền tố công thức cho chuỗi; giữ số âm dạng số |
| 37 | Trung bình | Giá phòng đưa vào data-price theo vi-VN làm Number() nhận NaN | Dùng InvariantCulture khi xuất số cho JavaScript ở Staff và popup sửa phòng |

Các phần liên quan cũng được sửa đồng bộ: số tiền còn phải thu sau phát sinh món ăn, QR chỉ dùng phần còn thiếu, không sửa lại khoản đã thu, badge/nút thu tiền/điều kiện trả phòng dựa trên tổng đã thu. Trang About/Contact bỏ nội dung mẫu không thuộc khách sạn; không bịa địa chỉ/số điện thoại. Các route đăng ký và AI dẫn về chức năng thực tế trên trang chủ.

**3. File và phương thức thay đổi chính**

| File | Phương thức/phần thay đổi |
|---|---|
| Controllers/HotelController.cs — mới | Transaction theo action; khóa nghiệp vụ; rollback và xử lý lỗi JSON |
| Filters/HotelSecurityFilter.cs — mới; App_Start/FilterConfig.cs | Xác minh Session với DB, quyền theo action, CSRF |
| Models/HotelOperations.cs — mới | AvailableRooms, SelectRooms, RoomStatus, RoomSnapshot, EnsureReservation, ValidateCheckIn, CreateMaintenance, EndMaintenance, ConfirmBookingPayment, SynchronizeOutstandingPayment, ChangeFoodStatus, ConfirmFoodPayment, RoomRevenue; metadata giữ chỗ tạm không map DB |
| Models/MoHinhBoSungKhachSan.cs | SyncPhysicalRoomStatuses: chỉ cache ngày hiện tại khi có thao tác ghi; không nuốt lỗi như thành công |
| Controllers/AccountController.cs | Login, Register GET, UserProfile, ForgotPassword, ResetPassword, Logout GET/POST |
| Controllers/BookingController.cs | Create; các API khả dụng; Payment/Success/CheckPaymentStatus; ConfirmCashPayment; RequestBankTransferReview; vô hiệu hóa ghi tiền mô phỏng |
| Controllers/StaffController.cs | Index; nhận/trả/xác nhận/hủy/thu tiền; CreateBooking; trạng thái và thanh toán món |
| Controllers/AdminController.cs | Xác nhận/nhận/trả/hủy/thu tiền; tồn phòng; khách hàng/CSV; báo cáo; ma trận; bảo trì; giải phóng; chống công thức CSV |
| Controllers/BookingHistoryController.cs | Details dùng tổng đã thu; Cancel khóa/reload trước ghi |
| Controllers/RestaurantController.cs | PlaceOrder, PlaceRoomServiceOrder, GetMyActiveRooms, CancelOrder, UpdateOrderStatus, UpdateOrderPayment |
| Controllers/HomeController.cs; AIChatController.cs | Route không còn trả view thiếu; nội dung giới thiệu/liên hệ; Dispose DbContext |
| Models/RoomMaintenance.cs | Map EndedAt đã có/được bổ sung bằng SQL |
| Models/TaoDatPhongRequest.cs; RestaurantOrderRequest.cs | Danh sách số phòng; liên kết booking và thu món cùng hóa đơn phòng |
| Views/Shared/_SecurityClient.cshtml — mới | CSRF cho form/AJAX; encode văn bản; thu chứng từ xác nhận trước khi gửi |
| Views/Shared/_LuxuryModal.cshtml | Encode dữ liệu, giữ định dạng đơn giản an toàn |
| Views/Home/Index.cshtml | Chọn nhiều phòng, đổi ngày xóa lựa chọn cũ, bỏ cache khả dụng cũ, chống phản hồi AJAX lỗi thời, chuyển sang thanh toán, encode dịch vụ/phòng |
| Views/Admin/Rooms.cshtml | Giữ lịch 7 ngày/popup; encode dữ liệu; phân biệt trạng thái và giữ chỗ tạm |
| Views/Admin/Index.cshtml, CurrentGuests.cshtml, CustomerDetails.cshtml | Badge/nút thu tiền theo tổng đã thu; mô tả giải phóng đúng hành vi |
| Views/Admin/StaffPermissions.cshtml | Encode tên nhân viên đúng ngữ cảnh JavaScript |
| Views/Staff/CreateBooking.cshtml, Bookings.cshtml | Chọn phòng vật lý; số liệu giá chuẩn; trạng thái khoản còn phải thu |
| Views/Booking/Payment.cshtml, Success.cshtml | Không báo thu tiền khi mới yêu cầu đối soát; QR theo dư nợ; loading/error/polling |
| Views/Restaurant/Index.cshtml, AdminOrders.cshtml | Tải số phòng; sửa SyntaxError; giới hạn trạng thái; xử lý HTTP/loading |
| Views/Account/Profile.cshtml; Logout.cshtml — mới | Form đúng action; logout bằng POST |
| Các view HTML độc lập và Shared/_Layout.cshtml | Nạp partial CSRF dùng chung; không thay thiết kế tổng thể |
| Web.config | Bỏ runtime build-provider EDMX cũ không dùng; customErrors RemoteOnly; giữ nguyên connectionStrings |
| Migrations/Configuration.cs | Tắt automatic migration/data loss; bỏ mật khẩu seed mặc định |
| QLKhachSan.csproj | Khai báo file mới; không publish Views.zip |
| database/20261104_safe_hotel_fixes.sql | Schema bổ sung an toàn, kiểm tra tồn tại |
| tests/* | Bộ hồi quy C#/EF, HTTP, Chrome và kiểm tra cú pháp JS |

RoomController, HotelDbContext và CSS không cần thay đổi logic để che lỗi. Danh sách phòng thường/Luxury và chi tiết đã được kiểm tra HTTP; giao diện chọn phòng được kiểm tra bằng Chrome ở desktop và mobile.

**4. Quy tắc dữ liệu được giữ thống nhất**

- Khoảng ngày là `[CheckInDate, CheckOutDate)`. Bảo trì 07/11–10/11 áp dụng ngày 07, 08, 09; không áp dụng ngày 06 và 10.
- Ngày xem lịch quyết định trạng thái. Đơn tương lai không chiếm phòng hôm nay. Đơn đã trả không chặn khả dụng; khi xem lịch quá khứ có số phòng đã lưu, hiển thị trạng thái đơn đã trả.
- Lịch sử ngày của đơn cũ dựa trên ngày lưu trú đã lưu; DB cũ không có timestamp nhận/trả thực tế để tái tạo chính xác trường hợp đến muộn/trả sớm. Không tự bịa timestamp lịch sử.
- Tag số phòng cũ vẫn đọc được. Đơn thiếu tag tiêu thụ dung lượng; vị trí giữ chỗ trên ma trận được ghi rõ **Chờ gán phòng**, không khẳng định đó là số phòng thực tế đã gán. Gán thật được xác minh khi xác nhận/nhận phòng. Không ghi lại lịch sử chỉ vì xem màn hình.
- Khóa bảo trì thủ công dùng trạng thái riêng. Cờ `Bảo trì` cũ chỉ được giữ như thủ công khi không có lịch bảo trì nào giải thích nó; cờ cache không làm lịch đã hết hạn bị khóa vĩnh viễn.
- Thu tiền cần người có quyền xác minh và nhập chứng từ/mã giao dịch. Một mã đã dùng không được dùng lại cho booking hay món ăn. Không tích hợp hoặc giả tạo giao dịch ngân hàng.
- Món thu cùng hóa đơn phòng chỉ tính một lần: phần món vào doanh thu món, phần còn lại vào doanh thu lưu trú/dịch vụ. Đơn món thu riêng vẫn có khoản thu riêng.
- Khoản đã thu không tự suy diễn thành đã hoàn. Khi không có chứng từ/nghiệp vụ hoàn tiền trong schema hiện có, ứng dụng chặn hủy/đảo khoản đã thu.

**5. Build và kết quả kiểm thử thực tế**

| Kiểm tra | Kết quả | Giới hạn |
|---|---|---|
| MSBuild Debug + MvcBuildViews=true | Thành công | Đã biên dịch cả Razor |
| MSBuild Release + MvcBuildViews=true | Thành công | Không publish/deploy |
| C#/EF6 regression | 92 kiểm tra đạt | Kịch bản ghi dùng fixture trong transaction và rollback |
| HTTP qua IIS Express | 26 kiểm tra đạt | Trang/API đọc, quyền truy cập và các POST bị từ chối; không commit đơn thật |
| Chrome desktop/mobile + Admin DOM fixture | 17 kiểm tra đạt | Home/Restaurant dùng HTTP thật; Admin calendar dùng script thật trong DOM/API fixture cô lập |
| Inline JavaScript | 28 script trong 21 view có inline JS đạt | Thay biểu thức Razor bằng placeholder để parse; kiểm tra cú pháp, không thay thế kiểm thử browser |
| SQL migration chạy lặp | Thành công | Đã áp dụng trên LocalDB; không chạy hosting |
| git diff --check | Đạt | Không commit/stage |

Các nhóm hồi quy gồm: 40 phòng/8 loại; còn 4 phòng cùng loại sau một đơn; chặn trùng phòng/sai loại/không tồn tại/nhân đôi số phòng; mốc ngày bảo trì; chặn bảo trì trùng bảo trì/booking; kết thúc sớm giữ lịch sử; khóa tài khoản; hash không dùng làm mật khẩu; vai trò Session cũ; quyền sở hữu; CSRF; không tự thu tiền; đủ tiền mới nhận/trả; không nhận hai lần; khoản phát sinh còn thiếu; không thu hai lần/cùng mã; thu món cùng phòng không đếm trùng; CSV an toàn.

Chrome kiểm tra mở/đóng popup, chọn nhiều phòng, đổi ngày xóa lựa chọn cũ, kích thước mobile, 7 ngày trước/sau/Hôm nay, 40 card, encode HTML và nút lưu bảo trì thoát loading khi HTTP lỗi. Phần Admin này là fixture UI, **không phải đăng nhập bằng tài khoản Admin thật rồi ghi dữ liệu qua HTTP**. Truy vấn ma trận thật được kiểm tra độc lập bằng controller/EF.

**6. Dữ liệu sau kiểm thử**

Đối chiếu LocalDB: 40 phòng vật lý, 8 loại, mỗi loại 5; không trùng số phòng. Bộ kiểm thử so sánh checksum các bảng nghiệp vụ trước/sau transaction và xác nhận giữ nguyên dữ liệu. Các fixture không tồn tại sau rollback. SQL Server có thể để lại khoảng trống identity sau rollback; không reset identity hoặc sửa ID để che khoảng trống.

Không tự hủy hai booking đang có, không xóa lịch bảo trì hoặc 10 đơn món hiện có. Giá trị Status cache cũ có thể khác snapshot tính theo ngày; view sử dụng snapshot và không tự ghi cache khi đọc.

**7. Phần chưa thể xác minh hoặc cần cấu hình thật**

1. Chưa kết nối SQL Server hosting `db_ad0565_luxuryhotel`; chưa kiểm tra trực tiếp schema, quyền sp_getapplock, timezone, IIS hoặc giao diện sau đăng nhập trên hosting.
2. Chưa gửi email reset thật. Cần SMTP/network/from hợp lệ và appSetting `PublicBaseUrl` là HTTPS của website thực tế. Không hiển thị token thay cho email khi chưa cấu hình.
3. Chưa có tích hợp ngân hàng/callback thật; đang dùng quy trình đối soát thủ công có quyền và chứng từ.
4. Chưa thực hiện POST dương tính qua phiên HTTP thật để commit booking/bảo trì/thu tiền. Luồng ghi đã được kiểm thử ở bộ xử lý dùng chung/controller với transaction rollback; tránh tạo đơn/thu tiền giả tồn tại trong DB đang dùng.
5. Chưa kiểm thử email, Web Deploy, trình duyệt Safari/iOS hoặc thiết bị vật lý. Chrome có kiểm tra viewport desktop/mobile.
6. Các đơn món cũ đã trả tiền nhưng không có PaidAt vẫn dùng CreatedAt làm mốc báo cáo tương thích. Không tự suy ra ngày thực nhận cho lịch sử; cần đối chiếu chứng từ nếu yêu cầu quyết toán theo ngày chính xác.
7. Dữ liệu lịch cũ thiếu số phòng hoặc thiếu timestamp thực tế cần nhân viên rà soát; không thể tái tạo lịch sử chính xác từ thông tin không được lưu.

Vì các giới hạn trên, không tuyên bố hệ thống đã được xác minh 100% trên production.

**8. SQL cần chạy**

File: `database/20261104_safe_hotel_fixes.sql`.

- Chỉ bổ sung cột còn thiếu: `RoomMaintenances.EndedAt`; `DonDatMon.PaidAt`, `PaymentReference`, `PaymentEvidence`, `PayWithBooking`, `BookingServiceId`.
- Bổ sung index truy vấn khoảng ngày; unique số phòng; unique PaymentReference không null; FK BookingServiceId đến DatPhongDichVu.
- Không tạo lại RoomMaintenances, không DROP/DELETE, không đổi kết nối, không chèn dữ liệu mẫu, không điền ngày thanh toán lịch sử giả.
- Nếu thiếu bảng nguồn hoặc có dữ liệu trùng không đáp ứng unique index, script báo lỗi thay vì tự xóa/sửa dữ liệu.

**9. Cập nhật LocalDB**

Script đã chạy thành công trong phiên này và đã kiểm tra chạy lại. Trên bản LocalDB khác, sao lưu trước rồi dùng:

```powershell
sqlcmd -S "(LocalDB)\MSSQLLocalDB" -d QLKhachSanDB -E -b -i .\database\20261104_safe_hotel_fixes.sql
```

Chạy lại kiểm tra bằng:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\run-regression.ps1
node .\tests\check-view-javascript.js
```

Trong Developer PowerShell của Visual Studio, kiểm tra đủ hai cấu hình và Razor:

```powershell
msbuild QLKhachSan.sln /t:Build /p:Configuration=Debug /p:MvcBuildViews=true
msbuild QLKhachSan.sln /t:Build /p:Configuration=Release /p:MvcBuildViews=true
```

ExecutionPolicy Bypass chỉ áp dụng cho tiến trình kiểm thử, không đổi chính sách hệ thống. Bộ hồi quy từ chối database ngoài LocalDB QLKhachSanDB.

HTTP/browser cần IIS Express chạy localhost cổng 51180 với thư mục web `QLKhachSan`; sau đó chạy `node tests/http-smoke.js` và `node tests/browser-smoke.js`. Script browser dùng Chrome đã cài, không cần cài npm package. Output/screenshot nằm trong `QLKhachSan/obj`, không đưa lên hosting.

**10. Cập nhật hosting an toàn — chưa thực hiện**

1. Sao lưu DB và bản web hiện tại; chọn đúng `db_ad0565_luxuryhotel` trong công cụ SQL.
2. Đối chiếu bảng/cột/index đang có với script. Giữ bảng RoomMaintenances hiện hữu. Chạy script bổ sung, không chạy automatic EF migration/Seed.
3. Kiểm tra quyền thực thi sp_getapplock bằng transaction rollback; đối chiếu ngày/giờ ứng dụng và SQL Server với ngày nghiệp vụ khách sạn.
4. Cấu hình SMTP và PublicBaseUrl bằng thông tin thật; giữ connection string, tài khoản hosting và API key hiện có.
5. Workflow hiện tại chủ động bỏ Web.config khi deploy. Vì vậy phải **merge thủ công** thay đổi buildProviders/customErrors cần thiết vào Web.config hosting, không chép đè phần connectionStrings. Đồng thời kiểm tra cách bảo toàn appsettings.json/API key trước khi triển khai.
6. Chỉ cập nhật sau khi xác nhận schema tương thích và chạy kiểm thử ở staging/phiên có kiểm soát. Upload đồng bộ DLL, view và partial mới; không upload test/obj/ZIP.
7. Đăng nhập ba vai trò kiểm tra quyền; kiểm tra chính xác các ngày bảo trì, booking trùng, đối soát thủ công và số dư khi trả phòng. Xác minh log nếu lỗi; không sửa thành trả thành công giả.

**11. Việc thủ công trước Commit/Push**

- Đọc diff, script SQL và báo cáo; kiểm tra lại yêu cầu nghiệp vụ với nhân viên vận hành, đặc biệt giải phóng không xóa lịch sử và cách đối soát chứng từ.
- Kiểm tra dữ liệu cũ thiếu phòng/thời điểm thực tế; không tự ghép tài khoản qua email hoặc tự viết lại lịch sử.
- Hoàn tất cấu hình SMTP/URL chuẩn và kiểm thử reset bằng hộp thư kiểm soát.
- Chạy lại Debug/Release/Razor và các bộ kiểm thử sau bất kỳ chỉnh sửa tiếp theo.
- Không stage ZIP có sẵn, bin/obj, screenshot, publish profile, token, mật khẩu hoặc API key.
- Workflow hiện có tự deploy khi push `main`. Cần bảo đảm schema/cấu hình đã sẵn sàng và cơ chế phê duyệt triển khai phù hợp, hoặc giữ thay đổi trên nhánh kiểm thử; không push main như một bước chỉ lưu code.
- Commit/Push/Deploy do người dùng chủ động thực hiện; phiên sửa này chưa thực hiện các thao tác đó.
