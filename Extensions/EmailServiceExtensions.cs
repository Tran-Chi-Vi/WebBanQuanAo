using WEBBANQUANAO.Models;
using WEBBANQUANAO.Services;

namespace WEBBANQUANAO.Extensions;

public static class EmailServiceExtensions
{
    /// <summary>
    /// Extension method đăng ký Dịch vụ Gửi Email bằng MailKit & Brevo HTTP API cho ASP.NET Core
    /// </summary>
    public static IServiceCollection AddMailKitEmailServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Nạp cấu hình từ appsettings.json
        services.Configure<EmailSettings>(configuration.GetSection("EmailSettings"));

        // Đăng ký HttpClient cho Brevo API Fallback
        services.AddHttpClient();

        // Đăng ký IEmailService với EmailService
        services.AddScoped<IEmailService, EmailService>();

        return services;
    }
}
