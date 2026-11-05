using System;
using System.Web.Mvc;
using QLKhachSan.Services;

namespace QLKhachSan.Controllers
{
    // Machine-to-machine scheduled endpoint, NOT a customer or admin page.
    // Header X-Job-Token is validated in HotelSecurityFilter before CSRF bypass.
    public sealed class OverdueJobController : Controller
    {
        [HttpPost]
        public ActionResult Run()
        {
            Response.Cache.SetCacheability(System.Web.HttpCacheability.NoCache);
            if (!OverdueBookingJob.IsValidToken(Request.Headers["X-Job-Token"]))
                return new HttpStatusCodeResult(403);
            try
            {
                var result = OverdueBookingJob.Run();
                return Json(new { success = true, cancelled = result.Cancelled,
                    paidNeedsReview = result.PaidReview });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("Overdue booking job failed: " + ex);
                Response.StatusCode = 500;
                Response.TrySkipIisCustomErrors = true;
                return Json(new { success = false, message = "Tác vụ tự hủy không hoàn tất. Kiểm tra log máy chủ." });
            }
        }
    }
}
