using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace QLKhachSan.Controllers
{
    public class AIChatController : Controller
    {
        // GET: AIChat
        public ActionResult Index()
        {
            if (Session["UserId"] != null)
            {
                string role = Convert.ToString(Session["Role"]);
                if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
                    return RedirectToAction("Index", "Admin", new { ai = 1 });
                if (string.Equals(role, "Staff", StringComparison.OrdinalIgnoreCase))
                    return RedirectToAction("Index", "Staff", new { ai = 1 });
            }
            return RedirectToAction("Index", "Home", new { ai = 1 });
        }
    }
}
