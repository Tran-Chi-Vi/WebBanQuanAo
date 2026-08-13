using Microsoft.EntityFrameworkCore;
using WEBBANQUANAO.Data;
using WEBBANQUANAO.Models.Entities;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace WEBBANQUANAO.Services;

public class EmailService : IEmailService
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public EmailService(ApplicationDbContext context, IConfiguration configuration, ILogger<EmailService> logger, IHttpClientFactory httpClientFactory)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<bool> SendOtpEmailAsync(string toEmail, string recipientName, string otpCode)
    {
        string subject = "[FASHION STORE] - Mã OTP Xác Thực Đổi Mật Khẩu";
        
        string body = $@"
            <div style=""font-family: 'Segoe UI', Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; background-color: #f8fafc; border-radius: 16px; border: 1px solid #e2e8f0;"">
                <div style=""text-align: center; padding-bottom: 20px; border-bottom: 2px solid #e2e8f0;"">
                    <h2 style=""background: linear-gradient(135deg, #6366f1, #ec4899); -webkit-background-clip: text; color: #6366f1; margin: 0; font-size: 24px;"">FASHION STORE</h2>
                    <p style=""color: #64748b; margin: 5px 0 0 0; font-size: 14px;"">Hệ thống thời trang cao cấp & phong cách hiện đại</p>
                </div>
                
                <div style=""padding: 30px 10px; text-align: center;"">
                    <h3 style=""color: #0f172a; margin-top: 0;"">XÁC NHẬN YÊU CẦU QUÊN MẬT KHẨU</h3>
                    <p style=""color: #334155; line-height: 1.6;"">Xin chào <strong>{recipientName}</strong>,</p>
                    <p style=""color: #334155; line-height: 1.6;"">Chúng tôi đã nhận được yêu cầu đặt lại mật khẩu cho tài khoản Gmail <strong>{toEmail}</strong>.</p>
                    <p style=""color: #334155; line-height: 1.6;"">Dưới đây là mã xác thực OTP 6 chữ số của bạn:</p>

                    <div style=""margin: 25px auto; width: 220px; padding: 15px; background: linear-gradient(135deg, #6366f1, #8b5cf6); color: white; font-size: 32px; font-weight: bold; letter-spacing: 8px; border-radius: 12px; box-shadow: 0 4px 15px rgba(99,102,241,0.3);"">
                        {otpCode}
                    </div>

                    <p style=""color: #ef4444; font-size: 13px; font-weight: bold;"">⚠️ Mã OTP có hiệu lực trong 10 phút. Vui lòng tuyệt đối không chia sẻ mã này cho bất kỳ ai.</p>
                </div>

                <div style=""border-top: 1px solid #e2e8f0; padding-top: 15px; text-align: center; color: #94a3b8; font-size: 12px;"">
                    <p style=""margin: 0;"">Nếu bạn không thực hiện yêu cầu này, vui lòng bỏ qua email này hoặc liên hệ hỗ trợ.</p>
                    <p style=""margin: 5px 0 0 0;"">© 2026 FASHION STORE. All rights reserved.</p>
                </div>
            </div>";

        return await SendEmailInternalAsync(toEmail, subject, body);
    }

    public async Task<bool> SendOrderInvoiceEmailAsync(int orderId)
    {
        var order = await _context.Orders
            .Include(o => o.User)
            .Include(o => o.Address)
            .Include(o => o.Payment)
            .Include(o => o.OrderDetails)
                .ThenInclude(od => od.Variant)
                    .ThenInclude(v => v.Product)
                        .ThenInclude(p => p.Images)
            .FirstOrDefaultAsync(o => o.OrderId == orderId);

        if (order == null || order.User == null) return false;

        string statusText = order.Status switch
        {
            OrderStatus.Pending => "<span style=\"color:#f59e0b;font-weight:bold;padding:4px 12px;background:#fef3c7;border-radius:20px;\">⏳ Đang Xử Lý</span>",
            OrderStatus.Shipping => "<span style=\"color:#06b6d4;font-weight:bold;padding:4px 12px;background:#cff4fc;border-radius:20px;\">🚚 Đang Giao Hàng</span>",
            OrderStatus.Completed => "<span style=\"color:#10b981;font-weight:bold;padding:4px 12px;background:#d1fae5;border-radius:20px;\">✅ Đã Hoàn Thành</span>",
            OrderStatus.Cancelled => "<span style=\"color:#ef4444;font-weight:bold;padding:4px 12px;background:#fee2e2;border-radius:20px;\">❌ Đã Hủy</span>",
            _ => "<span style=\"color:#64748b;\">Chờ Duyệt</span>"
        };

        string subject = $"[FASHION STORE] - Hóa Đơn Đơn Hàng #{order.OrderNumber} ({order.Status})";

        var itemsHtml = "";
        int totalItemsCount = 0;

        foreach (var item in order.OrderDetails)
        {
            var p = item.Variant?.Product;
            var imgUrl = p?.Images.FirstOrDefault(i => i.IsMain)?.ImageUrl ?? p?.Images.FirstOrDefault()?.ImageUrl ?? "https://via.placeholder.com/80";
            if (!imgUrl.StartsWith("http") && !imgUrl.StartsWith("/")) imgUrl = "/" + imgUrl;
            
            totalItemsCount += item.Quantity;
            decimal itemTotal = item.UnitPrice * item.Quantity;

            itemsHtml += $@"
                <tr>
                    <td style=""padding: 12px; border-bottom: 1px solid #e2e8f0; vertical-align: middle;"">
                        <img src=""{imgUrl}"" alt=""{p?.ProductName}"" style=""width: 60px; height: 60px; object-fit: cover; border-radius: 8px; border: 1px solid #e2e8f0;"" />
                    </td>
                    <td style=""padding: 12px; border-bottom: 1px solid #e2e8f0; vertical-align: middle;"">
                        <strong style=""color: #0f172a; font-size: 14px; d-block;"">{p?.ProductName}</strong>
                        <div style=""color: #64748b; font-size: 12px; margin-top: 4px;"">Size: <strong>{item.Variant?.Size}</strong> | Màu: <strong>{item.Variant?.Color}</strong></div>
                    </td>
                    <td style=""padding: 12px; border-bottom: 1px solid #e2e8f0; text-align: center; vertical-align: middle; font-weight: bold;"">
                        x{item.Quantity}
                    </td>
                    <td style=""padding: 12px; border-bottom: 1px solid #e2e8f0; text-align: right; vertical-align: middle; color: #4338ca; font-weight: bold;"">
                        {itemTotal:N0}đ
                    </td>
                </tr>";
        }

        string addressText = order.Address != null 
            ? $"{order.Address.RecipientName} - SĐT: {order.Address.Phone} ({order.Address.DetailAddress}, {order.Address.Ward}, {order.Address.District}, {order.Address.Province})"
            : "Chưa cập nhật";

        string body = $@"
            <div style=""font-family: 'Segoe UI', Arial, sans-serif; max-width: 650px; margin: 0 auto; padding: 25px; background-color: #ffffff; border-radius: 16px; border: 1px solid #e2e8f0; box-shadow: 0 4px 20px rgba(0,0,0,0.05);"">
                
                <!-- HEADER -->
                <div style=""display: flex; justify-content: space-between; align-items: center; padding-bottom: 20px; border-bottom: 2px solid #f1f5f9;"">
                    <div>
                        <h2 style=""color: #6366f1; margin: 0; font-size: 24px; font-weight: bold;"">FASHION STORE</h2>
                        <span style=""color: #64748b; font-size: 13px;"">HÓA ĐƠN XÁC NHẬN ĐƠN HÀNG</span>
                    </div>
                    <div style=""text-align: right;"">
                        <div style=""font-family: monospace; font-size: 14px; font-weight: bold; color: #0f172a;"">#{order.OrderNumber}</div>
                        <div style=""color: #94a3b8; font-size: 12px; margin-top: 4px;"">{order.OrderDate:dd/MM/yyyy HH:mm}</div>
                    </div>
                </div>

                <!-- STATUS BADGE -->
                <div style=""margin: 20px 0; padding: 15px; background: #f8fafc; border-radius: 12px; display: flex; align-items: center; justify-content: space-between;"">
                    <span style=""color: #334155; font-size: 14px; font-weight: 500;"">Trạng Thái Đơn Hàng Xét Duyệt:</span>
                    <div>{statusText}</div>
                </div>

                <!-- RECIPIENT INFO -->
                <div style=""margin-bottom: 25px; padding: 15px; background: #f8fafc; border-radius: 12px; font-size: 13px; color: #334155; line-height: 1.6;"">
                    <div style=""font-weight: bold; color: #0f172a; margin-bottom: 6px; font-size: 14px;"">📌 THÔNG TIN GIAO HÀNG:</div>
                    <div>Khách hàng: <strong>{order.User.FullName}</strong> ({order.User.Email})</div>
                    <div>Địa chỉ nhận: <strong>{addressText}</strong></div>
                    <div>Thanh toán: <strong>{(order.Payment?.Status == PaymentStatus.Success ? "Đã thanh toán" : "Thanh toán khi nhận hàng / Chờ xác nhận")}</strong></div>
                </div>

                <!-- PRODUCT ITEMS TABLE -->
                <h4 style=""color: #0f172a; margin: 0 0 12px 0; font-size: 15px;"">🛒 DANH SÁCH SẢN PHẨM MUA ({totalItemsCount} sản phẩm):</h4>
                <table style=""width: 100%; border-collapse: collapse; margin-bottom: 20px;"">
                    <thead>
                        <tr style=""background: #f1f5f9; color: #475569; font-size: 12px; text-transform: uppercase;"">
                            <th style=""padding: 10px; text-align: left;"">Hình ảnh</th>
                            <th style=""padding: 10px; text-align: left;"">Sản phẩm</th>
                            <th style=""padding: 10px; text-align: center;"">SL</th>
                            <th style=""padding: 10px; text-align: right;"">Thành tiền</th>
                        </tr>
                    </thead>
                    <tbody>
                        {itemsHtml}
                    </tbody>
                </table>

                <!-- TOTAL SUMMARY -->
                <div style=""text-align: right; padding-top: 15px; border-top: 2px dashed #e2e8f0;"">
                    <div style=""font-size: 18px; font-weight: bold; color: #0f172a;"">
                        TỔNG CỘNG THANH TOÁN: <span style=""color: #6366f1; font-size: 24px;"">{order.TotalAmount:N0}đ</span>
                    </div>
                </div>

                <!-- FOOTER -->
                <div style=""margin-top: 30px; padding-top: 20px; border-top: 1px solid #f1f5f9; text-align: center; color: #94a3b8; font-size: 12px; line-height: 1.5;"">
                    <p style=""margin: 0;"">Cảm ơn bạn đã tin tưởng mua sắm tại <strong>FASHION STORE</strong>!</p>
                    <p style=""margin: 4px 0 0 0;"">Mọi thắc mắc về đơn hàng xin vui lòng liên hệ Hotline: 1900-FASHION hoặc Email: support@fashionstore.vn</p>
                </div>
            </div>";

        return await SendEmailInternalAsync(order.User.Email, subject, body);
    }

    public async Task<bool> SendChurnWinBackEmailAsync(string toEmail, string recipientName, string voucherCode, decimal discountValue, DiscountType discountType, DateTime endDate)
    {
        string discountText = discountType == DiscountType.Percentage 
            ? $"{discountValue:N0}%" 
            : $"{discountValue:N0}đ";

        string subject = $"🎁 [FASHION STORE] - Tặng Riêng Bạn Voucher {discountText} - Chúng Tôi Rất Nhớ Bạn!";

        string htmlBody = $@"
            <div style=""font-family: 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; max-width: 620px; margin: 0 auto; padding: 24px; background-color: #f8fafc; border-radius: 20px; border: 1px solid #e2e8f0;"">
                <!-- Header -->
                <div style=""text-align: center; padding-bottom: 20px; border-bottom: 2px solid #e2e8f0;"">
                    <h2 style=""background: linear-gradient(135deg, #6366f1, #ec4899); -webkit-background-clip: text; color: #6366f1; margin: 0; font-size: 26px; font-weight: 800; font-family: 'Helvetica Neue', sans-serif;"">FASHION STORE</h2>
                    <p style=""color: #64748b; margin: 6px 0 0 0; font-size: 14px; font-weight: 500;"">Hệ Thống Thời Trang Cao Cấp & Trải Nghiệm Mua Sắm VIP</p>
                </div>
                
                <!-- Content Body -->
                <div style=""padding: 30px 15px; text-align: center;"">
                    <div style=""font-size: 48px; margin-bottom: 15px;"">🛍️✨</div>
                    <h3 style=""color: #0f172a; margin-top: 0; font-size: 22px; font-weight: 700;"">FASHION STORE RẤT NHỚ BẠN!</h3>
                    <p style=""color: #334155; line-height: 1.7; font-size: 15px;"">Xin chào <strong>{recipientName}</strong>,</p>
                    <p style=""color: #334155; line-height: 1.7; font-size: 15px;"">Đã một thời gian chúng tôi chưa được đồng hành cùng bạn trong những bộ trang phục thời thượng nhất. Để tri ân tình cảm của bạn, FASHION STORE xin gửi tặng riêng bạn một phần quà đặc biệt:</p>

                    <!-- Voucher Callout Box -->
                    <div style=""margin: 30px auto; max-width: 420px; padding: 24px; background: linear-gradient(135deg, #6366f1, #8b5cf6); color: white; border-radius: 16px; box-shadow: 0 10px 25px -5px rgba(99, 102, 241, 0.4); border: 2px dashed #a5b4fc;"">
                        <div style=""font-size: 13px; text-transform: uppercase; letter-spacing: 2px; opacity: 0.9; font-weight: 600;"">ƯU ĐÃI NÍU CHÂN KHÁCH HÀNG VIP</div>
                        <div style=""font-size: 32px; font-weight: 800; margin: 10px 0; font-family: monospace; letter-spacing: 4px; color: #ffffff;"">{voucherCode}</div>
                        <div style=""font-size: 16px; font-weight: 700; background: rgba(255,255,255,0.2); padding: 6px 14px; border-radius: 20px; display: inline-block; margin-top: 5px;"">Giảm Ngay {discountText} Cho Đơn Hàng</div>
                        <div style=""font-size: 12px; margin-top: 12px; opacity: 0.85;"">Hạn sử dụng: đến hết ngày {endDate:dd/MM/yyyy}</div>
                    </div>

                    <!-- Security Notice Box -->
                    <div style=""background-color: #f1f5f9; border-left: 4px solid #6366f1; padding: 14px; border-radius: 8px; text-align: left; margin: 20px 0;"">
                        <p style=""color: #1e293b; margin: 0; font-size: 13px; font-weight: 600;"">🔒 QUYỀN LỢI ĐỘC QUYỀN & BẢO MẬT GMAIL:</p>
                        <p style=""color: #475569; margin: 5px 0 0 0; font-size: 12px; line-height: 1.5;"">Mã giảm giá này được gán trực tiếp và <strong>chỉ duy nhất tài khoản Gmail ({toEmail})</strong> mới có quyền áp dụng khi thanh toán. Các tài khoản khác sẽ bị hệ thống tự động từ chối để bảo vệ quyền lợi của bạn.</p>
                    </div>

                    <!-- CTA Button -->
                    <div style=""margin-top: 30px;"">
                        <a href=""https://fashionstore-zjc7.onrender.com"" style=""background: linear-gradient(135deg, #6366f1, #4f46e5); color: #ffffff; text-decoration: none; padding: 14px 32px; font-size: 15px; font-weight: bold; border-radius: 30px; display: inline-block; box-shadow: 0 4px 14px rgba(79, 70, 229, 0.4);"">
                            🛍️ KHÁM PHÁ BỘ SƯU TẬP MỚI VÀ DÙNG VOUCHER
                        </a>
                    </div>
                </div>

                <!-- Footer -->
                <div style=""border-top: 1px solid #e2e8f0; padding-top: 20px; text-align: center; color: #94a3b8; font-size: 12px;"">
                    <p style=""margin: 0;"">Bạn nhận được email này vì bạn là khách hàng thân thiết tại FASHION STORE.</p>
                    <p style=""margin: 6px 0 0 0;"">© 2026 FASHION STORE CO. All rights reserved.</p>
                </div>
            </div>";

        return await SendEmailInternalAsync(toEmail, subject, htmlBody);
    }

    public async Task<(bool Success, string ErrorMessage)> SendEmailDetailedAsync(string toEmail, string subject, string htmlBody)
    {
        if (string.IsNullOrWhiteSpace(toEmail) || !toEmail.Contains("@") || toEmail.EndsWith(".fashionstore.vn"))
        {
            return (false, $"Địa chỉ Email nhận không hợp lệ: '{toEmail}'");
        }

        string senderEmail = _configuration["EmailSettings:SenderEmail"];
        if (string.IsNullOrWhiteSpace(senderEmail)) senderEmail = _configuration["EmailSettings__SenderEmail"];
        if (string.IsNullOrWhiteSpace(senderEmail)) senderEmail = Environment.GetEnvironmentVariable("EmailSettings__SenderEmail");
        if (string.IsNullOrWhiteSpace(senderEmail)) senderEmail = "tranchivi29102005@gmail.com";

        string senderName = "FASHION STORE";

        // 1. BREVO / RESEND HTTP API (CỔNG 443 BẢO MẬT KHÔNG BAO GIỜ BỊ CHẶN BỞI RENDER)
        string brevoApiKey = _configuration["Brevo:ApiKey"] 
            ?? Environment.GetEnvironmentVariable("BREVO_API_KEY") 
            ?? _configuration["EmailSettings:ApiKey"];
        if (!string.IsNullOrWhiteSpace(brevoApiKey))
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                var request = new HttpRequestMessage(HttpMethod.Post, "https://api.brevo.com/v3/smtp/email");
                request.Headers.Add("api-key", brevoApiKey.Trim());
                request.Headers.Add("accept", "application/json");

                var payload = new
                {
                    sender = new { name = senderName, email = senderEmail.Trim() },
                    to = new[] { new { email = toEmail.Trim() } },
                    subject = subject,
                    htmlContent = htmlBody
                };

                string jsonPayload = System.Text.Json.JsonSerializer.Serialize(payload);
                request.Content = new StringContent(jsonPayload, System.Text.Encoding.UTF8, "application/json");

                var response = await client.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation($"[EMAIL SUCCESS via BREVO API] Gửi email thành công tới {toEmail}");
                    return (true, "Gửi email thành công qua Brevo HTTP API.");
                }
                else
                {
                    string errStr = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning($"[BREVO API ERROR] status={response.StatusCode}, error={errStr}");
                    return (false, $"Lỗi API Brevo (Status {response.StatusCode}): {errStr}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[BREVO API EXCEPTION] Lỗi kết nối API Brevo: {ex.Message}");
            }
        }

        // 2. GỬI QUA MAILKIT SMTP GMAIL
        try
        {
            string smtpServer = _configuration["EmailSettings:SmtpServer"];
            if (string.IsNullOrWhiteSpace(smtpServer)) smtpServer = "smtp.gmail.com";

            int smtpPort = int.TryParse(_configuration["EmailSettings:SmtpPort"], out int p) ? p : 587;

            // Đọc mật khẩu từ IConfiguration và trực tiếp từ OS Environment Variable trên Render
            string password = _configuration["EmailSettings:Password"];
            if (string.IsNullOrWhiteSpace(password)) password = _configuration["EmailSettings__Password"];
            if (string.IsNullOrWhiteSpace(password)) password = Environment.GetEnvironmentVariable("EmailSettings__Password");
            if (string.IsNullOrWhiteSpace(password)) password = Environment.GetEnvironmentVariable("EMAILSETTINGS__PASSWORD");

            if (string.IsNullOrWhiteSpace(password))
            {
                _logger.LogError("[EMAIL ERROR] Chưa cấu hình EmailSettings__Password trong biến môi trường Render!");
                return (false, "Chưa cấu hình biến EmailSettings__Password trên Render Dashboard (hoặc biến bị rỗng). Vui lòng thêm biến EmailSettings__Password vào mục Environment trên Render!");
            }

            password = password.Replace(" ", "").Trim();

            _logger.LogInformation($"[EMAIL SENDING] Đang gửi email tới {toEmail} qua {smtpServer}:{smtpPort} với tài khoản {senderEmail}");

            // Tạo email bằng MimeKit (MailKit)
            var emailMessage = new MimeMessage();
            emailMessage.From.Add(new MailboxAddress(senderName, senderEmail.Trim()));
            emailMessage.To.Add(new MailboxAddress("", toEmail.Trim()));
            emailMessage.Subject = subject;

            var bodyBuilder = new BodyBuilder { HtmlBody = htmlBody };
            emailMessage.Body = bodyBuilder.ToMessageBody();

            // Gửi email bằng MailKit SmtpClient (hỗ trợ Linux / Render container)
            using var smtpClient = new MailKit.Net.Smtp.SmtpClient();
            smtpClient.Timeout = 10000; // 10 giây timeout để chuyển fallback nhanh chóng
            
            // Bỏ qua lỗi SSL Certificate Store trên môi trường Linux Render Container
            smtpClient.ServerCertificateValidationCallback = (s, c, ch, e) => true;

            bool isConnected = false;
            
            // THỬ CỔNG 587 TRƯỚC (STARTTLS)
            try
            {
                _logger.LogInformation($"[EMAIL CONNECT] Thử kết nối {smtpServer}:587 (STARTTLS)...");
                await smtpClient.ConnectAsync(smtpServer.Trim(), 587, SecureSocketOptions.StartTls);
                isConnected = true;
            }
            catch (Exception ex587)
            {
                _logger.LogWarning($"[EMAIL PORT 587 BLOCKED] Cổng 587 bị chặn hoặc Timeout ({ex587.Message}). Đang tự động chuyển sang Cổng 465 (SSL)...");
            }

            // NẾU CỔNG 587 BỊ RENDER CHẶN (TIMEOUT), TỰ ĐỘNG THỬ CỔNG 465 (SSL)
            if (!isConnected)
            {
                try
                {
                    await smtpClient.ConnectAsync(smtpServer.Trim(), 465, SecureSocketOptions.SslOnConnect);
                    isConnected = true;
                    _logger.LogInformation($"[EMAIL CONNECT SUCCESS] Kết nối thành công qua Cổng 465 (SSL)!");
                }
                catch (Exception ex465)
                {
                    _logger.LogError(ex465, $"[EMAIL PORT 465 FAILED] Cả 2 cổng 587 & 465 đều bị Render chặn: {ex465.Message}");
                    return (false, $"Lỗi Gmail SMTP (Cả Cổng 587 & 465 đều bị Render chặn Timeout): {ex465.Message}. Vui lòng dùng Brevo HTTP API (Port 443 không bao giờ bị chặn).");
                }
            }

            await smtpClient.AuthenticateAsync(senderEmail.Trim(), password);
            await smtpClient.SendAsync(emailMessage);
            await smtpClient.DisconnectAsync(true);

            _logger.LogInformation($"[EMAIL SUCCESS via GMAIL SMTP] Gửi email thành công tới {toEmail} - Tiêu đề: '{subject}'");
            return (true, "Gửi email thành công qua Gmail SMTP.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"[EMAIL ERROR] Không thể gửi email tới {toEmail}. Chi tiết lỗi: {ex.Message}");
            return (false, $"Lỗi Gmail SMTP ({ex.GetType().Name}): {ex.Message}");
        }
    }

    private async Task<bool> SendEmailInternalAsync(string toEmail, string subject, string htmlBody)
    {
        var result = await SendEmailDetailedAsync(toEmail, subject, htmlBody);
        return result.Success;
    }
}
