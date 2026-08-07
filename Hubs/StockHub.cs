using Microsoft.AspNetCore.SignalR;

namespace WEBBANQUANAO.Hubs;

/// <summary>
/// Theo dõi StockQuantity của từng ProductVariant.
/// Khi 1 khách đặt hàng thành công, InventoryService sẽ gọi
/// hub.Clients.Group($"variant-{variantId}").SendAsync("StockUpdated", newQuantity)
/// để đẩy trạng thái mới tới các client đang xem trang sản phẩm đó.
/// </summary>
public class StockHub : Hub
{
    public async Task JoinVariantGroup(int variantId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"variant-{variantId}");
    }

    public async Task LeaveVariantGroup(int variantId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"variant-{variantId}");
    }
}
