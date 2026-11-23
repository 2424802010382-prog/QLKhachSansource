using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web.Mvc;
using Newtonsoft.Json.Linq;
using QLKhachSan.Models;

namespace QLKhachSan.Controllers
{
    public class GeminiController : Controller
    {
        private static readonly HttpClient http =
            new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            };

        [HttpPost]
        public async Task<JsonResult> Ask(string message, string contextRole = null)
        {
            var profile = AIChatProfile.ForSession(
                Session["UserId"] != null,
                Convert.ToString(Session["Role"]),
                Session["Permissions"] as List<string>
            );

            if (contextRole != null && contextRole != profile.Role)
            {
                Response.StatusCode = 409;
                return Json(new
                {
                    success = false,
                    reply = "Vai trò đăng nhập đã thay đổi. Vui lòng tải lại trang để mở đúng trợ lý."
                });
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                return Json(new { success = false, reply = "Vui lòng nhập câu hỏi." });
            }

            if (message.Length > 2000)
            {
                return Json(new { success = false, reply = "Câu hỏi không được quá 2000 ký tự." });
            }

            // 1. Thu thập toàn bộ dữ liệu từ Database kèm tính toán số lượng phòng trống thực tế hôm nay
            List<RoomInfo> roomList = new List<RoomInfo>();
            List<PromoInfo> promoList = new List<PromoInfo>();
            string hotelContext = "";

            try
            {
                using (var db = new HotelDbContext())
                {
                    var rooms = db.Rooms.AsNoTracking()
                        .Where(r => r.IsActive)
                        .OrderByDescending(r => r.Price)
                        .ToList();

                    DateTime today = DateTime.Today;
                    DateTime tomorrow = today.AddDays(1);

                    foreach (var r in rooms)
                    {
                        int availableCount = r.TotalRooms;
                        try
                        {
                            availableCount = HotelOperations.AvailableRooms(db, r.RoomId, today, tomorrow).Count;
                        }
                        catch
                        {
                            availableCount = r.TotalRooms;
                        }

                        roomList.Add(new RoomInfo
                        {
                            RoomId = r.RoomId,
                            Name = r.RoomName,
                            Type = r.RoomType,
                            Price = r.Price,
                            Capacity = r.Capacity,
                            Description = r.Description,
                            TotalRooms = r.TotalRooms,
                            AvailableRoomsToday = availableCount
                        });
                    }

                    var promos = db.KhuyenMais.AsNoTracking()
                        .Where(k => k.HoatDong == true && k.NgayKetThuc >= DateTime.Now)
                        .ToList();

                    foreach (var k in promos)
                    {
                        promoList.Add(new PromoInfo
                        {
                            Code = k.MaCode,
                            Ten = k.TenKhuyenMai,
                            Percent = (double)k.GiaTriGiam,
                            MaxGiam = k.GiamToiDa ?? 0,
                            MinDon = k.DonToiThieu
                        });
                    }

                    var menuItems = db.MonAns.AsNoTracking()
                        .Where(m => m.HoatDong)
                        .Take(50)
                        .ToList();

                    int? userId = null;
                    if (Session["UserId"] != null && int.TryParse(Session["UserId"].ToString(), out int parsedUid))
                    {
                        userId = parsedUid;
                    }

                    List<Booking> userBookings = new List<Booking>();
                    List<DonDatMon> userOrders = new List<DonDatMon>();
                    if (userId.HasValue)
                    {
                        userBookings = db.Bookings.AsNoTracking()
                            .Where(b => b.UserId == userId.Value)
                            .OrderByDescending(b => b.BookingId)
                            .Take(5)
                            .ToList();

                        userOrders = db.DonDatMons.AsNoTracking()
                            .Where(o => o.UserId == userId.Value)
                            .OrderByDescending(o => o.MaDon)
                            .Take(5)
                            .ToList();
                    }

                    int totalRoomsCount = db.Rooms.Count(r => r.IsActive);
                    int activeBookingsCount = db.Bookings.Count(b => b.Status == "Đang ở" || b.Status == "Đã xác nhận");
                    decimal totalRevenue = db.Bookings.Where(b => b.Status == "Đã thanh toán" || b.Status == "Hoàn thành").Sum(b => (decimal?)b.TotalAmount) ?? 0;

                    var sbCtx = new StringBuilder();
                    sbCtx.AppendLine("\n--- DỮ LIỆU THỰC TẾ TỪ DATABASE KHÁCH SẠN ---");
                    sbCtx.AppendLine($"Thời gian hệ thống: {DateTime.Now:dd/MM/yyyy HH:mm}");
                    sbCtx.AppendLine("QUY TẮC TRẢ LỜI CỰC KỲ QUAN TRỌNG: Khi khách hỏi còn bao nhiêu phòng trống hoặc tình trạng phòng, hãy trả lời CHÍNH XÁC số lượng phòng trống thực tế hôm nay (AvailableRoomsToday) được cung cấp ngay lập tức. Tuyệt đối KHÔNG kêu khách lên website kiểm tra lại.");

                    if (roomList.Any())
                    {
                        sbCtx.AppendLine("\n[CÁC HẠNG PHÒNG, GIÁ & SỐ LƯỢNG TRỐNG HÔM NAY]:");
                        foreach (var r in roomList)
                        {
                            sbCtx.AppendLine($"- Tên phòng: {r.Name} | Loại: {r.Type} | Còn trống hôm nay: {r.AvailableRoomsToday} phòng (Tổng số: {r.TotalRooms}) | Giá: {r.Price:N0} VNĐ/đêm | Sức chứa: {r.Capacity} người | Mô tả: {r.Description}");
                        }
                    }

                    if (menuItems.Any())
                    {
                        sbCtx.AppendLine("\n[THỰC ĐƠN NHÀ HÀNG & MÓN ĂN]:");
                        foreach (var m in menuItems)
                        {
                            sbCtx.AppendLine($"- {m.TenMonAn}: {m.Gia:N0} VNĐ ({m.DonVi})");
                        }
                    }

                    if (promoList.Any())
                    {
                        sbCtx.AppendLine("\n[VOUCHER KHUYẾN MÃI]:");
                        foreach (var p in promoList)
                        {
                            sbCtx.AppendLine($"- Mã [{p.Code}]: {p.Ten}, Giảm {p.Percent}% (Tối đa {p.MaxGiam:N0} VNĐ, đơn từ {p.MinDon:N0} VNĐ)");
                        }
                    }

                    if (userId.HasValue)
                    {
                        sbCtx.AppendLine($"\n[THÔNG TIN TÀI KHOẢN NGƯỜI DÙNG ĐANG ĐĂNG NHẬP (UserId: {userId.Value}, Vai trò: {profile.Role})]:");
                        if (userBookings.Any())
                        {
                            sbCtx.AppendLine("Đơn đặt phòng của khách:");
                            foreach (var b in userBookings)
                            {
                                sbCtx.AppendLine($"- Đơn #{b.BookingId} | Phòng ID: {b.RoomId} | Nhận: {b.CheckInDate:dd/MM/yyyy} - Trả: {b.CheckOutDate:dd/MM/yyyy} | Tổng: {b.TotalAmount:N0} VNĐ | Trạng thái: {b.Status}");
                            }
                        }
                        else
                        {
                            sbCtx.AppendLine("Khách chưa có đơn đặt phòng nào trên hệ thống.");
                        }

                        if (userOrders.Any())
                        {
                            sbCtx.AppendLine("Đơn gọi món của khách:");
                            foreach (var o in userOrders)
                            {
                                sbCtx.AppendLine($"- Đơn món #{o.MaDon} | Phòng/Bàn: {o.SoPhong ?? o.SoBan} | Tổng: {o.TongTien:N0} VNĐ | Trạng thái: {o.TrangThai}");
                            }
                        }
                    }

                    if (string.Equals(profile.Role, "Admin", StringComparison.OrdinalIgnoreCase) || string.Equals(profile.Role, "Staff", StringComparison.OrdinalIgnoreCase))
                    {
                        sbCtx.AppendLine($"\n[THỐNG KÊ QUẢN TRỊ NỘI BỘ]:");
                        sbCtx.AppendLine($"- Tổng loại phòng hoạt động: {totalRoomsCount}");
                        sbCtx.AppendLine($"- Đơn đang hoạt động/chờ: {activeBookingsCount}");
                        sbCtx.AppendLine($"- Tổng doanh thu ghi nhận: {totalRevenue:N0} VNĐ");
                    }

                    hotelContext = sbCtx.ToString();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceWarning("GeminiController: Error fetching full database context: " + ex.Message);
            }

            // 2. Gửi yêu cầu đến Gemini AI API
            try
            {
                GeminiSettings config = GeminiSettings.Load();
                string apiKey = config.ApiKey;
                string model = string.IsNullOrWhiteSpace(config.Model) || config.Model.Contains("3.5")
                    ? "gemini-1.5-flash"
                    : config.Model;
                int maxTokens = config.MaxOutputTokens;

                string instruction = profile.Instruction + hotelContext;

                JObject body = new JObject
                {
                    ["system_instruction"] = new JObject
                    {
                        ["parts"] = new JArray(
                            new JObject { ["text"] = instruction })
                    },
                    ["contents"] = new JArray(
                        new JObject
                        {
                            ["role"] = "user",
                            ["parts"] = new JArray(
                                new JObject { ["text"] = message.Trim() })
                        }),
                    ["generationConfig"] = new JObject
                    {
                        ["maxOutputTokens"] = maxTokens
                    }
                };

                string url = "https://generativelanguage.googleapis.com/v1beta/models/" +
                    Uri.EscapeDataString(model) + ":generateContent";

                using (var request = new HttpRequestMessage(HttpMethod.Post, url))
                {
                    request.Headers.Add("x-goog-api-key", apiKey);
                    request.Content = new StringContent(body.ToString(), Encoding.UTF8, "application/json");

                    using (var response = await http.SendAsync(request))
                    {
                        string json = await response.Content.ReadAsStringAsync();

                        if (response.IsSuccessStatusCode)
                        {
                            JObject result = JObject.Parse(json);
                            JArray parts = result.SelectToken("candidates[0].content.parts") as JArray;
                            string answer = parts == null ? "" : string.Join("\n", parts.Select(p => (string)p["text"]).Where(t => !string.IsNullOrWhiteSpace(t)));

                            if (!string.IsNullOrWhiteSpace(answer))
                            {
                                return Json(new { success = true, reply = answer });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("Gemini API Exception: " + ex.ToString());
            }

            // 3. Fallback thông minh tích hợp dữ liệu DB khi Gemini API không phản hồi
            string fallbackReply = GenerateFallbackReply(message, profile, roomList, promoList);
            return Json(new { success = true, reply = fallbackReply });
        }

        private string GenerateFallbackReply(string message, AIChatProfile profile, List<RoomInfo> rooms, List<PromoInfo> promos)
        {
            string norm = RemoveAccents(message);

            // 1. Hỏi về phòng / giá / loại phòng / phòng trống / số lượng phòng
            if (norm.Contains("phong"))
            {
                if (rooms != null && rooms.Any())
                {
                    // Kiểm tra xem khách hỏi cụ thể hạng phòng nào không (vd: phòng tổng thống, deluxe, suite, standard...)
                    var matchedRoom = rooms.FirstOrDefault(r => norm.Contains(RemoveAccents(r.Name)) || norm.Contains(RemoveAccents(r.Type)));
                    if (matchedRoom != null)
                    {
                        return $"Hạng phòng **{matchedRoom.Name}** ({matchedRoom.Type}) tại Luxury Hotel hiện có:\n\n" +
                               $"• **Còn trống hôm nay:** {matchedRoom.AvailableRoomsToday} phòng (trên tổng số {matchedRoom.TotalRooms} phòng)\n" +
                               $"• **Sức chứa:** {matchedRoom.Capacity} người\n" +
                               $"• **Giá phòng:** {matchedRoom.Price:N0} VNĐ / đêm\n" +
                               (string.IsNullOrEmpty(matchedRoom.Description) ? "" : $"• **Mô tả:** {matchedRoom.Description}\n\n") +
                               $"Quý khách có thể bấm nút **Đặt Phòng** ngay để giữ phòng!";
                    }

                    // Nếu hỏi tổng số phòng / phòng trống hôm nay
                    if (norm.Contains("bao nhieu") || norm.Contains("tong") || norm.Contains("con") || norm.Contains("trong") || norm.Contains("may phong"))
                    {
                        var breakdown = string.Join("\n", rooms.Select(r => $"- {r.Name}: **Còn trống {r.AvailableRoomsToday} phòng** (Tổng: {r.TotalRooms} phòng, Giá: {r.Price:N0} VNĐ)"));
                        return $"Tình trạng phòng trống hôm nay tại Luxury Hotel:\n\n{breakdown}\n\nQuý khách muốn đặt hạng phòng nào ạ?";
                    }

                    // Mặc định trả về danh sách kèm số lượng trống
                    var sb = new StringBuilder();
                    sb.AppendLine("Tình trạng phòng trống hôm nay tại Luxury Hotel:");
                    sb.AppendLine();
                    foreach (var r in rooms)
                    {
                        sb.AppendLine($"• **{r.Name}** ({r.Type}): **Còn trống {r.AvailableRoomsToday}/{r.TotalRooms} phòng** - Giá: {r.Price:N0} VNĐ/đêm");
                    }
                    return sb.ToString();
                }
            }

            // 2. Hỏi về Đặt phòng
            if (norm.Contains("dat phong") || norm.Contains("muon dat") || norm.Contains("huong dan dat"))
            {
                return "Để đặt phòng tại Luxury Hotel, quý khách vui lòng thực hiện các bước đơn giản sau:\n\n" +
                       "1️⃣ Truy cập mục **Đặt Phòng** trên thanh menu chính.\n" +
                       "2️⃣ Chọn loại phòng mong muốn, ngày nhận phòng (Check-in) và ngày trả phòng (Check-out).\n" +
                       "3️⃣ Nhập thông tin khách hàng và mã **Voucher / Khuyến mãi** (nếu có).\n" +
                       "4️⃣ Bấm **Xác nhận đặt phòng** và thanh toán bằng chuyển khoản QR Code hoặc Tiền mặt tại quầy.";
            }

            // 3. Hỏi về Voucher / Khuyến mãi
            if (norm.Contains("voucher") || norm.Contains("khuyen mai") || norm.Contains("ma giam") || norm.Contains("giam gia") || norm.Contains("uu dai"))
            {
                if (promos != null && promos.Any())
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("Hiện tại Luxury Hotel đang có các chương trình Khuyến mãi / Voucher ưu đãi:");
                    sb.AppendLine();
                    foreach (var p in promos)
                    {
                        sb.AppendLine($"🎁 Mã: **{p.Code}** - {p.Ten}");
                        sb.AppendLine($"   • Giảm **{p.Percent}%** (Tối đa {p.MaxGiam:N0} VNĐ cho đơn từ {p.MinDon:N0} VNĐ)");
                    }
                    sb.AppendLine();
                    sb.AppendLine("💡 Nhập mã voucher tại bước **Đặt phòng** để nhận ưu đãi giảm giá trực tiếp!");
                    return sb.ToString();
                }

                return "Quý khách có thể kiểm tra các mã Voucher ưu đãi mới nhất tại mục **Khuyến Mãi** trên website. Nhập mã tại bước Đặt phòng để được giảm giá trực tiếp!";
            }

            // 4. Hỏi về gọi món / dịch vụ phòng / đồ ăn
            if (norm.Contains("goi mon") || norm.Contains("thuc don") || norm.Contains("dich vu phong") || norm.Contains("do an") || norm.Contains("do uong") || norm.Contains("nha hang"))
            {
                return "Để gọi món ăn hoặc đồ uống phục vụ tận phòng tại Luxury Hotel:\n\n" +
                       "🛎️ Quý khách nhấn vào biểu tượng **Chuông dịch vụ phòng** ở góc dưới màn hình hoặc chọn mục **Dịch Vụ Phòng**.\n" +
                       "🍽️ Chọn các món ăn yêu thích và nhập số phòng của quý khách để nhân viên mang tới tận nơi!";
            }

            // 5. Hỏi về thanh toán / chuyển khoản / QR / lịch sử đơn
            if (norm.Contains("thanh toan") || norm.Contains("chuyen khoan") || norm.Contains("qr") || norm.Contains("lich su") || norm.Contains("xem don"))
            {
                return "Hướng dẫn Thanh toán & Xem lịch sử đặt phòng tại Luxury Hotel:\n\n" +
                       "💳 **Chuyển khoản QR:** Sau khi hoàn tất bấm đặt phòng, ứng dụng sẽ hiển thị mã QR VietQR/SePay. Quý khách chỉ cần quét mã để chuyển khoản, nhân viên sẽ kiểm tra và xác nhận đơn.\n" +
                       "📜 **Lịch sử đặt phòng:** Đăng nhập tài khoản và chọn **Lịch sử đặt phòng** trên menu để theo dõi trạng thái đơn của bạn.";
            }

            // 6. Trường hợp vai trò Admin
            if (string.Equals(profile.Role, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                if (norm.Contains("doanh thu") || norm.Contains("bao cao"))
                    return "Báo cáo doanh thu và thống kê hoạt động được hiển thị tại bảng điều khiển **Dashboard Admin**. Bạn có thể lọc theo khoảng thời gian để xem chi tiết.";
                if (norm.Contains("phan quyen") || norm.Contains("nhan vien"))
                    return "Để phân quyền nhân viên, bạn vào mục **Quản lý Nhân viên** trong Admin Panel, chọn nhân viên cần chỉnh sửa và gán các quyền nghiệp vụ tương ứng.";
                if (norm.Contains("phong") || norm.Contains("bao tri"))
                    return "Bạn có thể quản lý trạng thái phòng và tạo lịch bảo trì trong mục **Quản lý Phòng** tại trang Admin.";
            }

            // 7. Trường hợp vai trò Staff
            if (string.Equals(profile.Role, "Staff", StringComparison.OrdinalIgnoreCase))
            {
                if (norm.Contains("checkin") || norm.Contains("nhan phong"))
                    return "Quy trình Check-in: Kiểm tra thông tin đặt phòng của khách -> Đối soát CMND/CCCD -> Chọn phòng thực tế -> Tiến hành Check-in trên hệ thống.";
                if (norm.Contains("checkout") || norm.Contains("tra phong"))
                    return "Quy trình Check-out: Kiểm tra số phòng -> Kiểm tra dịch vụ phát sinh / đồ uống minibar -> Tiến hành thanh toán/Check-out trên hệ thống.";
            }

            // 8. Phản hồi chung mặc định
            return "Xin chào! Trợ lý Luxury Hotel có thể hỗ trợ bạn các thông tin sau:\n\n" +
                   "• 🛏️ Thông tin & giá các loại phòng hiện có\n" +
                   "• 📅 Hướng dẫn quy trình Đặt phòng trực tuyến\n" +
                   "• 🎁 Mã Voucher & Chương trình Khuyến mãi\n" +
                   "• 🍽️ Gọi món & Dịch vụ phục vụ tận phòng\n" +
                   "• 💳 Hướng dẫn thanh toán QR Code & Lịch sử đơn\n\n" +
                   "Bạn hãy bấm vào gợi ý nhanh bên dưới hoặc nhập câu hỏi cụ thể để tôi hỗ trợ nhé!";
        }

        private static string RemoveAccents(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            text = text.Normalize(NormalizationForm.FormD);
            var chars = text.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray();
            return new string(chars).Normalize(NormalizationForm.FormC).ToLowerInvariant();
        }

        private class RoomInfo
        {
            public int RoomId { get; set; }
            public string Name { get; set; }
            public string Type { get; set; }
            public decimal Price { get; set; }
            public int Capacity { get; set; }
            public string Description { get; set; }
            public int TotalRooms { get; set; }
            public int AvailableRoomsToday { get; set; }
        }

        private class PromoInfo
        {
            public string Code { get; set; }
            public string Ten { get; set; }
            public double Percent { get; set; }
            public decimal MaxGiam { get; set; }
            public decimal MinDon { get; set; }
        }
    }
}
