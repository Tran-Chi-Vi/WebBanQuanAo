using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WEBBANQUANAO.Data;
using WEBBANQUANAO.Models.Entities;

namespace WEBBANQUANAO.Services;

/// <summary>
/// Service Chạy Ngầm Tự Động Định Kỳ Mỗi Tuần 1 Lần:
/// Phân tích khách hàng có nguy cơ rời đi CAO (> 14 ngày chưa tương tác/mua hàng)
/// Tự động sinh Mã Giảm Giá ĐỘC QUYỀN riêng cho khách hàng đó (chỉ email khách hàng mới áp dụng được)
/// Tự động gửi Email Níu Chân Khách Hàng qua Google Webhook / Email Engine
/// </summary>
public class WeeklyChurnWinBackBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<WeeklyChurnWinBackBackgroundService> _logger;

    public WeeklyChurnWinBackBackgroundService(IServiceProvider serviceProvider, ILogger<WeeklyChurnWinBackBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🚀 [AUTOMATED WIN-BACK SERVICE] Đã khởi chạy Dịch vụ Tự Động Gửi Voucher Níu Chân Khách Hàng Hàng Tuần!");

        // Chờ 30 giây sau khi ứng dụng khởi động hoàn tất trước khi quét lần đầu
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessWeeklyChurnWinBackAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ [AUTOMATED WIN-BACK ERROR] Lỗi khi thực thi tiến trình tự động gửi Voucher níu chân:");
            }

            // Chạy định kỳ 24 giờ một lần (Tự động kiểm tra và gửi đúng chu kỳ 7 ngày cho từng khách hàng)
            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }

    private async Task ProcessWeeklyChurnWinBackAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

        var now = DateTime.Now;
        var sevenDaysAgo = now.AddDays(-7);

        // 1. Phân tích danh sách Khách hàng không phải Admin và có Email hợp lệ
        var users = await context.Users
            .AsNoTracking()
            .Include(u => u.Role)
            .Include(u => u.Orders)
            .Where(u => u.Role.RoleName != "Admin" && !string.IsNullOrEmpty(u.Email) && u.Email.Contains("@") && !u.Email.EndsWith(".fashionstore.vn"))
            .ToListAsync();

        int sentCount = 0;

        foreach (var user in users)
        {
            var validOrders = user.Orders.Where(o => o.Status != OrderStatus.Cancelled).ToList();
            var lastOrderDate = validOrders.OrderByDescending(o => o.OrderDate).Select(o => (DateTime?)o.OrderDate).FirstOrDefault();

            int daysInactive = lastOrderDate.HasValue ? (int)(now - lastOrderDate.Value).TotalDays : 30;

            // Chỉ áp dụng cho Khách hàng có Mức độ nguy cơ CAO (daysInactive >= 14)
            if (daysInactive >= 14)
            {
                string userEmailLower = user.Email.Trim().ToLower();

                // Kiểm tra xem trong 7 ngày qua khách hàng này đã được tự động cấp Voucher níu chân chưa
                bool alreadySentThisWeek = await context.Promotions
                    .AnyAsync(p => p.AllowedEmail != null && p.AllowedEmail.ToLower() == userEmailLower && p.StartDate >= sevenDaysAgo);

                if (!alreadySentThisWeek)
                {
                    // 2. Tạo Mã Giảm Giá ĐỘC QUYỀN (chỉ duy nhất tài khoản email này dùng được)
                    string voucherCode = $"WINBACK-{user.UserId}-{Guid.NewGuid().ToString("N").Substring(0, 6).ToUpper()}";
                    decimal discountValue = 50000; // Giảm 50.000đ
                    DateTime startDate = now;
                    DateTime endDate = now.AddDays(14); // Hạn dùng 14 ngày

                    var voucher = new Promotion
                    {
                        Code = voucherCode,
                        DiscountType = DiscountType.FixedAmount,
                        DiscountValue = discountValue,
                        MinOrderValue = 150000,
                        StartDate = startDate,
                        EndDate = endDate,
                        AssignedUserId = user.UserId,
                        AllowedEmail = userEmailLower
                    };

                    context.Promotions.Add(voucher);
                    await context.SaveChangesAsync();

                    // 3. Tự động gửi Email Níu Chân Nối Với Google Webhook Engine
                    string recipientName = string.IsNullOrWhiteSpace(user.FullName) ? user.Username : user.FullName;
                    bool sendSuccess = await emailService.SendChurnWinBackEmailAsync(user.Email, recipientName, voucherCode, discountValue, DiscountType.FixedAmount, endDate);

                    if (sendSuccess)
                    {
                        sentCount++;
                        _logger.LogInformation($"✅ [AUTOMATED WIN-BACK SUCCESS] Đã tự động tạo Voucher độc quyền '{voucherCode}' và gửi Email níu chân thành công tới '{user.Email}'!");
                    }
                }
            }
        }

        if (sentCount > 0)
        {
            _logger.LogInformation($"🎉 [AUTOMATED WIN-BACK BATCH DONE] Hoàn tất chu kỳ tự động gửi Email níu chân cho {sentCount} khách hàng nguy cơ CAO!");
        }
    }
}
