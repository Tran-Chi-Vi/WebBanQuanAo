using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WEBBANQUANAO.Data;
using WEBBANQUANAO.Models.Entities;

using System.Security.Claims;
using WEBBANQUANAO.Services;

namespace WEBBANQUANAO.Controllers;

public class HomeController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IRecommendationService _recommendationService;

    public HomeController(ApplicationDbContext context, IRecommendationService recommendationService)
    {
        _context = context;
        _recommendationService = recommendationService;
    }

    public async Task<IActionResult> Index()
    {
        var sessionFav = HttpContext.Session.GetString("FavoriteProductIds");
        var favIds = string.IsNullOrEmpty(sessionFav)
            ? new List<int>()
            : sessionFav.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();

        ViewBag.FavoriteProductIds = favIds;

        ViewBag.Categories = await _context.Categories
            .Where(c => c.ParentCategoryId == null)
            .Include(c => c.SubCategories)
            .Take(8)
            .ToListAsync();

        ViewBag.Brands = await _context.Brands
            .Take(6)
            .ToListAsync();

        // Sản phẩm mới nhất
        var newArrivals = await _context.Products
            .Where(p => p.Status == ProductStatus.Active)
            .Include(p => p.Images)
            .Include(p => p.Category)
            .Include(p => p.Variants)
            .OrderByDescending(p => p.CreatedAt)
            .Take(12)
            .ToListAsync();

        // Sản phẩm bán chạy (dựa trên tổng số lượng đặt mua)
        var bestsellerProductIds = await _context.OrderDetails
            .GroupBy(od => od.Variant.ProductId)
            .OrderByDescending(g => g.Sum(od => od.Quantity))
            .Select(g => g.Key)
            .Take(8)
            .ToListAsync();

        var bestsellers = await _context.Products
            .Where(p => bestsellerProductIds.Contains(p.ProductId) && p.Status == ProductStatus.Active)
            .Include(p => p.Images)
            .Include(p => p.Category)
            .Include(p => p.Variants)
            .ToListAsync();

        if (bestsellers.Count < 4)
        {
            bestsellers = newArrivals;
        }

        // Priority sort favorited products to TOP!
        if (favIds.Any())
        {
            newArrivals = newArrivals.OrderByDescending(p => favIds.Contains(p.ProductId)).ThenByDescending(p => p.CreatedAt).ToList();
            bestsellers = bestsellers.OrderByDescending(p => favIds.Contains(p.ProductId)).ToList();
        }

        // Khuyến mãi đang hoạt động
        ViewBag.ActivePromotions = await _context.Promotions
            .Where(p => p.StartDate <= DateTime.Now && p.EndDate >= DateTime.Now)
            .Take(3)
            .ToListAsync();

        int? userId = null;
        var uIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (int.TryParse(uIdStr, out int uId) && uId > 0) userId = uId;
        string? sessionId = HttpContext.Session.GetString("fs_behavior_sid");

        ViewBag.PersonalizedRecommendations = await _recommendationService.GetPersonalizedRecommendationsAsync(userId, sessionId, 8);
        ViewBag.TopSearchRecommendations = await _recommendationService.GetTrendingAndTopSearchProductsAsync(8);

        ViewBag.NewArrivals = newArrivals;
        ViewBag.Bestsellers = bestsellers;

        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    public IActionResult Contact()
    {
        return View();
    }

    public IActionResult SizeGuide()
    {
        return View();
    }

    public IActionResult ReturnPolicy()
    {
        return View();
    }

    public IActionResult PaymentPolicy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View();
    }
}
