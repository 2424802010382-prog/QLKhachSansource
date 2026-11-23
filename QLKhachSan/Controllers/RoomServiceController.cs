using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using QLKhachSan.Models;
using QLKhachSan.Services;

namespace QLKhachSan.Controllers
{
    public class RoomServiceController : Controller
    {
        private readonly HotelDbContext db = new HotelDbContext();
        private int Actor { get { int id; return int.TryParse(Convert.ToString(Session["UserId"]), out id) ? id : 0; } }
        private bool Admin { get { return Actor > 0 && Convert.ToString(Session["Role"]) == "Admin"; } }
        private bool Staff { get { return Actor > 0 && Convert.ToString(Session["Role"]) == "Staff"; } }
        private bool Manage { get { return Admin || Staff; } }
        private bool CanPay { get { return Staff && (Session["Permissions"] as List<string> ?? new List<string>()).Any(p => p == "payments.manage" || p == "services.manage" || p == "roomservice.manage"); } }
        private const string CatalogSql = "SELECT d.*,c.Code,c.ImageUrl,c.Timing FROM dbo.DichVu d INNER JOIN dbo.RoomServiceCatalog c ON c.MaDichVu=d.MaDichVu";
        private const string OrdersSql = "SELECT o.*,p.RoomNumber,b.CustomerName FROM dbo.RoomServiceOrders o INNER JOIN dbo.PhysicalRooms p ON p.PhysicalRoomId=o.PhysicalRoomId INNER JOIN dbo.Bookings b ON b.BookingId=o.BookingId";
        private static readonly object DiagnosticLogLock = new object();
        public sealed class SchemaTableProbe
        {
            public string TableName { get; set; }
            public int? ObjectId { get; set; }
            public int? CanSelect { get; set; }
        }
        public sealed class DatabaseProbe
        {
            public string DatabaseName { get; set; }
            public int? CanViewDefinition { get; set; }
        }
        private void LogSchema(string databaseName, string state, string missing, string denied, System.Data.SqlClient.SqlException error = null)
        {
            // Never log ConnectionString, credentials, query parameters or exception messages.
            Func<string, string> safe = value => (value ?? "").Replace("\r", " ").Replace("\n", " ");
            string line = DateTime.UtcNow.ToString("o") + " RoomServiceSchema State=" + state +
                "; DatabaseName=" + safe(databaseName) + "; DataSource=" + safe(db.Database.Connection.DataSource) +
                "; MissingOrInvisible=" + safe(missing) + "; SelectDenied=" + safe(denied) +
                (error == null ? "" : "; ErrorType=SqlException; Number=" + error.Number + "; Class=" + error.Class + "; SqlState=" + error.State);
            System.Diagnostics.Trace.TraceInformation(line);
            try
            {
                string folder = System.Web.Hosting.HostingEnvironment.MapPath("~/App_Data/logs");
                if (folder == null) return; // Standalone read-only probes use Trace instead.
                lock (DiagnosticLogLock)
                {
                    System.IO.Directory.CreateDirectory(folder);
                    System.IO.File.AppendAllText(System.IO.Path.Combine(folder, "roomservice-schema-" + DateTime.UtcNow.ToString("yyyyMMdd") + ".log"), line + Environment.NewLine, System.Text.Encoding.UTF8);
                }
            }
            catch (Exception logError)
            {
                System.Diagnostics.Trace.TraceWarning("RoomService diagnostic file unavailable: {0}", logError.GetType().Name);
            }
        }
        private static bool schemaReadyCache = false;
        private static DateTime lastSchemaCheck = DateTime.MinValue;
        protected override void OnActionExecuting(ActionExecutingContext context)
        {
            if (schemaReadyCache && (DateTime.UtcNow - lastSchemaCheck).TotalMinutes < 10)
            {
                base.OnActionExecuting(context);
                return;
            }
            string databaseName = db.Database.Connection.Database;
            try
            {
                // Check object existence and read permission, NEVER row counts. An empty bed policy is valid.
                var identity = db.Database.SqlQuery<DatabaseProbe>(@"SELECT DB_NAME() DatabaseName,
                    HAS_PERMS_BY_NAME(DB_NAME(),'DATABASE','VIEW DEFINITION') CanViewDefinition").Single();
                databaseName = identity.DatabaseName;
                var tables = db.Database.SqlQuery<SchemaTableProbe>(@"SELECT t.TableName,
                    OBJECT_ID(N'dbo.' + t.TableName, N'U') ObjectId,
                    HAS_PERMS_BY_NAME(N'dbo.' + t.TableName, 'OBJECT', 'SELECT') CanSelect
                    FROM (VALUES ('RoomServiceCatalog'),('RoomServiceProducts'),('RoomServiceBedPolicy'),
                    ('RoomServiceOrders'),('RoomServiceHistory')) t(TableName)").ToList();
                var missing = tables.Where(t => !t.ObjectId.HasValue).Select(t => "dbo." + t.TableName).ToList();
                var denied = tables.Where(t => t.ObjectId.HasValue && t.CanSelect != 1).Select(t => "dbo." + t.TableName).ToList();
                string state = missing.Count > 0 ? (identity.CanViewDefinition == 1 ? "SchemaMissing" : "SchemaMissingOrInvisible") :
                    denied.Count > 0 ? "SchemaReadDenied" : "Ready";
                LogSchema(databaseName, state, string.Join(",", missing), string.Join(",", denied));
                if (state != "Ready") { context.Result = SchemaUnavailable(state); return; }
                schemaReadyCache = true;
                lastSchemaCheck = DateTime.UtcNow;
            }
            catch (System.Data.SqlClient.SqlException error)
            {
                LogSchema(databaseName, "DatabaseCheckFailed", "unknown", "unknown", error);
                context.Result = SchemaUnavailable("DatabaseCheckFailed");
                return;
            }
            base.OnActionExecuting(context);
        }
        private ActionResult SchemaUnavailable(string state)
        {
                Response.StatusCode = 503;
                Response.TrySkipIisCustomErrors = true;
                Response.Cache.SetCacheability(System.Web.HttpCacheability.NoCache);
                Response.AddHeader("X-RoomService-State", state);
                return Content(
                    "<!doctype html><html lang=\"vi\"><meta charset=\"utf-8\">" +
                    "<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">" +
                    "<title>Dịch vụ phòng chưa sẵn sàng - Luxury Hotel</title>" +
                    "<body style=\"font-family:Arial,sans-serif;background:#f8f5ee;color:#153044;padding:24px\">" +
                    "<main style=\"max-width:680px;margin:40px auto;background:white;padding:28px;border-radius:16px\">" +
                    "<h1>Dịch vụ phòng chưa sẵn sàng</h1>" +
                    "<p>Chưa thể truy cập đầy đủ dữ liệu dịch vụ phòng. Vui lòng liên hệ lễ tân nếu cần hỗ trợ dịch vụ.</p>" +
                    "<p>Các chức năng khác của website vẫn có thể sử dụng bình thường.</p>" +
                    "<a href=\"" + System.Web.HttpUtility.HtmlAttributeEncode(System.Web.VirtualPathUtility.ToAbsolute("~/", Request.ApplicationPath ?? "/")) +
                    "\">Về trang chủ Luxury Hotel</a></main></body></html>", "text/html", System.Text.Encoding.UTF8);
        }
        private ActionResult Login() { return RedirectToAction("Index", "Home", new { loginRequired = 1, returnUrl = Request.RawUrl }); }
        private RoomServiceItem Item(int id) { return db.Database.SqlQuery<RoomServiceItem>(CatalogSql + " WHERE d.MaDichVu=@p0", id).SingleOrDefault(); }
        private RoomServiceOrder Order(int id) { return db.Database.SqlQuery<RoomServiceOrder>(OrdersSql + " WHERE o.OrderId=@p0", id).SingleOrDefault(); }
        private bool Own(RoomServiceOrder o) { return o != null && Actor > 0 && (Manage || o.UserId == Actor); }
        private void Event(int id, string message)
        {
            db.Database.ExecuteSqlCommand("INSERT dbo.RoomServiceHistory(OrderId,ActorId,CreatedAt,Message) VALUES(@p0,@p1,@p2,@p3)", id, Actor, DateTime.Now, message);
        }
        [HttpGet] public ActionResult Index(string q, string timing)
        {
            var items = db.Database.SqlQuery<RoomServiceItem>(CatalogSql).ToList();
            if (!Admin) items = items.Where(x => x.HoatDong).ToList();
            if (!string.IsNullOrWhiteSpace(q)) items = items.Where(x => x.TenDichVu.IndexOf(q, StringComparison.CurrentCultureIgnoreCase) >= 0).ToList();
            if (!string.IsNullOrEmpty(timing)) items = items.Where(x => x.Timing == timing).ToList();
            ViewBag.Admin = Admin; ViewBag.Manage = Manage;
            return View(items);
        }
        [HttpGet] public ActionResult Details(int id)
        {
            var item = Item(id);
            if (item == null || (!item.HoatDong && !Admin)) return HttpNotFound();
            ViewBag.Products = db.Database.SqlQuery<RoomServiceProduct>("SELECT * FROM dbo.RoomServiceProducts WHERE MaDichVu=@p0 AND Active=1", id).ToList();
            return View(item);
        }
        private int Owner(int bookingId)
        {
            return db.Database.SqlQuery<int?>("SELECT UserId FROM dbo.Bookings WHERE BookingId=@p0", bookingId).SingleOrDefault() ?? 0;
        }
        private bool Eligible(Booking booking, RoomServiceItem service, DateTime at, DateTime now)
        {
            decimal paid = booking == null ? 0 : db.ThanhToans.Where(x => x.BookingId == booking.BookingId && x.TrangThai == "Đã thanh toán").Select(x => (decimal?)x.SoTien).Sum() ?? 0;
            return RoomServiceRules.Eligible(booking, service.Timing, paid, now, at);
        }
        private List<RoomServiceRoom> Rooms(RoomServiceItem service)
        {
            var ids = db.Database.SqlQuery<int>("SELECT BookingId FROM dbo.Bookings WHERE UserId IS NOT NULL AND (@p0=1 OR UserId=@p1)", Manage ? 1 : 0, Actor).ToList();
            var bookings = db.Bookings.AsNoTracking().Where(x => ids.Contains(x.BookingId) && (x.Status == "Đang ở" || x.Status == "Đã xác nhận")).ToList();
            var physical = db.PhysicalRooms.AsNoTracking().Where(x => x.IsActive).ToList();
            var result = new List<RoomServiceRoom>();
            DateTime now = DateTime.Now;
            foreach (var b in bookings)
            {
                DateTime at = now.AddMinutes(15);
                if (at.Date < b.CheckInDate.Date) at = b.CheckInDate.Date.AddHours(14);
                if (service.Timing == "Luggage" && at.Date != b.CheckInDate.Date && at.Date < b.CheckOutDate.Date) at = b.CheckOutDate.Date.AddHours(9);
                if (!Eligible(b, service, at, now)) continue;
                foreach (var p in physical.Where(p => p.RoomTypeId == b.RoomId && PhysicalRoomHelper.IsBookingAssignedToRoom(b.SpecialRequest, p.RoomNumber.Trim())))
                    result.Add(new RoomServiceRoom { BookingId=b.BookingId, PhysicalRoomId=p.PhysicalRoomId, UserId=Owner(b.BookingId), Label="#"+b.BookingId+" • Phòng "+p.RoomNumber+" • "+b.CheckInDate.ToString("dd/MM")+"–"+b.CheckOutDate.ToString("dd/MM/yyyy")+(Manage ? " • "+b.CustomerName : "") });
            }
            return result;
        }
        [HttpGet] public ActionResult Create(int id)
        {
            if (Actor == 0) return Login();
            var item = Item(id);
            if (item == null || !item.HoatDong) return HttpNotFound();
            return View(new RoomServiceRequest { ServiceId=id, Quantity=1, UseAt=DateTime.Now.AddHours(1), RequestKey=Guid.NewGuid(), Service=item, Rooms=Rooms(item), Products=db.Database.SqlQuery<RoomServiceProduct>("SELECT * FROM dbo.RoomServiceProducts WHERE MaDichVu=@p0 AND Active=1", id).ToList() });
        }
        [HttpPost, ValidateAntiForgeryToken] public ActionResult Create(RoomServiceRequest request)
        {
            if (Actor == 0) return new HttpStatusCodeResult(401);
            if (!ModelState.IsValid || request == null || request.RequestKey == Guid.Empty || request.Quantity < 1 || request.Quantity > 30)
                return new HttpStatusCodeResult(400, "Invalid request");
            using (var tx = db.Database.BeginTransaction())
            {
                HotelOperations.LockInventory(db);
                var existing = db.Database.SqlQuery<int>("SELECT OrderId FROM dbo.RoomServiceOrders WHERE CreatedBy=@p0 AND RequestKey=@p1", Actor, request.RequestKey).FirstOrDefault();
                if (existing > 0) { tx.Commit(); return RedirectToAction("OrderDetails", new { id=existing }); }
                var item = Item(request.ServiceId);
                var b = db.Bookings.AsNoTracking().SingleOrDefault(x => x.BookingId == request.BookingId);
                var room = db.PhysicalRooms.AsNoTracking().SingleOrDefault(x => x.PhysicalRoomId == request.PhysicalRoomId);
                int owner = Owner(request.BookingId);
                if (item == null || !item.HoatDong || !RoomServiceRules.CanUseRoom(b, room, owner, Actor, Manage) || !Eligible(b, item, request.UseAt, DateTime.Now))
                    return new HttpStatusCodeResult(400, "Booking, room or service time is not eligible");
                decimal price = item.Gia;
                string name = item.TenDichVu;
                if (item.Code == "MINIBAR")
                {
                    var product = db.Database.SqlQuery<RoomServiceProduct>("SELECT * FROM dbo.RoomServiceProducts WHERE ProductId=@p0 AND MaDichVu=@p1 AND Active=1", request.ProductId ?? 0, item.MaDichVu).SingleOrDefault();
                    if (product == null) return new HttpStatusCodeResult(400, "Select a priced minibar product");
                    price = product.Price; name += " • " + product.Name;
                }
                else request.ProductId = null;
                if (item.Code == "EXTRA_BED")
                {
                    int limit = db.Database.SqlQuery<int>("SELECT MaxExtraBeds FROM dbo.RoomServiceBedPolicy WHERE RoomId=@p0", b.RoomId).FirstOrDefault();
                    var end = request.UseAt.Date.AddDays(request.Quantity);
                    int used = db.Database.SqlQuery<int>(@"SELECT COUNT(*) FROM dbo.RoomServiceOrders WHERE BookingId=@p0 AND PhysicalRoomId=@p1 AND ServiceId=@p2 AND Status<>N'Từ chối' AND UseAt<@p3 AND DATEADD(day,Quantity,CAST(UseAt AS date))>@p4", b.BookingId, room.PhysicalRoomId, item.MaDichVu, end, request.UseAt.Date).Single();
                    if (limit <= used || end > b.CheckOutDate.Date) return new HttpStatusCodeResult(400, "Extra bed capacity or number of nights is invalid");
                }
                if (price < 0 || price > 100000000m / request.Quantity || decimal.Truncate(price) != price) return new HttpStatusCodeResult(400, "Invalid price");
                int id = db.Database.SqlQuery<int>(@"INSERT dbo.RoomServiceOrders(BookingId,PhysicalRoomId,UserId,ServiceId,ProductId,ServiceName,Unit,Timing,Quantity,UnitPrice,Total,CreatedAt,UseAt,Note,PaymentStatus,Status,RequestKey,CreatedBy)
                    OUTPUT INSERTED.OrderId VALUES(@p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8,@p9,@p10,@p11,@p12,@p13,@p14,N'Chờ xác nhận',@p15,@p16)",
                    b.BookingId, room.PhysicalRoomId, owner, item.MaDichVu, (object)request.ProductId ?? DBNull.Value, name, item.DonVi, item.Timing, request.Quantity, price, price*request.Quantity, DateTime.Now, request.UseAt, (object)request.Note ?? DBNull.Value, price == 0 ? "Miễn phí" : "Chờ thanh toán", request.RequestKey, Actor).Single();
                Event(id, Manage ? "Nhân viên tạo yêu cầu hộ khách." : "Khách tạo yêu cầu dịch vụ.");
                tx.Commit(); return RedirectToAction("OrderDetails", new { id });
            }
        }
        [HttpGet] public ActionResult Orders(string room, string status)
        {
            if (Actor == 0) return Login();
            var rows = db.Database.SqlQuery<RoomServiceOrder>(OrdersSql+" WHERE (@p0=1 OR o.UserId=@p1) AND (@p2='' OR p.RoomNumber=@p2) AND (@p3='' OR o.Status=@p3) ORDER BY o.CreatedAt DESC", Manage ? 1 : 0, Actor, room ?? "", status ?? "").ToList();
            ViewBag.Manage = Manage; ViewBag.Admin = Admin;
            return View(rows);
        }
        [HttpGet] public JsonResult Updates()
        {
            if (Actor == 0) { Response.StatusCode=401; return Json(new { count=0 },JsonRequestBehavior.AllowGet); }
            int count=db.Database.SqlQuery<int>(@"SELECT COUNT(*) FROM dbo.RoomServiceHistory h INNER JOIN dbo.RoomServiceOrders o ON o.OrderId=h.OrderId WHERE (@p0=1 OR o.UserId=@p1)",Manage ? 1 : 0,Actor).Single();
            Response.Cache.SetCacheability(System.Web.HttpCacheability.NoCache);
            return Json(new { count },JsonRequestBehavior.AllowGet);
        }
        [HttpGet] public ActionResult OrderDetails(int id)
        {
            if (Actor == 0) return Login();
            var o = Order(id); if (!Own(o)) return HttpNotFound();
            ViewBag.Manage=Manage; ViewBag.CanProcess=Staff; ViewBag.CanPay=CanPay;
            ViewBag.Events=db.Database.SqlQuery<RoomServiceEvent>("SELECT CreatedAt,ActorId,Message FROM dbo.RoomServiceHistory WHERE OrderId=@p0 ORDER BY EventId", id).ToList();
            var settings = BankPaymentSettings.Load();
            string content = BankPaymentSettings.Content("RoomService", id);
            ViewBag.Bank = settings;
            ViewBag.PaymentContent = content;
            ViewBag.QrUrl = o.Total > 0 && o.PaymentStatus != "Đã thanh toán" && o.Status != "Từ chối" ? settings.QrUrl(content, o.Total) : null;
            return View(o);
        }
        [HttpGet] public ActionResult Success(int id)
        {
            if (Actor == 0) return Login();
            var order = Order(id);
            if (!Own(order)) return HttpNotFound();
            Response.Cache.SetCacheability(System.Web.HttpCacheability.NoCache);
            Response.Cache.SetNoStore();
            if (order.PaymentStatus != "Đã thanh toán")
                return RedirectToAction("OrderDetails", new { id });
            return View(order);
        }
        [HttpGet] public JsonResult CheckPaymentStatus(int id)
        {
            if (Actor == 0) return Json(new { success = false }, JsonRequestBehavior.AllowGet);
            var o = Order(id);
            if (!Own(o)) return Json(new { success = false }, JsonRequestBehavior.AllowGet);
            bool isPaid = o.PaymentStatus == "Đã thanh toán";
            bool canPay = o.Status != "Từ chối" && !isPaid;
            bool needsReview = SePayPaymentService.NeedsReview(db, "RoomService", id);
            Response.Cache.SetCacheability(System.Web.HttpCacheability.NoCache);
            return Json(new { success = true, isPaid, canPay, outstandingAmount = o.Total, needsReview }, JsonRequestBehavior.AllowGet);
        }
        [HttpPost] public JsonResult RequestBankTransferReview(int id)
        {
            if (Actor == 0) return Json(new { success = false, message = "Chưa đăng nhập." }, JsonRequestBehavior.AllowGet);
            using (var tx = db.Database.BeginTransaction())
            {
                HotelOperations.LockInventory(db);
                var o = Order(id);
                if (!Own(o)) return Json(new { success = false, message = "Không tìm thấy đơn." }, JsonRequestBehavior.AllowGet);
                if (o.PaymentStatus == "Đã thanh toán" || o.Status == "Từ chối")
                    return Json(new { success = false, message = "Đơn không ở trạng thái chờ xác minh thanh toán." }, JsonRequestBehavior.AllowGet);
                db.Database.ExecuteSqlCommand("UPDATE dbo.RoomServiceOrders SET PaymentStatus=N'Chờ xác minh thanh toán' WHERE OrderId=@p0", id);
                Event(id, "Khách yêu cầu đối soát chuyển khoản; chưa xác nhận đã nhận tiền.");
                tx.Commit();
                return Json(new { success = true, message = "Đã gửi yêu cầu. Nhân viên sẽ đối soát giao dịch trước khi xác nhận." }, JsonRequestBehavior.AllowGet);
            }
        }
        [HttpPost, ValidateAntiForgeryToken] public ActionResult ReportPayment(int id)
        {
            using (var tx=db.Database.BeginTransaction())
            {
                HotelOperations.LockInventory(db);
                var o=Order(id); if (!Own(o)) return HttpNotFound();
                if (o.Total>0 && o.Status!="Từ chối" && o.PaymentStatus=="Chờ thanh toán")
                {
                    db.Database.ExecuteSqlCommand("UPDATE dbo.RoomServiceOrders SET PaymentStatus=N'Chờ xác minh thanh toán' WHERE OrderId=@p0",id);
                    Event(id,"Khách báo chuyển khoản; chưa xác nhận nhận tiền.");
                }
                tx.Commit(); return RedirectToAction("OrderDetails",new {id});
            }
        }
        [HttpPost, ValidateAntiForgeryToken] public ActionResult ConfirmPayment(int id, string evidence, decimal amount, bool verified=false)
        {
            if (!CanPay) return new HttpStatusCodeResult(403);
            evidence=(evidence ?? "").Trim();
            using(var tx=db.Database.BeginTransaction())
            {
                HotelOperations.LockInventory(db);
                var o=Order(id);
                if(o==null || o.Status=="Từ chối" || o.Total<=0 || o.PaymentStatus=="Đã thanh toán" || !verified || amount!=o.Total || evidence.Length<3 || evidence.Length>200)
                    return new HttpStatusCodeResult(400,"Verify bank receipt, exact amount and reference first");
                int used=db.Database.SqlQuery<int>(@"SELECT (SELECT COUNT(*) FROM dbo.RoomServiceOrders WHERE Evidence=@p0)+(SELECT COUNT(*) FROM dbo.ThanhToan WHERE MaGiaoDich=@p0)+(SELECT COUNT(*) FROM dbo.DonDatMon WHERE PaymentReference=@p0)",evidence).Single();
                int sepay=db.Database.SqlQuery<int>(@"IF OBJECT_ID('dbo.SePayTransactions','U') IS NULL SELECT 0 ELSE SELECT COUNT(*) FROM dbo.SePayTransactions WHERE Status='Applied' AND (BankReference=@p0 OR 'SEPAY:'+CONVERT(varchar(30),SePayId)=@p0)",evidence).Single();
                if(used+sepay>0) return new HttpStatusCodeResult(409,"Payment reference already used");
                db.Database.ExecuteSqlCommand("UPDATE dbo.RoomServiceOrders SET PaymentStatus=N'Đã thanh toán',Evidence=@p0,PaidAt=@p1,PaidBy=@p2 WHERE OrderId=@p3",evidence,DateTime.Now,Actor,id);
                Event(id,"Đã đối soát và nhận đủ tiền. Chứng từ: "+evidence);
                tx.Commit(); return RedirectToAction("OrderDetails",new {id});
            }
        }
        [HttpPost, ValidateAntiForgeryToken] public ActionResult Transition(int id,string status,string reason)
        {
            if(!Staff) return new HttpStatusCodeResult(403, "Chỉ Nhân viên mới có quyền cập nhật trạng thái xử lý đơn dịch vụ.");
            using(var tx=db.Database.BeginTransaction())
            {
                HotelOperations.LockInventory(db);
                var o=Order(id);
                if(o==null || !RoomServiceRules.CanTransition(o.Status,status)) return new HttpStatusCodeResult(409,"Invalid transition");
                reason=(reason ?? "").Trim();
                if(reason.Length>800 || (status=="Từ chối" && (reason.Length==0 || o.PaymentStatus=="Đã thanh toán"))) return new HttpStatusCodeResult(400,"Reason required; paid orders require refund review");
                if(status!="Từ chối")
                {
                    var b=db.Bookings.AsNoTracking().SingleOrDefault(x=>x.BookingId==o.BookingId);
                    var p=db.PhysicalRooms.AsNoTracking().SingleOrDefault(x=>x.PhysicalRoomId==o.PhysicalRoomId);
                    var now=DateTime.Now; var at=o.UseAt>now ? o.UseAt : now;
                    if(b==null || p==null || !p.IsActive || p.RoomTypeId!=b.RoomId || !PhysicalRoomHelper.IsBookingAssignedToRoom(b.SpecialRequest,p.RoomNumber.Trim()) || !Eligible(b,new RoomServiceItem{Timing=o.Timing},at,now)) return new HttpStatusCodeResult(409,"Stay is no longer eligible");
                    if(status=="Hoàn thành" && (o.UseAt>DateTime.Now || (o.Total>0 && o.PaymentStatus!="Đã thanh toán"))) return new HttpStatusCodeResult(409,"Service time or payment incomplete");
                }
                db.Database.ExecuteSqlCommand("UPDATE dbo.RoomServiceOrders SET Status=@p0 WHERE OrderId=@p1",status,id);
                Event(id,status+(reason.Length>0 ? ": "+reason : ""));
                tx.Commit(); return RedirectToAction("OrderDetails",new {id});
            }
        }
        [HttpGet] public ActionResult Edit(int id=0)
        {
            if(!Admin) return new HttpStatusCodeResult(403);
            var item=id==0 ? new RoomServiceItem{HoatDong=true,Timing="Stay",ImageUrl="/Images/room-service.svg"} : Item(id);
            if(item==null) return HttpNotFound();
            ViewBag.RoomTypes=db.Rooms.AsNoTracking().ToList();
            ViewBag.Products=db.Database.SqlQuery<RoomServiceProduct>("SELECT * FROM dbo.RoomServiceProducts WHERE MaDichVu=@p0",id).ToList();
            return View(item);
        }
        [HttpPost, ValidateAntiForgeryToken] public ActionResult Edit(int id,string name,string description,decimal price,string unit,string imageUrl,bool active=false)
        {
            if(!Admin) return new HttpStatusCodeResult(403);
            if(string.IsNullOrWhiteSpace(name)||name.Length>100||string.IsNullOrWhiteSpace(unit)||unit.Length>50||(description??"").Length>500||price<0||price>100000000||decimal.Truncate(price)!=price||!ModelState.IsValid)
                return new HttpStatusCodeResult(400,"Invalid catalog values");
            imageUrl=(imageUrl??"").Trim();
            if(imageUrl.Length>500 || !(imageUrl.StartsWith("/Images/",StringComparison.Ordinal) && !imageUrl.Contains("..") && !imageUrl.Contains("\\"))) return new HttpStatusCodeResult(400,"Use a local /Images/ asset path");
            using(var tx=db.Database.BeginTransaction())
            {
                HotelOperations.LockInventory(db);
                if(id==0)
                {
                    id=db.Database.SqlQuery<int>("INSERT dbo.DichVu(TenDichVu,MoTa,Gia,DonVi,HoatDong) OUTPUT INSERTED.MaDichVu VALUES(@p0,@p1,@p2,@p3,@p4)",name,description??"",price,unit,active).Single();
                    db.Database.ExecuteSqlCommand("INSERT dbo.RoomServiceCatalog VALUES(@p0,@p1,@p2,'Stay')",id,"CUSTOM_"+id,imageUrl);
                }
                else
                {
                    var item=Item(id); if(item==null) return HttpNotFound();
                    if((item.Code=="LUGGAGE"||item.Code=="WAKEUP"||item.Code=="MINIBAR") && price!=0) return new HttpStatusCodeResult(400,"This service must retain a zero base price");
                    db.Database.ExecuteSqlCommand("UPDATE dbo.DichVu SET TenDichVu=@p0,MoTa=@p1,Gia=@p2,DonVi=@p3,HoatDong=@p4 WHERE MaDichVu=@p5",name,description??"",price,unit,active,id);
                    db.Database.ExecuteSqlCommand("UPDATE dbo.RoomServiceCatalog SET ImageUrl=@p0 WHERE MaDichVu=@p1",imageUrl,id);
                }
                tx.Commit(); return RedirectToAction("Index");
            }
        }
        [HttpPost, ValidateAntiForgeryToken] public ActionResult SaveProduct(int serviceId,int productId,string name,decimal price,bool active=false)
        {
            if(!Admin) return new HttpStatusCodeResult(403);
            var item=Item(serviceId);
            if(item==null || item.Code!="MINIBAR" || string.IsNullOrWhiteSpace(name) || name.Length>150 || price<=0 || price>100000000 || price!=decimal.Truncate(price) || !ModelState.IsValid) return new HttpStatusCodeResult(400);
            using(var tx=db.Database.BeginTransaction())
            {
                HotelOperations.LockInventory(db);
                if(productId==0) db.Database.ExecuteSqlCommand("INSERT dbo.RoomServiceProducts(MaDichVu,Name,Price,Active) VALUES(@p0,@p1,@p2,@p3)",serviceId,name,price,active);
                else db.Database.ExecuteSqlCommand("UPDATE dbo.RoomServiceProducts SET Name=@p0,Price=@p1,Active=@p2 WHERE ProductId=@p3 AND MaDichVu=@p4",name,price,active,productId,serviceId);
                tx.Commit(); return RedirectToAction("Edit",new{id=serviceId});
            }
        }
        [HttpPost, ValidateAntiForgeryToken] public ActionResult BedPolicy(int roomId,int maxExtraBeds)
        {
            if(!Admin) return new HttpStatusCodeResult(403);
            if(maxExtraBeds<0||maxExtraBeds>5||!db.Rooms.Any(x=>x.RoomId==roomId)||!ModelState.IsValid) return new HttpStatusCodeResult(400);
            using(var tx=db.Database.BeginTransaction())
            {
                HotelOperations.LockInventory(db);
                db.Database.ExecuteSqlCommand("UPDATE dbo.RoomServiceBedPolicy SET MaxExtraBeds=@p0 WHERE RoomId=@p1; IF @@ROWCOUNT=0 INSERT dbo.RoomServiceBedPolicy VALUES(@p1,@p0)",maxExtraBeds,roomId);
                tx.Commit(); return RedirectToAction("Index");
            }
        }
        protected override void Dispose(bool disposing) { if(disposing) db.Dispose(); base.Dispose(disposing); }
    }
}



