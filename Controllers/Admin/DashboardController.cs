using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WEBBANQUANAO.Data;
using WEBBANQUANAO.Models.Entities;
using WEBBANQUANAO.Models.ViewModels;

namespace WEBBANQUANAO.Controllers.Admin;

[Area("Admin")]
[Route("sys-admin-management/[controller]/[action]")]
[Authorize(Roles = "Admin")]
public class DashboardController : Controller
{
    private readonly ApplicationDbContext _context;

    public DashboardController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var totalRevenue = await _context.Orders
            .Where(o => o.Status == OrderStatus.Completed || o.Status == OrderStatus.Shipping)
            .SumAsync(o => (decimal?)o.TotalAmount) ?? 0m;

        var totalOrders = await _context.Orders.CountAsync();
        var pendingOrdersCount = await _context.Orders.CountAsync(o => o.Status == OrderStatus.Pending);
        var totalProducts = await _context.Products.CountAsync(p => p.Status == ProductStatus.Active);
        var totalCustomers = await _context.Users.CountAsync(u => u.Role.RoleName == "Customer");

        var lowStockVariants = await _context.ProductVariants
            .Include(v => v.Product)
            .Where(v => v.StockQuantity < 10 && v.Product.Status == ProductStatus.Active)
            .OrderBy(v => v.StockQuantity)
            .Take(5)
            .ToListAsync();

        var recentOrders = await _context.Orders
            .Include(o => o.User)
            .OrderByDescending(o => o.OrderDate)
            .Take(5)
            .ToListAsync();

        var topSellingProducts = await _context.OrderDetails
            .Where(od => od.Order.Status == OrderStatus.Completed || od.Order.Status == OrderStatus.Shipping)
            .GroupBy(od => new { od.Variant.ProductId, od.Variant.Product.ProductName, od.Variant.Product.Category.CategoryName })
            .Select(g => new TopProductItem
            {
                ProductId = g.Key.ProductId,
                ProductName = g.Key.ProductName,
                CategoryName = g.Key.CategoryName,
                TotalSold = g.Sum(x => x.Quantity),
                TotalRevenue = g.Sum(x => x.UnitPrice * x.Quantity)
            })
            .OrderByDescending(x => x.TotalSold)
            .Take(5)
            .ToListAsync();

        var sixMonthsAgo = DateTime.Now.AddMonths(-5);
        var revenueByMonthRaw = await _context.Orders
            .Where(o => o.OrderDate >= new DateTime(sixMonthsAgo.Year, sixMonthsAgo.Month, 1) &&
                        (o.Status == OrderStatus.Completed || o.Status == OrderStatus.Shipping))
            .GroupBy(o => new { o.OrderDate.Year, o.OrderDate.Month })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                Revenue = g.Sum(o => o.TotalAmount)
            })
            .OrderBy(x => x.Year).ThenBy(x => x.Month)
            .ToListAsync();

        var revenueByMonth = revenueByMonthRaw.Select(x => new MonthlyRevenueItem
        {
            Year = x.Year,
            Month = x.Month,
            Revenue = x.Revenue
        }).ToList();

        var viewModel = new AdminDashboardViewModel
        {
            TotalRevenue = totalRevenue,
            TotalOrders = totalOrders,
            PendingOrdersCount = pendingOrdersCount,
            TotalProducts = totalProducts,
            TotalCustomers = totalCustomers,
            LowStockVariants = lowStockVariants,
            RecentOrders = recentOrders,
            TopSellingProducts = topSellingProducts,
            RevenueByMonth = revenueByMonth
        };

        return View(viewModel);
    }
}
