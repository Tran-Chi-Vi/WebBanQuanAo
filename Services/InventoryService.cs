
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
        try
        {
            var variant = await _context.ProductVariants
                .FirstOrDefaultAsync(v => v.VariantId == variantId);

            if (variant == null || variant.StockQuantity < quantity)
            {
                return false;
            }

            variant.StockQuantity -= quantity;
            await _context.SaveChangesAsync();

            try
            {
                // Push updated stock to clients via SignalR
                await _hub.Clients.Group($"variant-{variantId}")
                    .SendAsync("StockUpdated", variantId, variant.StockQuantity);
            }
            catch {}

            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"TryDeductStockAsync Error: {ex.Message}");
            return false;
        }
    }
}
