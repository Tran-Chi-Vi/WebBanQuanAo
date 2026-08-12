using WEBBANQUANAO.Models.Entities;

namespace WEBBANQUANAO.Services;

public interface IEmailService
{
    Task<bool> SendOtpEmailAsync(string toEmail, string recipientName, string otpCode);
    Task<bool> SendOrderInvoiceEmailAsync(int orderId);
    Task<bool> SendChurnWinBackEmailAsync(string toEmail, string recipientName, string voucherCode, decimal discountValue, DiscountType discountType, DateTime endDate);
}
