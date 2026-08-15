using WEBBANQUANAO.Models.Entities;

namespace WEBBANQUANAO.Services;

public interface IEmailService
{
    Task<bool> SendOtpEmailAsync(string toEmail, string recipientName, string otpCode);
    Task<bool> SendOrderInvoiceEmailAsync(int orderId);
    Task<bool> SendOrderCompletedEmailAsync(int orderId, DateTime completedTime);
    Task<bool> SendOrderCancelledEmailAsync(int orderId, DateTime cancelledTime, string reason);
    Task<bool> SendChurnWinBackEmailAsync(string toEmail, string recipientName, string voucherCode, decimal discountValue, DiscountType discountType, DateTime endDate);
    Task<(bool Success, string ErrorMessage)> SendEmailDetailedAsync(string toEmail, string subject, string htmlBody);
}
