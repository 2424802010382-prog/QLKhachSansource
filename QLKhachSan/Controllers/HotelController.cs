using System;
using System.Data.Entity;
using System.Web.Mvc;
using QLKhachSan.Models;

namespace QLKhachSan.Controllers
{
    // One context/transaction for multi-step operations. Existing actions with explicit
    // transactions retain ownership of those transactions.
    public abstract class HotelController : Controller
    {
        protected readonly HotelDbContext db = new HotelDbContext();
        private DbContextTransaction operationTransaction;
        private bool operationFailed;

        protected void FailOperation() { operationFailed = true; }

        private void FinishOperation(bool commit)
        {
            var transaction = operationTransaction;
            operationTransaction = null;
            if (transaction == null) return;
            try
            {
                if (commit) transaction.Commit();
                else transaction.Rollback();
            }
            finally { transaction.Dispose(); }
        }

        private static bool IsFailureResult(ActionResult result)
        {
            var status = result as HttpStatusCodeResult;
            if (status != null && status.StatusCode >= 400) return true;
            var json = result as JsonResult;
            if (json == null || json.Data == null) return false;
            var dictionary = json.Data as System.Collections.IDictionary;
            object success = dictionary != null && dictionary.Contains("success") ? dictionary["success"] : null;
            var property = json.Data.GetType().GetProperty("success");
            if (property != null) success = property.GetValue(json.Data, null);
            return success is bool && !(bool)success;
        }

        protected override void OnActionExecuting(ActionExecutingContext context)
        {
            string action = context.ActionDescriptor.ActionName;
            operationFailed = false;
            if (Request.HttpMethod == "POST" && action != "Create" && action != "SaveRoomMaintenance" &&
                !(this is BookingHistoryController) && action != "PlaceOrder" && action != "PlaceRoomServiceOrder")
            {
                operationTransaction = db.Database.BeginTransaction();
                HotelOperations.LockInventory(db);
            }
            base.OnActionExecuting(context);
        }

        protected override void OnActionExecuted(ActionExecutedContext context)
        {
            if (operationTransaction != null)
            {
                try
                {
                    FinishOperation(context.Exception == null && !operationFailed && ModelState.IsValid &&
                        Response.StatusCode < 400 && !IsFailureResult(context.Result));
                }
                catch { FinishOperation(false); throw; }
            }
            base.OnActionExecuted(context);
        }

        protected override void OnException(ExceptionContext context)
        {
            FinishOperation(false);
            System.Diagnostics.Trace.TraceError(context.Exception.ToString());
            Response.TrySkipIisCustomErrors = true;
            Response.StatusCode = 500;
            if (Request.IsAjaxRequest() || (Request.ContentType ?? "").IndexOf("application/json", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                context.Result = Json(new { success = false, message = "Không thể hoàn tất thao tác. Vui lòng tải lại dữ liệu và thử lại." });
                context.ExceptionHandled = true;
            }
            base.OnException(context);
        }
    }
}
