using System;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace QLKhachSan.Services
{
    public sealed class SePayTransaction
    {
        public long Id { get; set; }
        public string Gateway { get; set; }
        public string AccountNumber { get; set; }
        public string TransferType { get; set; }
        public decimal Amount { get; set; }
        public string Content { get; set; }
        public string Reference { get; set; }
        public DateTime TransactionDate { get; set; } // SePay sends Vietnam local time.
    }

    public static class SePayProtocol
    {
        public static bool Verify(byte[] body, string timestamp, string signature, string secret, DateTimeOffset now)
        {
            long seconds;
            if (string.IsNullOrEmpty(secret) || body == null || !Regex.IsMatch(timestamp ?? "", @"^\d{10,12}$")
                || !long.TryParse(timestamp, out seconds) || Math.Abs(now.ToUnixTimeSeconds() - seconds) > 300
                || !Regex.IsMatch(signature ?? "", @"^sha256=[a-fA-F0-9]{64}$")) return false;
            byte[] prefix = Encoding.UTF8.GetBytes(timestamp + ".");
            var signed = new byte[prefix.Length + body.Length];
            Buffer.BlockCopy(prefix, 0, signed, 0, prefix.Length);
            Buffer.BlockCopy(body, 0, signed, prefix.Length, body.Length);
            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret)))
            {
                byte[] expected = hmac.ComputeHash(signed);
                int difference = 0;
                for (int i = 0; i < expected.Length; i++) difference |= expected[i] ^ Convert.ToByte(signature.Substring(7 + i * 2, 2), 16);
                return difference == 0;
            }
        }

        public static SePayTransaction Parse(byte[] raw)
        {
            JObject j;
            using (var reader = new JsonTextReader(new System.IO.StringReader(new UTF8Encoding(false, true).GetString(raw))) { DateParseHandling = DateParseHandling.None, MaxDepth = 16 })
            {
                j = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                if (reader.Read()) throw new FormatException("Trailing JSON.");
            }
            DateTime date;
            long id; decimal amount;
            if (j["id"]?.Type != JTokenType.Integer || !long.TryParse((string)j["id"], out id) || id <= 0
                || !decimal.TryParse((string)j["transferAmount"], NumberStyles.Number, CultureInfo.InvariantCulture, out amount)
                || amount <= 0 || amount != decimal.Truncate(amount) || amount > 9999999999999m
                || !DateTime.TryParseExact((string)j["transactionDate"], "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
                throw new FormatException("Invalid transaction.");
            var t = new SePayTransaction { Id = id, Amount = amount, TransactionDate = date,
                Gateway = Text(j, "gateway", 50), AccountNumber = Text(j, "accountNumber", 50),
                TransferType = Text(j, "transferType", 10), Content = Text(j, "content", 4000), Reference = Text(j, "referenceCode", 200, true) };
            if (t.TransferType != "in" && t.TransferType != "out") throw new FormatException("Invalid direction.");
            return t;
        }

        private static string Text(JObject j, string key, int max, bool optional = false)
        {
            var token = j[key];
            if (optional && (token == null || token.Type == JTokenType.Null)) return "";
            if (token == null || token.Type != JTokenType.String) throw new FormatException("Invalid field.");
            string value = (string)token;
            if (value.Length > max || (!optional && string.IsNullOrWhiteSpace(value))) throw new FormatException("Invalid field length.");
            return value;
        }

        public static bool TryOrder(string content, out string type, out int id)
        {
            type = null; id = 0;
            // Bank descriptions may add text around the reference; never accept two references.
            var matches = Regex.Matches(content ?? "", @"(?<![A-Z0-9])SEVQR\s+QLKS\s+(?:(MON|DV)\s+)?([1-9][0-9]*)(?![A-Z0-9])", RegexOptions.IgnoreCase);
            if (matches.Count != 1 || Regex.Matches(content ?? "", "SEVQR", RegexOptions.IgnoreCase).Count != 1
                || !int.TryParse(matches[0].Groups[2].Value, out id)) return false;
            string group1 = (matches[0].Groups[1].Value ?? "").ToUpperInvariant();
            type = group1 == "MON" ? "Food" : (group1 == "DV" ? "RoomService" : "Booking");
            return true;
        }

        public static string Hash(byte[] bytes)
        { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
    }
}
