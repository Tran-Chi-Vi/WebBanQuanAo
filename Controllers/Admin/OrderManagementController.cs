using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WEBBANQUANAO.Data;
using WEBBANQUANAO.Models.Entities;
using WEBBANQUANAO.Models.ViewModels;
using WEBBANQUANAO.Services;

namespace WEBBANQUANAO.Controllers.Admin;

[Area("Admin")]
[Route("sys-admin-management/[controller]/[action]")]
[Authorize(Roles = "Admin")]
public class OrderManagementController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IEmailService _emailService;

    public OrderManagementController(ApplicationDbContext context, IEmailService emailService)
    {
        _context = context;
        _emailService = emailService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        OrderStatus? statusFilter,
        string? searchQuery,
        DateTime? fromDate,
        DateTime? toDate,
        int page = 1)
    {
        int pageSize = 15;
        var query = _context.Orders
            .Include(o => o.User)
            .Include(o => o.Address)
            .Include(o => o.Payment)
            .Include(o => o.OrderDetails)
            .AsQueryable();

        if (statusFilter.HasValue)
        {
            query = query.Where(o => o.Status == statusFilter.Value);
        }

        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            string keyword = searchQuery.Trim().ToLower();
            if (int.TryParse(keyword.Replace("#", ""), out int orderIdSearch))
            {
                query = query.Where(o => o.OrderId == orderIdSearch);
            }
            else
            {
                query = query.Where(o => o.User.FullName.ToLower().Contains(keyword) ||
                                         (o.User.Phone != null && o.User.Phone.Contains(keyword)) ||
                                         o.Address.RecipientName.ToLower().Contains(keyword) ||
                                         o.Address.Phone.Contains(keyword));
            }
        }

        if (fromDate.HasValue)
        {
            query = query.Where(o => o.OrderDate >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            var endOfDay = toDate.Value.Date.AddDays(1).AddTicks(-1);
            query = query.Where(o => o.OrderDate <= endOfDay);
        }

        int totalOrdersCount = await query.CountAsync();
        int totalPages = (int)Math.Ceiling(totalOrdersCount / (double)pageSize);
        page = Math.Max(1, Math.Min(page, Math.Max(1, totalPages)));

        var orders = await query
            .OrderByDescending(o => o.OrderDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var viewModel = new AdminOrderListViewModel
        {
            Orders = orders,
            StatusFilter = statusFilter,
            SearchQuery = searchQuery,
            FromDate = fromDate,
            ToDate = toDate,
            CurrentPage = page,
            TotalPages = totalPages,
            TotalOrdersCount = totalOrdersCount
        };

        return View(viewModel);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Details(int id)
    {
        var order = await _context.Orders
            .Include(o => o.User)
            .Include(o => o.Address)
            .Include(o => o.Promotion)
            .Include(o => o.Payment)
            .Include(o => o.OrderDetails)
                .ThenInclude(od => od.Variant)
                    .ThenInclude(v => v.Product)
                        .ThenInclude(p => p.Images)
            .FirstOrDefaultAsync(o => o.OrderId == id);

        if (order == null) return NotFound();

        return View(order);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int orderId, OrderStatus newStatus, PaymentStatus? paymentStatus)
    {
        var order = await _context.Orders
            .Include(o => o.Payment)
            .Include(o => o.OrderDetails)
            .FirstOrDefaultAsync(o => o.OrderId == orderId);

        if (order == null) return NotFound();

        var oldStatus = order.Status;
        order.Status = newStatus;

        // Nếu trạng thái đổi sang Cancelled -> Hoàn trả tồn kho nếu trước đó chưa Cancelled
        if (newStatus == OrderStatus.Cancelled && oldStatus != OrderStatus.Cancelled)
        {
            foreach (var detail in order.OrderDetails)
            {
                var variant = await _context.ProductVariants.FindAsync(detail.VariantId);
                if (variant != null)
                {
                    variant.StockQuantity += detail.Quantity;
                }
            }

            if (order.Payment != null)
            {
                order.Payment.Status = PaymentStatus.Failed;
            }
        }

        // Cập nhật trạng thái thanh toán nếu chọn
        if (paymentStatus.HasValue && order.Payment != null)
        {
            order.Payment.Status = paymentStatus.Value;
            if (paymentStatus.Value == PaymentStatus.Success && !order.Payment.PaidAt.HasValue)
            {
                order.Payment.PaidAt = DateTime.Now;
            }
        }

        // Nếu đơn hàng chuyển sang Completed -> Tự động xác nhận thanh toán thành công
        if (newStatus == OrderStatus.Completed && order.Payment != null)
        {
            order.Payment.Status = PaymentStatus.Success;
            if (!order.Payment.PaidAt.HasValue) order.Payment.PaidAt = DateTime.Now;
        }

        await _context.SaveChangesAsync();

        // Gửi Email Hóa Đơn & Cập Nhật Trạng Thái Cho Khách Hàng
        try
        {
            await _emailService.SendOrderInvoiceEmailAsync(orderId);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Lỗi gửi email cập nhật đơn hàng: " + ex.Message);
        }

        TempData["SuccessMessage"] = $"Đã cập nhật trạng thái đơn hàng #{orderId} thành '{newStatus}' và gửi email thông báo!";
        return RedirectToAction("Details", new { id = orderId });
    }
}
