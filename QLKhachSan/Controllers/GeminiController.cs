
using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web.Mvc;
using Newtonsoft.Json.Linq;
using QLKhachSan.Models;

namespace QLKhachSan.Controllers
{
    public class GeminiController : Controller
    {
        private static readonly HttpClient http =
            new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(90)
            };

        [HttpPost]
        public async Task<JsonResult> Ask(string message)
        {
            // 1. Kiểm tra câu hỏi
            if (string.IsNullOrWhiteSpace(message))
            {
                return Json(new
                {
                    success = false,
                    reply = "Vui lòng nhập câu hỏi."
                });
            }

            if (message.Length > 2000)
            {
                return Json(new
                {
                    success = false,
                    reply = "Câu hỏi không được quá 2000 ký tự."
                });
            }

            try
            {
                // 2. Đọc API Key và Model từ appsettings.json
                GeminiSettings config = GeminiSettings.Load();

                string apiKey = config.ApiKey;
                string model = config.Model;
                int maxTokens = config.MaxOutputTokens;

                // 3. Cấu hình trợ lý khách sạn
                string instruction =
                    "Bạn là Trợ lý AI thông minh của Luxury Hotel (Khách sạn sang trọng). " +
                    "Thân thiện, lịch sự và luôn trả lời bằng tiếng Việt.\n\n" +
                    "Thông tin khách sạn để tư vấn cho khách hàng:\n" +
                    "- Loại phòng & Giá tham khảo:\n" +
                    "  • Standard: 800.000đ/đêm (phù hợp 2 người, đầy đủ tiện nghi)\n" +
                    "  • Twin Room: 1.000.000đ/đêm (2 giường đơn)\n" +
                    "  • Superior: 1.200.000đ/đêm (phòng rộng rãi, view phố)\n" +
                    "  • Family Room: 1.500.000đ/đêm (cho gia đình 3-4 người)\n" +
                    "  • Deluxe: 2.000.000đ/đêm (sang trọng, view đẹp)\n" +
                    "  • Premium: 2.500.000đ/đêm (cao cấp, tiện nghi hiện đại)\n" +
                    "  • Luxury Balcony Suite: 3.500.000đ/đêm (ban công riêng, view toàn cảnh)\n" +
                    "  • Phòng Tổng Thống: 8.000.000đ/đêm (đẳng cấp 5 sao)\n" +
                    "- Dịch vụ & Tiện ích: Nhà hàng cao cấp, Đặt món ăn tận phòng, Đặt xe đưa đón, Spa, Hồ bơi, Wifi tốc độ cao miễn phí, Phục vụ 24/7.\n" +
                    "- Thanh toán: Tiền mặt, Thẻ ngân hàng, Chuyển khoản QR Code nhanh chóng.\n" +
                    "- Check-in / Check-out: Nhận phòng từ 14:00, Trả phòng trước 12:00 trưa.\n" +
                    "- Quy tắc trả lời: Trả lời ngắn gọn, súc tích, dễ nhìn, trình bày đẹp bằng dấu gạch đầu dòng nếu có danh sách.";

                // 4. Chuẩn bị dữ liệu gửi Gemini
                JObject body = new JObject
                {
                    ["system_instruction"] = new JObject
                    {
                        ["parts"] = new JArray(
                            new JObject
                            {
                                ["text"] = instruction
                            })
                    },

                    ["contents"] = new JArray(
                        new JObject
                        {
                            ["role"] = "user",
                            ["parts"] = new JArray(
                                new JObject
                                {
                                    ["text"] = message.Trim()
                                })
                        })
                };

                body["generationConfig"] = new JObject
                {
                    ["maxOutputTokens"] = maxTokens
                };

                // 5. Tạo địa chỉ Gemini API
                string url =
                    "https://generativelanguage.googleapis.com" +
                    "/v1beta/models/" +
                    Uri.EscapeDataString(model) +
                    ":generateContent";

                // 6. Gửi câu hỏi đến Gemini
                using (var request =
                    new HttpRequestMessage(HttpMethod.Post, url))
                {
                    request.Headers.Add(
                        "x-goog-api-key", apiKey);

                    request.Content = new StringContent(
                        body.ToString(),
                        Encoding.UTF8,
                        "application/json");

                    using (var response =
                        await http.SendAsync(request))
                    {
                        string json =
                            await response.Content.ReadAsStringAsync();

                        if (!response.IsSuccessStatusCode)
                        {
                            System.Diagnostics.Trace.TraceWarning(
                                "Gemini API HTTP " +
                                (int)response.StatusCode +
                                ": " + json);

                            return Json(new
                            {
                                success = false,
                                reply = "Trợ lý AI đang bận hoặc cấu hình API key chưa chính xác. Bạn vui lòng thử lại sau ít phút!"
                            });
                        }

                        // 7. Đọc câu trả lời Gemini
                        JObject result = JObject.Parse(json);

                        JArray parts = result.SelectToken(
                            "candidates[0].content.parts"
                        ) as JArray;

                        string answer = parts == null
                            ? ""
                            : string.Join("\n",
                                parts
                                    .Select(p => (string)p["text"])
                                    .Where(t =>
                                        !string.IsNullOrWhiteSpace(t)));

                        if (string.IsNullOrWhiteSpace(answer))
                        {
                            return Json(new
                            {
                                success = false,
                                reply = "AI chưa có câu trả lời."
                            });
                        }

                        // 8. Trả kết quả về chatbot
                        return Json(new
                        {
                            success = true,
                            reply = answer
                        });
                    }
                }
            }
            catch (TaskCanceledException)
            {
                return Json(new
                {
                    success = false,
                    reply = "Hệ thống AI phản hồi quá lâu. Vui lòng thử lại!"
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError(
                    ex.ToString());

                return Json(new
                {
                    success = false,
                    reply = "Không thể kết nối trợ lý AI lúc lúc này. Vui lòng thử lại sau!"
                });
            }
        }
    }
}
