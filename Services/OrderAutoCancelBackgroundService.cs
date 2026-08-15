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
/// Service Chạy Ngầm Tự Động Định Kỳ Mỗi Giờ 1 Lần:
/// Kiểm tra các đơn hàng ở trạng thái 'WaitingForCustomer' (Chờ Khách Xác Nhận Giao Lại)
/// Nếu sau 5 NĂM/NGÀY (5 ngày) khách hàng KHÔNG bấm xác nhận sẵn sàng nhận hàng:
/// -> Tự động HỦY ĐƠN HÀNG
/// -> Tự động HOÀN TRẢ SẢN PHẨM VỀ TỒN KHO CỬA HÀNG
/// -> Tự động GỬI EMAIL THÔNG BÁO HỦY ĐƠN VÌ QUÁ THỜI HẠN 5 NGÀY
/// </summary>
public class OrderAutoCancelBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OrderAutoCancelBackgroundService> _logger;

    public OrderAutoCancelBackgroundService(IServiceProvider serviceProvider, ILogger<OrderAutoCancelBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🚀 [ORDER AUTO-CANCEL SERVICE] Đã khởi chạy Dịch vụ Tự Động Hủy Đơn Hàng Quá Hạn 5 Ngày Chờ Khách Xác Nhận!");

        // Chờ 15 giây sau khi server khởi động trước khi quét lần đầu
        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessExpiredWaitingOrdersAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Lỗi xảy ra trong quá trình tự động kiểm tra và hủy đơn hàng quá hạn 5 ngày.");
            }

            // Quét định kỳ mỗi 1 giờ 1 lần
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }

    private async Task ProcessExpiredWaitingOrdersAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

        // Lấy thời điểm 5 ngày trước
        DateTime cutoffDate = DateTime.Now.AddDays(-5);

        var expiredOrders = await dbContext.Orders
            .Include(o => o.Payment)
            .Include(o => o.OrderDetails)
            .Where(o => o.Status == OrderStatus.WaitingForCustomer && o.OrderDate <= cutoffDate)
            .ToListAsync();

        if (!expiredOrders.Any()) return;

        _logger.LogInformation($"🔍 Phát hiện {expiredOrders.Count} đơn hàng ở trạng thái 'Chờ Khách Xác Nhận' đã quá thời hạn 5 ngày. Đang tiến hành tự động Hủy & Hoàn kho...");

        foreach (var order in expiredOrders)
        {
            try
            {
                order.Status = OrderStatus.Cancelled;
                DateTime cancelTime = DateTime.UtcNow;

                if (order.Payment != null)
                {
                    order.Payment.Status = PaymentStatus.Failed;
                }

                // Tự động cộng hoàn trả toàn bộ số lượng sản phẩm về tồn kho
                foreach (var detail in order.OrderDetails)
                {
                    var variant = await dbContext.ProductVariants.FindAsync(detail.VariantId);
                    if (variant != null)
                    {
                        variant.StockQuantity += detail.Quantity;
                    }
                }

                await dbContext.SaveChangesAsync();

                // Gửi email thông báo cho khách hàng
                await emailService.SendOrderCancelledEmailAsync(order.OrderId, cancelTime, "Tự động hủy do quá thời hạn 5 ngày chờ khách hàng bấm xác nhận sẵn sàng nhận hàng");
                _logger.LogInformation($"✅ Đã hủy đơn hàng #{order.OrderNumber} và cộng hoàn tồn kho thành công vì quá hạn 5 ngày!");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ Lỗi khi tự động hủy đơn hàng #{order.OrderNumber}");
            }
        }
    }
}
