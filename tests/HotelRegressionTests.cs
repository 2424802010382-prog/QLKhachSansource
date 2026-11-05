using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Helpers;
using System.Web.Mvc;
using System.Web.Routing;
using QLKhachSan.Controllers;
using QLKhachSan.Filters;
using QLKhachSan.Models;

internal static class HotelRegressionTests
{
    static int passed;
    static void Check(string name, bool value) { if (!value) throw new Exception("FAIL: " + name); passed++; Console.WriteLine("PASS: " + name); }
    static void Reject(string name, Action action) { bool rejected = false; try { action(); } catch (InvalidOperationException) { rejected = true; } Check(name, rejected); }
    static bool Success(JsonResult result) { return (bool)result.Data.GetType().GetProperty("success").GetValue(result.Data, null); }
    static T Wire<T>(T controller, HotelDbContext db, SessionStub session = null) where T : Controller
    {
        var type = controller is AccountController ? typeof(AccountController) : typeof(HotelController);
        type.GetField("db", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(controller, db);
        controller.ControllerContext = new ControllerContext(new ContextStub(session ?? new SessionStub()), new RouteData(), controller);
        controller.Url = new UrlHelper(new RequestContext(controller.HttpContext, new RouteData()), Routes());
        controller.TempData = new TempDataDictionary();
        return controller;
    }
    static RouteCollection Routes() { var routes = new RouteCollection(); routes.MapRoute("Default", "{controller}/{action}/{id}", new { id = UrlParameter.Optional }); return routes; }
    static Dictionary<string, int> Fingerprint(HotelDbContext db)
    {
        var result = new Dictionary<string, int>();
        foreach (var name in new[] { "Bookings", "PhysicalRooms", "Rooms", "Users", "ThanhToan", "RoomMaintenances", "DonDatMon", "DatPhongDichVu", "KhuyenMai", "UserPermissions", "ChiTietDonDatMon", "DichVu" })
            result[name] = db.Database.SqlQuery<int>("SELECT ISNULL(CHECKSUM_AGG(BINARY_CHECKSUM(*)),0) FROM dbo.[" + name + "]").Single();
        return result;
    }
    static Booking NewBooking(int type, string number, DateTime start, DateTime end)
    {
        return new Booking { RoomId = type, CustomerName = "REGRESSION ROLLBACK ONLY", CustomerPhone = "0900000000",
            CustomerEmail = "rollback@example.invalid", CheckInDate = start, CheckOutDate = end, GuestCount = 1,
            RoomCount = 1, SpecialRequest = "[Phòng: " + number + "]", PaymentMethod = "Tiền mặt", TotalAmount = 100000,
            Status = "Đã xác nhận", CreatedAt = DateTime.Now };
    }
    public static int Main()
    {
        try
        {
            Database.SetInitializer<HotelDbContext>(null);
            HttpContext.Current = new HttpContext(new HttpRequest("", "http://localhost/", ""), new HttpResponse(new StringWriter()));
            PureRules();
            using (var db = new HotelDbContext())
            {
                Check("LocalDB only safeguard", db.Database.Connection.DataSource.IndexOf("localdb", StringComparison.OrdinalIgnoreCase) >= 0 && db.Database.Connection.Database == "QLKhachSanDB");
                var before = Fingerprint(db);
                Check("40 physical rooms", db.PhysicalRooms.Count() == 40);
                Check("Eight room types each have five physical rooms", db.PhysicalRooms.GroupBy(p => p.RoomTypeId).Count() == 8 && db.PhysicalRooms.GroupBy(p => p.RoomTypeId).All(g => g.Count() == 5));
                var snapshot = HotelOperations.RoomSnapshot(db, DateTime.Today);
                Check("Snapshot materializes all rooms with EF6", snapshot.Count == 40);
                Check("Viewing snapshot does not change data", before.All(p => Fingerprint(db)[p.Key] == p.Value));
                ReadApis(db);
                MatrixReadTests(db);
                Authorization(db);
                using (var transaction = db.Database.BeginTransaction())
                {
                    HotelOperations.LockInventory(db);
                    MatrixWriteTests(db);
                    DatabaseRules(db);
                    transaction.Rollback();
                }
                db.ChangeTracker.Entries().ToList().ForEach(e => e.State = EntityState.Detached);
                var after = Fingerprint(db);
                Check("All business data unchanged after rollback", before.All(p => after[p.Key] == p.Value));
            }
            ConcurrentLock();
            TransactionOutcomes();
            Console.WriteLine("TOTAL PASSED: " + passed);
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    static void PureRules()
    {
        var start = new DateTime(2026, 11, 7); var end = new DateTime(2026, 11, 10);
        var maintenance = new RoomMaintenance { PhysicalRoomId = 2, StartDate = start, EndDate = end, Status = "Đang bảo trì" };
        foreach (int day in new[] { 6, 7, 8, 9, 10 })
        {
            var date = new DateTime(2026, 11, day);
            Check("Maintenance boundary Nov " + day, HotelOperations.MaintenanceOverlaps(maintenance, date, date.AddDays(1)) == (day >= 7 && day < 10));
        }
        maintenance.Status = "Đã kết thúc sớm"; maintenance.EndedAt = start.AddDays(1);
        Check("Early maintenance retains previous day", HotelOperations.MaintenanceOverlaps(maintenance, start, start.AddDays(1)));
        Check("Early maintenance releases end day", !HotelOperations.MaintenanceOverlaps(maintenance, start.AddDays(1), end));
        maintenance.EndedAt = start.AddDays(-1);
        Check("Canceled future maintenance never blocks a broad interval", !HotelOperations.MaintenanceOverlaps(maintenance, start.AddDays(-2), end.AddDays(1)));
        Check("Checkout is exclusive", !HotelOperations.Overlaps(start, end, end, end.AddDays(2)));
        var rooms = Enumerable.Range(1, 5).Select(i => new PhysicalRoom { PhysicalRoomId = i, RoomTypeId = 1, RoomNumber = (100 + i).ToString(), IsActive = true, Status = "Trống" }).ToList();
        var booking = NewBooking(1, "101", start, end);
        var bookings = new List<Booking> { booking };
        Check("One Deluxe booking leaves four Deluxe rooms", HotelOperations.AvailableRooms(rooms, bookings, new List<RoomMaintenance>(), start, end).Count == 4);
        Check("Future booking does not occupy current day", HotelOperations.RoomStatus(rooms[0], bookings, new List<RoomMaintenance>(), start.AddDays(-1)) == "Trống");
        Check("Checkout day is free", HotelOperations.RoomStatus(rooms[0], bookings, new List<RoomMaintenance>(), end) == "Trống");
        rooms[0].Status = "Bảo trì";
        Check("Legacy cached maintenance status does not lock a room forever", HotelOperations.RoomStatus(rooms[0], new Booking[0], new[] { new RoomMaintenance { PhysicalRoomId = rooms[0].PhysicalRoomId, StartDate = start, EndDate = end, Status = "Đang bảo trì" } }, end) == "Trống");
        Check("Legacy manual maintenance without any schedule remains blocked", HotelOperations.RoomStatus(rooms[0], new Booking[0], new RoomMaintenance[0], end) == "Bảo trì");
        rooms[0].Status = "Trống";
        booking.Status = "Đang ở";
        var pending = NewBooking(1, "101", start, end); pending.Status = "Chờ xác nhận";
        Check("Occupied state takes priority", HotelOperations.RoomStatus(rooms[0], new[] { pending, booking }, new RoomMaintenance[0], start) == "Đang ở");
        booking.Status = "Đã trả phòng";
        Check("Checked-out booking does not block a room", HotelOperations.RoomStatus(rooms[0], new[] { booking }, new RoomMaintenance[0], start) == "Trống");
        var completed = NewBooking(1, "101", DateTime.Today.AddDays(-3), DateTime.Today.AddDays(-1)); completed.Status = "Đã trả phòng";
        Check("Past calendar retains completed booking status", HotelOperations.RoomStatus(rooms[0], new[] { completed }, new RoomMaintenance[0], DateTime.Today.AddDays(-2)) == "Đã trả phòng");
        booking.Status = "Đã xác nhận"; booking.RoomCount = 3;
        Check("Legacy partial assignment reserves remaining capacity", HotelOperations.AvailableRooms(rooms, bookings, new List<RoomMaintenance>(), start, end).Count == 2);
        booking.RoomCount = 2; booking.SpecialRequest = "[Phòng: 101] [Phòng: 102]";
        Check("Multiple rooms reserve every assigned number", HotelOperations.AvailableRooms(rooms, bookings, new List<RoomMaintenance>(), start, end).Count == 3);
        var legacy = new List<Booking> { NewBooking(1, "", start, end), NewBooking(1, "", start, end) };
        legacy[0].BookingId = 100; legacy[0].SpecialRequest = ""; legacy[1].BookingId = 101; legacy[1].SpecialRequest = "";
        Check("Unassigned bookings never occupy an arbitrary number on matrix", rooms.All(r => HotelOperations.RoomStatus(r, legacy, new RoomMaintenance[0], start) == "Trống"));
        Check("Unassigned bookings still reserve availability capacity", HotelOperations.AvailableRooms(rooms, legacy, new List<RoomMaintenance>(), start, end).Count == 3);
        var notes = HotelOperations.AssignmentNotes(new[] { rooms[0] }, "[Phòng: 999] Extra note");
        Check("User note cannot inject a room assignment", HotelOperations.AssignedNumbers(notes).SequenceEqual(new[] { "101" }) && notes.Contains("Extra note"));
        Check("Food terminal state cannot resurrect", !HotelOperations.CanChangeFoodStatus("Hoàn tất", "Chờ xác nhận"));
        Check("Food status cannot skip preparation", !HotelOperations.CanChangeFoodStatus("Chờ xác nhận", "Hoàn tất"));
        var csv = typeof(AdminController).GetMethod("Csv", BindingFlags.Instance | BindingFlags.NonPublic);
        Check("CSV text formula is neutralized", ((string)csv.Invoke(new AdminController(), new object[] { "=SUM(1,1)" })).StartsWith("\"'="));
        Check("CSV negative numeric amounts stay numeric", ((string)csv.Invoke(new AdminController(), new object[] { -50m })) == "\"-50\"");
    }
    static void ReadApis(HotelDbContext db)
    {
        var controller = Wire(new BookingController(), db);
        var start = DateTime.Today.AddDays(90); var end = start.AddDays(2);
        Check("Physical-room JSON executes real EF query", Success(controller.GetAvailablePhysicalRooms(1, start, end)));
        Check("Room-type search executes real EF query", Success(controller.GetAvailableRooms(start, end, null)));
        var foodRowType = typeof(RestaurantController).GetNestedType("FoodPriceRow", BindingFlags.NonPublic);
        Check("Restaurant price projection materializes all required columns", db.Database.SqlQuery(foodRowType, "SELECT TOP 1 MaMonAn,TenMonAn,Gia,HoatDong FROM dbo.MonAn").Cast<object>().Any());
        Check("Invalid date range rejected", !Success(controller.GetAvailablePhysicalRooms(1, end, start)));
        Check("Availability API counts physical rooms", Success(controller.CheckAvailability(1, start.ToString("yyyy-MM-dd"), end.ToString("yyyy-MM-dd"))));
        Check("Anonymous cannot view another payment", controller.Payment(44) is HttpNotFoundResult);
        Check("Anonymous cannot modify another payment", !Success(controller.ConfirmCashPayment(44)));
        Check("Payment simulator never records money", !Success(controller.SimulateBankTransferPaid(44)));
        var matrix = Wire(new AdminController(), db).GetPhysicalRoomsMatrixByDate("2026-11-07");
        Check("Seven-day matrix query compiles and materializes", Success(matrix));
    }
    static object Prop(object value, string name) { return value.GetType().GetProperty(name).GetValue(value, null); }
    static List<object> Items(object value, string name) { return ((System.Collections.IEnumerable)Prop(value, name)).Cast<object>().ToList(); }
    static object Canonical(object value)
    {
        var dictionary = value as IDictionary<string, object>;
        if (dictionary != null) return new SortedDictionary<string, object>(dictionary.ToDictionary(p => p.Key, p => Canonical(p.Value)), StringComparer.Ordinal);
        var array = value as object[];
        return array == null ? value : array.Select(Canonical).ToArray();
    }
    static string Json(object value)
    {
        var serializer = new System.Web.Script.Serialization.JavaScriptSerializer();
        return serializer.Serialize(Canonical(serializer.DeserializeObject(serializer.Serialize(value))));
    }
    static SessionStub MatrixSession(params string[] permissions)
    {
        var session = new SessionStub(); session["UserId"] = 1; session["Role"] = "Staff";
        session["Permissions"] = permissions.ToList(); return session;
    }
    static void MatrixReadTests(HotelDbContext db)
    {
        var admin = Wire(new AdminController(), db);
        var staff = Wire(new StaffController(), db, MatrixSession("rooms.view", "bookings.view"));
        foreach (var date in new[] { DateTime.Today, new DateTime(2026, 11, 3), new DateTime(2026, 11, 7), new DateTime(2026, 11, 9), new DateTime(2026, 11, 10) })
        {
            var value = date.ToString("yyyy-MM-dd");
            var a = admin.GetPhysicalRoomsMatrixByDate(value);
            var s = (JsonResult)staff.GetPhysicalRoomsMatrixByDate(value);
            Check("Admin and Staff identical matrix " + value, Success(a) && Success(s) && Json(a.Data) == Json(s.Data));
            var states = Items(a.Data, "rooms").ToDictionary(r => (int)Prop(r, "physicalRoomId"), r => (string)Prop(r, "status"));
            Check("Cache snapshot agrees with both dashboards " + value, HotelOperations.RoomSnapshot(db, date).All(r => states[r.PhysicalRoomId] == r.Status));
        }
        var limited = Wire(new StaffController(), db, MatrixSession("rooms.view"));
        var payload = ((JsonResult)limited.GetPhysicalRoomsMatrixByDate("2026-11-07")).Data;
        Check("Room-only Staff gets states without booking/customer payload", Items(payload, "rooms").All(r => Prop(r, "currentBooking") == null && Items(r, "upcomingBookings").Count == 0) && Items(payload, "unassignedBookings").Count == 0);
        var denied = Wire(new StaffController(), db, MatrixSession("bookings.view")).GetPhysicalRoomsMatrixByDate("2026-11-07") as HttpStatusCodeResult;
        Check("Staff matrix rejects missing room permission at backend", denied != null && denied.StatusCode == 403);
        var customer = MatrixSession("rooms.view"); customer["Role"] = "Customer";
        denied = Wire(new StaffController(), db, customer).GetPhysicalRoomsMatrixByDate(null) as HttpStatusCodeResult;
        Check("Customer cannot call Staff matrix backend", denied != null && denied.StatusCode == 403);
        Check("Malformed matrix date falls back safely", RoomMatrixService.ParseDate("not-a-date") == DateTime.Today && RoomMatrixService.ParseDate("9999-12-31") == DateTime.Today);
    }
    static void MatrixWriteTests(HotelDbContext db)
    {
        // LocalDB-only, inside the existing rollback transaction; never persisted.
        var start = DateTime.Today.AddDays(180);
        var rooms = db.PhysicalRooms.AsNoTracking().Where(p => p.RoomTypeId == 1).OrderBy(p => p.RoomNumber).ToList();
        var pending = NewBooking(1, rooms[0].RoomNumber, start, start.AddDays(2)); pending.Status = "Chờ xác nhận";
        var later = NewBooking(1, rooms[0].RoomNumber, start.AddDays(3), start.AddDays(5));
        var occupied = NewBooking(1, rooms[1].RoomNumber, start, start.AddDays(2)); occupied.Status = "Đang ở";
        var unassigned = NewBooking(1, "", start, start.AddDays(2)); unassigned.SpecialRequest = "";
        db.Bookings.AddRange(new[] { pending, later, occupied, unassigned });
        var maintenance = new RoomMaintenance { PhysicalRoomId = rooms[2].PhysicalRoomId, StartDate = start, EndDate = start.AddDays(3), Status = "Đang bảo trì", Reason = "ROLLBACK matrix test", CreatedAt = DateTime.Now };
        db.RoomMaintenances.Add(maintenance); db.SaveChanges();
        Func<DateTime, object> load = date => RoomMatrixService.Load(db, date);
        Func<object, int, string> status = (data, id) => (string)Prop(Items(data, "rooms").Single(r => (int)Prop(r, "physicalRoomId") == id), "status");
        var day = load(start);
        Check("Matrix pending on assigned date and number", status(day, rooms[0].PhysicalRoomId) == "Chờ xác nhận");
        Check("Matrix occupied booking", status(day, rooms[1].PhysicalRoomId) == "Đang ở");
        Check("Matrix scheduled maintenance", status(day, rooms[2].PhysicalRoomId) == "Bảo trì");
        Check("Matrix empty room with no assignment", status(day, rooms[3].PhysicalRoomId) == "Trống");
        Check("Unassigned booking listed for processing", Items(day, "unassignedBookings").Any(b => (int)Prop(b, "bookingId") == unassigned.BookingId && (int)Prop(b, "missingRoomCount") == 1));
        Check("Unassigned booking is never attached to any room popup", !Items(day, "rooms").Any(r => Prop(r, "currentBooking") != null && (int)Prop(Prop(r, "currentBooking"), "bookingId") == unassigned.BookingId));
        Check("Future pending does not occupy prior date", status(load(start.AddDays(-1)), rooms[0].PhysicalRoomId) == "Trống");
        Check("Checkout date releases first booking", status(load(start.AddDays(2)), rooms[0].PhysicalRoomId) == "Trống");
        Check("Same room later booking shown on its date", status(load(start.AddDays(3)), rooms[0].PhysicalRoomId) == "Đã đặt");
        Check("Maintenance end date free", status(load(start.AddDays(3)), rooms[2].PhysicalRoomId) == "Trống");
        maintenance.Status = "Đã kết thúc sớm"; maintenance.EndedAt = start.AddDays(1); db.SaveChanges();
        Check("Early maintenance end refreshes calendar", status(load(start.AddDays(1)), rooms[2].PhysicalRoomId) == "Trống" && status(load(start), rooms[2].PhysicalRoomId) == "Bảo trì");
        occupied.Status = "Đã trả phòng"; db.SaveChanges();
        Check("Checkout update refresh releases room", status(load(start), rooms[1].PhysicalRoomId) == "Trống");
        var a = Wire(new AdminController(), db).GetPhysicalRoomsMatrixByDate(start.ToString("yyyy-MM-dd"));
        var s = (JsonResult)Wire(new StaffController(), db, MatrixSession("rooms.view", "bookings.view")).GetPhysicalRoomsMatrixByDate(start.ToString("yyyy-MM-dd"));
        Check("Admin matrix responds after writes", Success(a));
        Check("Staff matrix responds after writes", Success(s));
        var adminJson = Json(a.Data); var staffJson = Json(s.Data);
        Check("Admin Staff identical after booking and maintenance writes", adminJson == staffJson);
        Check("Matrix reads do not write assignment notes", db.Bookings.AsNoTracking().Single(b => b.BookingId == unassigned.BookingId).SpecialRequest == "");
    }
    static void Authorization(HotelDbContext db)
    {
        foreach (string controllerName in new[] { "Admin", "Staff" })
        {
            var fake = new ContextStub(new SessionStub());
            var route = new RouteData(); route.Values["controller"] = controllerName; route.Values["action"] = "Index";
            var controller = new HomeController();
            var cc = new ControllerContext(fake, route, controller);
            var descriptor = new ReflectedControllerDescriptor(typeof(HomeController)).FindAction(cc, "Index");
            var auth = new AuthorizationContext(cc, descriptor);
            new HotelSecurityFilter().OnAuthorization(auth);
            Check("Anonymous denied " + controllerName, auth.Result is HttpStatusCodeResult && ((HttpStatusCodeResult)auth.Result).StatusCode == 401);
        }
        var user = db.Users.AsNoTracking().FirstOrDefault(u => u.Role == "Customer" && u.IsActive);
        if (user != null)
        {
            var session = new SessionStub(); session["UserId"] = user.UserId; session["Role"] = "Admin";
            var fake = new ContextStub(session); var route = new RouteData(); route.Values["controller"] = "Admin"; route.Values["action"] = "Index";
            var controller = new HomeController(); var cc = new ControllerContext(fake, route, controller);
            var descriptor = new ReflectedControllerDescriptor(typeof(HomeController)).FindAction(cc, "Index");
            var auth = new AuthorizationContext(cc, descriptor); new HotelSecurityFilter().OnAuthorization(auth);
            Check("Forged/stale Session role is refreshed from database", auth.Result is HttpStatusCodeResult && ((HttpStatusCodeResult)auth.Result).StatusCode == 403);
        }
        var csrfContext = new ContextStub(new SessionStub()); csrfContext.Input.Method = "POST";
        var csrfRoute = new RouteData(); csrfRoute.Values["controller"] = "Account"; csrfRoute.Values["action"] = "Login";
        var csrfController = new HomeController(); var csrfCc = new ControllerContext(csrfContext, csrfRoute, csrfController);
        var csrfDescriptor = new ReflectedControllerDescriptor(typeof(HomeController)).FindAction(csrfCc, "Index");
        var csrf = new AuthorizationContext(csrfCc, csrfDescriptor); new HotelSecurityFilter().OnAuthorization(csrf);
        Check("Mutation without CSRF token rejected", csrf.Result is HttpStatusCodeResult && ((HttpStatusCodeResult)csrf.Result).StatusCode == 400);
        foreach (var role in new[] { "Staff", "Customer" })
        {
            var actor = db.Users.AsNoTracking().FirstOrDefault(u => u.IsActive && u.Role == role);
            Check("Existing role available for matrix authorization test: " + role, actor != null);
            if (actor == null) continue;
            foreach (var controllerName in new[] { "Staff", "Admin" })
            {
                var session = new SessionStub(); session["UserId"] = actor.UserId;
                session["Role"] = "Admin"; session["Permissions"] = new List<string> { "rooms.view" }; // forged session must not grant access
                var route = new RouteData(); route.Values["controller"] = controllerName; route.Values["action"] = "GetPhysicalRoomsMatrixByDate";
                var ctrl = new HomeController(); var cc = new ControllerContext(new ContextStub(session), route, ctrl);
                var descriptor = new ReflectedControllerDescriptor(typeof(HomeController)).FindAction(cc, "Index");
                var auth = new AuthorizationContext(cc, descriptor); new HotelSecurityFilter().OnAuthorization(auth);
                bool shouldAllow = controllerName == "Staff" && role == "Staff" && db.UserPermissions.Any(p => p.UserId == actor.UserId && (p.PermissionCode == "rooms.view" || p.PermissionCode == "rooms.manage"));
                Check("Global matrix role/permission rule " + role + " -> " + controllerName,
                    shouldAllow ? auth.Result == null : auth.Result is HttpStatusCodeResult && ((HttpStatusCodeResult)auth.Result).StatusCode == 403);
            }
        }
    }
    static void DatabaseRules(HotelDbContext db)
    {
        var room = HotelOperations.AvailableRooms(db, 1, DateTime.Today, DateTime.Today.AddDays(2)).First();
        string password = "Rollback-" + Guid.NewGuid().ToString("N");
        var user = new User { Username = "rollback-" + Guid.NewGuid().ToString("N"), Email = "rollback-" + Guid.NewGuid().ToString("N") + "@example.invalid",
            FullName = "Regression rollback only", PasswordHash = Crypto.HashPassword(password), Role = "Customer", IsActive = false, CreatedAt = DateTime.Now };
        db.Users.Add(user); db.SaveChanges();
        var blockedSession = new SessionStub(); var account = Wire(new AccountController(), db, blockedSession);
        account.Login(user.Username, password);
        Check("Locked account cannot log in or unlock itself", blockedSession["UserId"] == null && !db.Users.Find(user.UserId).IsActive);
        user.IsActive = true; db.SaveChanges();
        var hashSession = new SessionStub(); Wire(new AccountController(), db, hashSession).Login(user.Username, user.PasswordHash);
        Check("Stored password hash is not a password", hashSession["UserId"] == null);
        var goodSession = new SessionStub(); Wire(new AccountController(), db, goodSession).Login(user.Username, password);
        Check("Existing hashed-password login remains valid", Convert.ToInt32(goodSession["UserId"]) == user.UserId);
        int availableBefore = HotelOperations.AvailableRooms(db, room.RoomTypeId, DateTime.Today, DateTime.Today.AddDays(2)).Count;
        var booking = NewBooking(room.RoomTypeId, room.RoomNumber, DateTime.Today, DateTime.Today.AddDays(2));
        db.Bookings.Add(booking); db.SaveChanges();
        db.Database.ExecuteSqlCommand("UPDATE dbo.Bookings SET UserId=@p0 WHERE BookingId=@p1", user.UserId, booking.BookingId);
        var ownSession = new SessionStub(); ownSession["UserId"] = user.UserId;
        Check("Owner can view own payment", Wire(new BookingController(), db, ownSession).Payment(booking.BookingId) is ViewResult);
        var otherSession = new SessionStub(); otherSession["UserId"] = user.UserId + 1000000;
        Check("Different customer cannot view or change payment", Wire(new BookingController(), db, otherSession).Payment(booking.BookingId) is HttpNotFoundResult &&
            !Success(Wire(new BookingController(), db, otherSession).ConfirmCashPayment(booking.BookingId)));
        Check("Persisted booking blocks only its physical room", HotelOperations.AvailableRooms(db, room.RoomTypeId, booking.CheckInDate, booking.CheckOutDate).Count == availableBefore - 1);
        Reject("Same room and overlapping dates rejected", () => HotelOperations.SelectRooms(db, room.RoomTypeId, booking.CheckInDate, booking.CheckOutDate, 1, new[] { room.RoomNumber }, true));
        var wrongType = db.PhysicalRooms.AsNoTracking().First(p => p.RoomTypeId != room.RoomTypeId);
        Reject("Room from another type rejected", () => HotelOperations.SelectRooms(db, room.RoomTypeId, booking.CheckInDate, booking.CheckOutDate, 1, new[] { wrongType.RoomNumber }, true));
        Reject("Unknown room rejected", () => HotelOperations.SelectRooms(db, room.RoomTypeId, booking.CheckInDate, booking.CheckOutDate, 1, new[] { "DOES-NOT-EXIST" }, true));
        Reject("Duplicate selected numbers rejected", () => HotelOperations.SelectRooms(db, room.RoomTypeId, booking.CheckInDate, booking.CheckOutDate, 2, new[] { room.RoomNumber, room.RoomNumber }, true));
        Reject("Unpaid check-in rejected", () => HotelOperations.ValidateCheckIn(db, booking));
        Reject("Payment without evidence rejected", () => HotelOperations.ConfirmBookingPayment(db, booking, "", user.UserId));
        HotelOperations.ConfirmBookingPayment(db, booking, "REGRESSION CASH RECEIPT - ROLLBACK", user.UserId);
        var paymentSession = new SessionStub(); paymentSession["UserId"] = user.UserId;
        var paymentController = Wire(new BookingController(), db, paymentSession);
        Check("Paid booking cannot be downgraded to cash pending", !Success(paymentController.ConfirmCashPayment(booking.BookingId)) && booking.PaymentMethod == "Tiền mặt");
        Check("Paid booking cannot be reopened as a transfer review", !Success(paymentController.RequestBankTransferReview(booking.BookingId)) && booking.PaymentMethod == "Tiền mặt");
        Reject("Duplicate full payment rejected", () => HotelOperations.ConfirmBookingPayment(db, booking, "REGRESSION CASH RECEIPT - ROLLBACK", user.UserId));
        HotelOperations.ValidateCheckIn(db, booking);
        Check("Paid, confirmed booking can check in", true);
        booking.Status = "Đang ở"; db.SaveChanges();
        Reject("Double check-in rejected", () => HotelOperations.ValidateCheckIn(db, booking));
        var roomSession = new SessionStub(); roomSession["UserId"] = user.UserId; roomSession["Role"] = "Customer";
        Check("Customer active-room API uses UserId and physical room", Success(Wire(new RestaurantController(), db, roomSession).GetMyActiveRooms()));
        booking.TotalAmount += 1000; db.SaveChanges();
        var additionalDue = HotelOperations.SynchronizeOutstandingPayment(db, booking);
        Check("Additional charges create only the outstanding balance", additionalDue != null && additionalDue.SoTien == 1000 && HotelOperations.PaymentStatus(booking) != "Đã thanh toán");
        var staffSession = new SessionStub(); staffSession["UserId"] = user.UserId; staffSession["Role"] = "Staff";
        Wire(new StaffController(), db, staffSession).CheckOutBooking(booking.BookingId);
        Check("Checkout rejects outstanding added charges", db.Bookings.Find(booking.BookingId).Status == "Đang ở");
        booking.TotalAmount -= 1000; db.SaveChanges(); HotelOperations.SynchronizeOutstandingPayment(db, booking);
        Check("Removing unpaid charge restores fully-paid state", HotelOperations.PaymentStatus(booking) == "Đã thanh toán");
        Wire(new StaffController(), db, staffSession).CheckOutBooking(booking.BookingId);
        Check("Paid guest can check out", db.Bookings.Find(booking.BookingId).Status == "Đã trả phòng");
        Check("Checkout releases its room", HotelOperations.AvailableRooms(db, room.RoomTypeId, booking.CheckInDate, booking.CheckOutDate).Any(p => p.PhysicalRoomId == room.PhysicalRoomId));
        DateTime future = DateTime.Today.AddDays(100);
        var m = new RoomMaintenance { PhysicalRoomId = room.PhysicalRoomId, StartDate = future, EndDate = future.AddDays(3), Reason = "Regression rollback only", Status = "Đang bảo trì", CreatedAt = DateTime.Now };
        db.RoomMaintenances.Add(m); db.SaveChanges();
        Reject("Overlapping maintenance creation rejected", () => HotelOperations.CreateMaintenance(db, room.PhysicalRoomId, future.AddDays(1), future.AddDays(2), "Rollback overlap", ""));
        Reject("Invalid maintenance dates rejected", () => HotelOperations.CreateMaintenance(db, room.PhysicalRoomId, future.AddDays(2), future, "Rollback invalid", ""));
        var adjacent = HotelOperations.CreateMaintenance(db, room.PhysicalRoomId, future.AddDays(3), future.AddDays(4), "Rollback adjacent", "");
        Check("Adjacent maintenance interval is allowed", adjacent.MaintenanceId > 0);
        HotelOperations.EndMaintenance(db, adjacent.MaintenanceId);
        Check("Early cancellation preserves maintenance record", db.RoomMaintenances.Find(adjacent.MaintenanceId) != null && adjacent.Status == "Đã kết thúc sớm");
        Reject("Maintenance room cannot be reserved", () => HotelOperations.SelectRooms(db, room.RoomTypeId, future, future.AddDays(1), 1, new[] { room.RoomNumber }, true));
        Check("Maintenance does not block outside its interval", HotelOperations.AvailableRooms(db, room.RoomTypeId, future.AddDays(3), future.AddDays(4)).Any(p => p.PhysicalRoomId == room.PhysicalRoomId));
        m.Status = "Đã kết thúc sớm"; m.EndedAt = future.AddDays(1); db.SaveChanges();
        Check("Ended maintenance keeps prior-day history", HotelOperations.RoomSnapshot(db, future).First(p => p.PhysicalRoomId == room.PhysicalRoomId).Status == "Bảo trì");
        Check("Ended maintenance releases subsequent dates", HotelOperations.AvailableRooms(db, room.RoomTypeId, future.AddDays(1), future.AddDays(2)).Any(p => p.PhysicalRoomId == room.PhysicalRoomId));
        var futureBooking = NewBooking(room.RoomTypeId, room.RoomNumber, future.AddDays(10), future.AddDays(12));
        db.Bookings.Add(futureBooking); db.SaveChanges();
        Reject("Maintenance cannot overlap a reserved physical room", () => HotelOperations.CreateMaintenance(db, room.PhysicalRoomId, future.AddDays(10), future.AddDays(11), "Rollback booking overlap", ""));
        decimal revenue = HotelOperations.RoomRevenue(db, DateTime.Today, DateTime.Today.AddDays(1));
        Check("Revenue query executes against actual schema", revenue >= 100000);
        FoodRules(db, user, room);
    }
    static int FoodFixture(HotelDbContext db, int userId, int? bookingId, decimal amount, bool bundled, int? serviceId = null)
    {
        return db.Database.SqlQuery<int>(@"INSERT dbo.DonDatMon(UserId,BookingId,HoTen,SoDienThoai,HinhThuc,SoPhong,TongTien,
            TrangThai,PhuongThucThanhToan,TrangThaiThanhToan,CreatedAt,PayWithBooking,BookingServiceId)
            VALUES(@p0,@p1,N'REGRESSION ROLLBACK ONLY','0900000000',N'Giao lên phòng','TEST',@p2,
            N'Chờ xác nhận',N'Thanh toán khi nhận món',N'Chưa thanh toán',GETDATE(),@p3,@p4); SELECT CAST(SCOPE_IDENTITY() AS int);",
            userId, bookingId.HasValue ? (object)bookingId.Value : DBNull.Value, amount, bundled,
            serviceId.HasValue ? (object)serviceId.Value : DBNull.Value).Single();
    }
    static decimal FoodRevenue(HotelDbContext db)
    {
        return db.Database.SqlQuery<decimal>(@"SELECT ISNULL(SUM(TongTien),0) FROM dbo.DonDatMon WHERE TrangThaiThanhToan=N'Đã thanh toán'
            AND COALESCE(PaidAt,CreatedAt)>=@p0 AND COALESCE(PaidAt,CreatedAt)<@p1", DateTime.Today, DateTime.Today.AddDays(1)).Single();
    }
    static void FoodRules(HotelDbContext db, User user, PhysicalRoom room)
    {
        decimal foodBefore = FoodRevenue(db);
        int orderId = FoodFixture(db, user.UserId, null, 25000, false);
        Reject("Food payment requires evidence", () => HotelOperations.ConfirmFoodPayment(db, orderId, "", user.UserId));
        HotelOperations.ConfirmFoodPayment(db, orderId, "REGRESSION CASH ONLY - ROLLBACK", user.UserId);
        Check("Food receipt records actual confirmation time", db.Database.SqlQuery<int>("SELECT COUNT(*) FROM dbo.DonDatMon WHERE MaDon=@p0 AND PaidAt IS NOT NULL", orderId).Single() == 1);
        Reject("Food payment cannot be confirmed twice", () => HotelOperations.ConfirmFoodPayment(db, orderId, "REGRESSION CASH ONLY - ROLLBACK", user.UserId));
        Reject("Paid food order cannot be silently canceled", () => HotelOperations.ChangeFoodStatus(db, orderId, "Đã hủy"));
        Check("Food revenue uses paid orders", FoodRevenue(db) == foodBefore + 25000);
        var bundle = NewBooking(room.RoomTypeId, room.RoomNumber, DateTime.Today, DateTime.Today.AddDays(2));
        bundle.Status = "Đang ở"; bundle.TotalAmount = 135000;
        db.Bookings.Add(bundle); db.SaveChanges();
        int serviceType = db.DichVus.Select(d => d.MaDichVu).First();
        var line = new DatPhongDichVu { BookingId = bundle.BookingId, MaDichVu = serviceType, SoLuong = 1, DonGia = 35000, ThanhTien = 35000 };
        db.DatPhongDichVus.Add(line); db.SaveChanges();
        int bundledOrder = FoodFixture(db, user.UserId, bundle.BookingId, 35000, true, line.MaDatPhongDichVu);
        decimal roomBefore = HotelOperations.RoomRevenue(db, DateTime.Today, DateTime.Today.AddDays(1));
        foodBefore = FoodRevenue(db);
        Reject("Bundled food cannot be charged separately", () => HotelOperations.ConfirmFoodPayment(db, bundledOrder, "REGRESSION CASH ONLY - ROLLBACK", user.UserId));
        HotelOperations.ConfirmBookingPayment(db, bundle, "REGRESSION BUNDLED CASH ONLY - ROLLBACK", user.UserId);
        Check("Room receipt settles bundled food", HotelOperations.FoodOrder(db, bundledOrder).TrangThaiThanhToan == "Đã thanh toán");
        Check("Room revenue excludes bundled food", HotelOperations.RoomRevenue(db, DateTime.Today, DateTime.Today.AddDays(1)) == roomBefore + 100000);
        Check("Food revenue includes bundled portion once", FoodRevenue(db) == foodBefore + 35000);
        var canceledLine = new DatPhongDichVu { BookingId = bundle.BookingId, MaDichVu = serviceType, SoLuong = 1, DonGia = 20000, ThanhTien = 20000 };
        db.DatPhongDichVus.Add(canceledLine); bundle.TotalAmount += 20000; db.SaveChanges();
        int canceledOrder = FoodFixture(db, user.UserId, bundle.BookingId, 20000, true, canceledLine.MaDatPhongDichVu);
        HotelOperations.ChangeFoodStatus(db, canceledOrder, "Đã hủy");
        Check("Canceling unpaid bundled food adjusts room balance", bundle.TotalAmount == 135000 && canceledLine.ThanhTien == 0);
        Check("Canceled food record is preserved", HotelOperations.FoodOrder(db, canceledOrder).TrangThai == "Đã hủy");
        // A reference tag on a rollback-only cash fixture tests duplicate detection;
        // no bank transaction or bank receipt is fabricated or sent.
        string duplicateTag = "REGRESSION-REFERENCE-NOT-A-BANK-TRANSFER";
        var cashReceipt = db.ThanhToans.First(p => p.BookingId == bundle.BookingId && p.TrangThai == "Đã thanh toán");
        cashReceipt.MaGiaoDich = duplicateTag; db.SaveChanges();
        var bankCandidate = NewBooking(room.RoomTypeId, room.RoomNumber, DateTime.Today.AddDays(20), DateTime.Today.AddDays(21));
        bankCandidate.PaymentMethod = "Chuyển khoản"; db.Bookings.Add(bankCandidate); db.SaveChanges();
        Reject("Same reference cannot be reused on a different booking", () => HotelOperations.ConfirmBookingPayment(db, bankCandidate, duplicateTag, user.UserId));
        int foodCandidate = FoodFixture(db, user.UserId, null, 15000, false);
        db.Database.ExecuteSqlCommand("UPDATE dbo.DonDatMon SET PhuongThucThanhToan=N'Chuyển khoản' WHERE MaDon=@p0", foodCandidate);
        Reject("Booking payment reference cannot be reused for food", () => HotelOperations.ConfirmFoodPayment(db, foodCandidate, duplicateTag, user.UserId));
    }
    static void ConcurrentLock()
    {
        using (var first = new HotelDbContext())
        using (var transaction = first.Database.BeginTransaction())
        {
            HotelOperations.LockInventory(first);
            var started = new ManualResetEventSlim();
            var task = Task.Run(() => {
                using (var second = new HotelDbContext())
                using (var tx = second.Database.BeginTransaction())
                {
                    started.Set(); HotelOperations.LockInventory(second); tx.Rollback();
                }
            });
            started.Wait(); Thread.Sleep(300);
            Check("Concurrent operation waits for inventory lock", !task.IsCompleted);
            transaction.Rollback();
            Check("Waiting operation resumes after lock release", task.Wait(15000));
        }
    }
    static void TransactionOutcomes()
    {
        using (var probe = new TransactionProbe())
        {
            probe.Initialize();
            foreach (string kind in new[] { "json", "dictionary", "caught", "http", "response", "redirect", "exception", "success" })
                Check("Action transaction outcome: " + kind, probe.Run(kind));
        }
    }
    public sealed class TransactionProbe : HotelController
    {
        public ActionResult Probe() { return new EmptyResult(); }
        public void Initialize()
        {
            if (db.Database.Connection.DataSource.IndexOf("localdb", StringComparison.OrdinalIgnoreCase) < 0)
                throw new Exception("LocalDB only");
            db.Database.Connection.Open();
            db.Database.ExecuteSqlCommand("CREATE TABLE #HotelTxProbe(Id int NOT NULL)");
            ControllerContext = new ControllerContext(new ContextStub(new SessionStub()), new RouteData(), this);
            ((ContextStub)HttpContext).Input.Method = "POST";
        }
        public bool Run(string kind)
        {
            Response.StatusCode = 200;
            int before = db.Database.SqlQuery<int>("SELECT COUNT(*) FROM #HotelTxProbe").Single();
            var descriptor = new ReflectedControllerDescriptor(typeof(TransactionProbe)).FindAction(ControllerContext, "Probe");
            OnActionExecuting(new ActionExecutingContext(ControllerContext, descriptor, new Dictionary<string, object>()));
            db.Database.ExecuteSqlCommand("INSERT #HotelTxProbe VALUES(1)");
            ActionResult result = new JsonResult { Data = new { success = true } };
            if (kind == "json" || kind == "caught") result = new JsonResult { Data = new { success = false } };
            if (kind == "caught") { try { throw new Exception("Injected fixture failure"); } catch { } }
            if (kind == "dictionary") result = new JsonResult { Data = new Dictionary<string, object> { { "success", false } } };
            if (kind == "http") result = new HttpStatusCodeResult(400);
            if (kind == "response") Response.StatusCode = 500;
            if (kind == "redirect") { FailOperation(); result = new RedirectResult("/"); }
            var executed = new ActionExecutedContext(ControllerContext, descriptor, false, kind == "exception" ? new Exception("Injected") : null) { Result = result };
            OnActionExecuted(executed);
            int after = db.Database.SqlQuery<int>("SELECT COUNT(*) FROM #HotelTxProbe").Single();
            return after == before + (kind == "success" ? 1 : 0);
        }
        protected override void Dispose(bool disposing) { if (disposing) db.Dispose(); base.Dispose(disposing); }
    }
    sealed class SessionStub : HttpSessionStateBase
    {
        readonly Dictionary<string, object> items = new Dictionary<string, object>();
        public override object this[string name] { get { return items.ContainsKey(name) ? items[name] : null; } set { items[name] = value; } }
        public override void Clear() { items.Clear(); }
        public override void Remove(string name) { items.Remove(name); }
        public override void Abandon() { items.Clear(); }
    }
    sealed class RequestStub : HttpRequestBase
    {
        public string Method = "GET";
        readonly NameValueCollection headers = new NameValueCollection(), form = new NameValueCollection();
        public override string HttpMethod { get { return Method; } }
        public override string ContentType { get { return "application/json"; } }
        public override NameValueCollection Headers { get { return headers; } }
        public override NameValueCollection Form { get { return form; } }
        public override NameValueCollection ServerVariables { get { return new NameValueCollection(); } }
        public override Uri Url { get { return new Uri("http://localhost/"); } }
        public override string ApplicationPath { get { return "/"; } }
        public override string AppRelativeCurrentExecutionFilePath { get { return "~/"; } }
        public override string PathInfo { get { return ""; } }
        public override bool IsLocal { get { return true; } }
        public override string this[string key] { get { return form[key]; } }
    }
    sealed class ResponseStub : HttpResponseBase
    {
        readonly CacheStub cache = new CacheStub();
        public override HttpCachePolicyBase Cache { get { return cache; } }
        public override int StatusCode { get; set; }
        public override bool TrySkipIisCustomErrors { get; set; }
        public override string ApplyAppPathModifier(string path) { return path; }
    }
    sealed class CacheStub : HttpCachePolicyBase
    {
        public override void SetCacheability(HttpCacheability value) { }
        public override void SetNoStore() { }
    }
    sealed class ContextStub : HttpContextBase
    {
        readonly SessionStub session; public readonly RequestStub Input = new RequestStub(); readonly ResponseStub output = new ResponseStub();
        public ContextStub(SessionStub value) { session = value; }
        public override HttpSessionStateBase Session { get { return session; } }
        public override HttpRequestBase Request { get { return Input; } }
        public override HttpResponseBase Response { get { return output; } }
    }
}
