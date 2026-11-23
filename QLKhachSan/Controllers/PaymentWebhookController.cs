using System;
using System.IO;
using System.Threading.Tasks;
using System.Web.Mvc;
using System.Web.SessionState;
using QLKhachSan.Models;
using QLKhachSan.Services;

namespace QLKhachSan.Controllers
{
    [SessionState(SessionStateBehavior.Disabled)]
    public sealed class PaymentWebhookController : Controller
    {
        [HttpPost]
        public async Task<ActionResult> Receive()
        {
            Response.TrySkipIisCustomErrors = true;
            Response.Cache.SetCacheability(System.Web.HttpCacheability.NoCache);
            string stage = "Configuration";
            string diagnosticId = Guid.NewGuid().ToString("N");
            try
            {
                var settings = BankPaymentSettings.Load();
                if (!settings.Ready) return Failure(stage, diagnosticId, null);
                stage = "RequestValidation";
                bool isSecure = Request.IsSecureConnection || string.Equals(Request.Headers["X-Forwarded-Proto"], "https", StringComparison.OrdinalIgnoreCase) || (Request.IsLocal && settings.Mode == "Sandbox");
                if (!isSecure) return Reply(403, false);
                if (!string.Equals((Request.ContentType ?? "").Split(';')[0].Trim(), "application/json", StringComparison.OrdinalIgnoreCase))
                    return Reply(415, false);
                if (Request.ContentLength > 65536) return Reply(413, false);
                byte[] raw;
                using (var memory = new MemoryStream())
                {
                    var buffer = new byte[4096]; int read;
                    if (Request.InputStream.CanSeek) Request.InputStream.Position = 0;
                    while ((read = await Request.InputStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        if (memory.Length + read > 65536) return Reply(413, false);
                        memory.Write(buffer, 0, read);
                    }
                    raw = memory.ToArray();
                }
                if (!SePayProtocol.Verify(raw, Request.Headers["X-SePay-Timestamp"], Request.Headers["X-SePay-Signature"], settings.Secret, DateTimeOffset.UtcNow))
                    return Reply(401, false);
                QLKhachSan.Services.SePayTransaction transaction;
                try { transaction = SePayProtocol.Parse(raw); }
                catch (Exception ex) when (ex is FormatException || ex is Newtonsoft.Json.JsonException || ex is System.Text.DecoderFallbackException)
                { return Reply(400, false); }
                stage = "BankVerification";
                bool live = settings.Mode == "Live" && await SePayLiveVerifier.VerifyAsync(transaction, settings.ApiToken);
                if (settings.Mode == "Live" && !live) return Reply(422, false);
                stage = "PaymentRecording";
                using (var db = new HotelDbContext())
                    new SePayPaymentService().Process(db, settings, transaction, SePayProtocol.Hash(raw), live);
                return Reply(200, true);
            }
            catch (Exception ex)
            {
                return Failure(stage, diagnosticId, ex);
            }
        }

        private JsonResult Failure(string stage, string diagnosticId, Exception error)
        {
            // Return only an allowlisted phase and numeric SQL code, never exception messages,
            // payloads, bank references, connection strings, tokens or stack traces.
            int? sqlNumber = null;
            for (var cause = error; cause != null; cause = cause.InnerException)
            {
                var sql = cause as System.Data.SqlClient.SqlException;
                if (sql != null) { sqlNumber = sql.Number; break; }
            }
            System.Diagnostics.Trace.TraceError("SePay failure Id={0}; Stage={1}; Type={2}; SqlNumber={3}",
                diagnosticId, stage, error == null ? "NotReady" : error.GetType().Name, sqlNumber);
            Response.StatusCode = 503;
            Response.Headers["Retry-After"] = "60";
            return Json(new { success = false, errorCode = "SEPAY_" + stage.ToUpperInvariant(), diagnosticId, sqlNumber });
        }

        private JsonResult Reply(int status, bool success)
        {
            Response.StatusCode = status;
            if (status == 503) Response.Headers["Retry-After"] = "60";
            return Json(new { success });
        }
    }
}
