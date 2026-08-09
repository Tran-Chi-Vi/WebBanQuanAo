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

        try
        {
            ViewBag.Categories = await _context.Categories
                .Where(c => c.ParentCategoryId == null)
                .Include(c => c.SubCategories)
                .Take(8)
                .ToListAsync();
        }
        catch
        {
            ViewBag.Categories = new List<Category>();
        }

        try
        {
            ViewBag.Brands = await _context.Brands
                .Take(6)
                .ToListAsync();
        }
        catch
        {
            ViewBag.Brands = new List<Brand>();
        }

        // Sản phẩm mới nhất
        List<Product> newArrivals = new List<Product>();
        try
        {
            newArrivals = await _context.Products
                .Where(p => p.Status == ProductStatus.Active)
                .Include(p => p.Images)
                .Include(p => p.Category)
                .Include(p => p.Variants)
                .OrderByDescending(p => p.CreatedAt)
                .Take(12)
                .ToListAsync();
        }
        catch { }

        // Sản phẩm bán chạy (dựa trên tổng số lượng đặt mua)
        List<Product> bestsellers = new List<Product>();
        try
        {
            var bestsellerProductIds = await _context.OrderDetails
                .Include(od => od.Variant)
                .Where(od => od.Variant != null)
                .GroupBy(od => od.Variant!.ProductId)
                .OrderByDescending(g => g.Sum(od => od.Quantity))
                .Select(g => g.Key)
                .Take(8)
                .ToListAsync();

            if (bestsellerProductIds.Any())
            {
                bestsellers = await _context.Products
                    .Where(p => bestsellerProductIds.Contains(p.ProductId) && p.Status == ProductStatus.Active)
                    .Include(p => p.Images)
                    .Include(p => p.Category)
                    .Include(p => p.Variants)
                    .ToListAsync();
            }
        }
        catch { }

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
        try
        {
            var now = DateTime.Now;
            ViewBag.ActivePromotions = await _context.Promotions
                .Where(p => p.StartDate <= now && p.EndDate >= now)
                .Take(3)
                .ToListAsync();
        }
        catch
        {
            ViewBag.ActivePromotions = new List<Promotion>();
        }

        int? userId = null;
        var uIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (int.TryParse(uIdStr, out int uId) && uId > 0) userId = uId;
        string? sessionId = HttpContext.Session.GetString("fs_behavior_sid");

        try
        {
            ViewBag.PersonalizedRecommendations = await _recommendationService.GetPersonalizedRecommendationsAsync(userId, sessionId, 8);
            ViewBag.TopSearchRecommendations = await _recommendationService.GetTrendingAndTopSearchProductsAsync(8);
        }
        catch
        {
            ViewBag.PersonalizedRecommendations = newArrivals.Take(8).ToList();
            ViewBag.TopSearchRecommendations = newArrivals.Take(8).ToList();
        }

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
