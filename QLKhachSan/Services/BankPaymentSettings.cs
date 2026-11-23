using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Web;
using System.Xml;

namespace QLKhachSan.Services
{
    public sealed class BankPaymentSettings
    {
        public string BankBin { get; set; }
        public string BankName { get; set; }
        public string AccountNumber { get; set; }
        public string AccountName { get; set; }
        public bool Enabled { get; set; }
        public string Mode { get; set; }
        public string Secret { get; set; }
        public string ApiToken { get; set; }
        public bool Ready { get { return Enabled && !string.IsNullOrWhiteSpace(AccountName) && !string.IsNullOrWhiteSpace(Secret)
            && (Mode == "Live" ? !string.IsNullOrWhiteSpace(ApiToken) : Mode == "Sandbox"); } }

        public bool FileExists { get; set; }
        public bool XmlParsed { get; set; }
        public bool ApiTokenLoaded { get { return !string.IsNullOrWhiteSpace(ApiToken); } }
        public bool SecretLoaded { get { return !string.IsNullOrWhiteSpace(Secret); } }
        public bool AccountNameLoaded { get { return !string.IsNullOrWhiteSpace(AccountName); } }
        public string ConfigFilePath { get; set; }
        public string DiagnosticMessage { get; set; }

        public static BankPaymentSettings Load()
        {
            var s = new BankPaymentSettings {
                BankBin = ConfigurationManager.AppSettings["BankPayment.Bin"] ?? "970415",
                BankName = ConfigurationManager.AppSettings["BankPayment.BankName"] ?? "VietinBank",
                AccountNumber = ConfigurationManager.AppSettings["BankPayment.AccountNumber"] ?? "108620022211",
                AccountName = ConfigurationManager.AppSettings["BankPayment.AccountName"] ?? "LUXURY HOTEL",
                Enabled = string.Equals(ConfigurationManager.AppSettings["SePay.Enabled"], "true", StringComparison.OrdinalIgnoreCase),
                Mode = ConfigurationManager.AppSettings["SePay.Mode"] ?? "Disabled",
                Secret = ConfigurationManager.AppSettings["SePay.Secret"]
                    ?? ConfigurationManager.AppSettings["SePay.WebhookSecret"]
                    ?? Environment.GetEnvironmentVariable("SEPAY_WEBHOOK_SECRET"),
                ApiToken = ConfigurationManager.AppSettings["SePay.ApiToken"]
                    ?? ConfigurationManager.AppSettings["SePay.ApiKey"]
                    ?? ConfigurationManager.AppSettings["SePay.Token"]
                    ?? Environment.GetEnvironmentVariable("SEPAY_API_TOKEN")
            };

            string appDataDir = null;
            if (System.Web.Hosting.HostingEnvironment.IsHosted)
            {
                try { appDataDir = System.Web.Hosting.HostingEnvironment.MapPath("~/App_Data"); } catch { }
            }
            if (string.IsNullOrWhiteSpace(appDataDir))
            {
                try
                {
                    string appPath = HttpRuntime.AppDomainAppPath ?? AppDomain.CurrentDomain.BaseDirectory;
                    appDataDir = Path.Combine(appPath, "App_Data");
                }
                catch { }
            }
            if (string.IsNullOrWhiteSpace(appDataDir) || !Directory.Exists(appDataDir))
            {
                try { appDataDir = AppDomain.CurrentDomain.BaseDirectory; } catch { }
            }

            string[] candidateFiles = {
                "paymentsecrets.config",
                "PaymentSecrets.config",
                "paymentsecrets.xml",
                "PaymentSecrets.xml",
                "paymentSecrets.config",
                "payment-secrets.config",
                "payment_secrets.config"
            };

            string bestPath = null;
            bool fileFound = false;
            bool xmlParsed = false;
            string diagnosticNote = null;
            string lastParseError = null;

            if (!string.IsNullOrWhiteSpace(appDataDir) && Directory.Exists(appDataDir))
            {
                var filesToCheck = new System.Collections.Generic.List<string>();
                foreach (var fileName in candidateFiles)
                {
                    try
                    {
                        string fullPath = Path.Combine(appDataDir, fileName);
                        if (File.Exists(fullPath) && !filesToCheck.Contains(fullPath))
                        {
                            filesToCheck.Add(fullPath);
                        }
                    }
                    catch { }
                }

                try
                {
                    foreach (var f in Directory.GetFiles(appDataDir, "*.config").Concat(Directory.GetFiles(appDataDir, "*.xml")))
                    {
                        if (f.IndexOf("payment", StringComparison.OrdinalIgnoreCase) >= 0 && !filesToCheck.Contains(f))
                        {
                            filesToCheck.Add(f);
                        }
                    }
                }
                catch { }

                foreach (var filePath in filesToCheck)
                {
                    fileFound = true;
                    try
                    {
                        var xml = new XmlDocument { XmlResolver = null };
                        using (var reader = XmlReader.Create(filePath, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
                        {
                            xml.Load(reader);
                        }
                        xmlParsed = true;
                        lastParseError = null;

                        string tempSecret = ExtractValue(xml,
                            "webhookSecret", "WebhookSecret", "webhooksecret",
                            "secret", "Secret",
                            "secretKey", "SecretKey", "secretkey",
                            "sepaySecret", "SePaySecret",
                            "webhook_secret", "secret_key", "sepay_secret");

                        string tempToken = ExtractValue(xml,
                            "apiToken", "ApiToken", "apitoken",
                            "sepayApiToken", "SePayApiToken", "sepayapitoken",
                            "userApiToken", "UserApiToken", "userapitoken",
                            "accessToken", "AccessToken", "accesstoken",
                            "bearerToken", "BearerToken", "bearertoken",
                            "apiKey", "ApiKey", "apikey",
                            "api_token", "sepay_api_token", "access_token", "bearer_token", "api_key",
                            "token", "Token");

                        string tempAccount = ExtractValue(xml,
                            "accountName", "AccountName", "accountname",
                            "account_name", "Account_Name",
                            "accountHolder", "AccountHolder", "accountholder",
                            "account_holder", "Account_Holder",
                            "name", "Name");

                        if (string.IsNullOrWhiteSpace(s.Secret) && !string.IsNullOrWhiteSpace(tempSecret))
                            s.Secret = tempSecret;

                        if (string.IsNullOrWhiteSpace(s.ApiToken) && !string.IsNullOrWhiteSpace(tempToken))
                        {
                            s.ApiToken = tempToken;
                            bestPath = filePath;
                        }

                        if (string.IsNullOrWhiteSpace(s.AccountName) && !string.IsNullOrWhiteSpace(tempAccount))
                            s.AccountName = tempAccount;

                        if (bestPath == null)
                        {
                            bestPath = filePath;
                        }

                        if (!string.IsNullOrWhiteSpace(tempToken))
                        {
                            bestPath = filePath;
                            break;
                        }
                        else
                        {
                            System.Diagnostics.Trace.TraceWarning("BankPaymentSettings: File '{0}' exists and parsed successfully, but ApiToken is missing.", filePath);
                        }
                    }
                    catch (Exception ex)
                    {
                        xmlParsed = false;
                        lastParseError = string.Format("{0}: {1}", ex.GetType().Name, ex.Message);
                        System.Diagnostics.Trace.TraceWarning("BankPaymentSettings: File '{0}' exists but failed to parse XML ({1}).", filePath, lastParseError);
                    }
                }
            }

            s.FileExists = fileFound;
            s.XmlParsed = xmlParsed;
            s.ConfigFilePath = bestPath;

            if (fileFound)
            {
                if (!xmlParsed && !string.IsNullOrWhiteSpace(lastParseError))
                {
                    diagnosticNote = string.Format("File found at '{0}' but XML parsing failed: {1}", bestPath ?? appDataDir, lastParseError);
                }
                else if (string.IsNullOrWhiteSpace(s.ApiToken))
                {
                    diagnosticNote = string.Format("Files in App_Data checked (path '{0}'). FileExists=true, XmlParsed={1}, ApiTokenLoaded=false. Reason: ApiToken tag not found or empty in configuration XML.", bestPath ?? appDataDir, xmlParsed);
                }
                else
                {
                    diagnosticNote = string.Format("Files in App_Data checked (path '{0}'). FileExists=true, XmlParsed={1}, ApiTokenLoaded=true.", bestPath ?? appDataDir, xmlParsed);
                }
            }
            else
            {
                diagnosticNote = string.Format("Secrets file not found in App_Data directory ('{0}'). FileExists=false.", appDataDir);
            }
            s.DiagnosticMessage = diagnosticNote;

            System.Diagnostics.Trace.TraceInformation(
                "BankPaymentSettings: ConfigPath='{0}', FileExists={1}, XmlParsed={2}, AccountNameLoaded={3}, SecretLoaded={4}, ApiTokenLoaded={5}.",
                s.ConfigFilePath,
                s.FileExists,
                s.XmlParsed,
                s.AccountNameLoaded,
                s.SecretLoaded,
                s.ApiTokenLoaded);

            return s;
        }

        private static string ExtractValue(XmlDocument xml, params string[] names)
        {
            if (xml == null || xml.DocumentElement == null) return null;

            foreach (XmlNode child in xml.DocumentElement.ChildNodes)
            {
                if (child.NodeType != XmlNodeType.Element) continue;
                foreach (var name in names)
                {
                    if (string.Equals(child.Name, name, StringComparison.OrdinalIgnoreCase))
                    {
                        string val = child.InnerText?.Trim();
                        if (!string.IsNullOrWhiteSpace(val)) return val;
                    }
                }
            }

            foreach (XmlNode node in xml.GetElementsByTagName("*"))
            {
                foreach (var name in names)
                {
                    if (string.Equals(node.Name, name, StringComparison.OrdinalIgnoreCase))
                    {
                        string val = node.InnerText?.Trim();
                        if (!string.IsNullOrWhiteSpace(val)) return val;
                    }
                }
            }

            return null;
        }

        public static string Content(string orderType, int id)
        {
            if (id <= 0 || (orderType != "Booking" && orderType != "Food" && orderType != "RoomService")) throw new ArgumentException("Invalid payment order.");
            return "SEVQR QLKS " + (orderType == "Food" ? "MON " : orderType == "RoomService" ? "DV " : "") + id.ToString(CultureInfo.InvariantCulture);
        }

        public string QrUrl(string content, decimal amount)
        {
            if (amount <= 0) return null;
            string bin = !string.IsNullOrWhiteSpace(BankBin) ? BankBin : "970415";
            string accNum = !string.IsNullOrWhiteSpace(AccountNumber) ? AccountNumber : "108620022211";
            string accName = !string.IsNullOrWhiteSpace(AccountName) ? AccountName : "LUXURY HOTEL";
            return "https://img.vietqr.io/image/" + Uri.EscapeDataString(bin) + "-" + Uri.EscapeDataString(accNum)
                + "-compact2.png?amount=" + amount.ToString("0", CultureInfo.InvariantCulture)
                + "&addInfo=" + Uri.EscapeDataString(content) + "&accountName=" + Uri.EscapeDataString(accName);
        }
    }
}
