using System;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace QLKhachSan.Services
{
    public static class SePayLiveVerifier
    {
        private static readonly HttpClient Client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };

        public static async Task<Tuple<bool, int, string>> TestApiConnectionAsync(string apiToken)
        {
            if (string.IsNullOrWhiteSpace(apiToken))
                return Tuple.Create(false, 400, "API Token is empty or missing.");

            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Get, "https://my.sepay.vn/userapi/transactions/list?limit=1"))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);
                    using (var response = await Client.SendAsync(request))
                    {
                        int statusCode = (int)response.StatusCode;
                        if (!response.IsSuccessStatusCode)
                        {
                            return Tuple.Create(false, statusCode, "HTTP " + statusCode + " (" + response.ReasonPhrase + ")");
                        }

                        string body = await response.Content.ReadAsStringAsync();
                        var json = JObject.Parse(body);
                        int status = (int?)json["status"] ?? statusCode;
                        bool isOk = status == 200;

                        return Tuple.Create(isOk, status, isOk ? "Connected successfully to SePay User API." : "SePay returned status " + status);
                    }
                }
            }
            catch (Exception ex)
            {
                return Tuple.Create(false, 500, "Network/Outbound Error: " + ex.GetType().Name + " - " + ex.Message);
            }
        }
        public static async Task<bool> VerifyAsync(SePayTransaction transaction, string apiToken)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get,
                "https://my.sepay.vn/userapi/transactions/details/" + transaction.Id.ToString(CultureInfo.InvariantCulture)))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);
                using (var response = await Client.SendAsync(request))
                {
                    if (!response.IsSuccessStatusCode) throw new InvalidOperationException("Live verification unavailable.");
                    var json = JObject.Parse(await response.Content.ReadAsStringAsync());
                    var row = json["transaction"] as JObject;
                    if ((int?)json["status"] != 200 || (bool?)json.SelectToken("messages.success") != true || row == null)
                        throw new InvalidOperationException("Live verification incomplete.");
                    return Matches(transaction, row);
                }
            }
        }

        public static bool Matches(SePayTransaction t, JObject row)
        {
            decimal incoming, outgoing;
            long id;
            return long.TryParse((string)row["id"], out id) && id == t.Id
                && decimal.TryParse((string)row["amount_in"], NumberStyles.Number, CultureInfo.InvariantCulture, out incoming)
                && decimal.TryParse((string)row["amount_out"], NumberStyles.Number, CultureInfo.InvariantCulture, out outgoing)
                && incoming == (t.TransferType == "in" ? t.Amount : 0) && outgoing == (t.TransferType == "out" ? t.Amount : 0)
                && (string)row["account_number"] == t.AccountNumber
                && string.Equals((string)row["bank_brand_name"], t.Gateway, StringComparison.OrdinalIgnoreCase)
                && (string)row["transaction_content"] == t.Content
                && ((string)row["reference_number"] ?? "") == t.Reference
                && (string)row["transaction_date"] == t.TransactionDate.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        }
    }
}
