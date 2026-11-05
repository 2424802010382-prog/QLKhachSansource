using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using QLKhachSan.Models;
using QLKhachSan.Migrations;

namespace QLKhachSan
{
    public class MvcApplication : System.Web.HttpApplication
    {
        private static System.Threading.Timer _overdueJobTimer;

        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);

            Database.SetInitializer<HotelDbContext>(null);

            // Start background timer to check overdue check-in bookings every 15 minutes
            _overdueJobTimer = new System.Threading.Timer(_ =>
            {
                try
                {
                    using (var db = new HotelDbContext())
                    {
                        OverdueBookingService.ProcessOverdueBookings(db);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("OverdueJobTimer Error: " + ex.Message);
                }
            }, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(15));
        }

        protected void Application_Error(object sender, EventArgs e)
        {
            try
            {
                var ex = Server.GetLastError();
                if (ex == null) return;

                string logDir = HttpContext.Current.Server.MapPath("~/App_Data/logs");
                if (!System.IO.Directory.Exists(logDir))
                {
                    System.IO.Directory.CreateDirectory(logDir);
                }

                string logFile = System.IO.Path.Combine(logDir, "error_" + DateTime.UtcNow.ToString("yyyyMMdd") + ".log");

                string message = SanitizeLogContent(ex.Message);
                string stackTrace = SanitizeLogContent(ex.StackTrace);
                string exType = ex.GetType().FullName;

                string routeInfo = "Unknown";
                try
                {
                    var httpContext = new HttpContextWrapper(HttpContext.Current);
                    var routeData = RouteTable.Routes.GetRouteData(httpContext);
                    if (routeData != null)
                    {
                        string ctrl = routeData.Values["controller"] as string ?? "";
                        string act = routeData.Values["action"] as string ?? "";
                        routeInfo = $"{ctrl}/{act}";
                    }
                }
                catch { }

                string logEntry = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC] | Type: {exType} | Route: {routeInfo} | Msg: {message}\nStack: {stackTrace}\n--------------------------------------------------\n";

                System.IO.File.AppendAllText(logFile, logEntry, System.Text.Encoding.UTF8);
            }
            catch
            {
                // Never throw from Application_Error
            }
        }

        private static string SanitizeLogContent(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            input = System.Text.RegularExpressions.Regex.Replace(input, @"(Data Source|Server|Password|Pwd|User ID|UID|Token|ApiKey|Provider Connection String)=[^;]+", "$1=***", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            return input;
        }
    }
}
