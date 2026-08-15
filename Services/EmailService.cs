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

    public EmailService(ApplicationDbContext context, IConfiguration configuration, ILogger<EmailService> logger)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
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
            if (!imgUrl.StartsWith("http"))
            {
                imgUrl = "https://fashionstore-zjc7.onrender.com" + (imgUrl.StartsWith("/") ? "" : "/") + imgUrl;
            }
            
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

        // Convert Render Server UTC Time to Vietnam ICT Local Time (UTC+7)
        DateTime vnOrderDate = order.OrderDate.AddHours(7);
        string orderDateStr = vnOrderDate.ToString("dd/MM/yyyy HH:mm");
        string deliveryTimeStr = order.Payment?.PaidAt.HasValue == true 
            ? order.Payment.PaidAt.Value.AddHours(7).ToString("dd/MM/yyyy HH:mm")
            : "Dự kiến 1 - 3 ngày làm việc";

        string trackOrderUrl = $"https://fashionstore-zjc7.onrender.com/Order/Track/{order.OrderGuid}";

        string body = $@"
            <div style=""font-family: 'Segoe UI', Arial, sans-serif; max-width: 650px; margin: 0 auto; padding: 25px; background-color: #ffffff; border-radius: 16px; border: 1px solid #e2e8f0; box-shadow: 0 4px 20px rgba(0,0,0,0.05);"">
                
                <!-- HEADER -->
                <div style=""display: flex; justify-content: space-between; align-items: center; padding-bottom: 20px; border-bottom: 2px solid #f1f5f9;"">
                    <div>
                        <h2 style=""color: #6366f1; margin: 0; font-size: 24px; font-weight: bold;"">FASHION STORE</h2>
                        <span style=""color: #64748b; font-size: 13px;"">HÓA ĐƠN XÁC NHẬN ĐƠN HÀNG & PHIẾU GIAO HÀNG</span>
                    </div>
                    <div style=""text-align: right;"">
                        <div style=""font-family: monospace; font-size: 15px; font-weight: bold; color: #0f172a;"">#{order.OrderNumber}</div>
                        <div style=""color: #475569; font-size: 12px; margin-top: 4px; font-weight: 600;"">🕒 Đặt hàng: {orderDateStr}</div>
                        <div style=""color: #10b981; font-size: 11px; margin-top: 2px;"">🚚 Giao hàng: {deliveryTimeStr}</div>
                    </div>
                </div>

                <!-- LINK TRACKING BUTTON -->
                <div style=""margin: 20px 0; padding: 15px; background: #eff6ff; border-radius: 12px; text-align: center;"">
                    <a href=""{trackOrderUrl}"" style=""background: #2563eb; color: #ffffff; text-decoration: none; padding: 10px 24px; font-size: 14px; font-weight: bold; border-radius: 20px; display: inline-block;"">
                        🔍 Xem Trực Tiếp Trạng Thái & Chi Tiết Đơn Hàng
                    </a>
                </div>

                <!-- STATUS BADGE -->
                <div style=""margin: 20px 0; padding: 15px; background: #f8fafc; border-radius: 12px; display: flex; align-items: center; justify-content: space-between;"">
                    <span style=""color: #334155; font-size: 14px; font-weight: 500;"">Trạng Thái Đơn Hàng:</span>
                    <div>{statusText}</div>
                </div>

                <!-- RECIPIENT INFO -->
                <div style=""margin-bottom: 25px; padding: 15px; background: #f8fafc; border-radius: 12px; font-size: 13px; color: #334155; line-height: 1.6;"">
                    <div style=""font-weight: bold; color: #0f172a; margin-bottom: 6px; font-size: 14px;"">📌 THÔNG TIN GIAO HÀNG & THU HỘ (COD):</div>
                    <div>Khách hàng nhận: <strong>{order.User.FullName}</strong> ({order.User.Email})</div>
                    <div>Địa chỉ nhận hàng: <strong>{addressText}</strong></div>
                    <div>Thời gian đặt hàng: <strong>{orderDateStr} (Giờ Việt Nam)</strong></div>
                    <div>Thời gian giao hàng: <strong>{deliveryTimeStr}</strong></div>
                    <div style=""margin-top: 4px;"">Hình thức thanh toán: <strong>{(order.Payment?.Status == PaymentStatus.Success ? "✅ ĐÃ THANH TOÁN (0đ COD)" : $"🚚 THANH TOÁN KHI NHẬN HÀNG (Cần thu hộ: {order.TotalAmount:N0}đ)")}</strong></div>
                </div>

                <!-- PRODUCT ITEMS TABLE -->
                <h4 style=""color: #0f172a; margin: 0 0 12px 0; font-size: 15px;"">🛒 CHI TIẾT SẢN PHẨM ĐANG GIAO ({totalItemsCount} sản phẩm):</h4>
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

    public async Task<bool> SendOrderCompletedEmailAsync(int orderId, DateTime completedTime)
    {
        var order = await _context.Orders
            .Include(o => o.User)
            .Include(o => o.Address)
            .Include(o => o.Payment)
            .Include(o => o.OrderDetails)
                .ThenInclude(od => od.Variant)
                    .ThenInclude(v => v.Product)
            .FirstOrDefaultAsync(o => o.OrderId == orderId);

        if (order == null || order.User == null) return false;

        DateTime vnCompletedTime = completedTime.AddHours(7);
        string timeStr = vnCompletedTime.ToString("dd/MM/yyyy HH:mm:ss");

        string subject = $"🎉 [FASHION STORE] - Đơn Hàng #{order.OrderNumber} Đã Giao Thành Công!";

        string addressText = order.Address != null 
            ? $"{order.Address.RecipientName} - SĐT: {order.Address.Phone} ({order.Address.DetailAddress}, {order.Address.Ward}, {order.Address.District}, {order.Address.Province})"
            : "Chưa cập nhật";

        var itemsHtml = "";
        foreach (var item in order.OrderDetails)
        {
            var p = item.Variant?.Product;
            decimal itemTotal = item.UnitPrice * item.Quantity;
            itemsHtml += $@"
                <tr>
                    <td style=""padding: 10px; border-bottom: 1px solid #e2e8f0;"">
                        <strong>{p?.ProductName}</strong> (Size: {item.Variant?.Size}, Màu: {item.Variant?.Color})
                    </td>
                    <td style=""padding: 10px; border-bottom: 1px solid #e2e8f0; text-align: center;"">x{item.Quantity}</td>
                    <td style=""padding: 10px; border-bottom: 1px solid #e2e8f0; text-align: right; font-weight: bold; color: #10b981;"">{itemTotal:N0}đ</td>
                </tr>";
        }

        string body = $@"
            <div style=""font-family: 'Segoe UI', Arial, sans-serif; max-width: 650px; margin: 0 auto; padding: 25px; background-color: #ffffff; border-radius: 16px; border: 1px solid #10b981; box-shadow: 0 4px 20px rgba(0,0,0,0.05);"">
                <div style=""text-align: center; padding-bottom: 15px; border-bottom: 2px solid #ecfdf5;"">
                    <h2 style=""color: #10b981; margin: 0; font-size: 24px; font-weight: bold;"">✅ ĐƠN HÀNG GIAO THÀNH CÔNG</h2>
                    <span style=""color: #64748b; font-size: 13px;"">Mã đơn hàng: <strong>#{order.OrderNumber}</strong></span>
                </div>

                <div style=""margin: 20px 0; padding: 16px; background: #ecfdf5; border-radius: 12px; border-left: 4px solid #10b981; color: #065f46;"">
                    <div style=""font-size: 15px; font-weight: bold; margin-bottom: 6px;"">🎉 Chúc mừng bạn đã nhận hàng thành công!</div>
                    <div style=""font-size: 13px; color: #047857;"">Shipper đã xác nhận giao thành công lúc: <strong>{timeStr} (Giờ Việt Nam)</strong></div>
                </div>

                <div style=""margin-bottom: 20px; font-size: 13px; color: #334155; line-height: 1.6;"">
                    <div>Khách hàng: <strong>{order.User.FullName}</strong></div>
                    <div>Địa chỉ giao: <strong>{addressText}</strong></div>
                    <div>Trạng thái thanh toán: <strong style=""color: #10b981;"">ĐÃ THANH TOÁN THÀNH CÔNG (0đ COD)</strong></div>
                </div>

                <h4 style=""color: #0f172a; margin: 0 0 10px 0; font-size: 14px;"">🛒 SẢN PHẨM ĐÃ GIAO:</h4>
                <table style=""width: 100%; border-collapse: collapse; margin-bottom: 15px; font-size: 13px;"">
                    <tbody>{itemsHtml}</tbody>
                </table>

                <div style=""text-align: right; font-size: 18px; font-weight: bold; color: #0f172a; border-top: 2px dashed #e2e8f0; padding-top: 10px;"">
                    TỔNG TIỀN: <span style=""color: #10b981; font-size: 22px;"">{order.TotalAmount:N0}đ</span>
                </div>

                <div style=""margin-top: 25px; text-align: center; color: #94a3b8; font-size: 12px;"">
                    Cảm ơn bạn đã lựa chọn <strong>FASHION STORE</strong>! Chúc bạn có trải nghiệm mặc đẹp tuyệt vời!
                </div>
            </div>";

        return await SendEmailInternalAsync(order.User.Email, subject, body);
    }

    public async Task<bool> SendOrderCancelledEmailAsync(int orderId, DateTime cancelledTime, string reason)
    {
        var order = await _context.Orders
            .Include(o => o.User)
            .Include(o => o.Address)
            .Include(o => o.OrderDetails)
                .ThenInclude(od => od.Variant)
                    .ThenInclude(v => v.Product)
            .FirstOrDefaultAsync(o => o.OrderId == orderId);

        if (order == null || order.User == null) return false;

        DateTime vnCancelledTime = cancelledTime.AddHours(7);
        string timeStr = vnCancelledTime.ToString("dd/MM/yyyy HH:mm:ss");

        string subject = $"❌ [FASHION STORE] - Thông Báo Hủy Đơn Hàng #{order.OrderNumber}";

        string body = $@"
            <div style=""font-family: 'Segoe UI', Arial, sans-serif; max-width: 650px; margin: 0 auto; padding: 25px; background-color: #ffffff; border-radius: 16px; border: 1px solid #ef4444; box-shadow: 0 4px 20px rgba(0,0,0,0.05);"">
                <div style=""text-align: center; padding-bottom: 15px; border-bottom: 2px solid #fef2f2;"">
                    <h2 style=""color: #ef4444; margin: 0; font-size: 24px; font-weight: bold;"">❌ ĐƠN HÀNG ĐÃ BỊ HỦY</h2>
                    <span style=""color: #64748b; font-size: 13px;"">Mã đơn hàng: <strong>#{order.OrderNumber}</strong></span>
                </div>

                <div style=""margin: 20px 0; padding: 16px; background: #fef2f2; border-radius: 12px; border-left: 4px solid #ef4444; color: #991b1b;"">
                    <div style=""font-size: 15px; font-weight: bold; margin-bottom: 6px;"">Thông báo tự động hủy đơn hàng:</div>
                    <div style=""font-size: 13px; color: #b91c1c; margin-bottom: 4px;"">Lý do: <strong>{reason}</strong></div>
                    <div style=""font-size: 12px; color: #7f1d1d;"">Thời gian cập nhật hủy: <strong>{timeStr} (Giờ Việt Nam)</strong></div>
                </div>

                <p style=""color: #334155; font-size: 13px; line-height: 1.6;"">
                    Sản phẩm trong đơn hàng đã được hệ thống tự động hoàn trả lại kho hàng. Nếu bạn vẫn muốn sở hữu sản phẩm này, hãy ghé thăm website FASHION STORE để đặt mua lại nhé!
                </p>

                <div style=""margin-top: 25px; text-align: center; color: #94a3b8; font-size: 12px;"">
                    Mọi thắc mắc xin vui lòng liên hệ Hotline 1900-FASHION. Trân trọng cảm ơn!
                </div>
            </div>";

        return await SendEmailInternalAsync(order.User.Email, subject, body);
    }

    public async Task<bool> SendDeliveryFailedCustomerConfirmationEmailAsync(int orderId, int attemptCount)
    {
        var order = await _context.Orders
            .Include(o => o.User)
            .Include(o => o.Address)
            .Include(o => o.OrderDetails)
                .ThenInclude(od => od.Variant)
                    .ThenInclude(v => v.Product)
            .FirstOrDefaultAsync(o => o.OrderId == orderId);

        if (order == null || order.User == null) return false;

        DateTime nowVn = DateTime.UtcNow.AddHours(7);
        string timeStr = nowVn.ToString("dd/MM/yyyy HH:mm");
        string confirmUrl = $"https://fashionstore-zjc7.onrender.com/Order/CustomerConfirmDelivery/{order.OrderGuid}";

        string subject = $"⚠️ [FASHION STORE] - Giao Hàng Thất Bại (Lần {attemptCount}/3) - Vui Lòng Xác Nhận Giao Lại";

        string body = $@"
            <div style=""font-family: 'Segoe UI', Arial, sans-serif; max-width: 650px; margin: 0 auto; padding: 25px; background-color: #ffffff; border-radius: 16px; border: 2px solid #f59e0b; box-shadow: 0 4px 20px rgba(0,0,0,0.05);"">
                <div style=""text-align: center; padding-bottom: 15px; border-bottom: 2px solid #fef3c7;"">
                    <h2 style=""color: #d97706; margin: 0; font-size: 22px; font-weight: bold;"">⚠️ THÔNG BÁO GIAO HÀNG THẤT BẠI (LẦN {attemptCount}/3)</h2>
                    <span style=""color: #64748b; font-size: 13px;"">Đơn hàng: <strong>#{order.OrderNumber}</strong></span>
                </div>

                <div style=""margin: 20px 0; padding: 16px; background: #fffbeb; border-radius: 12px; border-left: 4px solid #f59e0b; color: #92400e;"">
                    <div style=""font-size: 14px; font-weight: bold; margin-bottom: 4px;"">Xin chào {order.User.FullName},</div>
                    <div style=""font-size: 13px; line-height: 1.5;"">
                        Shipper vừa báo giao hàng không thành công vào lúc <strong>{timeStr} (Giờ Việt Nam)</strong> do không liên lạc được hoặc bận hẹn lại.
                    </div>
                </div>

                <div style=""margin: 25px 0; text-align: center; padding: 20px; background: #eff6ff; border-radius: 14px; border: 1px dashed #3b82f6;"">
                    <div style=""font-size: 14px; font-weight: bold; color: #1e40af; margin-bottom: 8px;"">
                        👉 ĐƠN HÀNG ĐANG Ở TRẠNG THÁI: <span style=""color: #d97706;"">CHỜ BẠN XÁC NHẬN GIAO LẠI</span>
                    </div>
                    <p style=""font-size: 12px; color: #475569; margin-bottom: 16px;"">
                        Vui lòng bấm vào nút bên dưới để xác nhận bạn sẵn sàng nhận hàng. Sau khi xác nhận, đơn hàng sẽ được tự động chuyển cho Shipper giao lại lần tiếp theo!
                    </p>
                    <a href=""{confirmUrl}"" style=""background: linear-gradient(135deg, #2563eb 0%, #1d4ed8 100%); color: #ffffff; text-decoration: none; padding: 14px 28px; font-size: 14px; font-weight: bold; border-radius: 30px; display: inline-block; box-shadow: 0 4px 12px rgba(37,99,235,0.35);"">
                        📲 XÁC NHẬN SẴN SÀNG NHẬN HÀNG LẦN GIAO TIẾP THEO
                    </a>
                </div>

                <div style=""font-size: 12px; color: #64748b; line-height: 1.5; text-align: center;"">
                    ⚠️ <em>Lưu ý: Nếu đơn hàng giao thất bại quá 3 lần, hệ thống sẽ tự động hủy đơn và hoàn trả sản phẩm về kho.</em>
                </div>
            </div>";

        return await SendEmailInternalAsync(order.User.Email, subject, body);
    }

    public async Task<bool> SendCustomerReDeliveryConfirmedEmailAsync(int orderId)
    {
        var order = await _context.Orders
            .Include(o => o.User)
            .Include(o => o.Address)
            .FirstOrDefaultAsync(o => o.OrderId == orderId);

        if (order == null || order.User == null) return false;

        DateTime nowVn = DateTime.UtcNow.AddHours(7);
        string timeStr = nowVn.ToString("dd/MM/yyyy HH:mm");

        string subject = $"✅ [FASHION STORE] - Đã Ghi Nhận Yêu Cầu Giao Lại Đơn Hàng #{order.OrderNumber}";

        string body = $@"
            <div style=""font-family: 'Segoe UI', Arial, sans-serif; max-width: 650px; margin: 0 auto; padding: 25px; background-color: #ffffff; border-radius: 16px; border: 2px solid #10b981; box-shadow: 0 4px 20px rgba(0,0,0,0.05);"">
                <div style=""text-align: center; padding-bottom: 15px; border-bottom: 2px solid #ecfdf5;"">
                    <h2 style=""color: #10b981; margin: 0; font-size: 22px; font-weight: bold;"">✅ ĐÃ XÁC NHẬN YÊU CẦU GIAO LẠI ĐƠN HÀNG</h2>
                    <span style=""color: #64748b; font-size: 13px;"">Mã đơn hàng: <strong>#{order.OrderNumber}</strong></span>
                </div>

                <div style=""margin: 20px 0; padding: 18px; background: #ecfdf5; border-radius: 12px; border-left: 4px solid #10b981; color: #065f46;"">
                    <div style=""font-size: 15px; font-weight: bold; margin-bottom: 6px;"">Xin chào {order.User.FullName},</div>
                    <div style=""font-size: 13.5px; line-height: 1.6; color: #047857;"">
                        FASHION STORE đã nhận được thông tin yêu cầu giao lại đơn hàng <strong>#{order.OrderNumber}</strong> của bạn vào lúc <strong>{timeStr} (Giờ Việt Nam)</strong>.
                    </div>
                </div>

                <div style=""padding: 16px; background: #f8fafc; border-radius: 12px; font-size: 13px; color: #334155; line-height: 1.6; text-align: center; border: 1px solid #e2e8f0; margin-bottom: 20px;"">
                    <strong>🚚 Đơn hàng của bạn đã được chuyển trở lại danh sách ĐANG GIAO HÀNG.</strong><br/>
                    Xin vui lòng chú ý nghe máy để Shipper liên lạc với bạn trong thời gian sớm nhất nhé!
                </div>

                <div style=""text-align: center; color: #94a3b8; font-size: 12px;"">
                    Trân trọng cảm ơn bạn đã đồng hành cùng <strong>FASHION STORE</strong>!
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

        // 1. GOOGLE APPS SCRIPT WEBHOOK ENGINE (GỬI GMAIL THẬT 100% KHÔNG BAO GIỜ BỊ CHẶN BỞI RENDER)
        string googleWebhookUrl = _configuration["GOOGLE_SCRIPT_WEBHOOK_URL"]
            ?? _configuration["GoogleScript:WebhookUrl"]
            ?? Environment.GetEnvironmentVariable("GOOGLE_SCRIPT_WEBHOOK_URL")
            ?? "https://script.google.com/macros/s/AKfycbxkohav9krou0kh_HbNxaE3QBx2jsrepE66e7Yolbw8VmRzTSOIPXf9Lk5bem_DRTQR/exec";

        if (!string.IsNullOrWhiteSpace(googleWebhookUrl))
        {
            try
            {
                var handler = new HttpClientHandler { AllowAutoRedirect = true };
                using var client = new HttpClient(handler);
                client.Timeout = TimeSpan.FromSeconds(15);

                var payload = new
                {
                    toEmail = toEmail.Trim(),
                    to = toEmail.Trim(),
                    email = toEmail.Trim(),
                    subject = subject,
                    htmlBody = htmlBody,
                    body = htmlBody
                };

                string jsonPayload = System.Text.Json.JsonSerializer.Serialize(payload);
                var content = new StringContent(jsonPayload, System.Text.Encoding.UTF8, "application/json");

                _logger.LogInformation($"[GOOGLE WEBHOOK] Đang gửi email tới {toEmail} qua {googleWebhookUrl}...");
                var response = await client.PostAsync(googleWebhookUrl.Trim(), content);
                string respStr = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode && !respStr.Contains("Không tìm thấy hàm"))
                {
                    _logger.LogInformation($"[GOOGLE WEBHOOK SUCCESS] Gửi email thành công tới {toEmail} qua Google Webhook! Response: {respStr}");
                    return (true, "Gửi email thành công 100% về Hộp thư Gmail thật qua Google Webhook!");
                }
                else
                {
                    _logger.LogWarning($"[GOOGLE WEBHOOK WARN] status={response.StatusCode}, error={respStr}. Đang thử query fallback...");
                    // Fallback query GET nếu POST bị Google đổi phương thức
                    string queryUrl = $"{googleWebhookUrl.Trim()}?toEmail={Uri.EscapeDataString(toEmail.Trim())}&subject={Uri.EscapeDataString(subject)}&htmlBody={Uri.EscapeDataString(htmlBody)}";
                    var respGet = await client.GetAsync(queryUrl);
                    string respGetStr = await respGet.Content.ReadAsStringAsync();
                    if (respGet.IsSuccessStatusCode)
                    {
                        _logger.LogInformation($"[GOOGLE WEBHOOK GET SUCCESS] Gửi email thành công tới {toEmail}!");
                        return (true, "Gửi email thành công 100% về Hộp thư Gmail thật qua Google Webhook (GET)!");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[GOOGLE WEBHOOK EXCEPTION] Lỗi kết nối Google Webhook: {ex.Message}");
            }
        }

        // 1. HỖ TRỢ MAILTRAP HTTP API (CỔNG 443 HTTPS KHÔNG BAO GIỜ BỊ CHẶN BỞI RENDER/FIREWALL)
        string mailtrapToken = _configuration["MAILTRAP_API_KEY"]
            ?? _configuration["Mailtrap:ApiKey"]
            ?? _configuration["Mailtrap__ApiKey"]
            ?? Environment.GetEnvironmentVariable("MAILTRAP_API_KEY")
            ?? Environment.GetEnvironmentVariable("Mailtrap__ApiKey")
            ?? _configuration["EmailSettings:ApiKey"]
            ?? "f64439a9215a2f1ac4128dda8a6897cd";

        string envPass = _configuration["EmailSettings:Password"] 
            ?? _configuration["EmailSettings__Password"] 
            ?? Environment.GetEnvironmentVariable("EmailSettings__Password") 
            ?? Environment.GetEnvironmentVariable("EMAILSETTINGS__PASSWORD") 
            ?? "";

        if (!string.IsNullOrWhiteSpace(envPass) && (envPass.Trim().Length >= 25 || envPass.Trim().Length == 32))
        {
            mailtrapToken = envPass.Trim();
        }

        if (!string.IsNullOrWhiteSpace(mailtrapToken))
        {
            try
            {
                using var client = new HttpClient();
                var request = new HttpRequestMessage(HttpMethod.Post, "https://send.api.mailtrap.io/api/send");
                request.Headers.Add("Authorization", $"Bearer {mailtrapToken.Trim()}");
                request.Headers.Add("Accept", "application/json");

                // Thí nghiệm gửi bằng Sender email Mailtrap Demo hoặc Email Sender từ hệ thống
                string fromEmail = senderEmail.Contains("@demomailtrap.com") ? senderEmail : "hello@demomailtrap.com";

                var payload = new
                {
                    from = new { email = fromEmail, name = senderName },
                    to = new[] { new { email = toEmail.Trim() } },
                    subject = subject,
                    html = htmlBody
                };

                string jsonPayload = System.Text.Json.JsonSerializer.Serialize(payload);
                request.Content = new StringContent(jsonPayload, System.Text.Encoding.UTF8, "application/json");

                var response = await client.SendAsync(request);
                string responseStr = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation($"[MAILTRAP SUCCESS] Gửi email thành công qua Mailtrap API tới {toEmail}");
                    return (true, "Gửi email thành công 100% qua Mailtrap HTTP API!");
                }
                else
                {
                    _logger.LogWarning($"[MAILTRAP API ERROR] status={response.StatusCode}, error={responseStr}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[MAILTRAP API EXCEPTION] {ex.Message}");
            }
        }

        // 2. GỬI QUA MAILKIT SMTP GMAIL / MAILTRAP SMTP
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
                    return (false, $"Lỗi Gmail SMTP (Cả Cổng 587 & 465 đều bị Render chặn Timeout): {ex465.Message}.");
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
