using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using QLKhachSan.Models;

namespace QLKhachSan.Controllers

{
    public class HomeController : Controller

    {
        private HotelDbContext db = new HotelDbContext();
        public ActionResult Index()
        {
            var rooms = db.Rooms
                          .Where(r => r.IsActive)
                          .ToList();

            return View(rooms);
        }
        public ActionResult Register()
        {
            return RedirectToAction("Index", "Home", new { registerRequired = 1 });
        }
        public ActionResult About()
        {
            ViewBag.RoomTypeCount = db.Rooms.Count(r => r.IsActive);
            ViewBag.PhysicalRoomCount = db.PhysicalRooms.Count(p => p.IsActive && p.RoomType.IsActive);

            return View();
        }

        public ActionResult Contact()
        {
            return View();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
