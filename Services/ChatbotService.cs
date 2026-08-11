using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WEBBANQUANAO.Data;
using WEBBANQUANAO.Models.Entities;

namespace WEBBANQUANAO.Services;

public class ChatbotService : IChatbotService
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _config;
    private readonly HttpClient _httpClient;

    public ChatbotService(ApplicationDbContext context, IConfiguration config, HttpClient httpClient)
    {
        _context = context;
        _config = config;
        _httpClient = httpClient;
    }

    public async Task<(string reply, object? data)> ProcessUserMessageAsync(string userMessage, int userId)
    {
        if (string.IsNullOrWhiteSpace(userMessage))
        {
            return ("Xin chào! Tôi là Trợ Lý AI của FASHION STORE. Tôi có thể giúp bạn chọn size, tư vấn phối đồ hoặc tra cứu đơn hàng!", null);
        }

        string rawMessage = userMessage.Trim();
        string lowerMessage = rawMessage.ToLower();

        // 1. Thu thập dữ liệu thời gian thực từ CSDL SQL Server (RAG Engine)
        var databaseContext = await BuildLiveDatabaseContextAsync(userId);
        
        // 2. Tìm kiếm sản phẩm liên quan trong CSDL chuẩn xác theo Nam/Nữ/Loại sản phẩm
        var matchedProductCards = await FindMatchingProductCardsAsync(lowerMessage);

        // 3. Đọc cấu hình API từ appsettings.json
        string apiKey = _config["Chatbot:ApiKey"] ?? string.Empty;
        string endpoint = _config["Chatbot:Endpoint"] ?? string.Empty;
        string model = _config["Chatbot:Model"] ?? "gemini-1.5-flash";

        string aiReply = string.Empty;

        // 4. Gọi AI LLM API (Google Gemini / OpenAI)
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            try
            {
                aiReply = await CallExternalAiApiAsync(apiKey, endpoint, model, rawMessage, databaseContext);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AI API Call Error: {ex.Message}");
            }
        }

        // 5. Nếu không gọi được API, dùng bộ sinh phản hồi CSDL thông minh nội bộ
        if (string.IsNullOrWhiteSpace(aiReply))
        {
            aiReply = await GenerateLocalDatabaseReplyAsync(lowerMessage, userId);
        }

        // 6. Lưu nhật ký hội thoại
        if (userId > 0)
        {
            _context.ChatConversations.Add(new ChatConversation
            {
                UserId = userId,
                UserMessage = rawMessage,
                BotResponse = aiReply,
                Timestamp = DateTime.Now
            });
            await _context.SaveChangesAsync();
        }

        return (aiReply, matchedProductCards);
    }

    #region AI API Callers (Google Gemini / OpenAI Integration)

    private async Task<string> CallExternalAiApiAsync(string apiKey, string endpoint, string model, string userQuery, string dbContextInfo)
    {
        string systemPrompt = $@"Bạn là Trợ Lý AI Tư Vấn Bán Hàng & Thời Trang của thương hiệu FASHION STORE.
Quy tắc trả lời BẮT BUỘC:
1. Khi khách hàng hỏi sản phẩm cho NAM (hoặc đồ nam), BẠN CHỈ ĐƯỢC LIỆT KÊ các sản phẩm dành cho NAM hoặc Unisex. KHÔNG ĐƯỢC đưa váy, đầm hoặc đồ nữ vào!
2. Khi khách hàng hỏi sản phẩm cho NỮ (hoặc đồ nữ), BẠN CHỈ ĐƯỢC LIỆT KÊ các sản phẩm dành cho NỮ hoặc Unisex. KHÔNG ĐƯỢC đưa thắt lưng nam, sơ mi nam vào!
3. Trả lời thân thiện, liệt kê giá tiền, chất liệu, size và màu sắc từ dữ liệu bên dưới.

DỮ LIỆU CƠ SỞ DỮ LIỆU SQL SERVER THỜI GIAN THỰC:
---------------------------------------------------
{dbContextInfo}
---------------------------------------------------

CÂU HỎI CỦA KHÁCH HÀNG: ""{userQuery}""";

        if (string.IsNullOrEmpty(endpoint) || endpoint.Contains("googleapis") || model.Contains("gemini"))
        {
            string url = !string.IsNullOrEmpty(endpoint) 
                ? endpoint 
                : $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";

            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = systemPrompt }
                        }
                    }
                }
            };

            var jsonContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(url, jsonContent);

            if (response.IsSuccessStatusCode)
            {
                var responseString = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(responseString);
                var candidates = doc.RootElement.GetProperty("candidates");
                if (candidates.GetArrayLength() > 0)
                {
                    var parts = candidates[0].GetProperty("content").GetProperty("parts");
                    if (parts.GetArrayLength() > 0)
                    {
                        return parts[0].GetProperty("text").GetString() ?? string.Empty;
                    }
                }
            }
        }
        else if (endpoint.Contains("openai") || model.Contains("gpt"))
        {
            var requestBody = new
            {
                model = string.IsNullOrEmpty(model) ? "gpt-3.5-turbo" : model,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userQuery }
                },
                temperature = 0.7
            };

            var jsonContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
            var response = await _httpClient.PostAsync(endpoint, jsonContent);

            if (response.IsSuccessStatusCode)
            {
                var responseString = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(responseString);
                var choices = doc.RootElement.GetProperty("choices");
                if (choices.GetArrayLength() > 0)
                {
                    return choices[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
                }
            }
        }

        return string.Empty;
    }

    #endregion

    #region Real-Time Database Context Extractor (RAG Data Engine)

    private async Task<string> BuildLiveDatabaseContextAsync(int userId)
    {
        var sb = new StringBuilder();

        var categories = await _context.Categories.Select(c => c.CategoryName).ToListAsync();
        var brands = await _context.Brands.Select(b => b.BrandName).ToListAsync();
        sb.AppendLine($"• DANH MỤC SẢN PHẨM: {string.Join(", ", categories)}");
        sb.AppendLine($"• THƯƠNG HIỆU: {string.Join(", ", brands)}");

        var products = await _context.Products
            .Where(p => p.Status == ProductStatus.Active)
            .Include(p => p.Category)
            .Include(p => p.Brand)
            .Include(p => p.Variants)
            .ToListAsync();

        sb.AppendLine("• DANH SÁCH SẢN PHẨM TRONG KHO (CHI TIẾT GIỚI TÍNH):");
        foreach (var p in products)
        {
            string genderStr = p.Gender switch
            {
                ProductGender.Male => "Dành Cho NAM",
                ProductGender.Female => "Dành Cho NỮ",
                _ => "UNISEX (Nam & Nữ Đều Dùng Được)"
            };

            var sizes = string.Join("/", p.Variants.Select(v => v.Size).Distinct());
            var colors = string.Join("/", p.Variants.Select(v => v.Color).Distinct());
            int totalStock = p.Variants.Sum(v => v.StockQuantity);

            sb.AppendLine($"  - [{p.ProductName}] | Giới tính: {genderStr} | Giá: {p.BasePrice:N0}đ | Danh mục: {p.Category.CategoryName} | Size: {sizes} | Màu: {colors} | Tồn kho: {totalStock} SP");
        }

        var promotions = await _context.Promotions
            .Where(p => p.StartDate <= DateTime.Now && p.EndDate >= DateTime.Now)
            .ToListAsync();

        if (promotions.Any())
        {
            sb.AppendLine("• MÃ KHUYẾN MÃI ĐANG ÁP DỤNG:");
            foreach (var promo in promotions)
            {
                string val = promo.DiscountType == DiscountType.Percentage ? $"{promo.DiscountValue}%" : $"{promo.DiscountValue:N0}đ";
                sb.AppendLine($"  - Mã: [{promo.Code}] | Giảm: {val} cho đơn từ {promo.MinOrderValue:N0}đ");
            }
        }

        if (userId > 0)
        {
            var userOrders = await _context.Orders
                .Where(o => o.UserId == userId)
                .OrderByDescending(o => o.OrderDate)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Variant)
                        .ThenInclude(v => v.Product)
                .Take(3)
                .ToListAsync();

            if (userOrders.Any())
            {
                sb.AppendLine("• LỊCH SỬ ĐƠN HÀNG CỦA KHÁCH:");
                foreach (var ord in userOrders)
                {
                    var items = string.Join(", ", ord.OrderDetails.Select(d => $"{d.Variant.Product.ProductName} (x{d.Quantity})"));
                    sb.AppendLine($"  - Mã đơn #{ord.OrderNumber} | Trạng thái: {ord.Status} | Tổng tiền: {ord.TotalAmount:N0}đ | Mặt hàng: {items}");
                }
            }
        }

        sb.AppendLine("• BẢNG TƯ VẤN SIZE CHUẨN:");
        sb.AppendLine("  - Size S: < 52kg (1m50 - 1m60)");
        sb.AppendLine("  - Size M: 53kg - 62kg (1m61 - 1m70)");
        sb.AppendLine("  - Size L: 63kg - 72kg (1m71 - 1m78)");
        sb.AppendLine("  - Size XL: 73kg - 85kg (1m79 - 1m85)");

        return sb.ToString();
    }

    private async Task<object?> FindMatchingProductCardsAsync(string lowerMessage)
    {
        var query = _context.Products
            .Where(p => p.Status == ProductStatus.Active)
            .Include(p => p.Images)
            .AsQueryable();

        bool isNamRequested = lowerMessage.Contains("nam") || lowerMessage.Contains("trai") || lowerMessage.Contains("đàn ông");
        bool isNuRequested = lowerMessage.Contains("nữ") || lowerMessage.Contains("gái") || lowerMessage.Contains("phụ nữ");

        if (isNamRequested && !isNuRequested)
        {
            query = query.Where(p => p.Gender == ProductGender.Male || p.Gender == ProductGender.Unisex);
        }
        else if (isNuRequested && !isNamRequested)
        {
            query = query.Where(p => p.Gender == ProductGender.Female || p.Gender == ProductGender.Unisex);
        }

        if (lowerMessage.Contains("áo")) query = query.Where(p => p.ProductName.Contains("Áo"));
        else if (lowerMessage.Contains("quần")) query = query.Where(p => p.ProductName.Contains("Quần"));
        else if (lowerMessage.Contains("váy") || lowerMessage.Contains("đầm")) query = query.Where(p => p.ProductName.Contains("Váy") || p.ProductName.Contains("Đầm"));

        var matchedProducts = await query.Take(4).ToListAsync();

        if (matchedProducts.Any())
        {
            return matchedProducts.Select(p => new
            {
                id = p.ProductId,
                name = p.ProductName,
                price = $"{p.BasePrice:N0}đ",
                image = p.Images.FirstOrDefault()?.ImageUrl ?? "/images/no-image.png",
                url = $"/Product/Details/{p.ProductId}"
            });
        }

        return null;
    }

    private async Task<string> GenerateLocalDatabaseReplyAsync(string lowerMessage, int userId)
    {
        bool isNam = lowerMessage.Contains("nam") || lowerMessage.Contains("đàn ông") || lowerMessage.Contains("trai");
        bool isNu = lowerMessage.Contains("nữ") || lowerMessage.Contains("phụ nữ") || lowerMessage.Contains("gái");

        // 1. CHÍNH SÁCH ĐỔI TRẢ & HẬU MÃI
        if (lowerMessage.Contains("đổi") || lowerMessage.Contains("trả") || lowerMessage.Contains("bảo hành") || lowerMessage.Contains("hậu mãi") || lowerMessage.Contains("lỗi"))
        {
            return "👑 **CHÍNH SÁCH ĐỔI TRẢ & BẢO HÀNH CHUẨN VIP TẠI FASHION STORE**:\n" +
                   "• **Đổi trả 7 ngày miễn phí**: Áp dụng cho mọi sản phẩm nguyên tem mác, chưa qua sử dụng.\n" +
                   "• **Bảo hành 1 đổi 1**: Nếu phát hiện lỗi từ nhà sản xuất hoặc giao sai size/màu, FASHION STORE sẽ cho shipper mang hàng mới đổi tận nhà hoàn toàn miễn phí!\n" +
                   "• **Hỗ trợ sửa đồ/Đổi size**: Hỗ trợ đổi size tận nơi nhanh chóng trong 48h.\n" +
                   "Bạn cần đổi trả sản phẩm nào, hãy cho tôi biết mã đơn hàng để hỗ trợ ngay nhé!";
        }

        // 2. CHÍNH SÁCH GIAO HÀNG & PHÍ SHIP
        if (lowerMessage.Contains("giao hàng") || lowerMessage.Contains("ship") || lowerMessage.Contains("vận chuyển") || lowerMessage.Contains("bao lâu") || lowerMessage.Contains("phí"))
        {
            return "🚚 **CHÍNH SÁCH VẬN CHUYỂN TOÀN QUỐC**:\n" +
                   "• **Miễn phí vận chuyển**: Cho tất cả đơn hàng từ **500.000đ** trở lên.\n" +
                   "• **Giao hỏa tốc 2H**: Áp dụng tại khu vực nội thành TP. Hồ Chí Minh.\n" +
                   "• **Giao tiêu chuẩn 2 - 3 ngày**: Áp dụng cho tất cả các tỉnh thành trên toàn quốc.\n" +
                   "• Bạn được mở hàng kiểm tra thoải mái trước khi thanh toán cho shipper!";
        }

        // 3. TƯ VẤN CHỌN SIZE THEO CHIỀU CAO & CÂN NẶNG
        if (lowerMessage.Contains("size") || lowerMessage.Contains("kích thước") || lowerMessage.Contains("chiều cao") || lowerMessage.Contains("cân nặng") || lowerMessage.Contains("mặc vừ"))
        {
            return "📏 **BẢNG QUY ĐỔI SIZE CHUẨN TẠI FASHION STORE**:\n" +
                   "• **Size S**: Dưới 52kg | Cao 1m50 - 1m60\n" +
                   "• **Size M**: 53kg - 62kg | Cao 1m61 - 1m70\n" +
                   "• **Size L**: 63kg - 72kg | Cao 1m71 - 1m78\n" +
                   "• **Size XL**: 73kg - 85kg | Cao 1m79 - 1m85\n" +
                   "• **Size XXL**: Trên 85kg | Dành cho phom rộng Oversize\n\n" +
                   "👉 Bạn có thể nhắn cho tôi: *Cân nặng và Chiều cao* của bạn để tôi tư vấn chuẩn xác nhất!";
        }

        // 4. PHƯƠNG THỨC THANH TOÁN
        if (lowerMessage.Contains("thanh toán") || lowerMessage.Contains("chuyển khoản") || lowerMessage.Contains("cod") || lowerMessage.Contains("vnpay"))
        {
            return "💳 **PHƯƠNG THỨC THANH TOÁN AN TOÀN**:\n" +
                   "• **Thanh toán COD**: Nhận hàng, kiểm tra rồi mới trả tiền mặt.\n" +
                   "• **VNPAY QR Code**: Quét mã nhận ngay ưu đãi giảm giá.\n" +
                   "• **Chuyển khoản Ngân hàng**: Tự động xác nhận đơn hàng sau 10 giây qua QR Code!";
        }

        // 5. ĐẶC QUYỀN THÀNH VIÊN VIP
        if (lowerMessage.Contains("vip") || lowerMessage.Contains("hạng") || lowerMessage.Contains("thành viên") || lowerMessage.Contains("tích lũy"))
        {
            return "🌟 **ĐẶC QUYỀN TÍCH LŨY HẠNG THÀNH VIÊN VIP**:\n" +
                   "• **Hạng Đồng (< 2 triệu)**: Tích điểm ưu đãi tiêu chuẩn.\n" +
                   "• **Hạng Bạc (≥ 2 triệu)**: Tự động GIẢM 5% cho mọi đơn hàng.\n" +
                   "• **Hạng Vàng (≥ 5 triệu)**: Tự động GIẢM 10% mọi đơn + Quà sinh nhật.\n" +
                   "• **Hạng Kim Cương (≥ 10 triệu)**: Tự động GIẢM 15% trọn đời + Chăm sóc VIP riêng!\n" +
                   "Đăng nhập tài khoản để xem hạng của bạn ngay trên Thanh Menu nhé!";
        }

        // 6. GỢI Ý ĐỒ NAM
        if (isNam && !isNu)
        {
            var maleProducts = await _context.Products
                .Where(p => p.Status == ProductStatus.Active && (p.Gender == ProductGender.Male || p.Gender == ProductGender.Unisex))
                .Take(5)
                .ToListAsync();

            var sb = new StringBuilder("👔 **BỘ SỰ TẬP THỜI TRANG NAM CAO CẤP BÁN CHẠY NHẤT**:\n");
            foreach (var p in maleProducts)
            {
                sb.AppendLine($"• **{p.ProductName}**: {p.BasePrice:N0}đ");
            }
            sb.AppendLine("\nBạn có thể nhấp vào các thẻ sản phẩm bên dưới để xem chi tiết ảnh và đặt mua nhé!");
            return sb.ToString();
        }

        // 7. GỢI Ý ĐỒ NỮ
        if (isNu && !isNam)
        {
            var femaleProducts = await _context.Products
                .Where(p => p.Status == ProductStatus.Active && (p.Gender == ProductGender.Female || p.Gender == ProductGender.Unisex))
                .Take(5)
                .ToListAsync();

            var sb = new StringBuilder("👗 **BỘ SỰ TẬP THỜI TRANG NỮ QUYẾN RŨ MỚI VỀ**:\n");
            foreach (var p in femaleProducts)
            {
                sb.AppendLine($"• **{p.ProductName}**: {p.BasePrice:N0}đ");
            }
            sb.AppendLine("\nBạn có thể nhấp vào các thẻ sản phẩm bên dưới để xem chi tiết ảnh và đặt mua nhé!");
            return sb.ToString();
        }

        // 8. TRA CỨU ĐƠN HÀNG
        if (lowerMessage.Contains("đơn hàng") || lowerMessage.Contains("trạng thái") || lowerMessage.Contains("tra cứu"))
        {
            if (userId > 0)
            {
                var recentOrder = await _context.Orders
                    .Where(o => o.UserId == userId)
                    .OrderByDescending(o => o.OrderDate)
                    .FirstOrDefaultAsync();

                if (recentOrder != null)
                {
                    string statusText = recentOrder.Status switch
                    {
                        OrderStatus.Pending => "Chờ xử lý",
                        OrderStatus.Shipping => "Đang giao hàng",
                        OrderStatus.Completed => "Đã hoàn thành",
                        OrderStatus.Cancelled => "Đã hủy",
                        _ => "Không xác định"
                    };

                    return $"📦 **ĐƠN HÀNG GẦN NHẤT CỦA BẠN**:\n" +
                           $"• Mã đơn: **#{recentOrder.OrderNumber}**\n" +
                           $"• Ngày đặt: {recentOrder.OrderDate:dd/MM/yyyy HH:mm}\n" +
                           $"• Trạng thái: **{statusText}**\n" +
                           $"• Tổng thanh toán: **{recentOrder.TotalAmount:N0}đ**";
                }
                return "Bạn chưa có đơn hàng nào trong hệ thống. Hãy dạo quanh cửa hàng và chọn cho mình bộ trang phục ưng ý nhé!";
            }
            return "Bạn vui lòng **Đăng nhập** tài khoản để tôi trợ giúp tra cứu lịch sử đơn hàng cá nhân nhé!";
        }

        // 9. MÃ GIẢM GIÁ
        if (lowerMessage.Contains("mã") || lowerMessage.Contains("khuyến mãi") || lowerMessage.Contains("voucher") || lowerMessage.Contains("giảm giá"))
        {
            var promos = await _context.Promotions
                .Where(p => p.StartDate <= DateTime.Now && p.EndDate >= DateTime.Now)
                .ToListAsync();

            if (promos.Any())
            {
                var sb = new StringBuilder("🎁 **DANH SÁCH MÃ GIẢM GIÁ ĐANG HOẠT ĐỘNG**:\n");
                foreach (var pr in promos)
                {
                    string val = pr.DiscountType == DiscountType.Percentage ? $"{pr.DiscountValue}%" : $"{pr.DiscountValue:N0}đ";
                    sb.AppendLine($"• Mã **{pr.Code}**: Giảm {val} cho đơn từ {pr.MinOrderValue:N0}đ.");
                }
                return sb.ToString();
            }
            return "🎁 Hiện tại cửa hàng đang áp dụng ưu đãi **Miễn phí vận chuyển** cho đơn hàng từ 500.000đ!";
        }

        // 10. THÔNG TIN CỬA HÀNG
        if (lowerMessage.Contains("địa chỉ") || lowerMessage.Contains("cửa hàng") || lowerMessage.Contains("hotline") || lowerMessage.Contains("liên hệ"))
        {
            return "🏪 **FASHION STORE - COMMAND CENTER**:\n" +
                   "• **Địa chỉ**: 123 Đường Thời Trang, Quận 1, TP. Hồ Chí Minh\n" +
                   "• **Hotline**: 1900 6789 (8:00 - 22:00 hàng ngày)\n" +
                   "• **Email**: support@fashionstore.vn\n" +
                   "Rất hân hạnh được đón tiếp và tư vấn cho bạn!";
        }

        return "Xin chào! Tôi là Trợ Lý AI của FASHION STORE. Bạn muốn tìm thời trang **Cho Nam** hay **Cho Nữ**, tư vấn **Chọn Size**, **Chính sách đổi trả** hay **Tra cứu đơn hàng** hãy nhắn cho tôi ngay nhé!";
    }

    #endregion
}
