using System;
using System.Linq;
using System.Web.Helpers;
using System.Web.Mvc;
using QLKhachSan.Models;

namespace QLKhachSan.Filters
{
    // Runs before controller action filters, including legacy Session role checks.
    public sealed class HotelSecurityFilter : IAuthorizationFilter
    {
        internal static void ValidateCsrf(System.Web.HttpContextBase context)
        {
            string header = context.Request.Headers["X-CSRF-Token"];
            if (!string.IsNullOrEmpty(header))
                AntiForgery.Validate(context.Request.Cookies[AntiForgeryConfig.CookieName]?.Value, header);
            else AntiForgery.Validate();
        }
        public void OnAuthorization(AuthorizationContext context)
        {
            var session = context.HttpContext.Session;
            int id;
            if (session["UserId"] != null && int.TryParse(Convert.ToString(session["UserId"]), out id))
            {
                using (var db = new HotelDbContext())
                {
                    var user = db.Users.AsNoTracking().FirstOrDefault(u => u.UserId == id && u.IsActive);
                    if (user == null) session.Clear();
                    else
                    {
                        session["Role"] = (user.Role ?? "Customer").Trim();
                        session["Email"] = user.Email;
                        session["Permissions"] = db.UserPermissions.Where(p => p.UserId == id).Select(p => p.PermissionCode).ToList();
                    }
                }
            }
            else if (session["UserId"] != null) session.Clear();

            string controller = Convert.ToString(context.RouteData.Values["controller"]);
            string action = Convert.ToString(context.RouteData.Values["action"]);
            string role = Convert.ToString(session["Role"]);
            string permission = null;
            bool denied = false;
            if (controller.Equals("Admin", StringComparison.OrdinalIgnoreCase))
                denied = session["UserId"] == null || !role.Equals("Admin", StringComparison.OrdinalIgnoreCase);
            if (controller.Equals("Staff", StringComparison.OrdinalIgnoreCase))
            {
                denied = session["UserId"] == null || !role.Equals("Staff", StringComparison.OrdinalIgnoreCase);
                switch (action.ToLowerInvariant())
                {
                    case "getphysicalroomsmatrixbydate": permission = "rooms.view|rooms.manage"; break;
                    case "bookings": permission = "bookings.view|bookings.approve|bookings.checkout|payments.manage"; break;
                    case "todaycheckins": case "checkinbooking": permission = "bookings.checkin"; break;
                    case "checkoutbooking": permission = "bookings.checkout"; break;
                    case "createbooking": case "confirmbooking": case "cancelbooking": permission = "bookings.approve"; break;
                    case "confirmpayment": case "confirmfoodpayment": permission = "payments.manage"; break;
                    case "foodorders": permission = "restaurant.orders|restaurant.manage|payments.manage"; break;
                    case "updatefoodorderstatus": permission = "restaurant.orders|restaurant.manage"; break;
                }
            }
            if (controller.Equals("Restaurant", StringComparison.OrdinalIgnoreCase))
            {
                if (new[] { "ManageFoods", "AddFood", "EditFood", "ToggleFoodStatus" }.Contains(action, StringComparer.OrdinalIgnoreCase))
                    denied = session["UserId"] == null || !role.Equals("Admin", StringComparison.OrdinalIgnoreCase);
                else if (new[] { "AdminOrders", "UpdateOrderStatus", "UpdateOrderPayment" }.Contains(action, StringComparer.OrdinalIgnoreCase))
                {
                    denied = session["UserId"] == null || !(role.Equals("Admin", StringComparison.OrdinalIgnoreCase) || role.Equals("Staff", StringComparison.OrdinalIgnoreCase));
                    permission = action.Equals("UpdateOrderPayment", StringComparison.OrdinalIgnoreCase) ? "payments.manage" : "restaurant.orders|restaurant.manage";
                }
            }
            if (permission != null && !role.Equals("Admin", StringComparison.OrdinalIgnoreCase))
            {
                var permissions = session["Permissions"] as System.Collections.Generic.List<string>;
                denied |= permissions == null || !permission.Split('|').Any(permissions.Contains);
            }
            if (denied)
            {
                context.HttpContext.Response.TrySkipIisCustomErrors = true;
                context.Result = new HttpStatusCodeResult(session["UserId"] == null ? 401 : 403, "Không có quyền thực hiện thao tác.");
                return;
            }
            if (controller.Equals("OverdueJob", StringComparison.OrdinalIgnoreCase) &&
                action.Equals("Run", StringComparison.OrdinalIgnoreCase))
            {
                if (context.HttpContext.Request.HttpMethod != "POST" ||
                    !QLKhachSan.Services.OverdueBookingJob.IsValidToken(
                        context.HttpContext.Request.Headers["X-Job-Token"]))
                {
                    context.Result = new HttpStatusCodeResult(403);
                }
                return;
            }
            string method = context.HttpContext.Request.HttpMethod;
            if (method == "POST" || method == "PUT" || method == "DELETE" || method == "PATCH")
            {
                try
                {
                    ValidateCsrf(context.HttpContext);
                }
                catch (System.Web.Mvc.HttpAntiForgeryException)
                { context.Result = new HttpStatusCodeResult(400, "Phiên thao tác đã hết hạn. Vui lòng tải lại trang."); }
            }
        }
    }

    // Explicit action protection remains effective even without the global filter.
    public sealed class HotelAntiForgeryAttribute : FilterAttribute, IAuthorizationFilter
    {
        public void OnAuthorization(AuthorizationContext context)
        {
            try { HotelSecurityFilter.ValidateCsrf(context.HttpContext); }
            catch (HttpAntiForgeryException)
            {
                context.HttpContext.Response.TrySkipIisCustomErrors = true;
                context.Result = new HttpStatusCodeResult(400, "Invalid or expired request token.");
            }
        }
    }
}
