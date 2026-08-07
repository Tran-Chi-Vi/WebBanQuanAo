
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using WEBBANQUANAO.Data;
using WEBBANQUANAO.Hubs;

namespace FashionStore.Web.Services;

public interface IInventoryService
{
    Task<bool> TryDeductStockAsync(int variantId, int quantity);
}

/// <summary>
/// Xử lý trừ tồn kho khi đặt hàng, có transaction để tránh race condition
/// (2 khách cùng mua nốt 1 sản phẩm cuối cùng), rồi broadcast qua SignalR.
/// </summary>
public class InventoryService : IInventoryService
{
    private readonly ApplicationDbContext _context;
    private readonly IHubContext<StockHub> _hub;

    public InventoryService(ApplicationDbContext context, IHubContext<StockHub> hub)
    {
        _context = context;
        _hub = hub;
    }

    public async Task<bool> TryDeductStockAsync(int variantId, int quantity)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // Khóa dòng (SQL Server: UPDLOCK, ROWLOCK) để tránh 2 giao dịch cùng đọc số lượng cũ
            var variant = await _context.ProductVariants
                .FromSqlInterpolated($"SELECT * FROM ProductVariants WITH (UPDLOCK, ROWLOCK) WHERE VariantId = {variantId}")
                .FirstOrDefaultAsync();

            if (variant is null || variant.StockQuantity < quantity)
            {
                await transaction.RollbackAsync();
                return false;
            }

            variant.StockQuantity -= quantity;
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            // Đẩy trạng thái mới tới các client đang xem trang sản phẩm này
            await _hub.Clients.Group($"variant-{variantId}")
                .SendAsync("StockUpdated", variantId, variant.StockQuantity);

            return true;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
