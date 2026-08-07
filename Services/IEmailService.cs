namespace WEBBANQUANAO.Services;

public interface IEmailService
{
    Task<bool> SendOtpEmailAsync(string toEmail, string recipientName, string otpCode);
    Task<bool> SendOrderInvoiceEmailAsync(int orderId);
}
