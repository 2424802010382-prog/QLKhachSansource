
using System;
using System.IO;
using System.Web;
using Newtonsoft.Json.Linq;

namespace QLKhachSan.Models
{
    public class GeminiSettings
    {
        public string ApiKey { get; set; }
        public string Model { get; set; }
        public int MaxOutputTokens { get; set; }

        public static GeminiSettings Load()
        {
            string path = HttpContext.Current.Server
                .MapPath("~/appsettings.json");

            if (!File.Exists(path))
                throw new FileNotFoundException(
                    "Không tìm thấy appsettings.json");

            JObject json = JObject.Parse(
                File.ReadAllText(path));

            JObject gemini = json["Gemini"] as JObject;

            if (gemini == null)
                throw new Exception("Thiếu cấu hình Gemini.");

            var settings = new GeminiSettings
            {
                ApiKey = (string)gemini["ApiKey"],
                Model = (string)gemini["Model"],
                MaxOutputTokens =
                    (int?)gemini["MaxOutputTokens"] ?? 2048
            };

            if (string.IsNullOrWhiteSpace(settings.Model) || settings.Model.Contains("3.5"))
            {
                settings.Model = "gemini-1.5-flash";
            }

            if (string.IsNullOrWhiteSpace(settings.ApiKey))
                throw new Exception(
                    "Thiếu Gemini ApiKey.");

            return settings;
        }
    }
}
