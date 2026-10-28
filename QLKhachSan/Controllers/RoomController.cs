using QLKhachSan.Models;
using System.Linq;
using System.Web.Mvc;

namespace QLKhachSan.Controllers
{
    public class RoomController : Controller
    {
        // Kết nối database
        private readonly HotelDbContext db = new HotelDbContext();

        // =========================================================
        // DANH SÁCH PHÒNG (DANH SÁCH THƯỜNG HOẶC LỌC PHÒNG LUXURY)
        // URL ví dụ:
        // /Room
        // /Room?isLuxury=true
        // =========================================================
        public ActionResult Index(bool? isLuxury)
        {
            // Lấy tất cả các phòng đang hoạt động
            var query = db.Rooms.Where(r => r.IsActive);

            if (isLuxury.HasValue && isLuxury.Value)
            {
                // Danh sách các từ khóa phòng hạng cao cấp
                var luxuryKeywords = new[] { "Deluxe", "Premium", "Balcony", "Suite", "Tổng thống", "President" };

                // Chỉ lấy các phòng thuộc danh mục cao cấp (Deluxe, Premium, Balcony, Suite, Tổng thống/President)
                // Loại bỏ hoàn toàn các phòng tiêu chuẩn/thường (Standard, Superior...)
                query = query.Where(r => luxuryKeywords.Any(k =>
                    (r.RoomType != null && r.RoomType.Contains(k)) ||
                    (r.RoomName != null && r.RoomName.Contains(k))
                ));

                ViewBag.Title = "Danh Sách Phòng LUXURY Cao Cấp";
                ViewBag.IsLuxury = true;
            }
            else
            {
                ViewBag.Title = "Danh Sách Tất Cả Các Phòng";
                ViewBag.IsLuxury = false;
            }

            var rooms = query.OrderByDescending(r => r.Price).ToList();
            return View(rooms);
        }

        // =========================================================
        // TRANG CHI TIẾT PHÒNG
        // URL ví dụ:
        // /Room/Details/1
        // =========================================================

        public ActionResult Details(int id)
        {
            // Tìm đúng phòng theo RoomId
            var room = db.Rooms.FirstOrDefault(r =>
                r.RoomId == id &&
                r.IsActive
            );

            // Nếu không tìm thấy phòng
            if (room == null)
            {
                return HttpNotFound();
            }

            // Gửi thông tin phòng sang
            // Views/Room/Details.cshtml
            return View(room);
        }

        // =========================================================
        // GIẢI PHÓNG KẾT NỐI DATABASE
        // =========================================================

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
